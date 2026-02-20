using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Serializes double/double? so that NaN and Infinity are written as JSON null,
/// avoiding "cannot be written as valid JSON" errors when report calculations divide by zero.
/// </summary>
public sealed class JsonDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return 0;
        return reader.GetDouble();
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            writer.WriteNumberValue(0); // double no nullable: evitar null, usar 0
        else
            writer.WriteNumberValue(value);
    }
}

/// <summary>
/// Same as JsonDoubleConverter but for nullable double.
/// </summary>
public sealed class JsonNullableDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        var v = reader.GetDouble();
        return double.IsNaN(v) || double.IsInfinity(v) ? null : v;
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }
        var v = value.Value;
        if (double.IsNaN(v) || double.IsInfinity(v))
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(v);
    }
}
