using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;
using yurt_lojman_yonetim_sistemi.Services;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/yetkili")]
[Authorize(Roles = AppRoles.Yetkili)]
public class YetkiliController(IYetkiliService yetkiliService) : ControllerBase
{
    private Guid CurrentYetkiliId()
    {
        var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(raw!);
    }

    [HttpGet("facilities")]
    public Task<IReadOnlyList<AdminFacilityListItemDto>> GetAssignedFacilities(CancellationToken cancellationToken)
        => yetkiliService.GetAssignedFacilitiesAsync(CurrentYetkiliId(), cancellationToken);

    [HttpGet("dashboard-stats")]
    public Task<AdminDashboardStatsDto> GetDashboardStats(CancellationToken cancellationToken)
        => yetkiliService.GetDashboardStatsAsync(CurrentYetkiliId(), cancellationToken);

    [HttpGet("students")]
    public Task<AdminPagedResponse<AdminUserListItemDto>> GetStudents([FromQuery] AdminUserQuery query, CancellationToken cancellationToken)
        => yetkiliService.GetStudentsAsync(CurrentYetkiliId(), query, cancellationToken);

    [HttpPost("students")]
    public async Task<ActionResult<AdminUserListItemDto>> CreateStudent(YetkiliCreateStudentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var student = await yetkiliService.CreateStudentAsync(CurrentYetkiliId(), request, cancellationToken);
            return Ok(student);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("students/{id:guid}")]
    public async Task<ActionResult<AdminUserListItemDto>> UpdateStudent(Guid id, YetkiliUpdateStudentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var student = await yetkiliService.UpdateStudentAsync(CurrentYetkiliId(), id, request, cancellationToken);
            return Ok(student);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("students/{id:guid}")]
    public async Task<IActionResult> DeleteStudent(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await yetkiliService.DeleteStudentAsync(CurrentYetkiliId(), id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("applications")]
    public Task<IReadOnlyList<AdminApplicationListItemDto>> GetApplications(CancellationToken cancellationToken)
        => yetkiliService.GetApplicationsAsync(CurrentYetkiliId(), cancellationToken);

    [HttpPost("applications/{id:int}/assign")]
    public async Task<IActionResult> AssignApplication(int id, ApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var placement = await yetkiliService.AssignApplicationAsync(CurrentYetkiliId(), id, request, cancellationToken);
            return Ok(new { success = true, placementId = placement.Id, roomId = placement.RoomId, message = "Başvuru başarıyla onaylandı ve seçilen tesisteki uygun odaya yerleştirildi." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("applications/{id:int}/reject")]
    public async Task<IActionResult> RejectApplication(int id, ApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await yetkiliService.RejectApplicationAsync(CurrentYetkiliId(), id, request, cancellationToken);
            return Ok(new { success = true, message = "Başvuru reddedildi ve bekleyen başvurular listesinden kaldırıldı." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("applications/{id:int}/under-review")]
    public async Task<IActionResult> MarkApplicationUnderReview(int id, CancellationToken cancellationToken)
    {
        try
        {
            await yetkiliService.MarkApplicationUnderReviewAsync(CurrentYetkiliId(), id, cancellationToken);
            return Ok(new { success = true, message = "Başvuru incelemeye alındı." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("applications/{id:int}/missing-information")]
    public async Task<IActionResult> RequestMissingInformation(int id, MissingInformationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            await yetkiliService.RequestMissingInformationAsync(CurrentYetkiliId(), id, request, cancellationToken);
            return Ok(new { success = true, message = "Ek bilgi talebi başvuru sahibine iletildi." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("applications/{id:int}/resend-activation")]
    public async Task<IActionResult> ResendActivation(int id, CancellationToken cancellationToken)
    {
        try
        {
            await yetkiliService.ResendActivationAsync(CurrentYetkiliId(), id, cancellationToken);
            return Ok(new { success = true, message = "Aktivasyon e-postası yeniden kuyruğa alındı." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("rooms/available")]
    public Task<IReadOnlyList<AdminRoomListItemDto>> GetAvailableRooms([FromQuery] AccommodationType type, CancellationToken cancellationToken)
        => yetkiliService.GetAvailableRoomsAsync(CurrentYetkiliId(), type, cancellationToken);

    [HttpGet("students-with-rooms")]
    public Task<IReadOnlyList<YetkiliStudentListItemDto>> GetStudentsWithRooms(CancellationToken cancellationToken)
        => yetkiliService.GetStudentsWithRoomsAsync(CurrentYetkiliId(), cancellationToken);

    [HttpGet("rooms")]
    public Task<IReadOnlyList<AdminRoomListItemDto>> GetAssignedRooms(CancellationToken cancellationToken)
        => yetkiliService.GetAssignedRoomsAsync(CurrentYetkiliId(), cancellationToken);

    [HttpPut("rooms/{id:int}")]
    public async Task<ActionResult<AdminRoomListItemDto>> UpdateRoom(int id, YetkiliRoomUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var room = await yetkiliService.UpdateRoomAsync(CurrentYetkiliId(), id, request, cancellationToken);
            return Ok(room);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("placements/{id:int}/change-room")]
    public async Task<IActionResult> ChangeRoom(int id, YetkiliPlacementMoveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var resident = await yetkiliService.ChangeRoomAsync(CurrentYetkiliId(), id, request, cancellationToken);
            return Ok(new { success = true, message = "Oda değişikliği başarıyla tamamlandı.", resident });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("placements/{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, CancellationToken cancellationToken)
    {
        try
        {
            await yetkiliService.CheckoutAsync(CurrentYetkiliId(), id, cancellationToken);
            return Ok(new { success = true, message = "Yerleşim sonlandırıldı." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("requests")]
    public Task<IReadOnlyList<AdminRequestListItemDto>> GetRequests([FromQuery] bool openOnly = false, CancellationToken cancellationToken = default)
        => yetkiliService.GetRequestsAsync(CurrentYetkiliId(), openOnly, cancellationToken);

    [HttpPatch("requests/{id:int}/status")]
    public async Task<IActionResult> SetRequestStatus(int id, MaintenanceStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await yetkiliService.SetRequestStatusAsync(CurrentYetkiliId(), id, request, cancellationToken);
            return Ok(new { success = true, message = "Talep durumu güncellendi." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("staff-assignments")]
    public Task<IReadOnlyList<StaffAssignmentResponse>> GetStaffAssignments(CancellationToken cancellationToken)
        => yetkiliService.GetStaffAssignmentsAsync(CurrentYetkiliId(), cancellationToken);

    [HttpPost("staff-assignments")]
    public async Task<IActionResult> CreateStaffAssignment(StaffAssignmentCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var assignment = await yetkiliService.CreateStaffAssignmentAsync(CurrentYetkiliId(), request, cancellationToken);
            return Ok(new { success = true, message = "Görev personele atandı.", assignment });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("fault-reports")]
    public Task<IReadOnlyList<AdminFaultReportListItemDto>> GetFaultReports(CancellationToken cancellationToken)
        => yetkiliService.GetFaultReportsAsync(CurrentYetkiliId(), cancellationToken);

    [HttpGet("facility-assignments")]
    public Task<IReadOnlyList<UserFacilityAssignmentDto>> GetScopedFacilityAssignments(CancellationToken cancellationToken)
        => yetkiliService.GetScopedFacilityAssignmentsAsync(CurrentYetkiliId(), cancellationToken);
}
