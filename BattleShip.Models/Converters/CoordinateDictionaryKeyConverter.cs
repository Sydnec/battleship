using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Entities;

namespace BattleShip.Models.Converters;

public class CoordinateDictionaryKeyConverter : JsonConverter<Coordinate>
{
    public override Coordinate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // This method is for reading the value, not the key. 
        // But since we are using this as a key converter, we might need to handle ReadAsPropertyName.
        // However, for a general converter, we should implement Read/Write as well if it's used as a value.
        // For dictionary keys, ReadAsPropertyName is used.
        
        // If used as a value (not key), it's a JSON object usually.
        // But here we are focusing on Dictionary Key.
        
        // Let's implement standard object reading for value usage
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        int row = 0;
        int col = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new Coordinate(row, col);
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string? propertyName = reader.GetString();
                reader.Read();
                switch (propertyName?.ToLower())
                {
                    case "row":
                        row = reader.GetInt32();
                        break;
                    case "column":
                    case "col":
                        col = reader.GetInt32();
                        break;
                }
            }
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, Coordinate value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("row", value.Row);
        writer.WriteNumber("column", value.Column);
        writer.WriteEndObject();
    }

    public override Coordinate ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        if (string.IsNullOrEmpty(s)) throw new JsonException();

        var parts = s.Split(',');
        if (parts.Length != 2) throw new JsonException();

        if (int.TryParse(parts[0], out int row) && int.TryParse(parts[1], out int col))
        {
            return new Coordinate(row, col);
        }

        throw new JsonException();
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Coordinate value, JsonSerializerOptions options)
    {
        writer.WritePropertyName($"{value.Row},{value.Column}");
    }
}
