namespace DineMaster.Application.Abstractions;

public record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

public interface IEmailService
{
    /// <summary>Xat yuboradi. Email provayder sozlanmagan bo'lsa ServiceUnavailableException.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
