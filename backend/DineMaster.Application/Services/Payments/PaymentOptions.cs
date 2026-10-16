namespace DineMaster.Application.Services.Payments;

/// <summary>appsettings.json → "Payments" bo'limi.</summary>
public class PaymentOptions
{
    /// <summary>Lokal sinov uchun "test" provayder (pul yechilmaydi). Production'da o'chiq bo'lsin.</summary>
    public bool EnableTestProvider { get; set; }

    /// <summary>To'lovdan keyin foydalanuvchi qaytadigan sahifa.</summary>
    public string ReturnUrl { get; set; } = "http://localhost:8080/profile?payment=return";

    public PaymeOptions Payme { get; set; } = new();
    public ClickOptions Click { get; set; } = new();

    public bool AnyEnabled => EnableTestProvider || Payme.Enabled || Click.Enabled;

    public IReadOnlyList<string> EnabledProviders =>
        new[] { (Payme.Enabled, "payme"), (Click.Enabled, "click"), (EnableTestProvider, "test") }
            .Where(p => p.Item1).Select(p => p.Item2).ToList();
}

public class PaymeOptions
{
    public string MerchantId { get; set; } = string.Empty;
    /// <summary>Kassa kaliti (test yoki prod). Webhook so'rovlari "Paycom:{Key}" Basic auth bilan keladi.</summary>
    public string Key { get; set; } = string.Empty;
    public bool TestMode { get; set; } = true;
    /// <summary>Kassa sozlamalaridagi hisob maydoni nomi (account.{field}).</summary>
    public string AccountField { get; set; } = "order_id";

    public bool Enabled => !string.IsNullOrWhiteSpace(MerchantId) && !string.IsNullOrWhiteSpace(Key);
    public string CheckoutBaseUrl => TestMode ? "https://test.paycom.uz" : "https://checkout.paycom.uz";
}

public class ClickOptions
{
    public string ServiceId { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    public bool Enabled =>
        !string.IsNullOrWhiteSpace(ServiceId) && !string.IsNullOrWhiteSpace(MerchantId) && !string.IsNullOrWhiteSpace(SecretKey);
}
