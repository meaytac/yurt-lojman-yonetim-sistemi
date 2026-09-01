using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;

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
        Assert.False(await db.Users.AnyAsync(x => x.Email == "ziyaretci@example.test"));
        Assert.Single(await db.ApplicationAccessTokens.Where(x => x.ApplicationId == application.Id).ToListAsync());
        Assert.Single(await db.EmailOutboxMessages.Where(x => x.ToEmail == "ziyaretci@example.test").ToListAsync());
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

    private static async Task<int> SeedFacilityAsync(WebApplicationFactory<Program> factory)
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
            IsApplicationOpen = true,
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

    private static async Task<HttpResponseMessage> CreateApplicationAsync(HttpClient client, int dormitoryId)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Ziyaretçi Aday"), "fullName" },
            { new StringContent("ziyaretci@example.test"), "email" },
            { new StringContent("12345678901"), "tcNo" },
            { new StringContent("Ogrenci"), "applicantRole" },
            { new StringContent("Yurt"), "accommodationType" },
            { new StringContent(dormitoryId.ToString()), "dormitoryId" }
        };
        return await client.PostAsync("/api/public/applications", form);
    }

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
}
