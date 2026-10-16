using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Waitlist;

public class WaitlistDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserPhone { get; set; }
    public Guid RestaurantId { get; set; }
    public string? RestaurantName { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly? Time { get; set; }
    public int GuestsCount { get; set; }
    public string Status { get; set; } = "waiting";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateWaitlistRequest
{
    [Required] public Guid RestaurantId { get; set; }
    [Required] public DateOnly Date { get; set; }
    public TimeOnly? Time { get; set; }
    [Range(1, 50)] public int GuestsCount { get; set; } = 2;
    public string? Note { get; set; }
}

public class UpdateWaitlistStatusRequest
{
    [Required][RegularExpression("^(waiting|notified|seated|cancelled)$")]
    public string Status { get; set; } = "waiting";
}
