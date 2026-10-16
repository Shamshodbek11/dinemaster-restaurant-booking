using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DineMaster.Api.Extensions;

/// <summary>
/// TimeOnly uchun "HH:mm" va "HH:mm:ss" ikkalasini qabul qiladi (standart konverter faqat
/// "HH:mm:ss" ni qabul qiladi, frontend esa ko'pincha "19:00" yuboradi). Yozishda "HH:mm:ss".
/// </summary>
public sealed class LenientTimeOnlyConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = { "HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF", "H:mm" };

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (TimeOnly.TryParseExact(s, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return t;
        throw new JsonException($"Vaqt noto'g'ri formatda: \"{s}\" (kutilgan: HH:mm).");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
}
