using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Restaurants;

public class RestaurantDto
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CuisineType { get; set; } = string.Empty;
    public string PriceRange { get; set; } = "$$";
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Phone { get; set; }
    public string OpeningHours { get; set; } = "{}";
    public int TotalSlots { get; set; }
    public List<string> ImagesUrl { get; set; } = new();
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public int? Discount { get; set; }
    public TimeOnly? DiscountTimeStart { get; set; }
    public TimeOnly? DiscountTimeEnd { get; set; }
    public bool IsApproved { get; set; }
    public bool IsSuspended { get; set; }
    public string? RejectionReason { get; set; }
    public List<string> Amenities { get; set; } = new();
    public List<string> Policies { get; set; } = new();
    public bool DepositRequired { get; set; }
    public decimal DepositPerGuest { get; set; }
    public decimal FoodRating { get; set; }
    public decimal ServiceRating { get; set; }
    public decimal InteriorRating { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateRestaurantRequest
{
    [Required, MaxLength(255)] public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CuisineType { get; set; } = string.Empty;
    public string PriceRange { get; set; } = "$$";
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Phone { get; set; }
    public int TotalSlots { get; set; } = 20;
    public List<string> ImagesUrl { get; set; } = new();
    public int? Discount { get; set; }
    public string OpeningHours { get; set; } = "{}";
    public List<string> Amenities { get; set; } = new();
    public List<string> Policies { get; set; } = new();
    public bool DepositRequired { get; set; } = false;
    public decimal DepositPerGuest { get; set; } = 0;
}

public class UpdateRestaurantRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? CuisineType { get; set; }
    public string? PriceRange { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Phone { get; set; }
    public int? TotalSlots { get; set; }
    public List<string>? ImagesUrl { get; set; }
    public int? Discount { get; set; }
    public TimeOnly? DiscountTimeStart { get; set; }
    public TimeOnly? DiscountTimeEnd { get; set; }
    public string? OpeningHours { get; set; }
    public List<string>? Amenities { get; set; }
    public List<string>? Policies { get; set; }
    public bool? DepositRequired { get; set; }
    public decimal? DepositPerGuest { get; set; }
}
