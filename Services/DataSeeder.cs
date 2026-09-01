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
        await SeedFacilityAssignmentsAsync(db, users);
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
        var random = new Random(20260823);
        var firstNames = new[]
        {
            "Ahmet", "Mehmet", "Mustafa", "Ali", "Hüseyin", "İbrahim", "İsmail", "Yusuf", "Ömer", "Osman",
            "Murat", "Ramazan", "Halil", "Süleyman", "Bekir", "Fatih", "Mahmut", "Salih", "Kemal", "Hakan",
            "Adem", "Metin", "Yasin", "Emre", "Burak", "Gökhan", "Onur", "Serkan", "Volkan", "Mesut",
            "Erdal", "Turan", "Uğur", "Oğuz", "Cihan", "Sinan", "Tarık", "Levent", "Umut", "Barış",
            "Erkan", "Deniz", "Can", "Kerem", "Efe", "Kaan", "Arda", "Ege", "Doruk", "Alp",
            "Ayşe", "Fatma", "Hatice", "Zeynep", "Elif", "Merve", "Büşra", "Kübra", "Esra", "Sümeyye",
            "Gözde", "Selin", "İrem", "Büşra", "Melike", "Ceren", "Ezgi", "Bahar", "Derya", "Ebru",
            "Gamze", "Gizem", "Hande", "Pınar", "Sinem", "Tuğba", "Yasemin", "Özlem", "Eda", "Sevgi",
            "Sultan", "Ayten", "Hacer", "Meryem", "Rukiye", "Emine", "Şerife", "Gül", "Figen", "Nur",
            "Betül", "Tuba", "Dilek", "Arzu", "Burcu", "Demet", "Fulya", "Gülşah", "Necla", "Nihal"
        };
        var lastNames = new[]
        {
            "Yılmaz", "Kaya", "Demir", "Çelik", "Şahin", "Yıldız", "Yıldırım", "Öztürk", "Aydın", "Özdemir",
            "Arslan", "Doğan", "Kılıç", "Aslan", "Çetin", "Kara", "Koç", "Kurt", "Özcan", "Şimşek",
            "Polat", "Özkan", "Erdoğan", "Yavuz", "Çakır", "Aksoy", "Güler", "Tekin", "Acar", "Can",
            "Dündar", "Ertürk", "Koçak", "Korkmaz", "Güneş", "Bulut", "Keskin", "Yalçın", "Topal", "Eren",
            "Gündoğdu", "Avcı", "Sönmez", "Özkan", "Aktaş", "Atalay", "Baş", "Bayrak", "Çiftçi", "Demirci",
            "Dinç", "Durmaz", "Gök", "Gözüpek", "Güven", "Işık", "Kaplan", "Kartal", "Kesici", "Köse",
            "Oğuz", "pek", "Sarıkaya", "Savaş", "Sezer", "Turan", "Uçar", "Ünal", "Yalçın", "Yaman",
            "Yaşar", "Yener", "Yeşilyurt", "Yiğit", "Yüksel", "Zengin", "Alkan", "Altun", "Ay", "Başaran",
            "Bayram", "Bozkurt", "Ceylan", "Çoban", "Demirel", "Dikmen", "Diler", "Ergin", "Gazioğlu", "Gümüş",
            "İnce", "Kahraman", "Kalaycı", "Karaca", "Karakurt", "Kart", "Kaygusuz", "Koca", "Küçük"
        };
        var usedFullNames = new HashSet<string>(StringComparer.Ordinal);
        var usedTcNumbers = (await userManager.Users
            .Select(x => x.TcNo)
            .ToListAsync())
            .ToHashSet(StringComparer.Ordinal);

        (string FullName, string Email) NextIdentity()
        {
            string fullName;
            do
            {
                fullName = $"{firstNames[random.Next(firstNames.Length)]} {lastNames[random.Next(lastNames.Length)]}";
            } while (!usedFullNames.Add(fullName));

            return (fullName, $"{ToEmailName(fullName)}@ozal.edu.tr");
        }

        var adminIdentity = (FullName: "Sistem Yöneticisi", Email: "admin@ozal.edu.tr");
        var officerIdentity = NextIdentity();
        var student1Identity = NextIdentity();
        var student2Identity = NextIdentity();
        var student3Identity = NextIdentity();
        var staff1Identity = NextIdentity();
        var staff2Identity = NextIdentity();

        var admin = await EnsureUserAsync(userManager, adminIdentity.Email, adminIdentity.FullName, CreateValidTcNo(random, usedTcNumbers), "ADMIN-001", AppRoles.Admin, "+904220000001", "Demo123!");
        var officer = await EnsureUserAsync(userManager, officerIdentity.Email, officerIdentity.FullName, CreateValidTcNo(random, usedTcNumbers), "YET-2026-001", AppRoles.Yetkili, "+904220000002");
        var student1 = await EnsureUserAsync(userManager, student1Identity.Email, student1Identity.FullName, CreateValidTcNo(random, usedTcNumbers), "OGR-2026-201", AppRoles.Ogrenci, "+905550000001");
        var student2 = await EnsureUserAsync(userManager, student2Identity.Email, student2Identity.FullName, CreateValidTcNo(random, usedTcNumbers), "OGR-2026-202", AppRoles.Ogrenci, "+905550000002");
        var student3 = await EnsureUserAsync(userManager, student3Identity.Email, student3Identity.FullName, CreateValidTcNo(random, usedTcNumbers), "OGR-2026-203", AppRoles.Ogrenci, "+905550000003");
        var staff1 = await EnsureUserAsync(userManager, staff1Identity.Email, staff1Identity.FullName, CreateValidTcNo(random, usedTcNumbers), "PER-2026-101", AppRoles.Personel, "+905550000004");
        var staff2 = await EnsureUserAsync(userManager, staff2Identity.Email, staff2Identity.FullName, CreateValidTcNo(random, usedTcNumbers), "PER-2026-102", AppRoles.Personel, "+905550000005");

        return new DemoUsers(admin, officer, student1, student2, student3, staff1, staff2);
    }

    private static string ToEmailName(string fullName) => fullName
        .ToLowerInvariant()
        .Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i")
        .Replace("ö", "o").Replace("ş", "s").Replace("ü", "u")
        .Replace(" ", ".");

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
        string phoneNumber,
        string password = "Admin123!")
    {
        var user = await userManager.FindByEmailAsync(email)
            ?? userManager.Users.FirstOrDefault(x => x.StudentStaffNo == studentStaffNo);
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

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", createResult.Errors.Select(x => x.Description)));
            }
        }

        user.UserName = email;
        user.Email = email;
        user.FullName = fullName;
        user.TcNo = tcNo;
        user.StudentStaffNo = studentStaffNo;
        user.Role = role;
        user.PhoneNumber = phoneNumber;
        user.EmailConfirmed = true;

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

    private static string CreateValidTcNo(Random random, HashSet<string> usedNumbers)
    {
        string tcNo;
        do
        {
            var digits = new int[11];
            digits[0] = random.Next(1, 10);
            for (var index = 1; index < 9; index++)
            {
                digits[index] = random.Next(10);
            }

            digits[9] = ((digits[0] + digits[2] + digits[4] + digits[6] + digits[8]) * 7
                - (digits[1] + digits[3] + digits[5] + digits[7])) % 10;
            if (digits[9] < 0) digits[9] += 10;
            digits[10] = digits.Take(10).Sum() % 10;
            tcNo = string.Concat(digits);
        } while (!usedNumbers.Add(tcNo));

        return tcNo;
    }

    private static async Task SeedFacilitiesAsync(AppDbContext db)
    {
        var random = new Random(20260822);
        var dormitory = await db.Dormitories.FirstOrDefaultAsync(x => x.Name == "MTÜ Erkek Öğrenci Yurdu")
            ?? db.Dormitories.Add(new Dormitory
            {
                Name = "MTÜ Erkek Öğrenci Yurdu",
                Type = AccommodationType.Yurt,
                CampusLocation = "Battalgazi Yerleşkesi",
                IsActive = true
            }).Entity;

        var secondDormitory = await db.Dormitories.FirstOrDefaultAsync(x => x.Name == "MTÜ Kız Öğrenci Yurdu")
            ?? db.Dormitories.Add(new Dormitory
            {
                Name = "MTÜ Kız Öğrenci Yurdu",
                Type = AccommodationType.Yurt,
                CampusLocation = "Yeşilyurt Yerleşkesi",
                IsActive = true
            }).Entity;

        var housing = await db.HousingUnits.FirstOrDefaultAsync(x => x.Name == "MTÜ Akademik Personel Lojmanı")
            ?? db.HousingUnits.Add(new HousingUnit
            {
                Name = "MTÜ Akademik Personel Lojmanı",
                Type = AccommodationType.Lojman,
                CampusLocation = "Battalgazi Yerleşkesi",
                IsActive = true
            }).Entity;

        await db.SaveChangesAsync();

        var targetDormitoryIds = new[] { dormitory.Id, secondDormitory.Id };
        var targetHousingUnitIds = new[] { housing.Id };
        var extraDormitories = await db.Dormitories
            .Where(x => !targetDormitoryIds.Contains(x.Id))
            .ToListAsync();
        var extraHousingUnits = await db.HousingUnits
            .Where(x => !targetHousingUnitIds.Contains(x.Id))
            .ToListAsync();
        var extraBuildings = await db.Buildings
            .Where(x => (x.DormitoryId.HasValue && !targetDormitoryIds.Contains(x.DormitoryId.Value)) ||
                        (x.HousingUnitId.HasValue && !targetHousingUnitIds.Contains(x.HousingUnitId.Value)))
            .ToListAsync();
        await RemoveBuildingsAsync(db, extraBuildings);
        db.Dormitories.RemoveRange(extraDormitories);
        db.HousingUnits.RemoveRange(extraHousingUnits);
        await db.SaveChangesAsync();

        var expectedBuildings = new (int? DormitoryId, int? HousingUnitId, string BlockName)[]
        {
            (dormitory.Id, (int?)null, "A Blok"), (dormitory.Id, (int?)null, "B Blok"),
            (secondDormitory.Id, (int?)null, "A Blok"), (secondDormitory.Id, (int?)null, "B Blok"),
            ((int?)null, housing.Id, "L Blok")
        };

        var existingBuildings = await db.Buildings
            .Where(x => (x.DormitoryId.HasValue && targetDormitoryIds.Contains(x.DormitoryId.Value)) ||
                        (x.HousingUnitId.HasValue && targetHousingUnitIds.Contains(x.HousingUnitId.Value)))
            .ToListAsync();
        var obsoleteBuildings = existingBuildings.Where(x => !expectedBuildings.Any(expected =>
            expected.Item1 == x.DormitoryId && expected.Item2 == x.HousingUnitId && expected.Item3 == x.BlockName)).ToList();
        var obsoleteRooms = new List<Room>();
        foreach (var expected in expectedBuildings)
        {
            var building = await EnsureBuildingAsync(db, expected.Item1, expected.Item2, expected.Item3);
            var extraFloors = await db.Floors
                .Where(x => x.BuildingId == building.Id && (x.FloorNumber < 1 || x.FloorNumber > 2))
                .ToListAsync();
            foreach (var extraFloor in extraFloors)
            {
                obsoleteRooms.AddRange(await db.Rooms.Where(x => x.BlockFloorId == extraFloor.Id).ToListAsync());
            }
            db.Floors.RemoveRange(extraFloors);
            await db.SaveChangesAsync();

            for (var floorNumber = 1; floorNumber <= 2; floorNumber++)
            {
                var floor = await EnsureFloorAsync(db, building.Id, floorNumber);
                var isHousing = expected.Item2.HasValue;
                var prefix = isHousing ? "L" : string.Empty;
                var expectedRoomNumbers = Enumerable.Range(1, 10)
                    .Select(index => $"{prefix}{floorNumber}{index:00}")
                    .ToHashSet();
                var existingRooms = await db.Rooms.Where(x => x.BlockFloorId == floor.Id).ToListAsync();
                obsoleteRooms.AddRange(existingRooms.Where(x => !expectedRoomNumbers.Contains(x.RoomNumber)));
                foreach (var roomNumber in expectedRoomNumbers)
                {
                    var capacity = isHousing ? random.Next(1, 3) : random.Next(3, 5);
                    var occupancy = random.Next(capacity + 1);
                    await EnsureRoomAsync(db, floor.Id, roomNumber, capacity, isHousing ? 5500 : 2500, occupancy);
                }
            }
        }

        await RelocatePlacementsAsync(db, obsoleteBuildings, obsoleteRooms);
        await RemoveRoomsAsync(db, obsoleteRooms);
        await RemoveBuildingsAsync(db, obsoleteBuildings);

        dormitory.TotalCapacity = await db.Rooms.Where(x => x.BlockFloor.Building.DormitoryId == dormitory.Id).SumAsync(x => x.Capacity);
        secondDormitory.TotalCapacity = await db.Rooms.Where(x => x.BlockFloor.Building.DormitoryId == secondDormitory.Id).SumAsync(x => x.Capacity);
        housing.TotalCapacity = await db.Rooms.Where(x => x.BlockFloor.Building.HousingUnitId == housing.Id).SumAsync(x => x.Capacity);
        await db.SaveChangesAsync();
    }

    private static async Task SeedFacilityAssignmentsAsync(AppDbContext db, DemoUsers users)
    {
        var defaultDormitoryId = await db.Dormitories
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        var defaultHousingUnitId = defaultDormitoryId.HasValue
            ? null
            : await db.HousingUnits
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();

        if (!defaultDormitoryId.HasValue && !defaultHousingUnitId.HasValue)
        {
            return;
        }

        var unassignedYetkiliUsers = await db.Users
            .Where(x => x.Role == AppRoles.Yetkili)
            .Where(x => !db.UserFacilityAssignments.Any(a => a.UserId == x.Id && a.IsActive))
            .ToListAsync();

        foreach (var user in unassignedYetkiliUsers)
        {
            db.UserFacilityAssignments.Add(new UserFacilityAssignment
            {
                UserId = user.Id,
                DormitoryId = defaultDormitoryId,
                HousingUnitId = defaultHousingUnitId,
                AssignedById = users.Admin.Id,
                AssignedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task RemoveBuildingsAsync(AppDbContext db, List<Building> buildings)
    {
        var buildingIds = buildings.Select(x => x.Id).ToList();
        var floorIds = await db.Floors.Where(x => buildingIds.Contains(x.BuildingId)).Select(x => x.Id).ToListAsync();
        var roomIds = await db.Rooms.Where(x => floorIds.Contains(x.BlockFloorId)).Select(x => x.Id).ToListAsync();
        await RemoveRoomsAsync(db, await db.Rooms.Where(x => roomIds.Contains(x.Id)).ToListAsync());
        db.Floors.RemoveRange(await db.Floors.Where(x => floorIds.Contains(x.Id)).ToListAsync());
        db.Buildings.RemoveRange(buildings);
        await db.SaveChangesAsync();
    }

    private static async Task RelocatePlacementsAsync(AppDbContext db, List<Building> buildings, List<Room> roomsToRemove)
    {
        if (buildings.Count == 0 && roomsToRemove.Count == 0)
        {
            return;
        }

        var buildingIds = buildings.Select(x => x.Id).ToList();
        var sourceRoomIds = await db.Rooms
            .Where(x => buildingIds.Contains(x.BlockFloor.BuildingId))
            .Select(x => x.Id)
            .ToListAsync();
        sourceRoomIds.AddRange(roomsToRemove.Select(x => x.Id));
        sourceRoomIds = sourceRoomIds.Distinct().ToList();
        var placements = await db.Placements
            .Include(x => x.Room)
            .ThenInclude(x => x.BlockFloor)
            .ThenInclude(x => x.Building)
            .Where(x => sourceRoomIds.Contains(x.RoomId))
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.CheckInDate)
            .ToListAsync();
        if (placements.Count == 0)
        {
            return;
        }

        var random = new Random(20260824);
        var targetRooms = await db.Rooms
            .Include(x => x.BlockFloor)
            .ThenInclude(x => x.Building)
            .Where(x => !buildingIds.Contains(x.BlockFloor.BuildingId) && !sourceRoomIds.Contains(x.Id) && x.Status != RoomStatus.Maintenance)
            .ToListAsync();
        var occupancy = targetRooms.ToDictionary(x => x.Id, x => 0);
        foreach (var placement in await db.Placements
                     .Where(x => !sourceRoomIds.Contains(x.RoomId) && x.IsActive)
                     .ToListAsync())
        {
            if (occupancy.ContainsKey(placement.RoomId))
            {
                occupancy[placement.RoomId]++;
            }
        }

        foreach (var placement in placements)
        {
            var sourceIsHousing = placement.Room.BlockFloor.Building.HousingUnitId.HasValue;
            var target = targetRooms
                .Where(x => x.BlockFloor.Building.HousingUnitId.HasValue == sourceIsHousing)
                .Where(x => !placement.IsActive || occupancy[x.Id] < x.Capacity)
                .OrderBy(_ => random.Next())
                .FirstOrDefault();
            if (target is null)
            {
                throw new InvalidOperationException("Eski blok yerleşimleri için uygun A/B/L odası bulunamadı.");
            }

            placement.RoomId = target.Id;
            if (placement.IsActive)
            {
                occupancy[target.Id]++;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task RemoveRoomsAsync(AppDbContext db, List<Room> rooms)
    {
        var roomIds = rooms.Select(x => x.Id).ToList();
        db.Placements.RemoveRange(await db.Placements.Where(x => roomIds.Contains(x.RoomId)).ToListAsync());
        db.Requests.RemoveRange(await db.Requests.Where(x => roomIds.Contains(x.RoomId)).ToListAsync());
        db.Rooms.RemoveRange(rooms);
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

    private static async Task<Room> EnsureRoomAsync(AppDbContext db, int floorId, string roomNumber, int capacity, decimal price, int occupancy)
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
                CurrentOccupancy = occupancy,
                Status = GetRoomStatus(occupancy, capacity)
            };
            db.Rooms.Add(room);
        }
        else
        {
            room.Capacity = capacity;
            room.Price = price;
            room.CurrentOccupancy = occupancy;
            room.Status = GetRoomStatus(occupancy, capacity);
        }

        await db.SaveChangesAsync();
        return room;
    }

    private static RoomStatus GetRoomStatus(int occupancy, int capacity) => occupancy == 0
        ? RoomStatus.Empty
        : occupancy == capacity ? RoomStatus.Full : RoomStatus.PartiallyFull;

    private static async Task SeedOperationalDataAsync(AppDbContext db, DemoUsers users)
    {
        if (!await db.Applications.AnyAsync(x => x.UserId == users.Student1.Id))
        {
            db.Applications.AddRange(
                new AccommodationApplication { UserId = users.Student1.Id, Source = ApplicationSource.RegisteredUser, ReferenceCode = "SEED000001", AccommodationType = AccommodationType.Yurt, DocumentUrl = "/uploads/demo/ogrenci-belgesi-ayse.pdf", Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new AccommodationApplication { UserId = users.Student2.Id, Source = ApplicationSource.RegisteredUser, ReferenceCode = "SEED000002", AccommodationType = AccommodationType.Yurt, DocumentUrl = "/uploads/demo/ogrenci-belgesi-mehmet.pdf", Status = ApplicationStatus.Approved, CreatedAt = DateTime.UtcNow.AddDays(-5), UpdatedAt = DateTime.UtcNow.AddDays(-2) },
                new AccommodationApplication { UserId = users.Staff1.Id, Source = ApplicationSource.RegisteredUser, ReferenceCode = "SEED000003", AccommodationType = AccommodationType.Lojman, DocumentUrl = "/uploads/demo/personel-gorev-yeri.pdf", Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new AccommodationApplication { UserId = users.Staff2.Id, Source = ApplicationSource.RegisteredUser, ReferenceCode = "SEED000004", AccommodationType = AccommodationType.Lojman, DocumentUrl = "/uploads/demo/personel-belgesi.pdf", Status = ApplicationStatus.Rejected, CreatedAt = DateTime.UtcNow.AddDays(-8), UpdatedAt = DateTime.UtcNow.AddDays(-6) },
                new AccommodationApplication { UserId = users.Student3.Id, Source = ApplicationSource.RegisteredUser, ReferenceCode = "SEED000005", AccommodationType = AccommodationType.Yurt, DocumentUrl = "/uploads/demo/ogrenci-belgesi-zeynep.pdf", Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-1) });
        }

        await db.SaveChangesAsync();

        // Ogrenci yerlestirmeleri artik PlaceDemoStudentsAsync ile rastgele yapilir (asagida).
        await EnsurePlacementAsync(db, users.Staff1.Id, "L101", DateTime.UtcNow.AddDays(-45));

        if (!await db.Requests.AnyAsync())
        {
            var room101 = await db.Rooms.FirstAsync(x => x.RoomNumber == "101");
            var room102 = await db.Rooms.FirstAsync(x => x.RoomNumber == "102");
            var roomL101 = await db.Rooms.FirstAsync(x => x.RoomNumber == "L101");

            db.Requests.AddRange(
                new MaintenanceRequest { UserId = users.Student2.Id, RoomId = room101.Id, Category = "Elektrik", Description = "Calisma masasindaki priz calismiyor.", PhotoUrl = "/uploads/demo/priz.jpg", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddHours(-8) },
                new MaintenanceRequest { UserId = users.Student3.Id, RoomId = room102.Id, Category = "Isitma", Description = "Petek yeterince isinmiyor.", PhotoUrl = "/uploads/demo/petek.jpg", Status = RequestStatus.InProgress, CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new MaintenanceRequest { UserId = users.Staff1.Id, RoomId = roomL101.Id, Category = "Su Tesisati", Description = "Mutfak lavabosunda damlama var.", PhotoUrl = "/uploads/demo/lavabo.jpg", Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new MaintenanceRequest { UserId = users.Student2.Id, RoomId = room101.Id, Category = "Mobilya", Description = "Dolap kapagi gevsemis.", Status = RequestStatus.Resolved, CreatedAt = DateTime.UtcNow.AddDays(-5) });
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

        await EnsureAnnouncementsAsync(db);
        await db.SaveChangesAsync();
        await RecalculateRoomsAsync(db);
    }

    private static async Task EnsureAnnouncementsAsync(AppDbContext db)
    {
        var announcements = await db.Announcements.OrderByDescending(x => x.CreatedAt).ToListAsync();
        if (announcements.Count == 5)
        {
            return;
        }

        db.Announcements.RemoveRange(announcements);
        var now = DateTime.UtcNow;
        db.Announcements.AddRange(Enumerable.Range(1, 5).Select(index => new Announcement
        {
            Title = $"Demo Duyurusu {index:00}",
            Content = "Yurt ve lojman yonetim sistemi bilgilendirmesidir.",
            TargetRole = AnnouncementTargetRole.All,
            CreatedAt = now.AddDays(-index),
            IsActive = true
        }));
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
            room.CurrentOccupancy = room.Placements.Count(x => x.IsActive);
            if (room.Status != RoomStatus.Maintenance)
            {
                room.Status = room.CurrentOccupancy == 0
                    ? RoomStatus.Empty
                    : room.CurrentOccupancy >= room.Capacity ? RoomStatus.Full : RoomStatus.PartiallyFull;
            }
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
