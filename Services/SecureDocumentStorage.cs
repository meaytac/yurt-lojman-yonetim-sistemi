using Microsoft.Extensions.Options;

namespace yurt_lojman_yonetim_sistemi.Services;

public interface ISecureDocumentStorage
{
    Task<string?> SavePublicDocumentAsync(IFormFile? file, CancellationToken cancellationToken);
    Task<(Stream Stream, string ContentType, string FileName)?> OpenAsync(string? documentKey, CancellationToken cancellationToken);
}

public class SecureDocumentStorage(IWebHostEnvironment environment, IOptions<PublicApplicationOptions> options) : ISecureDocumentStorage
{
    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    private readonly string root = Path.Combine(environment.ContentRootPath, "App_Data", "secure-public-documents");

    public async Task<string?> SavePublicDocumentAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        var maxBytes = Math.Max(1, options.Value.MaxDocumentMegabytes) * 1024L * 1024L;
        if (file.Length > maxBytes)
        {
            throw new InvalidOperationException($"Belge en fazla {options.Value.MaxDocumentMegabytes} MB olabilir.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedContentTypes.ContainsKey(extension))
        {
            throw new InvalidOperationException("Yalnızca PDF, JPEG veya PNG belge yüklenebilir.");
        }

        await using var input = file.OpenReadStream();
        var header = new byte[Math.Min(8, (int)file.Length)];
        var read = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!LooksLikeAllowedFile(extension, header.AsSpan(0, read)))
        {
            throw new InvalidOperationException("Belge içeriği dosya türüyle eşleşmiyor.");
        }

        input.Position = 0;
        var relative = Path.Combine(DateTime.UtcNow.ToString("yyyy"), $"{Guid.NewGuid():N}{extension}");
        var target = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var output = File.Create(target);
        await input.CopyToAsync(output, cancellationToken);
        return $"secure:{relative.Replace('\\', '/')}";
    }

    public Task<(Stream Stream, string ContentType, string FileName)?> OpenAsync(string? documentKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentKey) || !documentKey.StartsWith("secure:", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<(Stream, string, string)?>(null);
        }

        var relative = documentKey[7..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        var fullRoot = Path.GetFullPath(root);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return Task.FromResult<(Stream, string, string)?>(null);
        }

        var extension = Path.GetExtension(fullPath);
        return Task.FromResult<(Stream, string, string)?>((File.OpenRead(fullPath), AllowedContentTypes[extension], Path.GetFileName(fullPath)));
    }

    private static bool LooksLikeAllowedFile(string extension, ReadOnlySpan<byte> header)
    {
        if (extension == ".pdf") return header.Length >= 4 && header[0] == '%' && header[1] == 'P' && header[2] == 'D' && header[3] == 'F';
        if (extension is ".jpg" or ".jpeg") return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        if (extension == ".png")
        {
            ReadOnlySpan<byte> pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            return header.Length >= 8 && header.SequenceEqual(pngHeader);
        }
        return false;
    }
}
