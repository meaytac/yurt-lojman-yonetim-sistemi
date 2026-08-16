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
        if (!AppRoles.All.Contains(request.Role))
        {
            return BadRequest("Gecersiz rol.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            TcNo = request.TcNo,
            StudentStaffNo = request.StudentStaffNo,
            PhoneNumber = request.PhoneNumber,
            Role = request.Role
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await userManager.AddToRoleAsync(user, request.Role);
        var token = await tokenService.CreateTokenAsync(user);
        return Ok(new AuthResponse(user.Id, user.FullName, user.Email!, request.Role, token));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized("E-posta veya sifre hatali.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Unauthorized("Hesap dondurulmus. Lutfen sistem yoneticisi ile iletisime gecin.");
        }

        var token = await tokenService.CreateTokenAsync(user);
        return Ok(new AuthResponse(user.Id, user.FullName, user.Email!, user.Role, token));
    }
}
