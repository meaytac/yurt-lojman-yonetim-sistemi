using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using yurt_lojman_yonetim_sistemi.Models;

namespace yurt_lojman_yonetim_sistemi.Data;

public static class SeedData
{
    public static async Task<bool> SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            return false;
        }

        var random = new Random(20260820);
        var now = DateTime.UtcNow;
        var users = CreateUsers(random, now);
        var dormitories = CreateDormitories();
        var housingUnits = CreateHousingUnits();

        db.Users.AddRange(users);
        db.Dormitories.AddRange(dormitories);
        db.HousingUnits.AddRange(housingUnits);
        await db.SaveChangesAsync();

        var buildings = CreateBuildings(dormitories, housingUnits);
        db.Buildings.AddRange(buildings);
        await db.SaveChangesAsync();

        var floors = buildings
            .SelectMany(building => Enumerable.Range(1, 2).Select(floorNumber => new Floor
            {
                BuildingId = building.Id,
                FloorNumber = floorNumber
            }))
            .ToList();
        db.Floors.AddRange(floors);
        await db.SaveChangesAsync();

        var rooms = CreateRooms(floors, buildings, random);
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync();

        db.Applications.AddRange(CreateApplications(users, now));
        db.Payments.AddRange(CreatePayments(users, now));
        db.Announcements.AddRange(CreateAnnouncements(now));
        await db.SaveChangesAsync();

        var placements = CreatePlacements(users, rooms, now);
        var requests = CreateRequests(users, rooms, now);
        db.Placements.AddRange(placements);
        db.Requests.AddRange(requests);
        await db.SaveChangesAsync();

        await RecalculateRoomsAsync(db);

        return true;
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

    private static List<AppUser> CreateUsers(Random random, DateTime now)
    {
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
        var passwordHasher = new PasswordHasher<AppUser>();
        var users = new List<AppUser>(100);
        var usedTcNumbers = new HashSet<string>();
        var usedFullNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 1; index <= 100; index++)
        {
            string fullName;
            do
            {
                var firstName = firstNames[random.Next(firstNames.Length)];
                var lastName = lastNames[random.Next(lastNames.Length)];
                fullName = $"{firstName} {lastName}";
            } while (!usedFullNames.Add(fullName));

            var emailName = ToEmailName(fullName);
            var role = index <= 80 ? AppRoles.Ogrenci : AppRoles.Personel;
            var studentStaffNo = role == AppRoles.Ogrenci
                ? $"OGR-2026-{index:000}"
                : $"PER-2026-{index - 80:000}";
            var tcNo = CreateValidTcNo(random, usedTcNumbers);
            var email = $"{emailName}@ozal.edu.tr";

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                NormalizedUserName = email.ToUpperInvariant(),
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                FullName = fullName,
                TcNo = tcNo,
                StudentStaffNo = studentStaffNo,
                Role = role,
                PhoneNumber = $"+90555000{index:0000}",
                CreatedAt = now.AddDays(-random.Next(10, 365)),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                LockoutEnabled = true
            };

            user.PasswordHash = passwordHasher.HashPassword(user, "Demo123!");
            users.Add(user);
        }

        return users;
    }

    private static string ToEmailName(string fullName) => fullName
        .ToLowerInvariant()
        .Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i")
        .Replace("ö", "o").Replace("ş", "s").Replace("ü", "u")
        .Replace(" ", ".");

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

    private static List<Dormitory> CreateDormitories() => new()
    {
        new Dormitory
        {
            Name = "MTÜ Erkek Öğrenci Yurdu",
            Type = AccommodationType.Yurt,
            CampusLocation = "Battalgazi Yerleşkesi",
            IsActive = true
        },
        new Dormitory
        {
            Name = "MTÜ Kız Öğrenci Yurdu",
            Type = AccommodationType.Yurt,
            CampusLocation = "Yeşilyurt Yerleşkesi",
            IsActive = true
        }
    };

    private static List<HousingUnit> CreateHousingUnits() => new()
    {
        new HousingUnit
        {
            Name = "MTÜ Akademik Personel Lojmanı",
            Type = AccommodationType.Lojman,
            CampusLocation = "Battalgazi Yerleşkesi",
            IsActive = true
        }
    };

    private static List<Building> CreateBuildings(List<Dormitory> dormitories, List<HousingUnit> housingUnits)
    {
        var buildings = new List<Building>(5);
        foreach (var dormitory in dormitories)
        {
            foreach (var blockName in new[] { "A Blok", "B Blok" })
            {
                buildings.Add(new Building { DormitoryId = dormitory.Id, BlockName = blockName });
            }
        }

        buildings.Add(new Building { HousingUnitId = housingUnits[0].Id, BlockName = "L Blok" });

        return buildings;
    }

    private static List<Room> CreateRooms(List<Floor> floors, List<Building> buildings, Random random)
    {
        var housingBuildingId = buildings.Single(x => x.HousingUnitId.HasValue).Id;
        var rooms = new List<Room>(floors.Count * 10);
        foreach (var floor in floors)
        {
            var isHousing = floor.BuildingId == housingBuildingId;
            for (var roomIndex = 1; roomIndex <= 10; roomIndex++)
            {
                var capacity = isHousing ? random.Next(1, 3) : random.Next(3, 5);
                var occupancy = random.Next(capacity + 1);
                rooms.Add(new Room
                {
                    BlockFloorId = floor.Id,
                    RoomNumber = $"{(isHousing ? "L" : string.Empty)}{floor.FloorNumber}{roomIndex:00}",
                    Capacity = capacity,
                    CurrentOccupancy = occupancy,
                    Price = isHousing ? 5500 : 2500,
                    Status = occupancy == 0
                        ? RoomStatus.Empty
                        : occupancy == capacity ? RoomStatus.Full : RoomStatus.PartiallyFull
                });
            }
        }

        return rooms;
    }

    private static List<AccommodationApplication> CreateApplications(List<AppUser> users, DateTime now) =>
        Enumerable.Range(0, 200).Select(index => new AccommodationApplication
        {
            UserId = users[index % users.Count].Id,
            AccommodationType = index % 5 == 0 ? AccommodationType.Lojman : AccommodationType.Yurt,
            DocumentUrl = $"/uploads/demo/belge-{index + 1:000}.pdf",
            Status = (ApplicationStatus)(index % 3 + 1),
            CreatedAt = now.AddDays(-index % 90),
            UpdatedAt = index % 3 == 0 ? null : now.AddDays(-index % 30)
        }).ToList();

    private static List<Placement> CreatePlacements(List<AppUser> users, List<Room> rooms, DateTime now) =>
        users.Select((user, index) => new Placement
        {
            UserId = user.Id,
            RoomId = rooms[index].Id,
            CheckInDate = now.AddDays(-index - 1),
            IsActive = true
        }).ToList();

    private static List<Payment> CreatePayments(List<AppUser> users, DateTime now) =>
        users.Select((user, index) => new Payment
        {
            UserId = user.Id,
            Amount = index % 4 == 0 ? 5500 : 2500,
            DueDate = now.AddDays(index % 3 == 0 ? -10 : 15),
            PaidDate = index % 3 == 0 ? null : now.AddDays(-2),
            Status = index % 3 == 0 ? PaymentStatus.Overdue : PaymentStatus.Paid,
            Description = $"Demo {now:MMMM} donemi konaklama ucreti"
        }).ToList();

    private static List<MaintenanceRequest> CreateRequests(List<AppUser> users, List<Room> rooms, DateTime now)
    {
        var categories = new[] { "Elektrik", "Isitma", "Su Tesisati", "Mobilya" };
        return Enumerable.Range(0, 80).Select(index => new MaintenanceRequest
        {
            UserId = users[index % users.Count].Id,
            RoomId = rooms[index].Id,
            Category = categories[index % categories.Length],
            Description = $"Demo odasinda {categories[index % categories.Length].ToLowerInvariant()} kontrolu gerekiyor.",
            Status = (RequestStatus)(index % 4 + 1),
            CreatedAt = now.AddDays(-index % 45)
        }).ToList();
    }

    private static List<Announcement> CreateAnnouncements(DateTime now) =>
        Enumerable.Range(1, 5).Select(index => new Announcement
        {
            Title = $"Demo Duyurusu {index:00}",
            Content = "Yurt ve lojman yonetim sistemi bilgilendirmesidir.",
            TargetRole = (AnnouncementTargetRole)(index % 3 + 1),
            CreatedAt = now.AddDays(-index),
            IsActive = true
        }).ToList();
}