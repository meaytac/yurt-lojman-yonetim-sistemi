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
}