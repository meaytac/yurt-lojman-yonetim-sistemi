using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<Dormitory> Dormitories => Set<Dormitory>();
    public DbSet<HousingUnit> HousingUnits => Set<HousingUnit>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<AccommodationApplication> Applications => Set<AccommodationApplication>();
    public DbSet<ApplicationAccessToken> ApplicationAccessTokens => Set<ApplicationAccessToken>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<Placement> Placements => Set<Placement>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<MaintenanceRequest> Requests => Set<MaintenanceRequest>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<CleaningTask> CleaningTasks => Set<CleaningTask>();
    public DbSet<PeriodicMaintenance> PeriodicMaintenances => Set<PeriodicMaintenance>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<FaultReport> FaultReports => Set<FaultReport>();
    public DbSet<UserFacilityAssignment> UserFacilityAssignments => Set<UserFacilityAssignment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.TcNo).IsUnique();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(x => x.MustChangePassword).HasDefaultValue(false);
        });

        builder.Entity<UserFacilityAssignment>(entity =>
        {
            entity.HasOne(x => x.User)
                .WithMany(x => x.FacilityAssignments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Dormitory)
                .WithMany()
                .HasForeignKey(x => x.DormitoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HousingUnit)
                .WithMany()
                .HasForeignKey(x => x.HousingUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedBy)
                .WithMany()
                .HasForeignKey(x => x.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t => t.HasCheckConstraint("CK_UserFacilityAssignment_OneFacility",
                "([DormitoryId] IS NOT NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NULL AND [HousingUnitId] IS NOT NULL)"));
        });

        builder.Entity<Dormitory>()
            .Property(x => x.Type)
            .HasConversion<string>();

        builder.Entity<HousingUnit>()
            .Property(x => x.Type)
            .HasConversion<string>();

        builder.Entity<Building>(entity =>
        {
            entity.HasOne(x => x.Dormitory)
                .WithMany(x => x.Buildings)
                .HasForeignKey(x => x.DormitoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HousingUnit)
                .WithMany(x => x.Buildings)
                .HasForeignKey(x => x.HousingUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DormitoryId, x.BlockName }).IsUnique();
            entity.HasIndex(x => new { x.HousingUnitId, x.BlockName }).IsUnique();

            entity.ToTable(t => t.HasCheckConstraint("CK_Buildings_OneFacilityOwner",
                "([DormitoryId] IS NOT NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NULL AND [HousingUnitId] IS NOT NULL)"));
        });

        builder.Entity<Floor>()
            .HasIndex(x => new { x.BuildingId, x.FloorNumber })
            .IsUnique();

        builder.Entity<Room>(entity =>
        {
            entity.HasOne(x => x.BlockFloor)
                .WithMany(x => x.Rooms)
                .HasForeignKey(x => x.BlockFloorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.BlockFloorId, x.RoomNumber }).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>();
            entity.ToTable(t => t.HasCheckConstraint("CK_Rooms_Occupancy", "[CurrentOccupancy] >= 0 AND [CurrentOccupancy] <= [Capacity]"));
        });

        builder.Entity<AccommodationApplication>(entity =>
        {
            entity.ToTable("Applications");
            entity.Property(x => x.AccommodationType).HasConversion<string>();
            entity.Property(x => x.Source).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasIndex(x => x.ReferenceCode).IsUnique();
            entity.HasIndex(x => x.IdempotencyKeyHash).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.Applications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RequestedDormitory).WithMany().HasForeignKey(x => x.RequestedDormitoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RequestedHousingUnit).WithMany().HasForeignKey(x => x.RequestedHousingUnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ApprovedRoom).WithMany().HasForeignKey(x => x.ApprovedRoomId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_Applications_RequestedFacility",
                "([RequestedDormitoryId] IS NULL AND [RequestedHousingUnitId] IS NULL) OR ([RequestedDormitoryId] IS NOT NULL AND [RequestedHousingUnitId] IS NULL) OR ([RequestedDormitoryId] IS NULL AND [RequestedHousingUnitId] IS NOT NULL)"));
        });

        builder.Entity<ApplicationAccessToken>(entity =>
        {
            entity.Property(x => x.Purpose).HasConversion<string>();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.ApplicationId, x.Purpose, x.UsedAt, x.ExpiresAt });
            entity.HasOne(x => x.Application)
                .WithMany(x => x.AccessTokens)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.HasOne(x => x.Application)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ChangedBy)
                .WithMany()
                .HasForeignKey(x => x.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmailOutboxMessage>(entity =>
        {
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.HasIndex(x => new { x.SentAt, x.CreatedAt });
        });

        builder.Entity<Placement>(entity =>
        {
            entity.HasOne(x => x.User).WithMany(x => x.Placements).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Room).WithMany(x => x.Placements).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.UserId, x.IsActive }).HasFilter("[IsActive] = 1").IsUnique();
        });

        builder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>();
            entity.HasOne(x => x.User).WithMany(x => x.Payments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MaintenanceRequest>(entity =>
        {
            entity.ToTable("Requests");
            entity.Property(x => x.Status).HasConversion<string>();
            entity.HasOne(x => x.User).WithMany(x => x.Requests).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Room).WithMany(x => x.Requests).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CleaningTask>().Property(x => x.TaskType).HasMaxLength(80);
        builder.Entity<PeriodicMaintenance>().Property(x => x.SystemName).HasMaxLength(100);
        builder.Entity<StaffAssignment>(entity =>
        {
            entity.Property(x => x.Priority).HasMaxLength(20);
            entity.HasOne(x => x.Dormitory)
                .WithMany()
                .HasForeignKey(x => x.DormitoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.HousingUnit)
                .WithMany()
                .HasForeignKey(x => x.HousingUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_StaffAssignments_OneFacility",
                "([DormitoryId] IS NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NOT NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NULL AND [HousingUnitId] IS NOT NULL)"));
        });

        builder.Entity<FaultReport>(entity =>
        {
            entity.HasOne(x => x.Dormitory)
                .WithMany()
                .HasForeignKey(x => x.DormitoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.HousingUnit)
                .WithMany()
                .HasForeignKey(x => x.HousingUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_FaultReports_OneFacility",
                "([DormitoryId] IS NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NOT NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NULL AND [HousingUnitId] IS NOT NULL)"));
        });

        builder.Entity<Announcement>()
            .Property(x => x.TargetRole)
            .HasConversion<string>();

        SeedReferenceData(builder);
    }

    private static void SeedReferenceData(ModelBuilder builder)
    {
        builder.Entity<AppRole>().HasData(
            new AppRole { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = AppRoles.Admin, NormalizedName = AppRoles.Admin.ToUpperInvariant(), ConcurrencyStamp = "role-admin-v1" },
            new AppRole { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = AppRoles.Yetkili, NormalizedName = AppRoles.Yetkili.ToUpperInvariant(), ConcurrencyStamp = "role-yetkili-v1" },
            new AppRole { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = AppRoles.Ogrenci, NormalizedName = AppRoles.Ogrenci.ToUpperInvariant(), ConcurrencyStamp = "role-ogrenci-v1" },
            new AppRole { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = AppRoles.Personel, NormalizedName = AppRoles.Personel.ToUpperInvariant(), ConcurrencyStamp = "role-personel-v1" });

        builder.Entity<Announcement>().HasData(new Announcement
        {
            Id = 1,
            Title = "Basvuru Donemi Acildi",
            Content = "Yurt ve lojman basvurulari sistem uzerinden alinmaya baslanmistir.",
            TargetRole = AnnouncementTargetRole.All,
            CreatedAt = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });
    }
}
