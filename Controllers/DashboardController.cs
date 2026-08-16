using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.DTOs;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Yetkili}")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<DashboardResponse> Get()
    {
        var totalCapacity = await db.Rooms.SumAsync(x => (int?)x.Capacity) ?? 0;
        var currentOccupancy = await db.Rooms.SumAsync(x => (int?)x.CurrentOccupancy) ?? 0;
        var pendingApplications = await db.Applications.CountAsync(x => x.Status == ApplicationStatus.Pending);
        var openRequests = await db.Requests.CountAsync(x => x.Status == RequestStatus.Open || x.Status == RequestStatus.InProgress);
        var unpaidDebts = await db.Payments.CountAsync(x => x.Status == PaymentStatus.Unpaid || x.Status == PaymentStatus.Overdue);
        var rate = totalCapacity == 0 ? 0 : Math.Round((decimal)currentOccupancy / totalCapacity * 100, 2);
        return new DashboardResponse(totalCapacity, currentOccupancy, rate, pendingApplications, openRequests, unpaidDebts);
    }
}
