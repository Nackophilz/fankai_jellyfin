using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fankai.Utils;

/// <summary>
/// Convertit un ID TMDB depuis JSON, qui peut être un nombre ou une chaîne, en chaîne de caractères.
/// Gère les valeurs null, les chaînes vides, les nombres et les chaînes.
/// </summary>
public class TmdbIdJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out int intValue))
            {
                return intValue.ToString();
            }
            if (reader.TryGetInt64(out long longValue))
            {
                return longValue.ToString();
            }
            // Fallback pour d'autres types de nombres si nécessaire (oui c'est aussi arrivé)
            double doubleValue = reader.GetDouble();
            return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // Ne devrait pas arriver pour un ID TMDB valide qui est soit une chaîne, un nombre ou null
        throw new JsonException($"Type de jeton inattendu {reader.TokenType} lors de l'analyse de TmdbId.");
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            // Essaye d'écrire comme un nombre si c'est parsable, sinon comme une chaîne.
            writer.WriteStringValue(value);
        }
    }
}
