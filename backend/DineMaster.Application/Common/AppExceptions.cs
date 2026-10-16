namespace DineMaster.Application.Common;

/// <summary>
/// Biznes qoidasi buzilganda (foydalanuvchiga ko'rsatiladigan xabar bilan) → HTTP 400.
/// Faqat shu turdagi xatolarning matni klientga yuboriladi; .NET/EF ichki
/// xatolari (InvalidOperationException va h.k.) 500 sifatida yashiriladi.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>Resurs topilmadi → HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Foydalanuvchi tizimga kirgan, lekin bu amalga huquqi yo'q → HTTP 403.</summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "Bu amal uchun ruxsat yo'q.") : base(message) { }
}

/// <summary>Holat to'qnashuvi (masalan, sana band) → HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Tashqi xizmat (email, to'lov) sozlanmagan yoki ishlamayapti → HTTP 503.</summary>
public class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
