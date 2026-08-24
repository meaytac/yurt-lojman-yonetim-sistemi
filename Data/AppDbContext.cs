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
    public DbSet<Placement> Placements => Set<Placement>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<MaintenanceRequest> Requests => Set<MaintenanceRequest>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<CleaningTask> CleaningTasks => Set<CleaningTask>();
    public DbSet<PeriodicMaintenance> PeriodicMaintenances => Set<PeriodicMaintenance>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.TcNo).IsUnique();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
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
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.HasOne(x => x.User).WithMany(x => x.Applications).HasForeignKey(x => x.UserId);
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

        builder.Entity<Dormitory>().HasData(new Dormitory
        {
            Id = 1,
            Name = "MTU Merkez Ogrenci Yurdu",
            Type = AccommodationType.Yurt,
            CampusLocation = "Battalgazi Yerleskesi",
            TotalCapacity = 120,
            IsActive = true
        });

        builder.Entity<HousingUnit>().HasData(new HousingUnit
        {
            Id = 1,
            Name = "MTU Personel Lojmanlari",
            Type = AccommodationType.Lojman,
            CampusLocation = "Battalgazi Yerleskesi",
            TotalCapacity = 40,
            IsActive = true
        });

        builder.Entity<Building>().HasData(
            new Building { Id = 1, DormitoryId = 1, BlockName = "A Blok" },
            new Building { Id = 2, HousingUnitId = 1, BlockName = "L Blok" });

        builder.Entity<Floor>().HasData(
            new Floor { Id = 1, BuildingId = 1, FloorNumber = 1 },
            new Floor { Id = 2, BuildingId = 2, FloorNumber = 1 });

        builder.Entity<Room>().HasData(
            new Room { Id = 1, BlockFloorId = 1, RoomNumber = "101", Capacity = 4, CurrentOccupancy = 0, Status = RoomStatus.Empty, Price = 2500 },
            new Room { Id = 2, BlockFloorId = 1, RoomNumber = "102", Capacity = 4, CurrentOccupancy = 0, Status = RoomStatus.Empty, Price = 2500 },
            new Room { Id = 3, BlockFloorId = 2, RoomNumber = "L101", Capacity = 1, CurrentOccupancy = 0, Status = RoomStatus.Empty, Price = 5500 });

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
