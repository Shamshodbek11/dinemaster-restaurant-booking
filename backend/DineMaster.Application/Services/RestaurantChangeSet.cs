using System.Text.Json;
using System.Text.Json.Serialization;
using DineMaster.Domain.Entities;

namespace DineMaster.Application.Services;

/// <summary>
/// Restoranning moderatsiya talab qiladigan maydonlari (nomi, tavsif, manzil, rasmlar...).
/// Tasdiqlangan restoran egasi ularni o'zgartirsa, o'zgarish shu ko'rinishda saqlanadi va
/// superadmin tasdiqlagach qo'llanadi. Qolgan sozlamalar (ish vaqti, slotlar, depozit) darhol qo'llanadi.
/// </summary>
public class RestaurantChangeSet
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public string? PriceRange { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public List<string>? ImagesUrl { get; set; }

    [JsonIgnore]
    public bool IsEmpty =>
        Name is null && Description is null && CuisineType is null && PriceRange is null &&
        Address is null && City is null && Latitude is null && Longitude is null && ImagesUrl is null;

    /// <summary>Faqat haqiqatda o'zgargan maydonlarni qoldiradi.</summary>
    public RestaurantChangeSet OnlyDifferentFrom(Restaurant r) => new()
    {
        Name = Name is not null && Name.Trim() != r.Name ? Name.Trim() : null,
        Description = Description is not null && Description != r.Description ? Description : null,
        CuisineType = CuisineType is not null && CuisineType != r.CuisineType ? CuisineType : null,
        PriceRange = PriceRange is not null && PriceRange != r.PriceRange ? PriceRange : null,
        Address = Address is not null && Address != r.Address ? Address : null,
        City = City is not null && City != r.City ? City : null,
        Latitude = Latitude is not null && Latitude != r.Latitude ? Latitude : null,
        Longitude = Longitude is not null && Longitude != r.Longitude ? Longitude : null,
        ImagesUrl = ImagesUrl is not null && !ImagesUrl.SequenceEqual(r.ImagesUrl) ? ImagesUrl : null,
    };

    /// <summary>Yangi o'zgarishlarni avvalgi kutilayotganlarning ustiga yozadi.</summary>
    public RestaurantChangeSet MergeOnto(RestaurantChangeSet? older) => new()
    {
        Name = Name ?? older?.Name,
        Description = Description ?? older?.Description,
        CuisineType = CuisineType ?? older?.CuisineType,
        PriceRange = PriceRange ?? older?.PriceRange,
        Address = Address ?? older?.Address,
        City = City ?? older?.City,
        Latitude = Latitude ?? older?.Latitude,
        Longitude = Longitude ?? older?.Longitude,
        ImagesUrl = ImagesUrl ?? older?.ImagesUrl,
    };

    public void ApplyTo(Restaurant r)
    {
        if (Name is not null) r.Name = Name;
        if (Description is not null) r.Description = Description;
        if (CuisineType is not null) r.CuisineType = CuisineType;
        if (PriceRange is not null) r.PriceRange = PriceRange;
        if (Address is not null) r.Address = Address;
        if (City is not null) r.City = City;
        if (Latitude is not null) r.Latitude = Latitude;
        if (Longitude is not null) r.Longitude = Longitude;
        if (ImagesUrl is not null) r.ImagesUrl = ImagesUrl;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static RestaurantChangeSet? FromJson(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<RestaurantChangeSet>(json, Json);
}
