using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

public interface IApplicationWorkflowService
{
    Task<Placement?> ApproveAsync(Guid decidedById, int applicationId, ApplicationDecisionRequest request, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken);
    Task RejectAsync(Guid decidedById, int applicationId, ApplicationDecisionRequest request, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken);
    Task MarkUnderReviewAsync(Guid decidedById, int applicationId, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken);
    Task RequestMissingInformationAsync(Guid decidedById, int applicationId, MissingInformationRequest request, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken);
    Task ResendActivationAsync(Guid decidedById, int applicationId, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken);
}

public class ApplicationWorkflowService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    IAccommodationService accommodationService,
    IApplicationTokenService tokenService,
    IEmailOutboxService outbox,
    IOptions<PublicApplicationOptions> options) : IApplicationWorkflowService
{
    private static readonly string[] ApplicantRoles = [AppRoles.Ogrenci, AppRoles.Personel];

    public async Task<Placement?> ApproveAsync(Guid decidedById, int applicationId, ApplicationDecisionRequest request, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken)
    {
        var application = await db.Applications.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Başvuru bulunamadı.");

        if (application.Status != ApplicationStatus.Pending && application.Status != ApplicationStatus.UnderReview)
        {
            throw new InvalidOperationException("Yalnızca inceleme bekleyen başvurular onaylanabilir.");
        }

        EnsureApplicationInScope(application, dormitoryScope, housingUnitScope);
        application.User ??= await CreateLockedPublicUserAsync(application, cancellationToken);

        if (!ApplicantRoles.Contains(application.User.Role))
        {
            throw new InvalidOperationException("Yönetici ve yetkili profilleri başvuru akışına dahil edilemez.");
        }

        var placement = await accommodationService.PlaceUserAsync(
            application.User.Id,
            application.AccommodationType,
            request.RoomId,
            cancellationToken,
            dormitoryScope,
            housingUnitScope);

        application.ApprovedRoomId = placement.RoomId;
        application.DecidedById = decidedById;
        application.DecisionAt = DateTime.UtcNow;
        application.DecisionReason = request.Reason;
        application.UpdatedAt = DateTime.UtcNow;

        if (application.Source == ApplicationSource.ExternalApplicant)
        {
            application.Status = ApplicationStatus.ApprovedAwaitingActivation;
            application.ActivationSentAt = DateTime.UtcNow;
            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                Status = ApplicationStatus.ApprovedAwaitingActivation,
                ChangedById = decidedById,
                Note = "Başvuru onaylandı, hesap aktivasyonu bekleniyor."
            });

            await db.SaveChangesAsync(cancellationToken);
            var activationToken = await tokenService.CreateTokenAsync(
                application.Id,
                ApplicationTokenPurpose.AccountActivation,
                TimeSpan.FromHours(options.Value.ActivationTokenHours),
                cancellationToken);
            await SendActivationEmailAsync(application, activationToken, cancellationToken);
            return placement;
        }

        application.Status = ApplicationStatus.Approved;
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.Approved,
            ChangedById = decidedById,
            Note = request.Reason
        });
        await db.SaveChangesAsync(cancellationToken);
        return placement;
    }

    public async Task RejectAsync(Guid decidedById, int applicationId, ApplicationDecisionRequest request, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Başvuru bulunamadı.");

        if (application.Status != ApplicationStatus.Pending && application.Status != ApplicationStatus.UnderReview)
        {
            throw new InvalidOperationException("Yalnızca inceleme bekleyen başvurular reddedilebilir.");
        }

        EnsureApplicationInScope(application, dormitoryScope, housingUnitScope);
        application.Status = ApplicationStatus.Rejected;
        application.DecidedById = decidedById;
        application.DecisionAt = DateTime.UtcNow;
        application.DecisionReason = request.Reason;
        application.UpdatedAt = DateTime.UtcNow;
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.Rejected,
            ChangedById = decidedById,
            Note = request.Reason
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkUnderReviewAsync(Guid decidedById, int applicationId, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Başvuru bulunamadı.");

        if (application.Status != ApplicationStatus.Pending)
        {
            throw new InvalidOperationException("Yalnızca bekleyen başvurular incelemeye alınabilir.");
        }

        EnsureApplicationInScope(application, dormitoryScope, housingUnitScope);
        application.Status = ApplicationStatus.UnderReview;
        application.DecidedById = decidedById;
        application.UpdatedAt = DateTime.UtcNow;
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.UnderReview,
            ChangedById = decidedById,
            Note = "Başvuru incelemeye alındı."
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestMissingInformationAsync(Guid decidedById, int applicationId, MissingInformationRequest request, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Başvuru bulunamadı.");

        if (application.Status != ApplicationStatus.Pending && application.Status != ApplicationStatus.UnderReview)
        {
            throw new InvalidOperationException("Yalnızca inceleme aşamasındaki başvurular için ek bilgi istenebilir.");
        }

        EnsureApplicationInScope(application, dormitoryScope, housingUnitScope);
        application.Status = ApplicationStatus.MissingInformation;
        application.DecidedById = decidedById;
        application.DecisionReason = request.Reason.Trim();
        application.UpdatedAt = DateTime.UtcNow;
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.MissingInformation,
            ChangedById = decidedById,
            Note = request.Reason.Trim()
        });

        await db.SaveChangesAsync(cancellationToken);

        if (application.Source == ApplicationSource.ExternalApplicant && !string.IsNullOrWhiteSpace(application.ApplicantEmail))
        {
            var trackingToken = await tokenService.CreateTokenAsync(
                application.Id,
                ApplicationTokenPurpose.StatusTracking,
                TimeSpan.FromDays(options.Value.TrackingTokenDays),
                cancellationToken);
            await SendMissingInformationEmailAsync(application, trackingToken, request.Reason, cancellationToken);
        }
    }

    public async Task ResendActivationAsync(Guid decidedById, int applicationId, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Başvuru bulunamadı.");

        if (application.Source != ApplicationSource.ExternalApplicant || application.Status != ApplicationStatus.ApprovedAwaitingActivation)
        {
            throw new InvalidOperationException("Aktivasyon bağlantısı yalnızca aktivasyon bekleyen başvurular için gönderilebilir.");
        }

        EnsureApplicationInScope(application, dormitoryScope, housingUnitScope);
        application.ActivationSentAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.ApprovedAwaitingActivation,
            ChangedById = decidedById,
            Note = "Aktivasyon bağlantısı yeniden gönderildi."
        });
        await db.SaveChangesAsync(cancellationToken);

        var activationToken = await tokenService.CreateTokenAsync(
            application.Id,
            ApplicationTokenPurpose.AccountActivation,
            TimeSpan.FromHours(options.Value.ActivationTokenHours),
            cancellationToken);
        await SendActivationEmailAsync(application, activationToken, cancellationToken);
    }

    private async Task<AppUser> CreateLockedPublicUserAsync(AccommodationApplication application, CancellationToken cancellationToken)
    {
        var email = application.ApplicantEmail ?? throw new InvalidOperationException("Başvuru e-posta adresi eksik.");
        if (await userManager.FindByEmailAsync(email) is not null || await db.Users.AnyAsync(x => x.TcNo == application.ApplicantTcNo, cancellationToken))
        {
            throw new InvalidOperationException("Bu e-posta veya TC kimlik numarasıyla kayıtlı kullanıcı bulunuyor.");
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = application.ApplicantFullName ?? "Başvuru Sahibi",
            TcNo = application.ApplicantTcNo ?? string.Empty,
            PhoneNumber = application.ApplicantPhoneNumber,
            StudentStaffNo = application.ApplicantStudentStaffNo,
            Role = application.ApplicantRole == AppRoles.Personel ? AppRoles.Personel : AppRoles.Ogrenci,
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.UtcNow.AddYears(100),
            MustChangePassword = true
        };

        var randomPassword = $"Tmp{RandomNumberGenerator.GetInt32(100000, 999999)}a!";
        var result = await userManager.CreateAsync(user, randomPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, user.Role);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(x => x.Description)));
        }

        application.UserId = user.Id;
        application.User = user;
        return user;
    }

    private static void EnsureApplicationInScope(AccommodationApplication application, IReadOnlyList<int>? dormitoryScope, IReadOnlyList<int>? housingUnitScope)
    {
        if (application.AccommodationType == AccommodationType.Yurt && dormitoryScope is { Count: > 0 })
        {
            if (application.RequestedDormitoryId.HasValue && !dormitoryScope.Contains(application.RequestedDormitoryId.Value))
            {
                throw new InvalidOperationException("Bu başvuru yetki kapsamındaki yurtlara ait değil.");
            }
            return;
        }

        if (application.AccommodationType == AccommodationType.Lojman && housingUnitScope is { Count: > 0 })
        {
            if (application.RequestedHousingUnitId.HasValue && !housingUnitScope.Contains(application.RequestedHousingUnitId.Value))
            {
                throw new InvalidOperationException("Bu başvuru yetki kapsamındaki lojmanlara ait değil.");
            }
        }
    }

    private Task SendActivationEmailAsync(AccommodationApplication application, string rawToken, CancellationToken cancellationToken)
    {
        var link = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/activate-account.html?ref={WebUtility.UrlEncode(application.ReferenceCode)}&token={WebUtility.UrlEncode(rawToken)}";
        return outbox.EnqueueAsync(application.ApplicantEmail!, "Başvuru onaylandı - hesap aktivasyonu",
            $"<p>Başvurunuz onaylandı. Sisteme giriş yapabilmek için şifrenizi belirleyin.</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Hesabı aktive et</a></p>",
            cancellationToken);
    }

    private Task SendMissingInformationEmailAsync(AccommodationApplication application, string rawToken, string reason, CancellationToken cancellationToken)
    {
        var link = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/track-application.html?ref={WebUtility.UrlEncode(application.ReferenceCode)}&token={WebUtility.UrlEncode(rawToken)}";
        return outbox.EnqueueAsync(application.ApplicantEmail!, "Başvurunuz için ek bilgi gerekiyor",
            $"<p>Başvurunuzun incelenebilmesi için ek bilgi gerekiyor.</p><p><strong>Gerekçe:</strong> {WebUtility.HtmlEncode(reason)}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Başvuruyu güncelle</a></p>",
            cancellationToken);
    }
}
