using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

public interface IPublicApplicationService
{
    Task<IReadOnlyList<PublicFacilityResponse>> GetFacilitiesAsync(CancellationToken cancellationToken);
    Task<PublicFacilityResponse> GetFacilityAsync(AccommodationType type, int id, CancellationToken cancellationToken);
    Task<PublicApplicationCreatedResponse> CreateAsync(PublicApplicationCreateRequest request, CancellationToken cancellationToken);
    Task VerifyEmailAsync(PublicTokenRequest request, CancellationToken cancellationToken);
    Task ResendVerificationAsync(string referenceCode, string email, CancellationToken cancellationToken);
    Task<PublicTrackResponse> TrackAsync(PublicTrackRequest request, CancellationToken cancellationToken);
    Task ActivateAsync(ActivateAccountRequest request, CancellationToken cancellationToken);
}

public class PublicApplicationService(
    AppDbContext db,
    IApplicationTokenService tokenService,
    ISecureDocumentStorage documentStorage,
    IEmailOutboxService outbox,
    UserManager<AppUser> userManager,
    IOptions<PublicApplicationOptions> options) : IPublicApplicationService
{
    public async Task<IReadOnlyList<PublicFacilityResponse>> GetFacilitiesAsync(CancellationToken cancellationToken)
    {
        var dormitories = await db.Dormitories.AsNoTracking()
            .Where(x => x.IsActive && x.IsPublished)
            .Select(x => new PublicFacilityResponse(
                x.Id, x.Type, x.Name, x.CampusLocation, x.TotalCapacity,
                x.Buildings.SelectMany(b => b.Floors).SelectMany(f => f.Rooms).Sum(r => Math.Max(0, r.Capacity - r.CurrentOccupancy)),
                x.PublicDescription, x.Amenities, x.ImageUrl, x.ApplicationConditions, x.IsApplicationOpen))
            .ToListAsync(cancellationToken);

        var housingUnits = await db.HousingUnits.AsNoTracking()
            .Where(x => x.IsActive && x.IsPublished)
            .Select(x => new PublicFacilityResponse(
                x.Id, x.Type, x.Name, x.CampusLocation, x.TotalCapacity,
                x.Buildings.SelectMany(b => b.Floors).SelectMany(f => f.Rooms).Sum(r => Math.Max(0, r.Capacity - r.CurrentOccupancy)),
                x.PublicDescription, x.Amenities, x.ImageUrl, x.ApplicationConditions, x.IsApplicationOpen))
            .ToListAsync(cancellationToken);

        return dormitories.Concat(housingUnits).OrderBy(x => x.Type).ThenBy(x => x.Name).ToList();
    }

    public async Task<PublicFacilityResponse> GetFacilityAsync(AccommodationType type, int id, CancellationToken cancellationToken)
    {
        var facility = (await GetFacilitiesAsync(cancellationToken)).FirstOrDefault(x => x.Type == type && x.Id == id);
        return facility ?? throw new KeyNotFoundException("Tesis bulunamadı.");
    }

    public async Task<PublicApplicationCreatedResponse> CreateAsync(PublicApplicationCreateRequest request, CancellationToken cancellationToken)
    {
        var role = NormalizeApplicantRole(request.ApplicantRole);
        await EnsureFacilityAcceptsApplicationsAsync(request, cancellationToken);
        var documentKey = await documentStorage.SavePublicDocumentAsync(request.Document, cancellationToken);
        var referenceCode = await GenerateReferenceCodeAsync(cancellationToken);

        var application = new AccommodationApplication
        {
            Source = ApplicationSource.PublicVisitor,
            Status = ApplicationStatus.EmailVerificationPending,
            ReferenceCode = referenceCode,
            AccommodationType = request.AccommodationType,
            ApplicantFullName = request.FullName.Trim(),
            ApplicantEmail = request.Email.Trim().ToLowerInvariant(),
            ApplicantTcNo = request.TcNo.Trim(),
            ApplicantPhoneNumber = request.PhoneNumber?.Trim(),
            ApplicantStudentStaffNo = request.StudentStaffNo?.Trim(),
            ApplicantRole = role,
            ApplicantNote = request.Note?.Trim(),
            RequestedDormitoryId = request.DormitoryId,
            RequestedHousingUnitId = request.HousingUnitId,
            DocumentUrl = documentKey
        };

        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.EmailVerificationPending,
            Note = "E-posta doğrulaması bekleniyor."
        });

        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);

        var verificationToken = await tokenService.CreateTokenAsync(
            application.Id,
            ApplicationTokenPurpose.EmailVerification,
            TimeSpan.FromHours(options.Value.VerificationTokenHours),
            cancellationToken);

        await SendVerificationEmailAsync(application, verificationToken, cancellationToken);
        return new PublicApplicationCreatedResponse(referenceCode, application.Status, "Başvurunuz alındı. Yetkili incelemesi için e-posta doğrulaması gereklidir.");
    }

    public async Task VerifyEmailAsync(PublicTokenRequest request, CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.ConsumeTokenAsync(request.ReferenceCode, request.Token, ApplicationTokenPurpose.EmailVerification, cancellationToken)
            ?? throw new InvalidOperationException("Doğrulama bağlantısı geçersiz veya süresi dolmuş.");

        var application = accessToken.Application;
        if (application.Status == ApplicationStatus.EmailVerificationPending)
        {
            application.EmailVerifiedAt = DateTime.UtcNow;
            application.Status = ApplicationStatus.Pending;
            application.UpdatedAt = DateTime.UtcNow;
            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                Status = ApplicationStatus.Pending,
                Note = "E-posta doğrulandı ve başvuru inceleme kuyruğuna alındı."
            });
        }

        var trackingToken = await tokenService.CreateTokenAsync(
            application.Id,
            ApplicationTokenPurpose.StatusTracking,
            TimeSpan.FromDays(options.Value.TrackingTokenDays),
            cancellationToken);

        await SendTrackingEmailAsync(application, trackingToken, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResendVerificationAsync(string referenceCode, string email, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstOrDefaultAsync(x =>
            x.ReferenceCode == referenceCode.Trim().ToUpperInvariant()
            && x.ApplicantEmail == email.Trim().ToLowerInvariant()
            && x.Source == ApplicationSource.PublicVisitor
            && x.Status == ApplicationStatus.EmailVerificationPending,
            cancellationToken);

        if (application is null)
        {
            return;
        }

        var verificationToken = await tokenService.CreateTokenAsync(
            application.Id,
            ApplicationTokenPurpose.EmailVerification,
            TimeSpan.FromHours(options.Value.VerificationTokenHours),
            cancellationToken);

        await SendVerificationEmailAsync(application, verificationToken, cancellationToken);
    }

    public async Task<PublicTrackResponse> TrackAsync(PublicTrackRequest request, CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.ConsumeTokenAsync(request.ReferenceCode, request.Token, ApplicationTokenPurpose.StatusTracking, cancellationToken)
            ?? throw new InvalidOperationException("Takip bağlantısı geçersiz veya süresi dolmuş.");

        var application = await db.Applications.AsNoTracking()
            .Include(x => x.RequestedDormitory)
            .Include(x => x.RequestedHousingUnit)
            .Include(x => x.StatusHistory)
            .FirstAsync(x => x.Id == accessToken.ApplicationId, cancellationToken);

        var renewedToken = await tokenService.CreateTokenAsync(
            application.Id,
            ApplicationTokenPurpose.StatusTracking,
            TimeSpan.FromDays(options.Value.TrackingTokenDays),
            cancellationToken);

        await SendTrackingEmailAsync(application, renewedToken, cancellationToken);

        return new PublicTrackResponse(
            application.ReferenceCode,
            application.Status,
            application.AccommodationType,
            application.ApplicantFullName ?? "Başvuru sahibi",
            application.RequestedDormitory?.Name ?? application.RequestedHousingUnit?.Name,
            application.CreatedAt,
            application.UpdatedAt,
            application.StatusHistory.OrderBy(x => x.CreatedAt)
                .Select(x => new PublicApplicationHistoryDto(x.Status, x.Note, x.CreatedAt))
                .ToList());
    }

    public async Task ActivateAsync(ActivateAccountRequest request, CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.ConsumeTokenAsync(request.ReferenceCode, request.Token, ApplicationTokenPurpose.AccountActivation, cancellationToken)
            ?? throw new InvalidOperationException("Aktivasyon bağlantısı geçersiz veya süresi dolmuş.");

        var application = await db.Applications.Include(x => x.User).FirstAsync(x => x.Id == accessToken.ApplicationId, cancellationToken);
        if (application.User is null || application.Status != ApplicationStatus.ApprovedAwaitingActivation)
        {
            throw new InvalidOperationException("Aktivasyon için uygun başvuru bulunamadı.");
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(application.User);
        var result = await userManager.ResetPasswordAsync(application.User, resetToken, request.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        application.User.EmailConfirmed = true;
        application.User.LockoutEnd = null;
        application.User.LockoutEnabled = true;
        application.User.MustChangePassword = false;
        application.Status = ApplicationStatus.Approved;
        application.ActivatedAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.Approved,
            Note = "Hesap aktive edildi."
        });

        await userManager.UpdateAsync(application.User);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureFacilityAcceptsApplicationsAsync(PublicApplicationCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.AccommodationType == AccommodationType.Yurt)
        {
            var dormitory = await db.Dormitories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.DormitoryId, cancellationToken)
                ?? throw new InvalidOperationException("Yurt seçimi zorunludur.");
            if (!dormitory.IsActive || !dormitory.IsPublished || !dormitory.IsApplicationOpen)
            {
                throw new InvalidOperationException("Seçilen yurt başvuruya açık değil.");
            }
            request.HousingUnitId = null;
            return;
        }

        var housingUnit = await db.HousingUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.HousingUnitId, cancellationToken)
            ?? throw new InvalidOperationException("Lojman seçimi zorunludur.");
        if (!housingUnit.IsActive || !housingUnit.IsPublished || !housingUnit.IsApplicationOpen)
        {
            throw new InvalidOperationException("Seçilen lojman başvuruya açık değil.");
        }
        request.DormitoryId = null;
    }

    private static string NormalizeApplicantRole(string role)
    {
        role = role.Trim();
        if (role.Equals(AppRoles.Ogrenci, StringComparison.OrdinalIgnoreCase)) return AppRoles.Ogrenci;
        if (role.Equals(AppRoles.Personel, StringComparison.OrdinalIgnoreCase)) return AppRoles.Personel;
        throw new InvalidOperationException("Başvuru sahibi rolü yalnızca Öğrenci veya Personel olabilir.");
    }

    private async Task<string> GenerateReferenceCodeAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 10; i++)
        {
            var code = $"YL{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
            if (!await db.Applications.AnyAsync(x => x.ReferenceCode == code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Başvuru referansı üretilemedi.");
    }

    private Task SendVerificationEmailAsync(AccommodationApplication application, string rawToken, CancellationToken cancellationToken)
    {
        var link = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/verify-application.html?ref={WebUtility.UrlEncode(application.ReferenceCode)}&token={WebUtility.UrlEncode(rawToken)}";
        return outbox.EnqueueAsync(application.ApplicantEmail!, "Başvuru e-posta doğrulaması",
            $"<p>Başvurunuzu incelemeye alabilmemiz için e-posta adresinizi doğrulayın.</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Başvuruyu doğrula</a></p><p>Referans kodunuz: <strong>{WebUtility.HtmlEncode(application.ReferenceCode)}</strong></p>",
            cancellationToken);
    }

    private Task SendTrackingEmailAsync(AccommodationApplication application, string rawToken, CancellationToken cancellationToken)
    {
        var link = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/track-application.html?ref={WebUtility.UrlEncode(application.ReferenceCode)}&token={WebUtility.UrlEncode(rawToken)}";
        return outbox.EnqueueAsync(application.ApplicantEmail!, "Başvuru takip bağlantısı",
            $"<p>Başvuru durumunuzu güvenli bağlantıdan takip edebilirsiniz.</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Başvuruyu takip et</a></p>",
            cancellationToken);
    }
}
