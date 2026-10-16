using DineMaster.Domain.Entities;
using DineMaster.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineMaster.Infrastructure.Data;

/// <summary>
/// CREDENTIALS:
///   Superadmin: super@dinemaster.uz / DM@Super2025!
///   Admins:     admin1@dinemaster.uz ... admin10@dinemaster.uz / DM@Admin2025!
///   Demo User:  user@dinemaster.uz  / DM@User2025!
/// </summary>
public static class DbInitializer
{
    private const string SuperadminEmail = "super@dinemaster.uz";
    private const string SuperadminPassword = "DM@Super2025!";
    private const string AdminPassword = "DM@Admin2025!";
    private const string UserEmail = "user@dinemaster.uz";
    private const string UserPassword = "DM@User2025!";

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await EnsureUserAsync(db, SuperadminEmail, SuperadminPassword, "Super Admin",      "+998 90 000 00 00", AppRole.Superadmin, 0,   ct);
        await EnsureUserAsync(db, UserEmail,       UserPassword,       "Bekzod Toshmatov", "+998 90 333 33 33", AppRole.User,       500, ct);

        var adminProfiles = new[]
        {
            ("admin1@dinemaster.uz", "Aziz Karimov", "+998 90 111 11 11"),
            ("admin2@dinemaster.uz", "Dilnoza Saidova", "+998 90 222 22 22"),
            ("admin3@dinemaster.uz", "Jasur Rakhimov", "+998 90 333 11 22"),
            ("admin4@dinemaster.uz", "Madina Ergasheva", "+998 90 444 11 22"),
            ("admin5@dinemaster.uz", "Sherzod Yusupov", "+998 90 555 11 22"),
            ("admin6@dinemaster.uz", "Nodira Hakimova", "+998 90 666 11 22"),
            ("admin7@dinemaster.uz", "Rustam Nazarov", "+998 90 777 11 22"),
            ("admin8@dinemaster.uz", "Zarina Usmonova", "+998 90 888 11 22"),
            ("admin9@dinemaster.uz", "Farrukh Alimov", "+998 90 999 11 22"),
            ("admin10@dinemaster.uz", "Malika Sobirova", "+998 90 100 11 22"),
        };

        var admins = new List<AppUser>();
        foreach (var (email, fullName, phone) in adminProfiles)
        {
            admins.Add(await EnsureUserAsync(db, email, AdminPassword, fullName, phone, AppRole.Admin, 0, ct));
        }

        await SeedRestaurantsAsync(db, admins, ct);
        await EnsureAllRestaurantDemoDataAsync(db, ct);
        await DemoTextUz.ApplyAsync(db, ct);
        await SeedVenuesAsync(db, ct);

