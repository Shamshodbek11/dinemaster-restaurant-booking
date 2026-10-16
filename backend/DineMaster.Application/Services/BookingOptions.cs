namespace DineMaster.Application.Services;

/// <summary>appsettings.json → "Booking" bo'limi.</summary>
public class BookingOptions
{
    /// <summary>Bron qilish uchun email tasdiqlangan bo'lishi shart.</summary>
    public bool RequireConfirmedEmail { get; set; } = true;

    /// <summary>Oxirgi bron restoran yopilishidan kamida shuncha daqiqa oldin boshlanadi.</summary>
    public int LastBookingBeforeCloseMinutes { get; set; } = 60;

    /// <summary>Bron vaqtlari qadami (frontendda ko'rsatiladigan slotlar).</summary>
    public int SlotStepMinutes { get; set; } = 30;

    /// <summary>Oxirgi <see cref="NoShowLookbackDays"/> kunda shuncha marta kelmagan mijozdan depozit olinadi.</summary>
    public int NoShowThreshold { get; set; } = 2;
    public int NoShowLookbackDays { get; set; } = 180;

    /// <summary>Kelmaslik tarixi bor mijoz uchun minimal depozit (har bir mehmon, so'm).</summary>
    public decimal NoShowDepositPerGuest { get; set; } = 50_000;

    /// <summary>Depozit onlayn to'lanmasa, bron shuncha daqiqadan keyin bekor qilinadi (0 — o'chirilgan).
    /// Faqat onlayn to'lov yoqilgan bo'lsa ishlaydi.</summary>
    public int DepositHoldMinutes { get; set; } = 30;

    /// <summary>Bron boshlanishidan shuncha soat oldin bekor qilinsa — depozit to'liq qaytariladi.</summary>
    public int FreeCancellationHours { get; set; } = 24;

    /// <summary>Shundan kech bekor qilish "kelmaslik"ka tenglashtiriladi va bronni o'zgartirib bo'lmaydi.</summary>
    public int LateCancellationMinutes { get; set; } = 120;

    /// <summary>Restoran + yums chegirmasi jami ko'pi bilan.</summary>
    public int MaxTotalDiscountPercent { get; set; } = 50;

    /// <summary>Taklif qilingan do'st birinchi tashrifini yakunlasa — taklif qilganga.</summary>
    public int ReferralBonus { get; set; } = 500;

    /// <summary>Ko'pi bilan necha kun oldindan bron qilish mumkin (0 — cheklanmagan).</summary>
    public int MaxDaysAhead { get; set; } = 90;

    /// <summary>Emaildagi havolalar uchun frontend manzili.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:8080";
}
