using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Services;

namespace YurtLojman.Tests;

public class PublicApplicationFlowTests
{
    [Fact]
    public async Task Public_facilities_return_only_published_safe_fields()
    {
        await using var factory = CreateFactory();
        await SeedFacilityAsync(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/facilities");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("Yayınlı Test Yurdu", json);
        Assert.DoesNotContain("Gizli Test Yurdu", json);
        Assert.DoesNotContain("tcNo", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("users", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Public_application_waits_for_email_verification_without_creating_user()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();

        var response = await CreateApplicationAsync(client, dormitoryId);
        response.EnsureSuccessStatusCode();
        var created = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var reference = created.RootElement.GetProperty("referenceCode").GetString();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var application = await db.Applications.SingleAsync(x => x.ReferenceCode == reference);

        Assert.Equal(ApplicationStatus.EmailVerificationPending, application.Status);
        Assert.Null(application.UserId);
        Assert.False(await db.Users.AnyAsync(x => x.Email == "basvuru@example.test"));
        var accessTokens = await db.ApplicationAccessTokens.Where(x => x.ApplicationId == application.Id).ToListAsync();
        Assert.Equal(2, accessTokens.Count);
        Assert.Contains(accessTokens, x => x.Purpose == ApplicationTokenPurpose.EmailVerification);
        Assert.Contains(accessTokens, x => x.Purpose == ApplicationTokenPurpose.StatusTracking);
        Assert.Single(await db.EmailOutboxMessages.Where(x => x.ToEmail == "basvuru@example.test").ToListAsync());
    }

    [Fact]
    public async Task Public_application_create_returns_security_code_that_tracks_application()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();

        var createResponse = await CreateApplicationAsync(client, dormitoryId);
        createResponse.EnsureSuccessStatusCode();
        var created = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var reference = created.RootElement.GetProperty("referenceCode").GetString()!;
        var securityCode = created.RootElement.GetProperty("securityCode").GetString();

        Assert.False(string.IsNullOrWhiteSpace(securityCode));
        var track = await client.PostAsJsonAsync("/api/public/applications/track", new { referenceCode = reference, token = securityCode });

        track.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Public_application_validation_uses_basic_turkish_messages()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();

        using var form = new MultipartFormDataContent
        {
            { new StringContent(Guid.NewGuid().ToString("N")), "IdempotencyKey" },
            { new StringContent(string.Empty), "FullName" },
            { new StringContent("gecersiz"), "Email" },
            { new StringContent("123"), "TcNo" },
            { new StringContent(string.Empty), "PhoneNumber" },
            { new StringContent(string.Empty), "StudentStaffNo" },
            { new StringContent("Ogrenci"), "ApplicantRole" },
            { new StringContent("Yurt"), "AccommodationType" },
            { new StringContent(dormitoryId.ToString()), "DormitoryId" },
            { new StringContent("false"), "Consent" }
        };

        var response = await client.PostAsync("/api/public/applications", form);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Ad soyad alanı zorunludur.", json);
        Assert.Contains("Geçerli bir e-posta adresi girin.", json);
        Assert.Contains("T.C. Kimlik Numarası 11 rakam olmalıdır.", json);
        Assert.Contains("Telefon numarası zorunludur.", json);
        Assert.Contains("Öğrenci/Personel numarası zorunludur.", json);
        Assert.Contains("Başvuru bilgilerinin doğruluğunu onaylayın.", json);
        Assert.DoesNotContain("The FullName field is required", json);
        Assert.DoesNotContain("minimum length", json);
    }

    [Fact]
    public async Task Email_verification_moves_application_to_pending_and_token_is_single_use()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();

        var createResponse = await CreateApplicationAsync(client, dormitoryId);
        createResponse.EnsureSuccessStatusCode();
        var created = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var reference = created.RootElement.GetProperty("referenceCode").GetString()!;
        var token = await ExtractLatestTokenAsync(factory, reference);

        var verify = await client.PostAsJsonAsync("/api/public/applications/verify-email", new { referenceCode = reference, token });
        verify.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var application = await db.Applications.Include(x => x.StatusHistory).SingleAsync(x => x.ReferenceCode == reference);
            Assert.Equal(ApplicationStatus.Pending, application.Status);
            Assert.NotNull(application.EmailVerifiedAt);
            Assert.Contains(application.StatusHistory, x => x.Status == ApplicationStatus.Pending);
        }

        var secondUse = await client.PostAsJsonAsync("/api/public/applications/verify-email", new { referenceCode = reference, token });
        Assert.Equal(HttpStatusCode.BadRequest, secondUse.StatusCode);
    }