        logger.LogInformation("Seed: foydalanuvchilar, restoranlar va tadbir zallari tayyor.");
    }

    private static async Task SeedVenuesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Venues.AnyAsync(ct)) return;

        var oh = """{"mon":"09:00-23:00","tue":"09:00-23:00","wed":"09:00-23:00","thu":"09:00-23:00","fri":"09:00-00:00","sat":"09:00-00:00","sun":"10:00-22:00"}""";
        var venues = new List<Venue>
        {
            // ─── TO'YXONALAR ───────────────────────────────────────────
            new() { Name="Nilufar To'yxonasi", VenueType="To'yxona",
                Description="Zamonaviy interyerli keng to'yxona. Professional tomonida 800 kishi sig'adi. To'y, sunnat to'y, unashtiruv marosimlariga mos.",
                Address="Chilonzor tumani, Bunyodkor ko'chasi 12", City="Toshkent", Phone="+998 71 246 15 30",
                CapacityMin=100, CapacityMax=800, PricePerHead=80000, PricePerHour=500000,
                Rating=4.7m, ReviewCount=142, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1519167758481-83f550bb49b3?w=800"] },

            new() { Name="Shohruh Banket Saroyi", VenueType="To'yxona",
                Description="Muhtasham bezatilgan to'yxona. 3 ta zaldan iborat: kichik (200 kishi), o'rta (400 kishi) va katta (700 kishi). Audio-video uskunalar mavjud.",
                Address="Yunusobod tumani, Amir Temur ko'chasi 55", City="Toshkent", Phone="+998 71 150 33 40",
                CapacityMin=200, CapacityMax=700, PricePerHead=95000, PricePerHour=700000,
                Rating=4.8m, ReviewCount=215, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1464366400600-7168b8af9bc3?w=800"] },

            new() { Name="Barno Xonim To'yxonasi", VenueType="To'yxona",
                Description="An'anaviy o'zbek uslubidagi to'yxona. Milliy taomlar menyusi bilan. Bog' va oshxona birlashtirilgan.",
                Address="Sergeli tumani, Yangi Sergeli ko'chasi 8", City="Toshkent", Phone="+998 90 345 67 89",
                CapacityMin=150, CapacityMax=500, PricePerHead=70000, PricePerHour=400000,
                Rating=4.5m, ReviewCount=89, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1510076857177-7470076d4098?w=800"] },

            new() { Name="Navruz Saroyi", VenueType="To'yxona",
                Description="Navruz bayramiga bag'ishlangan uslubda bezatilgan noyob to'yxona. Milliy musiqa va raqs ansambli xizmatlari mavjud.",
                Address="Mirzo Ulug'bek tumani, Mustaqillik ko'chasi 23", City="Toshkent", Phone="+998 71 123 45 67",
                CapacityMin=100, CapacityMax=600, PricePerHead=85000, PricePerHour=600000,
                Rating=4.6m, ReviewCount=178, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1505373877841-8d25f7d46678?w=800"] },

            new() { Name="Samarqand To'yxonasi", VenueType="To'yxona",
                Description="Samarqand me'morchiligidan ilhomlanib qurilgan ulkan to'yxona. Ichkarida aylana gumbaz, ko'k ranglar va naqshlar bilan bezatilgan.",
                Address="Shayxontohur tumani, Hamza ko'chasi 44", City="Toshkent", Phone="+998 71 299 11 22",
                CapacityMin=200, CapacityMax=1000, PricePerHead=100000, PricePerHour=800000,
                Rating=4.9m, ReviewCount=302, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1519741497674-611481863552?w=800"] },

            new() { Name="Guliston Marosim Uyi", VenueType="To'yxona",
                Description="O'simliklar bilan bezatilgan romantik to'yxona. Veranda va ochiq bog' maydonchalari mavjud. Kechki yoritish tizimi alohida.",
                Address="Uchtepa tumani, Ring yo'li 7", City="Toshkent", Phone="+998 93 456 78 90",
                CapacityMin=80, CapacityMax=400, PricePerHead=75000, PricePerHour=450000,
                Rating=4.4m, ReviewCount=64, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1511795409834-ef04bbd61622?w=800"] },

            // ─── BANKET ZALLARI ─────────────────────────────────────────
            new() { Name="Grand Banket Zali", VenueType="Banket zali",
                Description="5 yulduzli mehmonxona standartlarida banket zali. Shampan, bufet va a la carte xizmatlari. Korporativ va shaxsiy tadbirlar uchun ideal.",
                Address="Mirzo Ulug'bek tumani, Ko'hna ko'cha 3", City="Toshkent", Phone="+998 71 200 50 60",
                CapacityMin=50, CapacityMax=300, PricePerHead=150000, PricePerHour=900000,
                Rating=4.8m, ReviewCount=110, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1540575467063-178a50c2df87?w=800"] },

            new() { Name="Prestige Banket Markazi", VenueType="Banket zali",
                Description="Zamonaviy mebel va yoritish. LED ekranlar va surround sound tizimi. Foto-zona va podyum mavjud. Kapasiyasi moslashuvchan.",
                Address="Yashnobod tumani, Farobiy ko'chasi 19", City="Toshkent", Phone="+998 95 778 99 11",
                CapacityMin=30, CapacityMax=250, PricePerHead=120000, PricePerHour=750000,
                Rating=4.6m, ReviewCount=87, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1530103862676-de8c9debad1d?w=800"] },

            new() { Name="Oq Saroy Banket Zali", VenueType="Banket zali",
                Description="Oq va oltin ranglardagi hashamatli interyeri bilan ajralib turadi. Tortlar uchun alohida xona. 2 qavatli bino.",
                Address="Bektemir tumani, Yangi ko'cha 1", City="Toshkent", Phone="+998 71 388 22 33",
                CapacityMin=60, CapacityMax=350, PricePerHead=110000, PricePerHour=650000,
                Rating=4.5m, ReviewCount=53, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1496337589254-7e19d01cec44?w=800"] },

            // ─── MAROSIM ZALLARI ────────────────────────────────────────
            new() { Name="Mehr Marosim Zali", VenueType="Marosim zali",
                Description="Sunnat to'y, unashtiruv va motam marosimlari uchun mo'ljallangan xotirjam muhit. Avtoturargoh bepul.",
                Address="Olmazor tumani, Bunyodkor ko'chasi 88", City="Toshkent", Phone="+998 71 133 44 55",
                CapacityMin=50, CapacityMax=300, PricePerHead=60000, PricePerHour=350000,
                Rating=4.3m, ReviewCount=41, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1478146896981-b80fe463b330?w=800"] },

            new() { Name="Bahor Marosim Uyi", VenueType="Marosim zali",
                Description="Shabnam va bahor gullari bilan bezatilgan ochiq-yopiq marosim zali. Bayram sutkalari uchun maxsus chegirmalar mavjud.",
                Address="Yangihayot tumani, Ko'karal ko'chasi 5", City="Toshkent", Phone="+998 94 223 55 66",
                CapacityMin=40, CapacityMax=200, PricePerHead=55000, PricePerHour=300000,
                Rating=4.4m, ReviewCount=33, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1519225421980-715cb0215aed?w=800"] },

            // ─── KORPORATIV ZALLAR ──────────────────────────────────────
            new() { Name="Business Hub Korporativ Zal", VenueType="Korporativ zal",
                Description="Zamonaviy korporativ tadbirlar uchun: konferensiyalar, seminarlar, taqdimotlar. Barcha texnik jihozlar bor: proyektor, flipchart, Wi-Fi.",
                Address="Yunusobod tumani, Mustakillik ko'chasi 105-B", City="Toshkent", Phone="+998 71 400 10 20",
                CapacityMin=20, CapacityMax=200, PricePerHead=100000, PricePerHour=600000,
                Rating=4.7m, ReviewCount=95, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1497366216548-37526070297c?w=800"] },

            new() { Name="InnoTech Konferens Zal", VenueType="Korporativ zal",
                Description="IT kompaniyalar va startaplar uchun moslashtirilgan. Yopiq kabinetlar, akvarium xona va networking maydon. Ko'p stsenariy uchun rejalanishi mumkin.",
                Address="Mirzo Ulug'bek tumani, Ziyolilar ko'chasi 14", City="Toshkent", Phone="+998 90 999 00 11",
                CapacityMin=15, CapacityMax=150, PricePerHead=120000, PricePerHour=700000,
                Rating=4.9m, ReviewCount=63, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1431540015161-0bf868a2d407?w=800"] },

            new() { Name="Diplomat Korporativ Markazi", VenueType="Korporativ zal",
                Description="Xalqaro hamkorliklar uchun sinxron tarjima kabinetlari va eshittirish tizimi. VIP lounge va qabul xonasi.",
                Address="Chilonzor tumani, Navoiy ko'chasi 67", City="Toshkent", Phone="+998 71 255 88 99",
                CapacityMin=30, CapacityMax=180, PricePerHead=130000, PricePerHour=800000,
                Rating=4.8m, ReviewCount=42, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1524178232363-1fb2b075b655?w=800"] },

            new() { Name="Smart Space Korporativ Zal", VenueType="Korporativ zal",
                Description="360° yoritiladigan ekranlar va AI yordamchi. Gibrid format uchun streaming jihozlar. Kofe-break stansiyasi.",
                Address="Sergeli tumani, Olimlar ko'chasi 3", City="Toshkent", Phone="+998 97 312 43 21",
                CapacityMin=10, CapacityMax=100, PricePerHead=90000, PricePerHour=500000,
                Rating=4.6m, ReviewCount=29, IsApproved=true, OpeningHours=oh,
                ImagesUrl=["https://images.unsplash.com/photo-1522071820081-009f0129c71c?w=800"] },
        };

        db.Venues.AddRange(venues);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedRestaurantsAsync(AppDbContext db, IReadOnlyList<AppUser> owners, CancellationToken ct)
    {
        if (owners.Count == 0) return;

        var oh = """{"mon":"10:00-23:00","tue":"10:00-23:00","wed":"10:00-23:00","thu":"10:00-23:00","fri":"10:00-00:00","sat":"10:00-00:00","sun":"10:00-22:00"}""";
        var seeds = new[]
        {
            new {
                OwnerIndex = 0, Name = "Afsona Plov & Grill", CuisineType = "Uzbek", PriceRange = "$$",
                Description = "Modern Uzbek restaurant with plov, grilled meats, private family rooms, and a warm national interior.",
                Address = "Toshkent shahri, Shayxontohur tumani, Navoiy ko'chasi 31", City = "Toshkent",
                Latitude = 41.3231m, Longitude = 69.2454m, Phone = "+998 71 201 10 01", Website = "https://afsona.demo.dinemaster.uz",
                TotalSlots = 96, Rating = 4.8m, ReviewCount = 224, FoodRating = 4.9m, ServiceRating = 4.7m, InteriorRating = 4.8m,
                Discount = (int?)15, DiscountStart = (TimeOnly?)new TimeOnly(12, 0), DiscountEnd = (TimeOnly?)new TimeOnly(16, 0),
                DepositRequired = true, DepositPerGuest = 25000m,
                Images = new[] { "https://images.unsplash.com/photo-1555396273-367ea4eb4db5?w=900", "https://images.unsplash.com/photo-1552566626-52f8b828add9?w=900" }
            },
            new {
                OwnerIndex = 1, Name = "Silk Road Terrace", CuisineType = "Uzbek-European", PriceRange = "$$$",
                Description = "Rooftop dining inspired by the Silk Road, with tasting sets, live dutar evenings, and terrace seating.",
                Address = "Samarqand shahri, Registon ko'chasi 18", City = "Samarqand",
                Latitude = 39.6542m, Longitude = 66.9756m, Phone = "+998 66 201 10 02", Website = "https://silkroad.demo.dinemaster.uz",
                TotalSlots = 84, Rating = 4.9m, ReviewCount = 312, FoodRating = 4.8m, ServiceRating = 4.9m, InteriorRating = 4.9m,
                Discount = (int?)10, DiscountStart = (TimeOnly?)new TimeOnly(15, 0), DiscountEnd = (TimeOnly?)new TimeOnly(18, 0),
                DepositRequired = true, DepositPerGuest = 30000m,
                Images = new[] { "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=900", "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?w=900" }
            },
            new {
                OwnerIndex = 2, Name = "Bukhara Caravan", CuisineType = "National", PriceRange = "$$",
                Description = "Family restaurant with Bukhara-style kebabs, lagman, samsa, and a colorful courtyard setting.",
                Address = "Buxoro shahri, Bahouddin Naqshband ko'chasi 42", City = "Buxoro",
                Latitude = 39.7747m, Longitude = 64.4286m, Phone = "+998 65 201 10 03", Website = "https://caravan.demo.dinemaster.uz",
                TotalSlots = 72, Rating = 4.7m, ReviewCount = 188, FoodRating = 4.8m, ServiceRating = 4.6m, InteriorRating = 4.7m,
                Discount = (int?)12, DiscountStart = (TimeOnly?)new TimeOnly(11, 0), DiscountEnd = (TimeOnly?)new TimeOnly(14, 0),
                DepositRequired = false, DepositPerGuest = 0m,
                Images = new[] { "https://images.unsplash.com/photo-1550966871-3ed3cdb5ed0c?w=900", "https://images.unsplash.com/photo-1559339352-11d035aa65de?w=900" }
            },
            new {
                OwnerIndex = 3, Name = "Chorsu Bistro", CuisineType = "Street Food", PriceRange = "$",
                Description = "Fast, bright bistro near Chorsu with manty, somsa, shashlik wraps, tea, and quick lunch combos.",
                Address = "Toshkent shahri, Eski Juva bozori, 5-pavilon", City = "Toshkent",
                Latitude = 41.3269m, Longitude = 69.2352m, Phone = "+998 71 201 10 04", Website = "https://chorsu.demo.dinemaster.uz",
                TotalSlots = 48, Rating = 4.5m, ReviewCount = 146, FoodRating = 4.6m, ServiceRating = 4.4m, InteriorRating = 4.3m,
                Discount = (int?)20, DiscountStart = (TimeOnly?)new TimeOnly(14, 0), DiscountEnd = (TimeOnly?)new TimeOnly(17, 0),
                DepositRequired = false, DepositPerGuest = 0m,
                Images = new[] { "https://images.unsplash.com/photo-1555992336-03a23c7b20ee?w=900", "https://images.unsplash.com/photo-1544148103-0773bf10d330?w=900" }
            },
            new {
                OwnerIndex = 4, Name = "Navruz Garden", CuisineType = "Uzbek", PriceRange = "$$",
                Description = "Garden restaurant in Fergana with outdoor tables, family cabins, kazan dishes, and seasonal salads.",
                Address = "Farg'ona shahri, Mustaqillik ko'chasi 77", City = "Farg'ona",
                Latitude = 40.3842m, Longitude = 71.7843m, Phone = "+998 73 201 10 05", Website = "https://navruzgarden.demo.dinemaster.uz",
                TotalSlots = 88, Rating = 4.6m, ReviewCount = 171, FoodRating = 4.7m, ServiceRating = 4.5m, InteriorRating = 4.6m,
                Discount = (int?)10, DiscountStart = (TimeOnly?)new TimeOnly(16, 0), DiscountEnd = (TimeOnly?)new TimeOnly(19, 0),
                DepositRequired = true, DepositPerGuest = 20000m,
                Images = new[] { "https://images.unsplash.com/photo-1551218808-94e220e084d2?w=900", "https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=900" }
            },
            new {
                OwnerIndex = 5, Name = "Anhor Steak House", CuisineType = "Steakhouse", PriceRange = "$$$",
                Description = "Premium steakhouse with dry-aged beef, chef sauces, business lunch, and private VIP dining.",
                Address = "Toshkent shahri, Yunusobod tumani, Anhor bo'yi 12", City = "Toshkent",
                Latitude = 41.3393m, Longitude = 69.2845m, Phone = "+998 71 201 10 06", Website = "https://anhorsteak.demo.dinemaster.uz",
                TotalSlots = 64, Rating = 4.8m, ReviewCount = 257, FoodRating = 4.9m, ServiceRating = 4.8m, InteriorRating = 4.7m,
                Discount = (int?)null, DiscountStart = (TimeOnly?)null, DiscountEnd = (TimeOnly?)null,
                DepositRequired = true, DepositPerGuest = 50000m,
                Images = new[] { "https://images.unsplash.com/photo-1544025162-d76694265947?w=900", "https://images.unsplash.com/photo-1579366948929-444eb79881eb?w=900" }
            },
            new {
                OwnerIndex = 6, Name = "Registan Family Restaurant", CuisineType = "Family", PriceRange = "$$",
                Description = "Large family venue with children's corner, group tables, national dishes, and friendly service.",
                Address = "Samarqand shahri, Universitet xiyoboni 9", City = "Samarqand",
                Latitude = 39.6574m, Longitude = 66.9708m, Phone = "+998 66 201 10 07", Website = "https://registanfamily.demo.dinemaster.uz",
                TotalSlots = 110, Rating = 4.6m, ReviewCount = 201, FoodRating = 4.6m, ServiceRating = 4.7m, InteriorRating = 4.5m,
                Discount = (int?)18, DiscountStart = (TimeOnly?)new TimeOnly(13, 0), DiscountEnd = (TimeOnly?)new TimeOnly(16, 30),
                DepositRequired = false, DepositPerGuest = 0m,
                Images = new[] { "https://images.unsplash.com/photo-1514933651103-005eec06c04b?w=900", "https://images.unsplash.com/photo-1550966871-3ed3cdb5ed0c?w=900" }
            },
            new {
                OwnerIndex = 7, Name = "Xiva Moonlight", CuisineType = "Khorezm", PriceRange = "$$",
                Description = "Khorezm cuisine with tuxum barak, shivit oshi, grilled fish, and an Ichan-Qala inspired interior.",
                Address = "Xiva shahri, Ichan-Qal'a yonida, 3-uy", City = "Xorazm",
                Latitude = 41.3783m, Longitude = 60.3599m, Phone = "+998 62 201 10 08", Website = "https://xivamoon.demo.dinemaster.uz",
                TotalSlots = 58, Rating = 4.7m, ReviewCount = 132, FoodRating = 4.8m, ServiceRating = 4.6m, InteriorRating = 4.8m,
                Discount = (int?)10, DiscountStart = (TimeOnly?)new TimeOnly(12, 30), DiscountEnd = (TimeOnly?)new TimeOnly(15, 30),
                DepositRequired = false, DepositPerGuest = 0m,
                Images = new[] { "https://images.unsplash.com/photo-1537047902294-62a40c20a6ae?w=900", "https://images.unsplash.com/photo-1551632436-cbf8dd35adfa?w=900" }
            },
            new {
                OwnerIndex = 8, Name = "Sirdaryo Fish House", CuisineType = "Seafood", PriceRange = "$$",
                Description = "Fresh river fish, kazan fries, crisp salads, and relaxed family seating close to the Sirdaryo route.",
                Address = "Guliston shahri, Sayhun ko'chasi 21", City = "Sirdaryo",
                Latitude = 40.4897m, Longitude = 68.7842m, Phone = "+998 67 201 10 09", Website = "https://fishhouse.demo.dinemaster.uz",
                TotalSlots = 62, Rating = 4.5m, ReviewCount = 98, FoodRating = 4.7m, ServiceRating = 4.4m, InteriorRating = 4.4m,
                Discount = (int?)15, DiscountStart = (TimeOnly?)new TimeOnly(11, 30), DiscountEnd = (TimeOnly?)new TimeOnly(15, 0),
                DepositRequired = false, DepositPerGuest = 0m,
                Images = new[] { "https://images.unsplash.com/photo-1559339352-11d035aa65de?w=900", "https://images.unsplash.com/photo-1543007630-9710e4a00a20?w=900" }
            },
            new {
                OwnerIndex = 9, Name = "Tashkent Vegan Lab", CuisineType = "Vegetarian", PriceRange = "$$",
                Description = "Fresh plant-based bowls, seasonal soups, house lemonades, and quiet laptop-friendly seating.",
                Address = "Toshkent shahri, Mirzo Ulug'bek tumani, Ziyolilar ko'chasi 24", City = "Toshkent",
                Latitude = 41.3349m, Longitude = 69.3362m, Phone = "+998 71 201 10 10", Website = "https://veganlab.demo.dinemaster.uz",
                TotalSlots = 44, Rating = 4.6m, ReviewCount = 119, FoodRating = 4.7m, ServiceRating = 4.6m, InteriorRating = 4.5m,
                Discount = (int?)12, DiscountStart = (TimeOnly?)new TimeOnly(10, 0), DiscountEnd = (TimeOnly?)new TimeOnly(13, 0),
                DepositRequired = false, DepositPerGuest = 0m,
                Images = new[] { "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?w=900", "https://images.unsplash.com/photo-1543362906-acfc16c67564?w=900" }
            },
        };

        foreach (var seed in seeds)
        {
            if (await db.Restaurants.AnyAsync(r => r.Name == seed.Name, ct)) continue;

            var owner = owners[Math.Min(seed.OwnerIndex, owners.Count - 1)];
            var restaurant = new Restaurant
            {
                Id = Guid.NewGuid(),
                OwnerId = owner.Id,
                Name = seed.Name,
                Description = seed.Description,
                CuisineType = seed.CuisineType,
                PriceRange = seed.PriceRange,
                Address = seed.Address,
                City = seed.City,
                Latitude = seed.Latitude,
                Longitude = seed.Longitude,
                Phone = seed.Phone,
                Website = seed.Website,
                OpeningHours = oh,
                TotalSlots = seed.TotalSlots,
                ImagesUrl = seed.Images.ToList(),
                Rating = seed.Rating,
                ReviewCount = seed.ReviewCount,
                FoodRating = seed.FoodRating,
                ServiceRating = seed.ServiceRating,
                InteriorRating = seed.InteriorRating,
                Amenities = new List<string> { "Wi-Fi", "Family rooms", "Parking", "Card payment", "Pre-order menu" },
                Policies = new List<string> { "Reservations are held for 15 minutes", "Smoking only in designated areas", "Outside food requires approval" },
                DepositRequired = seed.DepositRequired,
                DepositPerGuest = seed.DepositPerGuest,
                Discount = seed.Discount,
                DiscountTimeStart = seed.DiscountStart,
                DiscountTimeEnd = seed.DiscountEnd,
                IsApproved = true,
                IsSuspended = false
            };

            db.Restaurants.Add(restaurant);

            var categories = new[]
            {
                new MenuCategory { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Signature dishes", SortOrder = 1 },
                new MenuCategory { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Salads and starters", SortOrder = 2 },
                new MenuCategory { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Drinks and desserts", SortOrder = 3 },
            };
            db.MenuCategories.AddRange(categories);
            db.MenuItems.AddRange(CreateDemoMenu(restaurant.Id, categories, seed.CuisineType));
            db.RestaurantTables.AddRange(CreateDemoTables(restaurant.Id));
        }

        await db.SaveChangesAsync(ct);
    }

    private static IEnumerable<MenuItem> CreateDemoMenu(Guid restaurantId, IReadOnlyList<MenuCategory> categories, string cuisineType)
    {
        var mainDishes = cuisineType switch
        {
            "Seafood" => new[]
            {
                ("Grilled Sazan", "Fresh river fish with herbs and lemon.", 98000m),
                ("Fish Kazan Fry", "Crispy fish pieces with potatoes and house sauce.", 89000m),
                ("Shrimp Lagman", "Hand-pulled noodles with shrimp and vegetables.", 76000m),
            },
            "Steakhouse" => new[]
            {
                ("Ribeye Steak", "Charcoal-grilled ribeye with pepper sauce.", 185000m),
                ("Beef Tenderloin", "Tender fillet with mashed potatoes and jus.", 210000m),
                ("Smoked Brisket", "Slow-cooked brisket with pickles and bread.", 145000m),
            },
            "Vegetarian" => new[]
            {
                ("Green Power Bowl", "Quinoa, avocado, greens, chickpeas, and tahini.", 68000m),
                ("Mushroom Plov", "Vegetarian plov with mushrooms and yellow carrots.", 62000m),
                ("Pumpkin Ravioli", "Handmade ravioli with sage and walnuts.", 74000m),
            },
            "Street Food" => new[]
            {
                ("Shashlik Wrap", "Lamb skewer wrap with fresh herbs and tomato.", 42000m),
                ("Mini Somsa Set", "Four flaky somsas with yogurt dip.", 36000m),
                ("Lagman Bowl", "Rich noodle bowl with beef and vegetables.", 52000m),
            },
            _ => new[]
            {
                ("Tashkent Plov", "Classic rice, beef, carrots, chickpeas, and quail egg.", 72000m),
                ("Mixed Shashlik", "Lamb, beef, and chicken skewers with onions.", 94000m),
                ("Kazan Kabob", "Slow-fried lamb and potatoes from the kazan.", 88000m),
            }
        };

        var starters = new[]
        {
            ("Achichuk Salad", "Tomato, onion, basil, and light chili.", 26000m),
            ("Suzma Plate", "Strained yogurt with herbs and warm bread.", 22000m),
            ("Assorted Pickles", "House pickles with seasonal vegetables.", 24000m),
        };

        var drinks = new[]
        {
            ("House Lemonade", "Fresh lemon, mint, and sparkling water.", 28000m),
            ("Green Tea Pot", "Uzbek green tea served for the table.", 16000m),
            ("Honey Cake", "Layered honey cake with light cream.", 32000m),
        };

        var sort = 1;
        foreach (var (name, description, price) in mainDishes)
        {
            yield return new MenuItem
            {
                Id = Guid.NewGuid(), RestaurantId = restaurantId, CategoryId = categories[0].Id,
                Name = name, Description = description, Price = price, IsAvailable = true, IsPopular = sort <= 2, SortOrder = sort++
            };
        }

        sort = 1;
        foreach (var (name, description, price) in starters)
        {
            yield return new MenuItem
            {
                Id = Guid.NewGuid(), RestaurantId = restaurantId, CategoryId = categories[1].Id,
                Name = name, Description = description, Price = price, IsAvailable = true, IsPopular = sort == 1, SortOrder = sort++
            };
        }

        sort = 1;
        foreach (var (name, description, price) in drinks)
        {
            yield return new MenuItem
            {
                Id = Guid.NewGuid(), RestaurantId = restaurantId, CategoryId = categories[2].Id,
                Name = name, Description = description, Price = price, IsAvailable = true, IsPopular = sort == 1, SortOrder = sort++
            };
        }
    }

    private static IEnumerable<RestaurantTable> CreateDemoTables(Guid restaurantId)
    {
        var tables = new (string Label, int Capacity, string Location)[]
        {
            ("H1", 2, "hall"), ("H2", 4, "hall"), ("H3", 4, "hall"), ("H4", 6, "hall"),
            ("F1", 6, "family"), ("F2", 8, "family"), ("V1", 8, "vip"), ("V2", 10, "vip"),
            ("T1", 4, "terrace"), ("T2", 6, "terrace")
        };

        foreach (var (label, capacity, location) in tables)
        {
            yield return new RestaurantTable
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId,
                Label = label,
                Capacity = capacity,
                Location = location,
                IsActive = true
            };
        }
    }

    private static async Task EnsureAllRestaurantDemoDataAsync(AppDbContext db, CancellationToken ct)
    {
        var restaurants = await db.Restaurants
            .Include(r => r.MenuCategories)
            .Include(r => r.Tables)
            .ToListAsync(ct);
        var defaultHours = """{"mon":"10:00-23:00","tue":"10:00-23:00","wed":"10:00-23:00","thu":"10:00-23:00","fri":"10:00-00:00","sat":"10:00-00:00","sun":"10:00-22:00"}""";
        var defaultImages = new List<string>
        {
            "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=900",
            "https://images.unsplash.com/photo-1552566626-52f8b828add9?w=900"
        };

        foreach (var restaurant in restaurants)
        {
            if (string.IsNullOrWhiteSpace(restaurant.Description))
                restaurant.Description = $"{restaurant.Name} demo restorani. Bron, menyu, stol va admin boshqaruvi uchun to'liq ma'lumot bilan to'ldirilgan.";
            if (string.IsNullOrWhiteSpace(restaurant.CuisineType))
                restaurant.CuisineType = "Uzbek";
            if (string.IsNullOrWhiteSpace(restaurant.PriceRange))
                restaurant.PriceRange = "$$";
            if (string.IsNullOrWhiteSpace(restaurant.Address))
                restaurant.Address = $"{(string.IsNullOrWhiteSpace(restaurant.City) ? "Toshkent" : restaurant.City)}, demo manzil";
            if (string.IsNullOrWhiteSpace(restaurant.City))
                restaurant.City = "Toshkent";
            if (string.IsNullOrWhiteSpace(restaurant.Phone))
                restaurant.Phone = "+998 71 200 00 00";
            if (string.IsNullOrWhiteSpace(restaurant.OpeningHours) || restaurant.OpeningHours == "{}")
                restaurant.OpeningHours = defaultHours;
            if (restaurant.TotalSlots <= 0)
                restaurant.TotalSlots = 40;
            if (restaurant.ImagesUrl.Count == 0)
                restaurant.ImagesUrl = defaultImages;
            if (restaurant.Amenities.Count == 0)
                restaurant.Amenities = new List<string> { "Wi-Fi", "Parking", "Card payment", "Family rooms" };
            if (restaurant.Policies.Count == 0)
                restaurant.Policies = new List<string> { "Reservations are held for 15 minutes", "Please notify us for large groups" };
            if (restaurant.Rating <= 0)
                restaurant.Rating = 4.5m;
            if (restaurant.ReviewCount <= 0)
                restaurant.ReviewCount = 25;
            if (restaurant.FoodRating <= 0)
                restaurant.FoodRating = restaurant.Rating;
            if (restaurant.ServiceRating <= 0)
                restaurant.ServiceRating = restaurant.Rating;
            if (restaurant.InteriorRating <= 0)
                restaurant.InteriorRating = restaurant.Rating;

            var hasCategories = await db.MenuCategories.AnyAsync(c => c.RestaurantId == restaurant.Id, ct);
            if (!hasCategories)
            {
                var categories = new[]
                {
                    new MenuCategory { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Signature dishes", SortOrder = 1 },
                    new MenuCategory { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Salads and starters", SortOrder = 2 },
                    new MenuCategory { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Drinks and desserts", SortOrder = 3 },
                };
                db.MenuCategories.AddRange(categories);
                db.MenuItems.AddRange(CreateDemoMenu(restaurant.Id, categories, restaurant.CuisineType));
            }

            var hasTables = await db.RestaurantTables.AnyAsync(t => t.RestaurantId == restaurant.Id && t.IsActive, ct);
            if (!hasTables)
                db.RestaurantTables.AddRange(CreateDemoTables(restaurant.Id));
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<AppUser> EnsureUserAsync(
        AppDbContext db, string email, string password, string fullName,
        string phone, AppRole role, int yumsBalance, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await db.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);

        if (existing is not null)
        {
            if (!existing.Roles.Any(r => r.Role == role))
            {
                db.UserRoles.Add(new UserRoleEntity { UserId = existing.Id, Role = role });
                await db.SaveChangesAsync(ct);
            }
            return existing;
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(), Email = normalized,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            EmailConfirmed = true
        };
        db.Users.Add(user);
        db.Profiles.Add(new Profile
        {
            Id = user.Id, Email = normalized, FullName = fullName,
            Phone = phone, PreferredLanguage = "uz", YumsBalance = yumsBalance
        });
        db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, Role = AppRole.User });
        if (role != AppRole.User)
            db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, Role = role });

        await db.SaveChangesAsync(ct);
        return user;
    }
}
