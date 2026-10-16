using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Tables;

public class RestaurantTableDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Location { get; set; } = "hall";
    public bool IsActive { get; set; }
}

public class CreateTableRequest
{
    [Required, MaxLength(50)] public string Label { get; set; } = string.Empty;
    [Range(1, 30)] public int Capacity { get; set; } = 2;
    [MaxLength(30)] public string Location { get; set; } = "hall";
}

public class UpdateTableRequest
{
    public string? Label { get; set; }
    public int? Capacity { get; set; }
    public string? Location { get; set; }
    public bool? IsActive { get; set; }
}

public class TableAvailabilityDto
{
    public Guid TableId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Location { get; set; } = "hall";
    public bool IsAvailable { get; set; }
}
