# v8 — dizayn, nom va biznes bo'shliqlari

`dotnet test` 56/56, frontend `tsc` / `eslint` (0 xato) / `vitest` (tarjima qamrovi testi bilan) / `vite build` — o'tadi.
Migratsiya: `V8StaffReferralTelegramPolicies` (backend ishga tushganda avtomatik qo'llanadi).

## Dizayn
| Muammo | Yechim |
|---|---|
| Admin va superadmin paneli telefonda ishlamasdi | Yon menyu telefonda chiqib-kiruvchi panel (☰), jadvallar o'rniga kartochkalar, formalar moslashuvchan |
| Tillar aralashib ketgan | Barcha ommaviy sahifalar 3 tilda; inglizcha qoldiqlar ("Welcome back!", "Pending", "Check-in", 404 sahifa, kalendar oy/kun nomlari) tuzatildi; demo restoranlar matnlari (tavsif, qulayliklar, qoidalar, menyu) o'zbekchaga o'girildi; `src/test/i18n.test.ts` har bir kalit uchala tilda borligini tekshiradi |
| Bron 4 qadam | 2 qadam: (1) sana, vaqt, mehmonlar + ixtiyoriy stol va oldindan buyurtma, (2) tekshirish. Chegirmali vaqtlar belgilangan, bekor qilish shartlari ko'rinadi |
| Faqat qorong'i tema | Standart — qurilma sozlamasi; navbarda yorug'/qorong'i tugmasi; sozlamalarda 3 variant |
| Bosh sahifadagi soxta raqamlar (2 500+, 1M+) | Bazadan haqiqiy ko'rsatkichlar: `GET /api/public/stats` |

## Nom
Dastur nomi bitta joyda: frontend `VITE_BRAND_NAME` (`src/lib/brand.ts`), backend `Branding:Name` (email, PDF, Telegram).

## Biznes bo'shliqlari
| # | Nima qilindi |
|---|---|
| 1 | **Bekor qilish siyosati**: bron vaqtidan 24 soat oldin bekor qilinsa depozit qaytadi, kechroq yoki kelmasa — qaytmaydi; 2 soatdan kam qolganda "kech bekor qilish" (depozit qoidasida hisobga olinadi) |
| 2 | **Depozit qaytarish**: test to'lov — darhol; Payme — avtomatik; Click/naqd — "qaytarish so'ralgan" holati, superadmin (To'lovlar) yoki restoran menejeri "Qaytarildi" deb belgilaydi; mijozga xabar |
| 3 | **Restoran bekor qilsa / kelmadi deb belgilasa** — mijozga sabab bilan bildirishnoma; restoran bekor qilsa depozit to'liq qaytadi |
| 4 | **Bronni o'zgartirish** — sana, vaqt, mehmonlar (2 soatgacha), restoranga xabar |
| 5 | **Restoran chegirmasi** bronga avtomatik qo'llanadi (vaqt oralig'ida), yums bilan jami 50% gacha; bron, profil va admin panelda ko'rinadi |
| 6 | **Haqiqiy do'st taklifi**: shaxsiy kod/havola, do'st birinchi tashrifini yakunlasa 500 yums |
| 7 | Profildagi **soxta "to'lov kartalari"** olib tashlandi (`payment_methods` jadvali o'chirildi) |
| 8 | **Stol biriktirish** — admin bronga stol tanlaydi (to'qnashuv tekshiriladi) |
| 9 | **Xodimlar**: menejer va host rollari, egasi email bo'yicha qo'shadi; host — faqat bronlar, menejer — sozlama/menyu/hisobotlar, egasi — xodimlar va o'chirish |
| 10 | **Zal egasi paneli** — o'z zallari va tadbir so'rovlari (tasdiqlash/bekor/yakunlash) admin panelda |
| 11 | **Maxsus kunlar** (bayram, sanitariya kuni) — yopiq yoki qisqa ish vaqti; bron va qidiruvda hisobga olinadi |
| 12 | **Telegram bildirishnomalari** — bot orqali (Profil → Sozlamalar → Telegram) |
| 13 | **Bo'sh joy bo'yicha qidiruv** — sana + vaqt + mehmonlar: faqat joyi bor restoranlar |
| 14 | **Foydalanish shartlari, Maxfiylik siyosati, FAQ** sahifalari (3 tilda) |
| 15 | **Shaxsiy ma'lumotlarga rozilik** ro'yxatdan o'tishda majburiy (`terms_accepted_at`) |
| 16 | "Mening sharhlarim" haqiqiy (`GET /api/reviews/mine`), bildirishnomani o'qildi qilish tuzatildi (PATCH → POST) |

Qo'shimcha: superadmin foydalanuvchilar filtrida "Xodimlar", statistikada qaytarilgan depozitlar hisobga olinmaydi, NuGet'dagi zaif `System.Security.Cryptography.*` paketlari 10.0.13 ga yangilandi.

---

# v7 — superadmin, hisobotlar, bildirishnomalar

`dotnet test` 43/43, frontend `tsc`/`eslint` (0 xato)/`vite build` — o'tadi. Migratsiya: `V7PartnersAuditLog` (2 ta yangi jadval).

| # | So'rov | Nima qilindi |
|---|---|---|
| 1 | "Hoziroq qo'shiling" ishlamasdi | Tugma `/partner` sahifasiga olib boradi: hamkorlik arizasi → superadmin tasdiqlaydi → foydalanuvchiga restoran egasi roli. Profildagi soxta forma ham haqiqiy arizaga almashtirildi. |
| 2 | Superadmin kichik o'zgarishlarni ko'rmasdi | **Faoliyat jurnali**: menyu narxi, depozit, slotlar, stollar, ish vaqti, bron statuslari, superadmin amallari — kim, qachon, eski → yangi qiymat. Moderatsiya (tasdiq talab qiladigan) faqat nom/manzil/tavsif/rasmlar uchun qoldi. |
| 3 | Yums tushmadi | Kod to'g'ri edi (yums tashrifdan keyin), lekin sahifada "bron qiling — avtomatik 100 Yums" deb noto'g'ri yozilgan edi. Matnlar tuzatildi; profil va Yums sahifasida "tashrifdan so'ng +100" / "berildi" va kutilayotgan yums ko'rsatiladi. |
| 4 | Foydalanuvchilar filtri/qidiruvi | Rol bo'yicha filtr (mijoz / restoran egasi / superadmin / bloklangan), ism-email-telefon bo'yicha qidiruv, adminlar restoran nomi bo'yicha. Foydalanuvchi kartochkasi (bronlar, yums tarixi). |
| 5 | Superadmin funksiyalari kam | Yangi bo'limlar: hamkorlik arizalari, bronlar (filtr + status), sharhlar (o'chirish), zal bronlari (tasdiqlash), to'lovlar, faoliyat jurnali, ommaviy xabar; har qanday restoranni "Boshqarish" (menyu, stollar, sozlamalar, hisobotlar); zalni to'xtatish; emailni qo'lda tasdiqlash; restoran egasini almashtirish. |
| 6 | Keraksiz funksiyalar | Takroriy API'lar (`users/{id}/ban` va h.k., `restaurants/pending`, `approve`), ishlamaydigan "Email bildirishnomalari" kaliti olib tashlandi; "Brauzer notifikatsiyalari" haqiqatda ishlaydigan qilindi. |
| 7 | Admin hisobotlari | "Hisobotlar" bo'limi: kunlik/haftalik/oylik/yillik, grafik, jadval, gavjum soatlar, ko'p buyurtma qilingan taomlar, depozitlar, kelmaganlar ulushi; **PDF yuklab olish** (QuestPDF). |
| 10 | Bildirishnomalar ko'rinmasdi | Frontendda umuman yo'q edi → qo'ng'iroqcha (sayt, admin va superadmin panellarida), o'qilmaganlar soni, 30 soniyada yangilanadi. Restoran egasiga: yangi bron, bekor qilish, yangi sharh, kutish ro'yxati. Mijozga: sharhga javob, zal broni holati, yums. |

Qo'shimcha: admin bronlari restoran bo'yicha (500 tagacha, oldin 30 ta edi), bir nechta restoran egasi uchun tanlash, zal broni holatini o'zgartirish API'si.

---

# v6 — yangi imkoniyatlar

`dotnet build` (0 xato, 0 ogohlantirish), `dotnet test` (35/35), frontend `tsc`, `eslint` (0 xato), `vite build`, `vitest` — hammasi o'tadi.
Asosiy bazaga migratsiya qo'llanib, brauzerda ro'yxatdan o'tish → email kod → bron → depozit to'lovi oqimi sinab ko'rildi.

| # | Imkoniyat | Qayerda |
|---|---|---|
| 1 | **Ish vaqti bo'yicha tekshiruv** — tungi smena, dam olish kunlari, oxirgi bron yopilishdan 1 soat oldin; bron oynasida faqat mavjud vaqtlar; admin panelda ish vaqti muharriri | `OpeningHours.cs`, `BookingService`, `GET /api/restaurants/{id}/availability`, `ProBookingModal`, `OpeningHoursEditor` |
| 2 | **Kutish ro'yxati avtomatikasi** — bron bekor qilinsa navbatdagi birinchi mijozga bildirishnoma + email | `BookingService.NotifyWaitlistAsync` |
| 3 | **Email kod** — ro'yxatdan o'tishda emailni tasdiqlash (bron uchun shart), parolni 6 xonali kod bilan tiklash; SMTP (MailKit) | `EmailCodeService`, `SmtpEmailService`, `VerifyEmailPage`, `ForgotPasswordPage` |
| 4 | **Onlayn depozit to'lovi** — Payme Merchant API, Click SHOP API, lokal test provayder; to'lanmagan depozitli bron 30 daqiqada bekor bo'ladi | `PaymentService`, `PaymentsController`, `DepositPayButtons` |
| 5 | **Soatlik sig'im** | `Restaurant.HourlySlots`, admin panel |
| 6 | **Moderatsiya** — tasdiqlangan restoranning nom/manzil/tavsif o'zgarishlari superadmin tasdig'idan keyin | `RestaurantChangeSet`, `SuperadminController`, `ModerationPanel` |
| 7 | **"Kelmadi" himoyasi** — 180 kunda 2+ marta kelmagan mijozdan depozit | `BookingService.GetDepositRuleAsync`, `GET /api/reservations/quote` |
| 8 | **Avtomatik testlar** — 35 ta (unit + haqiqiy PostgreSQL bilan integratsion) | `backend/DineMaster.Tests` |

## Olib tashlandi
- Telefon/SMS bilan ishlaydigan barcha qismlar: `PhoneController`, Eskiz SMS xizmati, `phone_verifications` jadvali, `reservations.is_phone_verified`, frontend `phoneApi`, SMS bildirishnoma sozlamasi. Profildagi telefon raqami faqat aloqa uchun qoldi.
- Eski token-havolali parol tiklash (`password_reset_tokens`) — o'rniga email kod.
- Ishlatilmaydigan `BookingModal.tsx`.

## Boshqa
- `scripts/setup-dev-secrets.ps1` — user-secrets'ni bir buyruq bilan sozlash.
- `HH:mm` va `HH:mm:ss` vaqt formatlari ikkalasi ham qabul qilinadi.
- Rate limit qiymatlari sozlamadan (`RateLimiting:*`).

---

# v5 — ikkinchi kod tekshiruvi bo'yicha tuzatishlar

Backend `dotnet build` (0 xato, 0 ogohlantirish), `dotnet ef migrations has-pending-model-changes` (model mos),
frontend `tsc`, `eslint` (0 xato), `vite build`, `vitest` — hammasi o'tadi.

## Ma'lumotlar bazasi / ishga tushirish
| Muammo | Tuzatish |
|---|---|
| `launchSettings.json` yo'q edi → `dotnet run` Production'da ishlab, user-secrets o'qilmasdi va ilova "ConnectionStrings topilmadi" bilan yiqilardi; CORS ham bo'sh edi | `Properties/launchSettings.json` (Development, http://localhost:5000) |
| `dotnet ef migrations add` secretlarsiz ishlamasdi | `EF.IsDesignTime` da vaqtinchalik qiymatlar; `dotnet-ef` lokal tool sifatida qo'shildi |
| Ulanish xatosida tushunarsiz stack trace | `Startup/DatabaseStartup.cs`: parol/xizmat/huquq/baza yo'qligini aniq yozadi |
| Retry + Serializable tranzaksiyada qayta urinishda ma'lumot ikki marta yozilishi mumkin edi | Har urinishdan oldin `ChangeTracker.Clear()` |
| Taom/kategoriyani o'chirish FK xatosi (500) bilan yiqilardi | Buyurtmada bo'lsa — yashiriladi / 409 |
| Restoranni o'chirish barcha bronlar va sharhlarni cascade o'chirardi | Bronlar bo'lsa 409 |

## Bron va Yums
- Yarim tun atrofida stol ikki marta bron qilinardi (`TimeOnly` aylanib o'tishi) → sana+vaqt bilan solishtirish (`BookingRules.Overlaps`).
- Bugungi o'tgan soatga bron qilish mumkin edi → tuzatildi.
- Yums farming: yums endi tashrif yakunlangach beriladi (admin "completed" yoki 3 soatdan keyin avtomatik). Bekor qilishda faqat sarflangan yums qaytadi.
- Admin status o'zgartirganda yums hisobga olinmasdi → barcha o'zgarishlar `BookingService.ChangeStatusAsync` orqali; ruxsat etilgan o'tishlar jadvali.
- Pre-order'da bir taom ikki marta kelsa saqlash yiqilardi → miqdorlar qo'shiladi.
- Yetarli yums bo'lmasa chegirma jim e'tiborsiz qoldirilardi → aniq xabar.
- Telefon tasdiqi bir nechta bronda qayta ishlatilardi → bir marta.

## Zallar
- `GET /api/venues/{id}` JSON aylanma bog'lanish tufayli 500 berardi va boshqa mijozlarning bronlarini ochib qo'yardi → DTO.
- Bir sanaga ikki marta bron qilish mumkin edi → Serializable tekshiruv, 409.
- `PUT /api/venues/{id}` frontendda bor, backendda yo'q edi → qo'shildi.

## Xavfsizlik
- Xato middleware har qanday `InvalidOperationException` matnini klientga yuborardi → faqat `BusinessRuleException` va boshqalar; 404/403/409/503 to'g'ri kodlar.
- Bloklangan foydalanuvchi va olib qo'yilgan rol 24 soat ishlab turardi → har so'rovda bazadan tekshiriladi.
- OTP: 5 ta xato urinishdan keyin kod bekor qilinadi, yangi kod eskisini bekor qiladi, SMS limiti (5 ta / 10 daqiqa).
- Parol tiklash tokeni bazada SHA-256 hash ko'rinishida saqlanadi.
- Login: email mavjudligini javob vaqtidan aniqlab bo'lmaydi.
- `/api/reservations/calendar` faqat restoran egasi/superadmin uchun.
- Superadmin o'zini bloklay olmaydi va o'zidan superadmin rolini ololmaydi.

## Frontend–backend mosligi
- Sharhlar: `ownerResponse`/`userFullName`/`rating` maydonlari — restoran javobi saqlanmas va ko'rinmas edi.
- Kutish ro'yxati: `userFullName`.
- Bildirishnoma `PATCH .../read` qabul qilinadi.

## Validatsiya
Stol sig'imi, slotlar, depozit, chegirma, koordinatalar, taom narxi/nomi, zal sig'imi/narxi, kutish ro'yxati (sana, restoran, mehmonlar), profil (ism, telefon), sharh reytingi 1–10, `page < 1`, noto'g'ri `month`.

## Boshqa
- Statistikada "bugun" UTC emas, Toshkent vaqti bilan.
- Eslatmalar oynasi kengaytirildi (2 soatlik eslatma tushib qolmaydi).
- Profil: "kelgusi/o'tgan" bronlar mahalliy vaqt bo'yicha; tugagan bronni bekor qilish tugmasi chiqmaydi.
- Frontend: sahifalar lazy-load, vendor chunk'lar (806 KB → eng kattasi 267 KB), `useEffect` ogohlantirishlari.

---

# O'zgarishlar jurnali (kod tekshiruvi bo'yicha)

Asl topilmalar: `DineMaster-kod-tekshiruvi.md`. Quyida ularning holati.

## Tuzatildi

| # | Muammo | Holat | Qayerda |
|---|---|---|---|
| 1 | Maxfiy qiymatlar repoda | Tuzatildi — user-secrets / env | `appsettings*.json`, `Program.cs`, `frontend/.env` → `.env.example` |
| 2 | Test akkauntlar production'da | Tuzatildi — faqat Development | `Program.cs` |
| 3 | CORS: hamma domen + credentials | Tuzatildi — `Cors:AllowedOrigins` | `Program.cs` |
| 4 | Rate limiter global | Tuzatildi — IP bo'yicha partition | `Program.cs` |
| 5 | Reset token log'da | Tuzatildi — log'dan olib tashlandi, `IEmailService` qo'shildi | `AuthService.cs`, `EmailServices.cs` |
| 6 | Overbooking (izolyatsiya) | Tuzatildi — `Serializable` | `BookingService.cs` |
| 7 | O'tgan sanaga bron | Tuzatildi | `BookingService.cs`, `VenuesController.cs` |
| 8 | Admin boshqa sharhga javob yozadi | Tuzatildi — egalik tekshiruvi | `ReviewsController.cs` |
| 9 | Sharh reytinglari validatsiyasiz | Tuzatildi — 0–10, bron talab qilinadi | `ReviewsController.cs` |
| 10 | Venue: manfiy soat/narx | Tuzatildi | `VenuesController.cs` |
| 11 | Frontend–backend endpointlari mos emas | Tuzatildi — backendga 4 endpoint qo'shildi, frontend yo'llari tuzatildi | `WaitlistController`, `NotificationsController`, `PaymentMethodsController`, `api.ts` |
| 12 | Migratsiya xatosi yutiladi | Tuzatildi — ilova to'xtaydi | `Program.cs` |
| 13 | Vaqt zonasi (UTC vs Toshkent) | Tuzatildi — `BusinessTime` | `BusinessTime.cs`, `ReminderService.cs`, `VenuesController.cs` |
| 15 | Telefon tasdiqlash user'ga bog'lanmagan | Tuzatildi — `UserId` tekshiruvi | `BookingService.cs` |
| 16 | OTP zaif | Tuzatildi — kriptografik kod, rate limit | `PhoneController.cs` |
| 18 | Parol siyosati zaif | Tuzatildi — minimal 8 belgi | DTO'lar, `ProfilesController.cs`, `SignupPage.tsx` |
| 20 | Frontend default API porti | Tuzatildi — 5000 | `api.ts` |
| 21–22 | `.gitignore`, `.env` | Tuzatildi | `.gitignore`, `frontend/.gitignore` |
| 23 | Bo'sh `.git` | Tuzatildi — yangi repo | — |
| 24–25 | Lockfile va template qoldiqlari | Tuzatildi | `package.json`, `vite.config.ts` |
| 26 | Dead code | Tuzatildi | mock fayllar, `MenuSection`, `ReviewSection`, `NavLink`, `guestHistory` |
| 28 | Menyu kategoriyasi boshqa restoranniki | Tuzatildi | `MenuController.cs` |
| 29 | Ishlatilmagan rate limit policy | Tuzatildi | `Program.cs` |
| 31–32 | Log/izohlar | Tuzatildi — `.codex` olib tashlandi | — |

Qo'shimcha: frontend lint xatolari (12 ta) va TypeScript xatolari (6 ta) tuzatildi. Frontend build muvaffaqiyatli.

## Hali qilinmagan

| # | Sabab |
|---|---|
| 14 | Yums chegirmasi qoidasi — biznes qarori kerak |
| 17 | Register'da email enumeration — javob formatini o'zgartirish frontendga ta'sir qiladi |
| 19 | Refresh token va token bekor qilish — katta o'zgarish, alohida vazifa |
| 27 | `BookingService` da EF Core'ga cast — arxitektura refaktori |
| 30 | Backend testlari yo'q — alohida loyiha kerak |

## Tekshirilmagan

- **Backend kompilyatsiya qilinmagan.** Bu muhitda .NET SDK o'rnatib bo'lmadi (tarmoq siyosati). Backend o'zgarishlari diff orqali qo'lda tekshirildi va frontend endpoint yo'llari backend marshrutlari bilan avtomatik solishtirildi. Birinchi ishga tushirishda `dotnet build` ni ishlating.
- Bazaga qarshi integratsiya sinovi o'tkazilmagan.
