using Microsoft.EntityFrameworkCore;

namespace DineMaster.Infrastructure.Data;

/// <summary>
/// Demo restoranlarning dastlabki (inglizcha) matnlarini o'zbekchaga o'giradi: tavsif, oshxona turi,
/// qulayliklar, qoidalar va menyu. Faqat seed matni o'zgarmagan qiymatlar almashtiriladi —
/// restoran egasi o'zi yozgan matnga tegilmaydi. Har ishga tushganda xavfsiz (idempotent).
/// </summary>
internal static class DemoTextUz
{
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        ["Modern Uzbek restaurant with plov, grilled meats, private family rooms, and a warm national interior."] =
            "Palov, kabob, alohida oilaviy xonalar va iliq milliy interyerga ega zamonaviy o'zbek restorani.",
        ["Rooftop dining inspired by the Silk Road, with tasting sets, live dutar evenings, and terrace seating."] =
            "Buyuk Ipak yo'lidan ilhomlangan tomdagi restoran: degustatsiya setlari, jonli dutor kechalari va terrasa.",
        ["Family restaurant with Bukhara-style kebabs, lagman, samsa, and a colorful courtyard setting."] =
            "Buxorocha kabob, lag'mon, somsa va rang-barang hovliga ega oilaviy restoran.",
        ["Fast, bright bistro near Chorsu with manty, somsa, shashlik wraps, tea, and quick lunch combos."] =
            "Chorsu yaqinidagi tezkor va yorug' bistro: manti, somsa, shashlik-lavash, choy va tushlik setlari.",
        ["Garden restaurant in Fergana with outdoor tables, family cabins, kazan dishes, and seasonal salads."] =
            "Farg'onadagi bog' restorani: ochiq havodagi stollar, oilaviy kabinalar, qozon taomlari va mavsumiy salatlar.",
        ["Premium steakhouse with dry-aged beef, chef sauces, business lunch, and private VIP dining."] =
            "Yetiltirilgan mol go'shti, oshpaz souslari, biznes-lanch va alohida VIP xonali premium steakxaus.",
        ["Large family venue with children's corner, group tables, national dishes, and friendly service."] =
            "Bolalar burchagi, katta guruh stollari, milliy taomlar va samimiy xizmatga ega katta oilaviy restoran.",
        ["Khorezm cuisine with tuxum barak, shivit oshi, grilled fish, and an Ichan-Qala inspired interior."] =
            "Tuxum barak, shivit oshi, qovurilgan baliq va Ichan-Qal'adan ilhomlangan interyerli Xorazm oshxonasi.",
        ["Fresh river fish, kazan fries, crisp salads, and relaxed family seating close to the Sirdaryo route."] =
            "Yangi daryo baliqlari, qozonda qovurilgan kartoshka, salatlar va Sirdaryo yo'lidagi shinam oilaviy joy.",
        ["Fresh plant-based bowls, seasonal soups, house lemonades, and quiet laptop-friendly seating."] =
            "O'simlik asosidagi taomlar, mavsumiy sho'rvalar, uy limonadlari va noutbuk bilan ishlashga qulay sokin joy.",
    };

    private static readonly Dictionary<string, string> Cuisines = new()
    {
        ["Uzbek"] = "O'zbek", ["Uzbek-European"] = "O'zbek-Yevropa", ["National"] = "Milliy",
        ["Street Food"] = "Ko'cha taomlari", ["Steakhouse"] = "Steakxaus", ["Family"] = "Oilaviy",
        ["Khorezm"] = "Xorazm", ["Seafood"] = "Baliq taomlari", ["Vegetarian"] = "Vegetarian",
    };

    private static readonly Dictionary<string, string> ListItems = new()
    {
        ["Family rooms"] = "Oilaviy xonalar", ["Parking"] = "Avtoturargoh", ["Card payment"] = "Karta orqali to'lov",
        ["Pre-order menu"] = "Oldindan buyurtma",
        ["Reservations are held for 15 minutes"] = "Bron 15 daqiqa saqlanadi",
        ["Smoking only in designated areas"] = "Chekish faqat maxsus joyda",
        ["Outside food requires approval"] = "Tashqaridan taom olib kelish oldindan kelishiladi",
        ["Please notify us for large groups"] = "Katta guruhlar uchun oldindan xabar bering",
    };

    private static readonly Dictionary<string, string> Categories = new()
    {
        ["Signature dishes"] = "Asosiy taomlar",
        ["Salads and starters"] = "Salatlar va gazaklar",
        ["Drinks and desserts"] = "Ichimliklar va shirinliklar",
    };

    private static readonly Dictionary<string, (string Name, string Description)> Items = new()
    {
        ["Grilled Sazan"] = ("Panjarada sazan", "O'tlar va limon bilan yangi daryo balig'i."),
        ["Fish Kazan Fry"] = ("Qozonda qovurilgan baliq", "Kartoshka va uy sousi bilan qarsildoq baliq bo'laklari."),
        ["Shrimp Lagman"] = ("Krevetkali lag'mon", "Krevetka va sabzavotli qo'lda cho'zilgan lag'mon."),
        ["Ribeye Steak"] = ("Ribay steyk", "Ko'mirda pishirilgan ribay, qalampir sousi bilan."),
        ["Beef Tenderloin"] = ("Mol go'shti file", "Kartoshka pyuresi va sous bilan yumshoq file."),
        ["Smoked Brisket"] = ("Dudlangan brisket", "Sekin pishirilgan brisket, tuzlama va non bilan."),
        ["Green Power Bowl"] = ("Yashil bowl", "Kinoa, avokado, ko'katlar, no'xat va tahina."),
        ["Mushroom Plov"] = ("Qo'ziqorinli palov", "Qo'ziqorin va sariq sabzili vegetarian palov."),
        ["Pumpkin Ravioli"] = ("Qovoqli ravioli", "Shalfey va yong'oqli, qo'lda tayyorlangan ravioli."),
        ["Shashlik Wrap"] = ("Shashlik-lavash", "Ko'kat va pomidorli qo'y go'shti shashlik lavashda."),
        ["Mini Somsa Set"] = ("Mini somsa seti", "Qatiq sousi bilan to'rtta qatlamli somsa."),
        ["Lagman Bowl"] = ("Lag'mon", "Mol go'shti va sabzavotli quyuq lag'mon."),
        ["Tashkent Plov"] = ("Toshkent palovi", "Guruch, mol go'shti, sabzi, no'xat va bedana tuxumi."),
        ["Mixed Shashlik"] = ("Assorti shashlik", "Piyoz bilan qo'y, mol va tovuq go'shtidan shashlik."),
        ["Kazan Kabob"] = ("Qozon kabob", "Qozonda sekin qovurilgan qo'y go'shti va kartoshka."),
        ["Achichuk Salad"] = ("Achchiq-chuchuk", "Pomidor, piyoz, rayhon va ozgina achchiq qalampir."),
        ["Suzma Plate"] = ("Suzma", "Ko'katli suzma va issiq non."),
        ["Assorted Pickles"] = ("Tuzlamalar assortisi", "Mavsumiy sabzavotlardan uy tuzlamalari."),
        ["House Lemonade"] = ("Uy limonadi", "Yangi limon, yalpiz va gazli suv."),
        ["Green Tea Pot"] = ("Ko'k choy (choynak)", "Stol uchun o'zbekcha ko'k choy."),
        ["Honey Cake"] = ("Medovik", "Yengil kremli qatlamli asal torti."),
    };

    public static async Task ApplyAsync(AppDbContext db, CancellationToken ct)
    {
        var restaurants = await db.Restaurants.ToListAsync(ct);
        foreach (var r in restaurants)
        {
            if (Descriptions.TryGetValue(r.Description, out var d)) r.Description = d;
            if (Cuisines.TryGetValue(r.CuisineType, out var c)) r.CuisineType = c;
            if (r.Amenities.Any(ListItems.ContainsKey))
                r.Amenities = r.Amenities.Select(a => ListItems.GetValueOrDefault(a, a)).ToList();
            if (r.Policies.Any(ListItems.ContainsKey))
                r.Policies = r.Policies.Select(p => ListItems.GetValueOrDefault(p, p)).ToList();
        }

        var englishCategories = Categories.Keys.ToList();
        foreach (var cat in await db.MenuCategories.Where(x => englishCategories.Contains(x.Name)).ToListAsync(ct))
            cat.Name = Categories[cat.Name];

        var englishItems = Items.Keys.ToList();
        foreach (var item in await db.MenuItems.Where(x => englishItems.Contains(x.Name)).ToListAsync(ct))
        {
            var (name, description) = Items[item.Name];
            item.Name = name;
            item.Description = description;
        }

        await db.SaveChangesAsync(ct);
    }
}
