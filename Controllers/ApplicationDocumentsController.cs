using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
public class ApplicationDocumentsController(AppDbContext db, ISecureDocumentStorage secureStorage, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("api/admin/applications/{id:int}/document")]
    [HttpGet("api/yetkili/applications/{id:int}/document")]
    public async Task<IActionResult> GetDocument(int id, CancellationToken cancellationToken)
    {
        var application = await db.Applications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (application?.DocumentUrl is null)
        {
            return NotFound();
        }

        var secure = await secureStorage.OpenAsync(application.DocumentUrl, cancellationToken);
        if (secure is not null)
        {
            return File(secure.Value.Stream, secure.Value.ContentType, secure.Value.FileName);
        }

        if (application.DocumentUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var relative = application.DocumentUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var path = Path.GetFullPath(Path.Combine(environment.WebRootPath, relative));
            if (path.StartsWith(Path.GetFullPath(environment.WebRootPath), StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path))
            {
                return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
            }
        }

        return NotFound();
    }
}
