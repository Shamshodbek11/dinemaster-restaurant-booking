# DineMaster — o'rnatish va ishlatish qo'llanmasi

> 🇬🇧 English overview: [README.md](../README.md) · O'zgarishlar tarixi: [CHANGES.md](../CHANGES.md)

Restoran va tadbir zallarini onlayn bron qilish platformasi. Barcha buyruqlar repozitoriy ildiz papkasidan ishga tushiriladi.

- **Backend:** ASP.NET Core (.NET 10), Entity Framework Core, PostgreSQL
- **Frontend:** React 18, TypeScript, Vite, Tailwind CSS, shadcn/ui

## Talablar

- .NET 10 SDK
- PostgreSQL 15+
- Node.js 20+ va npm

## 1. Ma'lumotlar bazasini yaratish

```bash
psql -U postgres -f scripts/create-database.sql
```

Jadvallar backend birinchi ishga tushganda migratsiyalar orqali avtomatik yaratiladi.

## 2. Backend sozlamalari (maxfiy qiymatlar)

Parol va secretlar kodda saqlanmaydi (user-secrets — Windows'da `%APPDATA%\Microsoft\UserSecrets`).
Eng oson yo'l — **o'z terminalingizda** (PowerShell) bir marta skriptni ishga tushirish:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-dev-secrets.ps1
```

Skript PostgreSQL parolini so'raydi (ekranda ko'rinmaydi), JWT kalitini o'zi yaratadi va ixtiyoriy ravishda SMTP (email) sozlamalarini kiritadi.

Qo'lda kiritish ham mumkin:

```bash
cd backend/DineMaster.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=dine_master;Username=postgres;Password=SIZNING_PAROL"
dotnet user-secrets set "Jwt:Secret" "kamida-32-belgili-tasodifiy-kalit"
```

Ixtiyoriy:

```bash
dotnet user-secrets set "SetupKey" "bir-martalik-superadmin-kaliti"   # production'da superadmin yaratish uchun
```

## 3. Backendni ishga tushirish

```bash
cd backend/DineMaster.Api
dotnet run
```

- `Properties/launchSettings.json` muhitni **Development** qiladi va `http://localhost:5000` da ishga tushiradi. User-secrets faqat Development'da o'qiladi — boshqa muhitda `ConnectionStrings__DefaultConnection` va `Jwt__Secret` environment variable sifatida beriladi.
- Migratsiyalar avtomatik bajariladi (baza bo'lmasa, yaratiladi).
- Holatni tekshirish: `http://localhost:5000/health` → `Healthy`.
- **Development** muhitida test akkauntlar yaratiladi (`user@dinemaster.uz`, `admin1@dinemaster.uz` va h.k.). Parollar `DbInitializer.cs` da. Production'da bu akkauntlar yaratilmaydi.
- Swagger: `http://localhost:5000/swagger` (faqat Development).

## 4. Frontend

```bash
cd frontend
cp .env.example .env        # VITE_API_BASE_URL backend manziliga mos bo'lsin
npm install
npm run dev                 # http://localhost:8080
```

## Email (tasdiqlash va parol tiklash kodlari)

Ro'yxatdan o'tganda va parolni tiklashda emailga **6 xonali kod** yuboriladi (15 daqiqa amal qiladi, 5 ta xato urinishdan keyin bekor bo'ladi). Bron qilish uchun email tasdiqlangan bo'lishi kerak (`Booking:RequireConfirmedEmail`).

- SMTP sozlanmagan + Development → kod **backend konsoliga** chiqadi (`[DEV EMAIL]`).
- SMTP sozlangan → haqiqiy xat. Gmail uchun: Google akkaunt → Xavfsizlik → 2 bosqichli tekshiruv → **App passwords**.

```bash
dotnet user-secrets set "Email:Smtp:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:Smtp:Port" "587"
dotnet user-secrets set "Email:Smtp:Username" "sizning@gmail.com"
dotnet user-secrets set "Email:Smtp:Password" "app-password"
dotnet user-secrets set "Email:FromAddress" "sizning@gmail.com"
```

Production'da SMTP sozlanmasa, kod yuborish 503 qaytaradi.

## Onlayn to'lov (bron depoziti)

Depozit talab qilinadigan bronni mijoz Payme yoki Click orqali to'laydi. To'lanmagan depozitli bron `Booking:DepositHoldMinutes` (30) daqiqadan keyin avtomatik bekor qilinadi (faqat onlayn to'lov yoqilgan bo'lsa).

| Provayder | Sozlamalar (user-secrets / env) | Kassa kabinetida ko'rsatiladigan manzil |
|---|---|---|
| Payme | `Payments:Payme:MerchantId`, `Payments:Payme:Key`, `Payments:Payme:TestMode` (true — test.paycom.uz), hisob maydoni `order_id` | `https://SIZNING-DOMEN/api/payments/payme` |
| Click | `Payments:Click:ServiceId`, `Payments:Click:MerchantId`, `Payments:Click:SecretKey` | Prepare: `/api/payments/click/prepare`, Complete: `/api/payments/click/complete` |
| test | Development'da avtomatik yoqiq (`Payments:EnableTestProvider`) — pul yechilmaydi | — |

## Bron qoidalari

- **Ish vaqti**: bron restoran ochilgandan keyin va yopilishdan kamida 1 soat oldin boshlanishi kerak (`Booking:LastBookingBeforeCloseMinutes`). Admin panel → Restoran → "Ish vaqti". Tungi smena (18:00–02:00) qo'llab-quvvatlanadi.
- **Soatlik sig'im**: "Bir soatda qabul qilinadigan bronlar" (0 — cheklanmagan). Kunlik sig'im ham amal qiladi.
- **Kutish ro'yxati**: bron bekor qilinsa, shu kunga navbatda turgan birinchi mijozga ilova ichida va email orqali xabar boradi.
- **"Kelmadi" himoyasi**: oxirgi 180 kunda 2 marta kelmagan mijozdan har qanday restoranda depozit olinadi (har bir mehmon uchun 50 000 so'm; `Booking:NoShow*`).
- **Moderatsiya**: tasdiqlangan restoranning nomi, tavsifi, manzili, oshxona turi, rasmlari o'zgartirilsa — superadmin tasdiqlamaguncha eski qiymatlar ko'rinadi (Superadmin panel → Moderatsiya). Ish vaqti, slotlar, depozit darhol qo'llanadi.
- **Maxsus kunlar**: Admin panel → Restoran → "Maxsus kunlar" — bayram yoki sanitariya kunida restoran yopiq yoki qisqa ish vaqti bilan ishlaydi; bron va "bo'sh joy" qidiruvi shuni hisobga oladi.
- **Bekor qilish siyosati** (`Booking:FreeCancellationHours` = 24, `Booking:LateCancellationMinutes` = 120): bron vaqtidan 24 soat oldin bekor qilinsa depozit qaytariladi, kechroq yoki kelmasa — restoranda qoladi. 2 soatdan kam qolganda bekor qilish "kech" hisoblanadi va depozit qoidasida "kelmadi" bilan birga sanaladi. Restoran o'zi bekor qilsa, depozit doim to'liq qaytadi.
- **Depozitni qaytarish**: test to'lov — darhol; Payme — `CancelTransaction` orqali avtomatik; Click yoki naqd — bron "qaytarish so'ralgan" holatiga o'tadi: pulni kassa kabinetida qaytarib, Superadmin → To'lovlar (yoki restoran menejeri → Bronlar) da "Qaytarildi" ni bosing.
- **Bronni o'zgartirish**: mijoz sana, vaqt va mehmonlar sonini bron boshlanishidan 2 soat oldingacha profilida o'zgartiradi; ko'pi bilan 90 kun oldin bron qilinadi (`Booking:MaxDaysAhead`).
- **Chegirma**: restoran chegirmasi (vaqt oralig'ida yoki kun bo'yi) bronga avtomatik qo'llanadi; yums chegirmasi bilan jami `Booking:MaxTotalDiscountPercent` (50%) dan oshmaydi.
- **Do'st taklifi**: har bir foydalanuvchining kodi/havolasi bor (`/signup?ref=KOD`). Taklif qilingan do'st birinchi tashrifini yakunlasa, taklif qilganga `Booking:ReferralBonus` (500) yums.

## Restoran xodimlari

Admin panel → **Xodimlar** (faqat restoran egasi): xodim avval saytda ro'yxatdan o'tadi, egasi uning emailini kiritib rol tanlaydi.

| Rol | Nima qila oladi |
|---|---|
| Host (administrator) | Bronlar, kalendar, mehmonlar, "Keldi/Kelmadi", stol biriktirish, bekor qilish, kutish ro'yxati |
| Menejer | Host + menyu, stollar, sharhlarga javob, restoran sozlamalari, chegirmalar, hisobotlar, depozit qaytarilganini belgilash |
| Egasi | Hammasi + xodimlarni boshqarish va restoranni o'chirish |

## Telegram bildirishnomalari (ixtiyoriy)

1. Telegram'da **@BotFather** → `/newbot` → nom va username (masalan `DineMasterBot`) → token beriladi.
2. `scripts/setup-dev-secrets.ps1` ni ishga tushirib Telegram bo'limida token va username'ni kiriting (yoki `Telegram:BotToken`, `Telegram:BotUsername`).
3. Backendni qayta ishga tushiring. Foydalanuvchi: Profil → Sozlamalar → Telegram → **Ulash** → botda **Start**. Shundan so'ng saytdagi barcha bildirishnomalar Telegram'ga ham keladi.

Token sozlanmagan bo'lsa bot o'chiq turadi, sayt odatdagidek ishlaydi.

## Dastur nomi

Nom bitta joyda o'zgartiriladi:
- frontend: `frontend/.env` → `VITE_BRAND_NAME=YangiNom` (sarlavha, logotip, matnlar);
- backend: `appsettings.json` → `"Branding": { "Name": "YangiNom" }` (email xatlari, PDF hisobot, Telegram).

Qo'llab-quvvatlash emaili (Shartlar/Maxfiylik sahifalarida): `VITE_SUPPORT_EMAIL`.

## Ma'lumotlar bazasiga ulanish muammolari

Backend ishga tushganda ulanishni tekshiradi va xato bo'lsa, sababini log'ga aniq yozadi:

| Log'dagi xabar | Sabab | Yechim |
|---|---|---|
| `ConnectionStrings:DefaultConnection topilmadi` | User-secrets kiritilmagan yoki muhit Development emas | 2-bo'limdagi buyruqlar; `dotnet run` ni `DineMaster.Api` papkasida ishga tushiring |
| `... paroli noto'g'ri` | `Password=` noto'g'ri | user-secrets'dagi connection string'ni yangilang |
| `... PostgreSQL javob bermadi` | PostgreSQL xizmati o'chiq yoki port boshqa | Windows: `services.msc` → `postgresql-x64-*` → Start |
| `... huquqi yo'q` | Foydalanuvchida CREATEDB/CREATE huquqi yo'q | `scripts/create-database.sql` ni postgres foydalanuvchisi bilan bajaring |

Yangi migratsiya (`dotnet-ef` loyiha ichida o'rnatilgan, secretlar shart emas):

```bash
cd backend
dotnet tool restore
dotnet ef migrations add MigratsiyaNomi -p DineMaster.Infrastructure -s DineMaster.Api
dotnet ef migrations has-pending-model-changes -p DineMaster.Infrastructure -s DineMaster.Api
```

## Yums qoidasi

- Har bir bron uchun **+100 yums** — tashrif yakunlangach beriladi: restoran egasi "completed" deb belgilaganda yoki bron vaqtidan 3 soat o'tgach avtomatik.
- Bekor qilingan yoki "kelmadi" (no_show) bronlar uchun yums berilmaydi.
- 1000 yums → bitta bronga 20% chegirma. Bron bekor qilinsa, sarflangan yums qaytariladi.

## Production uchun eslatmalar

- `Cors:AllowedOrigins` ga frontend domenini qo'shing (`appsettings.json`).
- SMTP sozlamalarini kiriting (aks holda email kodlari yuborilmaydi).
- `Payments:EnableTestProvider` ni `false` qiling va Payme/Click kalitlarini kiriting.
- `Booking:FrontendBaseUrl` va `Payments:ReturnUrl` ni haqiqiy domenga o'zgartiring.
- Rate limiter IP bo'yicha ishlaydi. Reverse proxy ortida bo'lsangiz, `ForwardedHeaders` ni sozlang.
- Vaqt zonasi: barcha bron vaqtlari `Asia/Tashkent` deb hisoblanadi (`BusinessTime`).
- `/terms` va `/privacy` matnlari namuna — ishga tushirishdan oldin huquqshunos bilan tekshiring va kompaniya rekvizitlarini qo'shing.

## Avtomatik testlar

```bash
cd backend
dotnet test
```

- Unit testlar (ish vaqti, bron qoidalari, moderatsiya, Click imzosi) — bazasiz.
- Integratsion testlar alohida **`dine_master_test`** bazasida ishlaydi (har safar o'chirilib qayta yaratiladi — asosiy bazaga tegilmaydi). Ulanish: `DINEMASTER_TEST_DB` environment variable yoki Api user-secrets'dagi connection string. PostgreSQL topilmasa, integratsion testlar o'tkazib yuboriladi.

## Tekshiruv

```bash
cd frontend
npx tsc -p tsconfig.app.json --noEmit
npx eslint .
npx vite build

cd ../backend
dotnet build
```
