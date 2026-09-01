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

        var rooms = CreateRooms(floors);
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

        foreach (var room in rooms)
        {
            room.CurrentOccupancy = placements.Count(x => x.RoomId == room.Id);
            room.Status = room.CurrentOccupancy == 0
                ? RoomStatus.Empty
                : room.CurrentOccupancy >= room.Capacity
                    ? RoomStatus.Full
                    : RoomStatus.PartiallyFull;
        }

        await db.SaveChangesAsync();
        return true;
    }

    private static List<AppUser> CreateUsers(Random random, DateTime now)
    {
        var firstNames = new[] { "Ahmet", "Ayse", "Burak", "Ceren", "Deniz", "Ece", "Emre", "Fatma", "Gokhan", "Irem" };
        var lastNames = new[] { "Yilmaz", "Kaya", "Demir", "Sahin", "Celik", "Aydin", "Arslan", "Koc", "Polat", "Dogan" };
        var passwordHasher = new PasswordHasher<AppUser>();
        var users = new List<AppUser>(100);

        for (var index = 1; index <= 100; index++)
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = $"demo{index:000}@ozal.edu.tr",
                Email = $"demo{index:000}@ozal.edu.tr",
                NormalizedUserName = $"DEMO{index:000}@OZAL.EDU.TR",
                NormalizedEmail = $"DEMO{index:000}@OZAL.EDU.TR",
                EmailConfirmed = true,
                FullName = $"{firstNames[(index - 1) % firstNames.Length]} {lastNames[((index - 1) / firstNames.Length) % lastNames.Length]}",
                TcNo = $"9900000{index:0000}",
                StudentStaffNo = $"OGR-2026-{index:000}",
                Role = AppRoles.Ogrenci,
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

    private static List<Dormitory> CreateDormitories() => new();
    private static List<HousingUnit> CreateHousingUnits() => new();

    private static List<Building> CreateBuildings(List<Dormitory> dormitories, List<HousingUnit> housingUnits)
    {
        if (dormitories.Count == 0 && housingUnits.Count == 0) return new();
        var buildings = new List<Building>(10);
        for (var index = 0; index < 10; index++)
        {
            if (index < 6)
            {
                if (dormitories.Count == 0) continue;
                buildings.Add(new Building { DormitoryId = dormitories[index % dormitories.Count].Id, BlockName = $"Demo {index + 1} Blok" });
            }
            else
            {
                if (housingUnits.Count == 0) continue;
                buildings.Add(new Building { HousingUnitId = housingUnits[(index - 6) % housingUnits.Count].Id, BlockName = $"Lojman {index - 5} Blok" });
            }
        }

        return buildings;
    }

    private static List<Room> CreateRooms(List<Floor> floors)
    {
        var rooms = new List<Room>(300);
        foreach (var floor in floors)
        {
            for (var roomIndex = 1; roomIndex <= 15; roomIndex++)
            {
                var capacity = floor.BuildingId % 5 == 0 ? 1 : 4;
                rooms.Add(new Room
                {
                    BlockFloorId = floor.Id,
                    RoomNumber = $"{floor.FloorNumber}{roomIndex:00}",
                    Capacity = capacity,
                    Price = capacity == 1 ? 5500 : 2500,
                    Status = RoomStatus.Empty
                });
            }
        }

        return rooms;
    }

    private static List<AccommodationApplication> CreateApplications(List<AppUser> users, DateTime now) =>
        Enumerable.Range(0, 20).Select(index => new AccommodationApplication
        {
            UserId = users[index].Id,
            AccommodationType = index % 5 == 0 ? AccommodationType.Lojman : AccommodationType.Yurt,
            DocumentUrl = $"/uploads/demo/belge-{index + 1:000}.pdf",
            Status = (ApplicationStatus)(index % 3 + 1),
            CreatedAt = now.AddDays(-index % 30),
            UpdatedAt = index % 3 == 0 ? null : now.AddDays(-index % 15)
        }).ToList();

    private static List<Placement> CreatePlacements(List<AppUser> users, List<Room> rooms, DateTime now) =>
        rooms.Count == 0 ? new() : users.Select((user, index) => new Placement
        {
            UserId = user.Id,
            RoomId = rooms[index % rooms.Count].Id,
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
        if (rooms.Count == 0) return new();
        return new List<MaintenanceRequest>
        {
            new() { UserId = users[0].Id, RoomId = rooms[0].Id, Category = "Elektrik", Description = "Oda içindeki çalışma masası prizi temassızlık yapıyor, fiş takılıyken elektrik kesiliyor.", Status = RequestStatus.Open, CreatedAt = now.AddHours(-5) },
            new() { UserId = users[1].Id, RoomId = rooms[1].Id, Category = "Su Tesisatı", Description = "Banyo musluğu sürekli damlatıyor, su israfı oluyor ve geceleri ses yapıyor.", Status = RequestStatus.Open, CreatedAt = now.AddDays(-1) },
            new() { UserId = users[2].Id, RoomId = rooms[2].Id, Category = "Isıtma", Description = "Petek vanası sıkışmış, petek ısınmıyor, oda çok soğuk. Kış ayarı kontrol edilmeli.", Status = RequestStatus.InProgress, CreatedAt = now.AddDays(-2) },
            new() { UserId = users[3].Id, RoomId = rooms[3].Id, Category = "Mobilya", Description = "Ranza üst kat merdiveni gevşemiş, çıkarken sallanıyor, düşme riski var.", Status = RequestStatus.Open, CreatedAt = now.AddHours(-12) },
            new() { UserId = users[4].Id, RoomId = rooms[4].Id, Category = "İnternet", Description = "Oda içinde Wi-Fi sinyali çok zayıf, bağlantı sürekli kopuyor, çevrimiçi derslere katılamıyorum.", Status = RequestStatus.Open, CreatedAt = now.AddDays(-3) },
            new() { UserId = users[5].Id, RoomId = rooms[5].Id, Category = "Banyo", Description = "Banyo gideri tıkalı, duş sonrası su birikiyor ve kötü koku yapıyor.", Status = RequestStatus.Open, CreatedAt = now.AddHours(-8) },
            new() { UserId = users[6].Id, RoomId = rooms[6].Id, Category = "Kapı/Kilit", Description = "Oda kapı kilidi bazen açılmıyor, kartı birkaç kez okutmak gerekiyor.", Status = RequestStatus.Open, CreatedAt = now.AddDays(-4) }
        };
    }

    private static List<Announcement> CreateAnnouncements(DateTime now) =>
        new List<Announcement>
        {
            new() { Title = "Çamaşırhane Haftalık Bakım", Content = "A Blok çamaşırhanesi 02–03 Eylül tarihlerinde makine bakımı nedeniyle kapalı olacaktır. Acil ihtiyaçlar için B Blok çamaşırhanesini kullanabilirsiniz.", TargetRole = AnnouncementTargetRole.All, CreatedAt = now.AddDays(-2), IsActive = true },
            new() { Title = "Etüt Salonlarında Sessizlik Kuralları", Content = "Etüt salonlarında 19:00–23:00 saatleri arasında sessizlik kuralı uygulanacaktır. Grup çalışmaları için zemin kattaki tartışma odalarını kullanınız.", TargetRole = AnnouncementTargetRole.Student, CreatedAt = now.AddDays(-7), IsActive = true },
            new() { Title = "Güvenlik Giriş Kartı Yenileme", Content = "Güvenlik giriş kartlarının vizesi 15 Eylül'de sona ermektedir. Kartını yenilemek isteyenlerin danışmaya kimlikleri ile başvurması gerekmektedir. Kayıp kart bedeli 150 TL'dir.", TargetRole = AnnouncementTargetRole.All, CreatedAt = now.AddDays(-3), IsActive = true },
            new() { Title = "Kalorifer Sistemi Kış Ayarları", Content = "Kalorifer sistemi 20 Eylül itibarıyla kış moduna alınacaktır. Odalarda petek ayarlarını 3. kademede tutmanız ve pencereleri uzun süre açık bırakmamanız rica olunur. Arıza için lütfen arıza talebi oluşturun.", TargetRole = AnnouncementTargetRole.All, CreatedAt = now.AddDays(-1), IsActive = true }
        };
}