import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Navbar } from '@/components/Navbar';
import { Footer } from '@/components/Footer';
import { useApp } from '@/contexts/AppContext';
import { t, type Language } from '@/lib/i18n';
import { BRAND } from '@/lib/brand';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';

export type InfoKind = 'terms' | 'privacy' | 'faq';

interface Section { title: string; body: string[] }

const UPDATED = '2026-10-15';
const SUPPORT_EMAIL = (import.meta.env.VITE_SUPPORT_EMAIL as string | undefined)?.trim();

/** Matnlar: {brand} — dastur nomi. Yuridik matnlar ishga tushirishdan oldin huquqshunos bilan tekshirilishi kerak. */
const CONTENT: Record<InfoKind, Record<Language, Section[]>> = {
  terms: {
    uz: [
      { title: '1. Umumiy qoidalar', body: [
        "{brand} — restoran va tadbir zallarini onlayn bron qilish platformasi. Ro'yxatdan o'tish yoki xizmatdan foydalanish orqali siz ushbu shartlarga rozilik bildirasiz.",
        "Platforma mehmon va restoran o'rtasida vositachi hisoblanadi: taomlar, xizmat sifati va restoran ichidagi tartib uchun restoran javobgar.",
      ]},
      { title: '2. Hisob', body: [
        "Hisob ochish uchun haqiqiy ism va ishlaydigan email kerak. Email tasdiqlanmaguncha bron qilib bo'lmaydi.",
        "Parolingizni boshqalarga bermang. Hisobingizdan qilingan harakatlar uchun o'zingiz javobgarsiz.",
      ]},
      { title: '3. Bron qilish', body: [
        "Bron restoranning ish vaqti va bo'sh joylariga qarab qabul qilinadi. Bron 90 kungacha oldin qilinishi mumkin.",
        "Bron vaqtidan 2 soat oldingacha sana, vaqt va mehmonlar sonini profilingizda o'zgartirishingiz mumkin.",
        "Restoran zarurat tufayli bronni bekor qilishi mumkin — bunda sizga xabar yuboriladi va to'langan depozit to'liq qaytariladi.",
      ]},
      { title: '4. Depozit va bekor qilish', body: [
        "Ayrim restoranlar yoki tez-tez kelmaydigan mehmonlar uchun depozit talab qilinadi. Depozit Payme yoki Click orqali to'lanadi va restoran hisobiga o'tadi.",
        "Bron vaqtidan kamida 24 soat oldin bekor qilsangiz, depozit to'liq qaytariladi (odatda 1–3 ish kuni ichida).",
        "Undan kech bekor qilinsa yoki mehmon kelmasa, depozit qaytarilmaydi. Bron boshlanishiga 2 soatdan kam qolganda bekor qilish \"kech bekor qilish\" hisoblanadi.",
        "Muntazam kelmaslik keyingi bronlarda depozit talab qilinishiga olib keladi.",
      ]},
      { title: '5. Yums bonuslari va do\'st taklifi', body: [
        "Har bir yakunlangan tashrif uchun Yums bonusi beriladi. 1000 Yums keyingi bronda 20% chegirma beradi. Yums pulga almashtirilmaydi.",
        "Taklif kodingiz orqali ro'yxatdan o'tgan do'stingiz birinchi tashrifini yakunlasa, sizga bonus Yums beriladi. Suiiste'mol aniqlansa, bonuslar bekor qilinadi.",
      ]},
      { title: '6. Sharhlar', body: [
        "Sharh faqat haqiqiy tashrif asosida yoziladi. Haqoratli, yolg'on yoki reklama mazmunidagi sharhlar o'chiriladi.",
      ]},
      { title: '7. Javobgarlik va o\'zgarishlar', body: [
        "Platforma texnik nosozliklar sababli yuzaga kelgan bilvosita zararlar uchun javobgar emas, lekin ularni tezda bartaraf etishga harakat qiladi.",
        "Shartlar o'zgarishi mumkin; muhim o'zgarishlar haqida oldindan xabar beriladi.",
      ]},
    ],
    ru: [
      { title: '1. Общие положения', body: [
        "{brand} — платформа онлайн-бронирования ресторанов и банкетных залов. Регистрируясь или пользуясь сервисом, вы соглашаетесь с этими условиями.",
        "Платформа является посредником между гостем и рестораном: за блюда, качество обслуживания и порядок в заведении отвечает ресторан.",
      ]},
      { title: '2. Аккаунт', body: [
        "Для аккаунта нужны настоящее имя и рабочий email. Бронирование доступно после подтверждения email.",
        "Не передавайте пароль другим. Вы отвечаете за действия, совершённые из вашего аккаунта.",
      ]},
      { title: '3. Бронирование', body: [
        "Бронь принимается с учётом часов работы и свободных мест ресторана. Бронировать можно не более чем за 90 дней.",
        "Дату, время и число гостей можно изменить в профиле не позднее чем за 2 часа до брони.",
        "Ресторан может отменить бронь по необходимости — вы получите уведомление, а оплаченный депозит вернётся полностью.",
      ]},
      { title: '4. Депозит и отмена', body: [
        "Некоторые рестораны, а также гости с частыми неявками, вносят депозит через Payme или Click; он поступает ресторану.",
        "При отмене не позднее чем за 24 часа до брони депозит возвращается полностью (обычно за 1–3 рабочих дня).",
        "При более поздней отмене или неявке депозит не возвращается. Отмена менее чем за 2 часа считается «поздней».",
        "Регулярные неявки приводят к требованию депозита при следующих бронированиях.",
      ]},
      { title: '5. Бонусы Yums и приглашения', body: [
        "За каждый состоявшийся визит начисляются Yums. 1000 Yums дают скидку 20% на следующую бронь. Yums не обмениваются на деньги.",
        "Если друг, зарегистрировавшийся по вашему коду, завершит первый визит, вы получите бонус. При злоупотреблениях бонусы аннулируются.",
      ]},
      { title: '6. Отзывы', body: [
        "Отзыв пишется только на основе реального визита. Оскорбительные, ложные и рекламные отзывы удаляются.",
      ]},
      { title: '7. Ответственность и изменения', body: [
        "Платформа не отвечает за косвенный ущерб из-за технических сбоев, но старается быстро их устранять.",
        "Условия могут меняться; о существенных изменениях мы сообщим заранее.",
      ]},
    ],
    en: [
      { title: '1. General', body: [
        "{brand} is an online booking platform for restaurants and event venues. By registering or using the service you agree to these terms.",
        "The platform is an intermediary between guests and restaurants: the restaurant is responsible for food, service quality and house rules.",
      ]},
      { title: '2. Account', body: [
        "An account requires your real name and a working email. Booking is available after the email is confirmed.",
        "Do not share your password. You are responsible for actions taken from your account.",
      ]},
      { title: '3. Bookings', body: [
        "Bookings are accepted according to the restaurant's opening hours and availability, up to 90 days ahead.",
        "You can change the date, time and party size in your profile up to 2 hours before the booking.",
        "A restaurant may cancel a booking when necessary — you will be notified and any paid deposit is refunded in full.",
      ]},
      { title: '4. Deposits and cancellation', body: [
        "Some restaurants, and guests with repeated no-shows, pay a deposit via Payme or Click; it goes to the restaurant.",
        "Cancel at least 24 hours before the booking and the deposit is refunded in full (usually within 1–3 business days).",
        "Later cancellations and no-shows forfeit the deposit. Cancelling less than 2 hours before is a \"late cancellation\".",
        "Repeated no-shows lead to deposits being required for future bookings.",
      ]},
      { title: '5. Yums rewards and referrals', body: [
        "Each completed visit earns Yums. 1000 Yums give 20% off your next booking. Yums cannot be exchanged for money.",
        "When a friend who signed up with your code completes their first visit, you get a bonus. Abuse voids the bonuses.",
      ]},
      { title: '6. Reviews', body: [
        "Reviews must be based on a real visit. Abusive, false or promotional reviews are removed.",
      ]},
      { title: '7. Liability and changes', body: [
        "The platform is not liable for indirect damages caused by technical failures, but works to fix them quickly.",
        "These terms may change; we will announce significant changes in advance.",
      ]},
    ],
  },
  privacy: {
    uz: [
      { title: "1. Qanday ma'lumotlar yig'iladi", body: [
        "Hisob: ism, email, parol (shifrlangan holda), tanlangan til. Telefon raqami — ixtiyoriy, faqat restoran siz bilan bog'lanishi uchun.",
        "Bronlar: restoran, sana, vaqt, mehmonlar soni, maxsus istaklar, oldindan buyurtmalar, depozit holati.",
        "Telegram'ni ulasangiz — Telegram chat identifikatori (faqat bildirishnoma yuborish uchun).",
        "To'lov kartasi ma'lumotlari platformada saqlanmaydi: to'lov Payme/Click sahifasida amalga oshiriladi.",
      ]},
      { title: "2. Ma'lumotlardan foydalanish", body: [
        "Bronni bajarish: ismingiz, bron tafsilotlari va (kiritgan bo'lsangiz) telefoningiz faqat siz bron qilgan restoranga ko'rsatiladi.",
        "Bildirishnomalar: email kodlari, bron eslatmalari, holat o'zgarishlari (sayt ichida, ixtiyoriy ravishda Telegram'da).",
        "Xavfsizlik va suiiste'mollarning oldini olish (masalan, kelmaslik tarixi depozit qoidasi uchun).",
      ]},
      { title: "3. Uchinchi shaxslar", body: [
        "Ma'lumotlar sotilmaydi. Ular faqat xizmat uchun zarur bo'lganda uzatiladi: restoranga (bron), to'lov tizimiga (to'lov summasi va buyurtma raqami), email va Telegram xizmatlariga (xabar yuborish).",
      ]},
      { title: "4. Saqlash va himoya", body: [
        "Ma'lumotlar O'zbekiston Respublikasining \"Shaxsga doir ma'lumotlar to'g'risida\"gi Qonuni talablariga muvofiq qayta ishlanadi va himoyalangan serverlarda saqlanadi.",
        "Bron tarixi hisobot va nizolarni hal qilish uchun saqlanadi. Hisobni o'chirishni so'rasangiz, shaxsiy ma'lumotlaringiz o'chiriladi yoki anonimlashtiriladi.",
      ]},
      { title: "5. Huquqlaringiz", body: [
        "Profilingizda ma'lumotlaringizni ko'rish va tahrirlash, Telegram ulanishini istalgan vaqtda uzish mumkin.",
        "Ma'lumotlaringiz nusxasini olish yoki hisobni o'chirish uchun qo'llab-quvvatlash xizmatiga murojaat qiling.",
      ]},
    ],
    ru: [
      { title: '1. Какие данные собираются', body: [
        "Аккаунт: имя, email, пароль (в зашифрованном виде), язык. Телефон — по желанию, только чтобы ресторан мог связаться с вами.",
        "Брони: ресторан, дата, время, число гостей, пожелания, предзаказ, статус депозита.",
        "Если вы подключите Telegram — идентификатор чата (только для уведомлений).",
        "Данные банковских карт на платформе не хранятся: оплата проходит на странице Payme/Click.",
      ]},
      { title: '2. Использование данных', body: [
        "Выполнение брони: имя, детали брони и (если указан) телефон видит только ресторан, в котором вы бронируете.",
        "Уведомления: коды на email, напоминания, изменения статуса (на сайте и, по желанию, в Telegram).",
        "Безопасность и предотвращение злоупотреблений (например, история неявок для правила депозита).",
      ]},
      { title: '3. Третьи лица', body: [
        "Мы не продаём данные. Они передаются только когда это нужно для сервиса: ресторану (бронь), платёжной системе (сумма и номер заказа), сервисам email и Telegram (отправка сообщений).",
      ]},
      { title: '4. Хранение и защита', body: [
        "Данные обрабатываются в соответствии с Законом Республики Узбекистан «О персональных данных» и хранятся на защищённых серверах.",
        "История броней хранится для отчётности и разрешения споров. По запросу на удаление аккаунта персональные данные удаляются или обезличиваются.",
      ]},
      { title: '5. Ваши права', body: [
        "В профиле можно просматривать и изменять свои данные, а также в любой момент отключить Telegram.",
        "Чтобы получить копию данных или удалить аккаунт, обратитесь в поддержку.",
      ]},
    ],
    en: [
      { title: '1. What we collect', body: [
        "Account: name, email, password (stored hashed), language. Phone number is optional, only so the restaurant can contact you.",
        "Bookings: restaurant, date, time, party size, special requests, pre-orders, deposit status.",
        "If you connect Telegram — your Telegram chat ID (for notifications only).",
        "Card details are never stored on the platform: payments happen on the Payme/Click page.",
      ]},
      { title: '2. How we use it', body: [
        "To fulfil bookings: your name, booking details and (if provided) phone are shown only to the restaurant you book.",
        "Notifications: email codes, reminders, status changes (in the app and optionally in Telegram).",
        "Security and abuse prevention (e.g. no-show history for the deposit rule).",
      ]},
      { title: '3. Third parties', body: [
        "We never sell data. It is shared only when the service requires it: with the restaurant (booking), the payment provider (amount and order number), and email/Telegram services (sending messages).",
      ]},
      { title: '4. Storage and protection', body: [
        "Data is processed in line with the Law of the Republic of Uzbekistan \"On Personal Data\" and stored on protected servers.",
        "Booking history is kept for reporting and dispute resolution. If you ask to delete your account, personal data is deleted or anonymised.",
      ]},
      { title: '5. Your rights', body: [
        "You can view and edit your data in your profile, and disconnect Telegram at any time.",
        "To get a copy of your data or delete your account, contact support.",
      ]},
    ],
  },
  faq: {
    uz: [
      { title: "Bron qilish uchun pul to'lash kerakmi?", body: ["Ko'p restoranlarda bron bepul. Depozit faqat restoran talab qilsa yoki oxirgi oylarda bir necha marta kelmagan bo'lsangiz so'raladi."] },
      { title: "Bronni qanday bekor qilaman yoki o'zgartiraman?", body: ["Profil → Bronlarim bo'limida. O'zgartirish bron vaqtidan 2 soat oldingacha mumkin. Bekor qilish istalgan vaqtda mumkin, lekin depozit faqat 24 soatdan oldin bekor qilinsa qaytariladi."] },
      { title: 'Depozit qachon qaytadi?', body: ["Payme/Click orqali to'langan bo'lsa, odatda 1–3 ish kuni ichida kartangizga qaytadi. Restoranda naqd to'langan depozitni restoran qaytaradi. Holatini bron kartasida ko'rasiz."] },
      { title: 'Yums nima?', body: ["Har bir yakunlangan tashrif uchun 100 Yums beriladi. 1000 Yums to'plasangiz, keyingi bronda 20% chegirma olasiz."] },
      { title: "Do'stimni qanday taklif qilaman?", body: ["Profil → Do'stni taklif qilish bo'limidan havolani yuboring. Do'stingiz birinchi tashrifini yakunlagach, sizga bonus Yums tushadi."] },
      { title: "Bildirishnomalarni Telegram'da olsa bo'ladimi?", body: ["Ha. Profil → Sozlamalar → Telegram → \"Ulash\" tugmasini bosing va botda Start'ni bosing."] },
      { title: "Restoranimni platformaga qanday qo'shaman?", body: ["\"Hamkor bo'lish\" sahifasida ariza qoldiring. Tasdiqlangach, boshqaruv paneli ochiladi: bronlar, menyu, stollar, xodimlar va hisobotlar."] },
    ],
    ru: [
      { title: 'Нужно ли платить за бронь?', body: ["В большинстве ресторанов бронь бесплатна. Депозит нужен, только если его требует ресторан или у вас были неявки в последние месяцы."] },
      { title: 'Как отменить или изменить бронь?', body: ["В разделе Профиль → Мои бронирования. Изменить можно не позднее чем за 2 часа. Отменить можно в любое время, но депозит возвращается только при отмене за 24 часа и более."] },
      { title: 'Когда вернётся депозит?', body: ["При оплате через Payme/Click — обычно за 1–3 рабочих дня на вашу карту. Статус виден в карточке брони."] },
      { title: 'Что такое Yums?', body: ["За каждый состоявшийся визит — 100 Yums. Накопив 1000 Yums, вы получите скидку 20% на следующую бронь."] },
      { title: 'Как пригласить друга?', body: ["Отправьте ссылку из раздела Профиль → Пригласить друга. Когда друг завершит первый визит, вы получите бонус."] },
      { title: 'Можно ли получать уведомления в Telegram?', body: ["Да. Профиль → Настройки → Telegram → «Подключить», затем нажмите Start в боте."] },
      { title: 'Как добавить свой ресторан?', body: ["Оставьте заявку на странице «Стать партнёром». После одобрения откроется панель управления: брони, меню, столы, сотрудники и отчёты."] },
    ],
    en: [
      { title: 'Do I have to pay to book?', body: ["Most restaurants are free to book. A deposit is required only if the restaurant asks for one or you have had no-shows recently."] },
      { title: 'How do I cancel or change a booking?', body: ["In Profile → My Bookings. Changes are possible up to 2 hours before. You can cancel at any time, but the deposit is refunded only if you cancel at least 24 hours ahead."] },
      { title: 'When is my deposit refunded?', body: ["For Payme/Click payments, usually within 1–3 business days to your card. The status is shown on the booking card."] },
      { title: 'What are Yums?', body: ["Each completed visit earns 100 Yums. With 1000 Yums you get 20% off your next booking."] },
      { title: 'How do I invite a friend?', body: ["Share the link from Profile → Refer a Friend. When your friend completes their first visit, you get bonus Yums."] },
      { title: 'Can I get notifications in Telegram?', body: ["Yes. Profile → Settings → Telegram → \"Connect\", then press Start in the bot."] },
      { title: 'How do I list my restaurant?', body: ["Apply on the \"Become a partner\" page. Once approved you get a dashboard for bookings, menu, tables, staff and reports."] },
    ],
  },
};

