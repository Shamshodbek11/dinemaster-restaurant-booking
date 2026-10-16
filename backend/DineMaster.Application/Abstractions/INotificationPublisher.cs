namespace DineMaster.Application.Abstractions;

public record NotificationMessage(Guid UserId, string Title, string Body, string? Link);

/// <summary>
/// Bazaga saqlangan bildirishnomalarni tashqi kanallarga (Telegram) uzatish.
/// AppDbContext har bir muvaffaqiyatli SaveChanges'dan keyin yangi bildirishnomalarni shu yerga yuboradi.
/// </summary>
public interface INotificationPublisher
{
    void Publish(IReadOnlyCollection<NotificationMessage> messages);
}
