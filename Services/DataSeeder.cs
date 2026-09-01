using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Data;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Services;

public static class DataSeeder
{
    // Ogrenci ilk giris sifresi (Identity politikasi: buyuk harf + kucuk harf + rakam gerektirir)
    public const string OgrenciIlkSifre = "Ogrenci123";
    private const string EskiOrtakSifre = "Admin123!";

    public static async Task SeedDemoAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var accommodationService = scope.ServiceProvider.GetRequiredService<IAccommodationService>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        await SeedRolesAsync(roleManager);
        await SeedData.SeedAsync(db);
        var users = await SeedUsersAsync(userManager);
        foreach (var student in new[] { users.Student1, users.Student2, users.Student3 })
        {
            await ResetStudentPasswordIfLegacyAsync(userManager, student);
        }
        await SeedFacilitiesAsync(db);
        await SeedOperationalDataAsync(db, users);
        await PlaceDemoStudentsAsync(db, accommodationService, userManager, [users.Student1, users.Student2, users.Student3], env);
    }

    public static Task SeedIdentityAsync(IServiceProvider services) => SeedDemoAsync(services);

    private static async Task SeedRolesAsync(RoleManager<AppRole> roleManager)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new AppRole { Name = role });
            }
        }
    }

    private static async Task<DemoUsers> SeedUsersAsync(UserManager<AppUser> userManager)
    {
        var admin = await EnsureUserAsync(userManager, "admin@ozal.edu.tr", "Sistem Yoneticisi", "11111111111", "ADMIN-001", AppRoles.Admin, "+904220000001");
        var officer = await EnsureUserAsync(userManager, "yetkili@ozal.edu.tr", "Yurt Isleri Yetkilisi", "22222222222", "PER-100", AppRoles.Yetkili, "+904220000002");
        var student1 = await EnsureUserAsync(userManager, "ayse.yilmaz@ogr.ozal.edu.tr", "Ayse Yilmaz", "33333333333", "OGR-2026-001", AppRoles.Ogrenci, "+905550000001");
        var student2 = await EnsureUserAsync(userManager, "mehmet.kaya@ogr.ozal.edu.tr", "Mehmet Kaya", "44444444444", "OGR-2026-002", AppRoles.Ogrenci, "+905550000002");
        var student3 = await EnsureUserAsync(userManager, "zeynep.demir@ogr.ozal.edu.tr", "Zeynep Demir", "55555555555", "OGR-2026-003", AppRoles.Ogrenci, "+905550000003");
        var staff1 = await EnsureUserAsync(userManager, "ali.celik@ozal.edu.tr", "Ali Celik", "66666666666", "PRS-2026-014", AppRoles.TeknikPersonel, "+905550000004");
        var staff2 = await EnsureUserAsync(userManager, "elif.sahin@ozal.edu.tr", "Elif Sahin", "77777777777", "PRS-2026-019", AppRoles.TemizlikPersoneli, "+905550000005");

        return new DemoUsers(admin, officer, student1, student2, student3, staff1, staff2);
    }

    // Ogrenci hesaplari: eski ortak "Admin123!" sifresi hala kullaniliyorsa
    // "Ogrenci123" ile degistirilir ve ilk giriste sifre degisimi zorunlu kilinir.
    // Ogrenci sifresini kendisi degistirdikten sonra (MustChangePassword=false ve eski sifre artık gecersiz)
    // bu blok tekrar calismaz; kullanici sifresi korunur.
    private static async Task ResetStudentPasswordIfLegacyAsync(UserManager<AppUser> userManager, AppUser student)
    {
        if (!await userManager.CheckPasswordAsync(student, EskiOrtakSifre))
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(student);
        var result = await userManager.ResetPasswordAsync(student, token, OgrenciIlkSifre);
        if (!result.Succeeded)
        {
            Console.WriteLine($"[Seed] UYARI: {student.Email} sifre guncellenemedi: {string.Join(" ", result.Errors.Select(x => x.Description))}");
            return;
        }

        student.MustChangePassword = true;
        await userManager.UpdateAsync(student);
        Console.WriteLine($"[Seed] {student.Email} ilk sifresi '{OgrenciIlkSifre}' olarak ayarlandi (ilk giriste degistirilmeli).");
    }

    private static async Task<AppUser> EnsureUserAsync(
        UserManager<AppUser> userManager,
        string email,
        string fullName,
        string tcNo,
        string studentStaffNo,
        string role,
        string phoneNumber)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                TcNo = tcNo,
                StudentStaffNo = studentStaffNo,
                Role = role,
                PhoneNumber = phoneNumber,
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(user, "Admin123!");
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", createResult.Errors.Select(x => x.Description)));
            }
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(role))
        {
            if (currentRoles.Count > 0)
            {
                await userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(x => x.Description)));
            }
        }

        user.Role = role;
        user.LockoutEnd = null;
        await userManager.UpdateAsync(user);
        return user;
    }

    private static async Task SeedFacilitiesAsync(AppDbContext db)
    {
        var dormitory = await db.Dormitories.FirstOrDefaultAsync(x => x.Name == "MTU Merkez Ogrenci Yurdu")
            ?? db.Dormitories.Add(new Dormitory
            {
                Name = "MTU Merkez Ogrenci Yurdu",
                Type = AccommodationType.Yurt,
                CampusLocation = "Battalgazi Yerleskesi",
                TotalCapacity = 120,
                IsActive = true
            }).Entity;

        var secondDormitory = await db.Dormitories.FirstOrDefaultAsync(x => x.Name == "Yesilyurt Kiz Ogrenci Yurdu")
            ?? db.Dormitories.Add(new Dormitory
            {
                Name = "Yesilyurt Kiz Ogrenci Yurdu",
                Type = AccommodationType.Yurt,
                CampusLocation = "Yesilyurt Yerleskesi",
                TotalCapacity = 96,
                IsActive = true
            }).Entity;

        var housing = await db.HousingUnits.FirstOrDefaultAsync(x => x.Name == "MTU Personel Lojmanlari")
            ?? db.HousingUnits.Add(new HousingUnit
            {
                Name = "MTU Personel Lojmanlari",
                Type = AccommodationType.Lojman,
                CampusLocation = "Battalgazi Yerleskesi",
                TotalCapacity = 40,
                IsActive = true
            }).Entity;

        await db.SaveChangesAsync();

        var aBlock = await EnsureBuildingAsync(db, dormitory.Id, null, "A Blok");
        var bBlock = await EnsureBuildingAsync(db, secondDormitory.Id, null, "B Blok");
        var lBlock = await EnsureBuildingAsync(db, null, housing.Id, "L Blok");

        var aFloor1 = await EnsureFloorAsync(db, aBlock.Id, 1);
        var aFloor2 = await EnsureFloorAsync(db, aBlock.Id, 2);
        var bFloor1 = await EnsureFloorAsync(db, bBlock.Id, 1);
        var lFloor1 = await EnsureFloorAsync(db, lBlock.Id, 1);

        await EnsureRoomAsync(db, aFloor1.Id, "101", 4, 2500, RoomStatus.Empty);
        await EnsureRoomAsync(db, aFloor1.Id, "102", 4, 2500, RoomStatus.Empty);
        await EnsureRoomAsync(db, aFloor2.Id, "201", 3, 2700, RoomStatus.Empty);
        await EnsureRoomAsync(db, bFloor1.Id, "B-103", 4, 2400, RoomStatus.Empty);
        await EnsureRoomAsync(db, bFloor1.Id, "B-104", 4, 2400, RoomStatus.Maintenance);
        await EnsureRoomAsync(db, lFloor1.Id, "L101", 1, 5500, RoomStatus.Empty);
        await EnsureRoomAsync(db, lFloor1.Id, "L102", 1, 5750, RoomStatus.Empty);

        dormitory.TotalCapacity = await db.Rooms.Where(x => x.BlockFloor.Building.DormitoryId == dormitory.Id).SumAsync(x => x.Capacity);
        secondDormitory.TotalCapacity = await db.Rooms.Where(x => x.BlockFloor.Building.DormitoryId == secondDormitory.Id).SumAsync(x => x.Capacity);
        housing.TotalCapacity = await db.Rooms.Where(x => x.BlockFloor.Building.HousingUnitId == housing.Id).SumAsync(x => x.Capacity);
        await db.SaveChangesAsync();
    }

    private static async Task<Building> EnsureBuildingAsync(AppDbContext db, int? dormitoryId, int? housingUnitId, string blockName)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(x =>
            x.BlockName == blockName &&
            x.DormitoryId == dormitoryId &&
            x.HousingUnitId == housingUnitId);

        if (building is not null)
        {
            return building;
        }

        building = new Building { DormitoryId = dormitoryId, HousingUnitId = housingUnitId, BlockName = blockName };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        return building;
    }

    private static async Task<Floor> EnsureFloorAsync(AppDbContext db, int buildingId, int floorNumber)
    {
        var floor = await db.Floors.FirstOrDefaultAsync(x => x.BuildingId == buildingId && x.FloorNumber == floorNumber);
        if (floor is not null)
        {
            return floor;
        }

        floor = new Floor { BuildingId = buildingId, FloorNumber = floorNumber };
        db.Floors.Add(floor);
        await db.SaveChangesAsync();
        return floor;
    }

    private static async Task<Room> EnsureRoomAsync(AppDbContext db, int floorId, string roomNumber, int capacity, decimal price, RoomStatus status)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(x => x.BlockFloorId == floorId && x.RoomNumber == roomNumber);
        if (room is null)
        {
            room = new Room
            {
                BlockFloorId = floorId,
                RoomNumber = roomNumber,
                Capacity = capacity,
                Price = price,
                Status = status,
                CurrentOccupancy = 0
            };
            db.Rooms.Add(room);
        }
        else
        {
            room.Capacity = capacity;
            room.Price = price;
            if (status == RoomStatus.Maintenance)
            {
                room.Status = RoomStatus.Maintenance;
            }
        }

        await db.SaveChangesAsync();
        return room;
    }

    private static async Task SeedOperationalDataAsync(AppDbContext db, DemoUsers users)
    {
        if (await db.Applications.CountAsync() >= 20) { /* SeedData zaten 20 basvuru olusturdu, tekrar ekleme */ }
        else if (!await db.Applications.AnyAsync(x => x.UserId == users.Student1.Id))
        {
            db.Applications.AddRange(
                new AccommodationApplication { UserId = users.Student1.Id, AccommodationType = AccommodationType.Yurt, DocumentUrl = "/uploads/demo/ogrenci-belgesi-ayse.pdf", Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new AccommodationApplication { UserId = users.Student2.Id, AccommodationType = AccommodationType.Yurt, DocumentUrl = "/uploads/demo/ogrenci-belgesi-mehmet.pdf", Status = ApplicationStatus.Approved, CreatedAt = DateTime.UtcNow.AddDays(-5), UpdatedAt = DateTime.UtcNow.AddDays(-2) },
                new AccommodationApplication { UserId = users.Staff1.Id, AccommodationType = AccommodationType.Lojman, DocumentUrl = "/uploads/demo/personel-gorev-yeri.pdf", Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new AccommodationApplication { UserId = users.Staff2.Id, AccommodationType = AccommodationType.Lojman, DocumentUrl = "/uploads/demo/personel-belgesi.pdf", Status = ApplicationStatus.Rejected, CreatedAt = DateTime.UtcNow.AddDays(-8), UpdatedAt = DateTime.UtcNow.AddDays(-6) },
                new AccommodationApplication { UserId = users.Student3.Id, AccommodationType = AccommodationType.Yurt, DocumentUrl = "/uploads/demo/ogrenci-belgesi-zeynep.pdf", Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-1) });
        }

        await db.SaveChangesAsync();

        // Ogrenci yerlestirmeleri artik PlaceDemoStudentsAsync ile rastgele yapilir (asagida).
        await EnsurePlacementAsync(db, users.Staff1.Id, "L101", DateTime.UtcNow.AddDays(-45));

        if (await db.Requests.CountAsync() < 7)
        {
            var allRooms = await db.Rooms.OrderBy(x => x.RoomNumber).ToListAsync();
            var roomByNumber = allRooms.ToDictionary(x => x.RoomNumber, x => x.Id);

            var requestsToAdd = new List<MaintenanceRequest>();

            if (roomByNumber.TryGetValue("101", out var r101))
                requestsToAdd.Add(new MaintenanceRequest { UserId = users.Student2.Id, RoomId = r101, Category = "Elektrik", Description = "Oda içindeki çalışma masası prizi temassızlık yapıyor, fiş takılıyken elektrik kesiliyor.", PhotoUrl = "/uploads/demo/priz.jpg", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddHours(-5) });
            if (roomByNumber.TryGetValue("102", out var r102))
                requestsToAdd.Add(new MaintenanceRequest { UserId = users.Student3.Id, RoomId = r102, Category = "Su Tesisatı", Description = "Banyo musluğu sürekli damlatıyor, su israfı oluyor ve geceleri ses yapıyor.", PhotoUrl = "/uploads/demo/petek.jpg", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddDays(-1) });
            if (roomByNumber.TryGetValue("201", out var r201))
                requestsToAdd.Add(new MaintenanceRequest { UserId = users.Student1.Id, RoomId = r201, Category = "Isıtma", Description = "Petek vanası sıkışmış, petek ısınmıyor, oda çok soğuk. Kış ayarı kontrol edilmeli.", Status = RequestStatus.InProgress, CreatedAt = DateTime.UtcNow.AddDays(-2) });
            if (roomByNumber.TryGetValue("B-103", out var rB103))
                requestsToAdd.Add(new MaintenanceRequest { UserId = users.Student2.Id, RoomId = rB103, Category = "Mobilya", Description = "Ranza üst kat merdiveni gevşemiş, çıkarken sallanıyor, düşme riski var.", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddHours(-12) });
            if (roomByNumber.TryGetValue("B-104", out var rB104))
                requestsToAdd.Add(new MaintenanceRequest { UserId = users.Student1.Id, RoomId = rB104, Category = "İnternet", Description = "Oda içinde Wi-Fi sinyali çok zayıf, bağlantı sürekli kopuyor, çevrimiçi derslere katılamıyorum.", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddDays(-3) });
            if (roomByNumber.TryGetValue("L101", out var rL101))
                requestsToAdd.Add(new MaintenanceRequest { UserId = users.Staff1.Id, RoomId = rL101, Category = "Banyo", Description = "Banyo gideri tıkalı, duş sonrası su birikiyor ve kötü koku yapıyor.", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddHours(-8) });
            if (roomByNumber.TryGetValue("L102", out var rL102))
                requestsToAdd.Add(new MaintenanceRequest { UserId = users.Staff2.Id, RoomId = rL102, Category = "Kapı/Kilit", Description = "Oda kapı kilidi bazen açılmıyor, kartı birkaç kez okutmak gerekiyor.", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddDays(-4) });

            // Eksik kalanları tamamlamak için genel ekleme (oda sayısı yetersizse)
            while (requestsToAdd.Count < 7 && allRooms.Count > 0)
            {
                var fallbackRoom = allRooms[requestsToAdd.Count % allRooms.Count];
                requestsToAdd.Add(new MaintenanceRequest { UserId = users.Student1.Id, RoomId = fallbackRoom.Id, Category = "Genel", Description = "Genel bakım kontrolü gerekiyor.", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddHours(-requestsToAdd.Count) });
            }

            db.Requests.AddRange(requestsToAdd.Take(7));
        }

        if (!await db.CleaningTasks.AnyAsync())
        {
            db.CleaningTasks.AddRange(
                new CleaningTask { TaskType = "Oda temizliği", Location = "A Blok / Oda 101", Notes = "Banyo ve tuvalet dahil temizleyin." },
                new CleaningTask { TaskType = "Ortak alan temizliği", Location = "A Blok / 1. Kat koridoru", Notes = "Zemin ve korkulukları temizleyin." },
                new CleaningTask { TaskType = "Çöp toplama", Location = "B Blok / Katlar", Notes = "Kat toplama noktalarını kontrol edin." },
                new CleaningTask { TaskType = "Fiziksel düzenleme", Location = "Yurt yönetim ofisi", Notes = "İki çalışma masasını yerleştirin." });
        }

        if (!await db.PeriodicMaintenances.AnyAsync())
        {
            db.PeriodicMaintenances.AddRange(
                new PeriodicMaintenance { SystemName = "Yangın sistemi", Location = "A Blok", IntervalDays = 30, NextMaintenanceDate = DateTime.UtcNow.Date.AddDays(2), Notes = "Alarm paneli ve dedektör kontrolü." },
                new PeriodicMaintenance { SystemName = "Asansör", Location = "B Blok", IntervalDays = 30, NextMaintenanceDate = DateTime.UtcNow.Date.AddDays(6), Notes = "Periyodik güvenlik kontrolü." },
                new PeriodicMaintenance { SystemName = "Isıtma sistemi", Location = "Merkez kazan dairesi", IntervalDays = 14, NextMaintenanceDate = DateTime.UtcNow.Date.AddDays(1), Notes = "Basınç ve kaçak kontrolü." });
        }

        if (!await db.StaffAssignments.AnyAsync())
        {
            db.StaffAssignments.AddRange(
                new StaffAssignment { AssignedRole = AppRoles.TemizlikPersoneli, Title = "Ortak alan temizliği", Location = "A Blok / giriş ve 1. kat", Details = "Giriş zemini ve ortak kullanım alanlarını vardiya sonuna kadar temizleyin.", Priority = "Yüksek", DueDate = DateTime.UtcNow.Date },
                new StaffAssignment { AssignedRole = AppRoles.TeknikPersonel, Title = "Wi-Fi ağ düzenlemesi", Location = "B Blok / 1. kat", Details = "Erişim noktası kapsamasını kontrol edin ve kanal düzenlemesi yapın.", Priority = "Normal", DueDate = DateTime.UtcNow.Date.AddDays(2) },
                new StaffAssignment { AssignedRole = AppRoles.TeknikPersonel, Title = "Banyo aydınlatma arızası", Location = "A Blok / Oda 204", Details = "Aydınlatma armatürü ve anahtar kontrolü.", Priority = "Acil", IsMaintenanceRequest = true, DueDate = DateTime.UtcNow.Date.AddDays(1) });
        }

        if (!await db.Payments.AnyAsync())
        {
            db.Payments.AddRange(
                new Payment { UserId = users.Student2.Id, Amount = 2500, DueDate = DateTime.UtcNow.AddDays(-5), Status = PaymentStatus.Overdue, Description = "Agustos yurt ucreti" },
                new Payment { UserId = users.Student3.Id, Amount = 2500, DueDate = DateTime.UtcNow.AddDays(10), Status = PaymentStatus.Unpaid, Description = "Agustos yurt ucreti" },
                new Payment { UserId = users.Staff1.Id, Amount = 5500, DueDate = DateTime.UtcNow.AddDays(-2), Status = PaymentStatus.Overdue, Description = "Lojman kira tahakkuku" },
                new Payment { UserId = users.Student2.Id, Amount = 2500, DueDate = DateTime.UtcNow.AddMonths(-1), PaidDate = DateTime.UtcNow.AddDays(-20), Status = PaymentStatus.Paid, Description = "Temmuz yurt ucreti" });
        }

        if (await db.Announcements.CountAsync() < 5 && !await db.Announcements.AnyAsync(x => x.Title == "Demo Sunum Duyurusu"))
        {
            db.Announcements.Add(new Announcement
            {
                Title = "Demo Sunum Duyurusu",
                Content = "Yurt ve lojman yonetim sistemi demo verileriyle calismaktadir.",
                TargetRole = AnnouncementTargetRole.All,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
        await RecalculateRoomsAsync(db);
    }

    private static async Task EnsurePlacementAsync(AppDbContext db, Guid userId, string roomNumber, DateTime checkInDate)
    {
        if (await db.Placements.AnyAsync(x => x.UserId == userId && x.IsActive))
        {
            return;
        }

        var room = await db.Rooms.FirstAsync(x => x.RoomNumber == roomNumber);
        db.Placements.Add(new Placement { UserId = userId, RoomId = room.Id, CheckInDate = checkInDate, IsActive = true });
        await db.SaveChangesAsync();
    }

    // Demo ogrencilerini musait odalara RASTGELE yerlestirir ve kisa bir kayit dosyasi tutar.
    // Kayit dosyasi: <proje-koku>/ogrenci_yerlestirme_kaydi.txt
    private static async Task PlaceDemoStudentsAsync(
        AppDbContext db,
        IAccommodationService accommodationService,
        UserManager<AppUser> userManager,
        AppUser[] students,
        IWebHostEnvironment env)
    {
        var lines = new List<string>();

        foreach (var student in students)
        {
            if (await db.Placements.AnyAsync(x => x.UserId == student.Id && x.IsActive))
            {
                continue;
            }

            var availableRooms = await db.Rooms.AsNoTracking()
                .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.Dormitory)
                .Include(x => x.BlockFloor).ThenInclude(x => x.Building).ThenInclude(x => x.HousingUnit)
                .Where(x => x.Status != RoomStatus.Maintenance && x.CurrentOccupancy < x.Capacity)
                .ToListAsync();

            if (availableRooms.Count == 0)
            {
                Console.WriteLine("[Seed] UYARI: Musait oda yok, yerlestirme atlandi.");
                continue;
            }

            var room = availableRooms[Random.Shared.Next(availableRooms.Count)];
            var type = room.BlockFloor.Building.DormitoryId != null ? AccommodationType.Yurt : AccommodationType.Lojman;
            await accommodationService.PlaceUserAsync(student.Id, type, room.Id, CancellationToken.None);

            var facility = room.BlockFloor.Building.Dormitory?.Name ?? room.BlockFloor.Building.HousingUnit!.Name;
            var block = room.BlockFloor.Building.BlockName;
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm};{student.Email};{student.FullName};{facility};{block};Kat {room.BlockFloor.FloorNumber};Oda {room.RoomNumber}";
            lines.Add(line);
            Console.WriteLine($"[Seed] Yerlestirme: {student.FullName} -> {facility} / {block} / Oda {room.RoomNumber}");
        }

        if (lines.Count > 0)
        {
            var path = Path.Combine(env.ContentRootPath, "ogrenci_yerlestirme_kaydi.txt");
            await File.AppendAllLinesAsync(path, lines);
        }
    }

    private static async Task RecalculateRoomsAsync(AppDbContext db)
    {
        var rooms = await db.Rooms.Include(x => x.Placements).ToListAsync();
        foreach (var room in rooms)
        {
            if (room.Status == RoomStatus.Maintenance)
            {
                room.CurrentOccupancy = 0;
                continue;
            }

            room.CurrentOccupancy = room.Placements.Count(x => x.IsActive);
            room.Status = room.CurrentOccupancy == 0
                ? RoomStatus.Empty
                : room.CurrentOccupancy >= room.Capacity
                    ? RoomStatus.Full
                    : RoomStatus.PartiallyFull;
        }

        await db.SaveChangesAsync();
    }

    private sealed record DemoUsers(
        AppUser Admin,
        AppUser Officer,
        AppUser Student1,
        AppUser Student2,
        AppUser Student3,
        AppUser Staff1,
        AppUser Staff2);
}
