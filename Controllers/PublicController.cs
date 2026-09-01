using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public")]
[EnableRateLimiting("PublicApplications")]
public class PublicController(IPublicApplicationService publicApplications) : ControllerBase
{
    [HttpGet("facilities")]
    public async Task<IReadOnlyList<PublicFacilityResponse>> Facilities(CancellationToken cancellationToken)
        => await publicApplications.GetFacilitiesAsync(cancellationToken);

    [HttpGet("facilities/{type}/{id:int}")]
    public async Task<ActionResult<PublicFacilityResponse>> Facility(AccommodationType type, int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await publicApplications.GetFacilityAsync(type, id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("applications")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<PublicApplicationCreatedResponse>> Create([FromForm] PublicApplicationCreateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await publicApplications.CreateAsync(request, cancellationToken));
        }
        catch (IdempotencyConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("applications/verify-email")]
    public async Task<IActionResult> VerifyEmail(PublicTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await publicApplications.VerifyEmailAsync(request, cancellationToken);
            return Ok(new PublicMessageResponse("E-posta doğrulandı. Başvurunuz inceleme kuyruğuna alındı."));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new PublicMessageResponse("İşlem gerçekleştirilemedi. Bağlantı geçersiz veya süresi dolmuş olabilir."));
        }
    }

    [HttpPost("applications/resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        await publicApplications.ResendVerificationAsync(request.ReferenceCode, request.Email, cancellationToken);
        return Ok(new PublicMessageResponse("Bilgiler eşleşirse doğrulama e-postası yeniden gönderilecektir."));
    }

    [HttpPost("applications/track")]
    public async Task<ActionResult<PublicTrackResponse>> Track(PublicTrackRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await publicApplications.TrackAsync(request, cancellationToken));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new PublicMessageResponse("Başvuru durumu görüntülenemedi. Takip bağlantısı geçersiz veya süresi dolmuş olabilir."));
        }
    }

    [HttpPost("applications/update-missing-information")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> UpdateMissingInformation([FromForm] PublicApplicationUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            await publicApplications.ResubmitMissingInformationAsync(request, cancellationToken);
            return Ok(new PublicMessageResponse("Ek bilgiler alındı. Başvurunuz yeniden inceleme kuyruğuna alındı."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new PublicMessageResponse(ex.Message));
        }
    }

    [HttpPost("applications/activate")]
    public async Task<IActionResult> Activate(ActivateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            await publicApplications.ActivateAsync(request, cancellationToken);
            return Ok(new PublicMessageResponse("Hesabınız aktive edildi. Yeni şifrenizle giriş yapabilirsiniz."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new PublicMessageResponse(ex.Message));
        }
    }
}

public record ResendVerificationRequest(string ReferenceCode, string Email);
