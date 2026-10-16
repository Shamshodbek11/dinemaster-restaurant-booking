using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Menu;

public class MenuCategoryDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<MenuItemDto> Items { get; set; } = new();
}

public class MenuItemDto
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsPopular { get; set; }
    public int SortOrder { get; set; }
}

public class CreateMenuCategoryRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}

public class UpdateMenuCategoryRequest
{
    public string? Name { get; set; }
    public int? SortOrder { get; set; }
}

public class CreateMenuItemRequest
{
    [Required] public Guid CategoryId { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Range(0, 1_000_000_000)] public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPopular { get; set; } = false;
    public int SortOrder { get; set; } = 0;
}

public class UpdateMenuItemRequest
{
    public Guid? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool? IsAvailable { get; set; }
    public bool? IsPopular { get; set; }
    public int? SortOrder { get; set; }
}
