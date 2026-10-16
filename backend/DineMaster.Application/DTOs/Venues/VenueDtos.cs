using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Venues;

public class VenueDto
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VenueType { get; set; } = "To'yxona";
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = "Toshkent";
    public string? Phone { get; set; }
    public int CapacityMin { get; set; }
    public int CapacityMax { get; set; }
    public int PricePerHead { get; set; }
    public List<string> ImagesUrl { get; set; } = new();
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsApproved { get; set; }
    public bool IsSuspended { get; set; }
    public string? RejectionReason { get; set; }
    public string OpeningHours { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateVenueRequest
{
    [Required, MaxLength(255)] public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VenueType { get; set; } = "To'yxona";
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = "Toshkent";
    public string? Phone { get; set; }
    public int CapacityMin { get; set; } = 50;
    public int CapacityMax { get; set; } = 1000;
    public int PricePerHead { get; set; } = 0;
    public List<string> ImagesUrl { get; set; } = new();
    public string OpeningHours { get; set; } = "{}";
}

public class UpdateVenueRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? VenueType { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public int? CapacityMin { get; set; }
    public int? CapacityMax { get; set; }
    public int? PricePerHead { get; set; }
    public List<string>? ImagesUrl { get; set; }
    public string? OpeningHours { get; set; }
}

public class VenueReservationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid VenueId { get; set; }
    public string? VenueName { get; set; }
    public DateOnly EventDate { get; set; }
    public string EventType { get; set; } = "To'y";
    public int GuestsCount { get; set; }
    public string? SpecialRequests { get; set; }
    public int TotalPrice { get; set; }
    public string Status { get; set; } = "pending";
    public int YumsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateVenueReservationRequest
{
    [Required] public Guid VenueId { get; set; }
    [Required] public DateOnly EventDate { get; set; }
    public string EventType { get; set; } = "To'y";
    [Range(1, 10000)] public int GuestsCount { get; set; } = 100;
    public string? SpecialRequests { get; set; }
}
