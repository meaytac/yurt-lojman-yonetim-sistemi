using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(UserManager<AppUser> userManager, ITokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (request.Role is not (AppRoles.Ogrenci or AppRoles.Personel))
        {
            return BadRequest("Kayıt sırasında yalnızca öğrenci veya personel rolü seçilebilir.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            TcNo = request.TcNo,
            StudentStaffNo = request.StudentStaffNo,
            PhoneNumber = request.PhoneNumber,
            Role = request.Role,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        var roleResult = await userManager.AddToRoleAsync(user, request.Role);

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return BadRequest(roleResult.Errors);
        }

        var token = await tokenService.CreateTokenAsync(user);

        return Ok(
            new AuthResponse(
                user.Id,
                user.FullName,
                user.Email!,
                request.Role,
                token,
                user.MustChangePassword,
                user.PhoneNumber));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized("Giriş bilgileri geçersiz veya hesabınız henüz kullanıma açılmamış olabilir.");
        }

        if (!user.EmailConfirmed)
        {
            return Unauthorized("Giriş bilgileri geçersiz veya hesabınız henüz kullanıma açılmamış olabilir.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Unauthorized("Giriş bilgileri geçersiz veya hesabınız henüz kullanıma açılmamış olabilir.");
        }

        var token = await tokenService.CreateTokenAsync(user);
        return Ok(new AuthResponse(user.Id, user.FullName, user.Email!, user.Role, token, user.MustChangePassword, user.PhoneNumber));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound("Kullanıcı bulunamadı.");
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);

        return NoContent();
    }

    [HttpPost("update-phone")]
    [Authorize]
    public async Task<IActionResult> UpdatePhone(UpdatePhoneRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound("Kullanıcı bulunamadı.");
        }

        user.PhoneNumber = request.PhoneNumber;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return NoContent();
    }

    [HttpGet("must-change-password")]
    [Authorize]
    public async Task<ActionResult<bool>> GetMustChangePassword()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user.MustChangePassword);
    }
}