    [Fact]
    public async Task Idempotency_same_key_and_payload_returns_original_reference()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var first = await CreateApplicationAsync(client, dormitoryId, idempotencyKey);
        first.EnsureSuccessStatusCode();
        var firstJson = await JsonDocument.ParseAsync(await first.Content.ReadAsStreamAsync());
        var firstReference = firstJson.RootElement.GetProperty("referenceCode").GetString();

        var second = await CreateApplicationAsync(client, dormitoryId, idempotencyKey);
        second.EnsureSuccessStatusCode();
        var secondJson = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());

        Assert.Equal(firstReference, secondJson.RootElement.GetProperty("referenceCode").GetString());
    }

    [Fact]
    public async Task Idempotency_same_key_with_different_payload_returns_conflict()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        (await CreateApplicationAsync(client, dormitoryId, idempotencyKey)).EnsureSuccessStatusCode();
        var conflict = await CreateApplicationAsync(client, dormitoryId, idempotencyKey, fullName: "Farklı Başvuru");

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Closed_facility_cannot_receive_public_application()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory, isApplicationOpen: false);
        var client = factory.CreateClient();

        var response = await CreateApplicationAsync(client, dormitoryId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Building_name_must_be_unique_within_the_same_facility()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var first = await client.PostAsJsonAsync("/api/admin/buildings", new { dormitoryId, housingUnitId = (int?)null, blockName = "A Blok" });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/admin/buildings", new { dormitoryId, housingUnitId = (int?)null, blockName = "A Blok" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Document_mime_type_must_match_allowed_file_type()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();

        using var form = NewApplicationForm(dormitoryId, Guid.NewGuid().ToString("N"));
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(file, "Document", "test.pdf");
        var response = await client.PostAsync("/api/public/applications", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Tracking_token_can_be_used_without_being_consumed()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();
        var createResponse = await CreateApplicationAsync(client, dormitoryId);
        createResponse.EnsureSuccessStatusCode();
        var created = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var reference = created.RootElement.GetProperty("referenceCode").GetString()!;
        var verifyToken = await ExtractLatestTokenAsync(factory, reference);
        (await client.PostAsJsonAsync("/api/public/applications/verify-email", new { referenceCode = reference, token = verifyToken })).EnsureSuccessStatusCode();
        var trackingToken = await ExtractLatestTokenAsync(factory, reference);

        var first = await client.PostAsJsonAsync("/api/public/applications/track", new { referenceCode = reference, token = trackingToken });
        var second = await client.PostAsJsonAsync("/api/public/applications/track", new { referenceCode = reference, token = trackingToken });

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Missing_information_can_be_updated_with_tracking_token()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        var client = factory.CreateClient();
        var createResponse = await CreateApplicationAsync(client, dormitoryId);
        createResponse.EnsureSuccessStatusCode();
        var created = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var reference = created.RootElement.GetProperty("referenceCode").GetString()!;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var application = await db.Applications.SingleAsync(x => x.ReferenceCode == reference);
            application.Status = ApplicationStatus.MissingInformation;
            application.StatusHistory.Add(new ApplicationStatusHistory { Status = ApplicationStatus.MissingInformation, Note = "Belge okunaklı değil." });
            await db.SaveChangesAsync();
        }

        var trackingToken = await CreateTrackingTokenAsync(factory, reference);
        using var form = new MultipartFormDataContent
        {
            { new StringContent(reference), "ReferenceCode" },
            { new StringContent(trackingToken), "Token" },
            { new StringContent("Yeni belge eklendi."), "Note" }
        };
        var response = await client.PostAsync("/api/public/applications/update-missing-information", form);

        response.EnsureSuccessStatusCode();
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Applications.Include(x => x.StatusHistory).SingleAsync(x => x.ReferenceCode == reference);
        Assert.Equal(ApplicationStatus.Pending, updated.Status);
        Assert.Contains(updated.StatusHistory, x => x.Status == ApplicationStatus.Pending && x.Note!.Contains("Ek bilgi"));
    }

    [Fact]
    public async Task Approval_requires_verified_email_for_external_application()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        await SeedRoomAsync(factory, dormitoryId);
        var client = factory.CreateClient();
        var createResponse = await CreateApplicationAsync(client, dormitoryId);
        createResponse.EnsureSuccessStatusCode();
        var created = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var reference = created.RootElement.GetProperty("referenceCode").GetString()!;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = scope.ServiceProvider.GetRequiredService<IApplicationWorkflowService>();
        var applicationId = await db.Applications.Where(x => x.ReferenceCode == reference).Select(x => x.Id).SingleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.ApproveAsync(
            Guid.NewGuid(),
            applicationId,
            new ApplicationDecisionRequest(true, null, null, true, dormitoryId, null),
            [dormitoryId],
            null,
            CancellationToken.None));
    }

    [Fact]
    public async Task Approval_creates_locked_student_account_and_activation_email_only_after_verification()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        await SeedRoomAsync(factory, dormitoryId);
        var client = factory.CreateClient();
        var createResponse = await CreateApplicationAsync(client, dormitoryId);
        createResponse.EnsureSuccessStatusCode();
        var created = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var reference = created.RootElement.GetProperty("referenceCode").GetString()!;
        var verifyToken = await ExtractLatestTokenAsync(factory, reference);
        (await client.PostAsJsonAsync("/api/public/applications/verify-email", new { referenceCode = reference, token = verifyToken })).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = scope.ServiceProvider.GetRequiredService<IApplicationWorkflowService>();
        var applicationId = await db.Applications.Where(x => x.ReferenceCode == reference).Select(x => x.Id).SingleAsync();
        var placement = await workflow.ApproveAsync(
            Guid.NewGuid(),
            applicationId,
            new ApplicationDecisionRequest(true, null, null, true, dormitoryId, null),
            [dormitoryId],
            null,
            CancellationToken.None);

        var application = await db.Applications.Include(x => x.User).SingleAsync(x => x.ReferenceCode == reference);
        Assert.NotNull(placement);
        Assert.Equal(ApplicationStatus.ApprovedAwaitingActivation, application.Status);
        Assert.NotNull(application.UserId);
        Assert.Equal(AppRoles.Ogrenci, application.User!.Role);
        Assert.False(application.User.EmailConfirmed);
        Assert.True(application.User.LockoutEnd > DateTimeOffset.UtcNow);
        Assert.Contains(await db.EmailOutboxMessages.Where(x => x.ToEmail == "basvuru@example.test").ToListAsync(), x => x.Subject.Contains("aktivasyon", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Activation_sets_password_unlocks_account_and_consumes_token()
    {
        await using var factory = CreateFactory();
        var dormitoryId = await SeedFacilityAsync(factory);
        await SeedRoomAsync(factory, dormitoryId);
        var client = factory.CreateClient();
        var createResponse = await CreateApplicationAsync(client, dormitoryId);
        createResponse.EnsureSuccessStatusCode();
        var created = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var reference = created.RootElement.GetProperty("referenceCode").GetString()!;
        var verifyToken = await ExtractLatestTokenAsync(factory, reference);
        (await client.PostAsJsonAsync("/api/public/applications/verify-email", new { referenceCode = reference, token = verifyToken })).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var workflow = scope.ServiceProvider.GetRequiredService<IApplicationWorkflowService>();
            var applicationId = await db.Applications.Where(x => x.ReferenceCode == reference).Select(x => x.Id).SingleAsync();
            await workflow.ApproveAsync(Guid.NewGuid(), applicationId, new ApplicationDecisionRequest(true, null, null, true, dormitoryId, null), [dormitoryId], null, CancellationToken.None);
        }

        var activationToken = await ExtractLatestTokenAsync(factory, reference);
        var activation = await client.PostAsJsonAsync("/api/public/applications/activate", new
        {
            referenceCode = reference,
            token = activationToken,
            password = "Yeni123!",
            confirmPassword = "Yeni123!"
        });
        activation.EnsureSuccessStatusCode();
        var secondUse = await client.PostAsJsonAsync("/api/public/applications/activate", new
        {
            referenceCode = reference,
            token = activationToken,
            password = "Yeni123!",
            confirmPassword = "Yeni123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, secondUse.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var application = await verifyDb.Applications.Include(x => x.User).SingleAsync(x => x.ReferenceCode == reference);
        Assert.Equal(ApplicationStatus.Approved, application.Status);
        Assert.NotNull(application.ActivatedAt);
        Assert.True(application.User!.EmailConfirmed);
        Assert.False(application.User.MustChangePassword);
        Assert.True(application.User.LockoutEnd is null || application.User.LockoutEnd <= DateTimeOffset.UtcNow);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DemoMode"] = "true",
                    ["PublicApplications:PublicBaseUrl"] = "http://localhost"
                });
            });
        });
    }

    private static async Task<int> SeedFacilityAsync(WebApplicationFactory<Program> factory, bool isApplicationOpen = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Dormitories.RemoveRange(db.Dormitories.Where(x => x.Name.EndsWith("Test Yurdu")));
        await db.SaveChangesAsync();

        var published = new Dormitory
        {
            Name = "Yayınlı Test Yurdu",
            CampusLocation = "Test Kampüsü",
            TotalCapacity = 10,
            IsActive = true,
            IsPublished = true,
            IsApplicationOpen = isApplicationOpen,
            PublicDescription = "Public açıklama"
        };
        db.Dormitories.Add(published);
        db.Dormitories.Add(new Dormitory
        {
            Name = "Gizli Test Yurdu",
            CampusLocation = "Test Kampüsü",
            TotalCapacity = 10,
            IsActive = true,
            IsPublished = false,
            IsApplicationOpen = true
        });
        await db.SaveChangesAsync();
        return published.Id;
    }

    private static async Task SeedRoomAsync(WebApplicationFactory<Program> factory, int dormitoryId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Rooms.AnyAsync(x => x.BlockFloor.Building.DormitoryId == dormitoryId))
        {
            return;
        }

        var building = new Building { DormitoryId = dormitoryId, BlockName = $"Test Blok {Guid.NewGuid():N}"[..18] };
        var floor = new Floor { Building = building, FloorNumber = 1 };
        db.Rooms.Add(new Room
        {
            BlockFloor = floor,
            RoomNumber = "101",
            Capacity = 2,
            CurrentOccupancy = 0,
            Status = RoomStatus.Empty,
            Price = 1000
        });
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> CreateApplicationAsync(HttpClient client, int dormitoryId, string? idempotencyKey = null, string fullName = "Başvuru Adayı")
    {
        using var form = NewApplicationForm(dormitoryId, idempotencyKey ?? Guid.NewGuid().ToString("N"), fullName);
        return await client.PostAsync("/api/public/applications", form);
    }

    private static async Task AuthenticateAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@ozal.edu.tr", password = "Demo123!" });
        response.EnsureSuccessStatusCode();
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var token = json.RootElement.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static MultipartFormDataContent NewApplicationForm(int dormitoryId, string idempotencyKey, string fullName = "Başvuru Adayı")
        => new()
        {
            { new StringContent(idempotencyKey), "IdempotencyKey" },
            { new StringContent(fullName), "FullName" },
            { new StringContent("basvuru@example.test"), "Email" },
            { new StringContent("12345678901"), "TcNo" },
            { new StringContent("+905551112233"), "PhoneNumber" },
            { new StringContent("OGR-42"), "StudentStaffNo" },
            { new StringContent("Ogrenci"), "ApplicantRole" },
            { new StringContent("Yurt"), "AccommodationType" },
            { new StringContent(dormitoryId.ToString()), "DormitoryId" },
            { new StringContent("true"), "Consent" }
        };

    private static async Task<string> ExtractLatestTokenAsync(WebApplicationFactory<Program> factory, string reference)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var body = await db.EmailOutboxMessages
            .OrderByDescending(x => x.CreatedAt)
            .Where(x => x.HtmlBody.Contains(reference))
            .Select(x => x.HtmlBody)
            .FirstAsync();

        var marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, body);
        start += marker.Length;
        var end = body.IndexOf('"', start);
        return WebUtility.HtmlDecode(body[start..end]);
    }

    private static async Task<string> CreateTrackingTokenAsync(WebApplicationFactory<Program> factory, string reference)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<yurt_lojman_yonetim_sistemi.Services.IApplicationTokenService>();
        var applicationId = await db.Applications.Where(x => x.ReferenceCode == reference).Select(x => x.Id).SingleAsync();
        return await tokenService.CreateTokenAsync(applicationId, ApplicationTokenPurpose.StatusTracking, TimeSpan.FromDays(30), CancellationToken.None);
    }
}