const withBrand = (s: string) => s.split('{brand}').join(BRAND);

const InfoPage = ({ kind }: { kind: InfoKind }) => {
  const { language } = useApp();
  const sections = CONTENT[kind][language] ?? CONTENT[kind].uz;
  const title = t(kind === 'faq' ? 'footer.faq' : `footer.${kind}`, language);

  useEffect(() => {
    document.title = `${title} — ${BRAND}`;
    window.scrollTo(0, 0);
  }, [title]);

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="pt-24 pb-16 container mx-auto px-4 max-w-3xl">
        <h1 className="font-display text-3xl sm:text-4xl font-bold text-foreground mb-2">{title}</h1>
        {kind !== 'faq' && (
          <p className="text-sm text-muted-foreground mb-8">{t('legal.updated', language)}: {UPDATED.split('-').reverse().join('.')}</p>
        )}

        {kind === 'faq' ? (
          <Accordion type="single" collapsible className="mt-6 bg-card rounded-2xl border border-border px-4 sm:px-6">
            {sections.map((s, i) => (
              <AccordionItem key={i} value={String(i)} className={i === sections.length - 1 ? 'border-b-0' : ''}>
                <AccordionTrigger className="text-left text-foreground">{withBrand(s.title)}</AccordionTrigger>
                <AccordionContent className="text-muted-foreground">{s.body.map(withBrand).join(' ')}</AccordionContent>
              </AccordionItem>
            ))}
          </Accordion>
        ) : (
          <div className="space-y-8">
            {sections.map((s, i) => (
              <section key={i} className="space-y-3">
                <h2 className="font-display text-xl font-semibold text-foreground">{withBrand(s.title)}</h2>
                {s.body.map((p, j) => <p key={j} className="text-muted-foreground leading-relaxed">{withBrand(p)}</p>)}
              </section>
            ))}
          </div>
        )}

        {(SUPPORT_EMAIL || kind !== 'faq') && <div className="mt-10 rounded-xl border border-border bg-card p-5 text-sm text-muted-foreground">
          {t('legal.questions', language)}{' '}
          {SUPPORT_EMAIL
            ? <a href={`mailto:${SUPPORT_EMAIL}`} className="text-primary hover:underline">{SUPPORT_EMAIL}</a>
            : <Link to="/faq" className="text-primary hover:underline">{t('footer.faq', language)}</Link>}
        </div>}
      </main>
      <Footer />
    </div>
  );
};

export default InfoPage;
