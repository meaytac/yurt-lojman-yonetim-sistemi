using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

public interface IApplicationTokenService
{
    Task<string> CreateTokenAsync(int applicationId, ApplicationTokenPurpose purpose, TimeSpan lifetime, CancellationToken cancellationToken);
    Task<ApplicationAccessToken?> ConsumeTokenAsync(string referenceCode, string rawToken, ApplicationTokenPurpose purpose, CancellationToken cancellationToken);
    string HashValue(string value);
}

public class ApplicationTokenService(AppDbContext db) : IApplicationTokenService
{
    public async Task<string> CreateTokenAsync(int applicationId, ApplicationTokenPurpose purpose, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var token = new ApplicationAccessToken
        {
            ApplicationId = applicationId,
            Purpose = purpose,
            TokenHash = HashValue(raw),
            ExpiresAt = DateTime.UtcNow.Add(lifetime)
        };

        db.ApplicationAccessTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    public async Task<ApplicationAccessToken?> ConsumeTokenAsync(string referenceCode, string rawToken, ApplicationTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var candidates = await db.ApplicationAccessTokens
            .Include(x => x.Application)
            .Where(x => x.Application.ReferenceCode == referenceCode.Trim().ToUpperInvariant()
                && x.Purpose == purpose
                && x.UsedAt == null
                && x.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var hashBytes = Encoding.UTF8.GetBytes(HashValue(rawToken));
        var token = candidates.FirstOrDefault(candidate =>
            CryptographicOperations.FixedTimeEquals(hashBytes, Encoding.UTF8.GetBytes(candidate.TokenHash)));

        if (token is null)
        {
            return null;
        }

        token.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    public string HashValue(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return WebEncoders.Base64UrlEncode(bytes);
    }
}
