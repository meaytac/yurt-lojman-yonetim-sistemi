namespace yurt_lojman_yonetim_sistemi.DTOs;

public record DashboardResponse(
    int TotalCapacity,
    int CurrentOccupancy,
    decimal OccupancyRate,
    int PendingApplications,
    int OpenRequests,
    int UnpaidDebts);
