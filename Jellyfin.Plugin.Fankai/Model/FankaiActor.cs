using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fankai.Model;

/// <summary>
/// Représente les données d'un acteur provenant de l'API Fankai.
/// </summary>
public class FankaiActor
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("thumb_url")]
    public string? ThumbUrl { get; set; } 

    [JsonPropertyName("profile_url")]
    public string? ProfileUrl { get; set; } 

    /// <summary>
    /// ID TMDB brut provenant du JSON, peut être un nombre, une chaîne ou null.
    /// Cette propriété est destinée uniquement à la désérialisation (ne marche pas autrement).
    /// </summary>
    [JsonPropertyName("tmdb_id")]
    public object? TmdbIdRaw { get; set; }

    /// <summary>
    /// Obtient l'ID TMDB sous forme de chaîne, en normalisant à partir de divers types JSON possibles.
    /// Renvoie null si l'ID TMDB est manquant, "NULL" ou une chaîne vide.
    /// </summary>
    [JsonIgnore]
    public string? TmdbId
    {
        get
        {
            if (TmdbIdRaw == null) return null;

            if (TmdbIdRaw is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        string? sValue = element.GetString();
                        return string.IsNullOrEmpty(sValue) || "NULL".Equals(sValue, StringComparison.OrdinalIgnoreCase) 
                               ? null 
                               : sValue;
                    case JsonValueKind.Number:
                        return element.ToString();
                    case JsonValueKind.Null:
                        return null;
                    default:
                        string? rawString = element.ToString();
                        return string.IsNullOrEmpty(rawString) || "NULL".Equals(rawString, StringComparison.OrdinalIgnoreCase)
                               ? null
                               : rawString;
                }
            }
            
            if (TmdbIdRaw is string strValue)
            {
                 return string.IsNullOrEmpty(strValue) || "NULL".Equals(strValue, StringComparison.OrdinalIgnoreCase) 
                               ? null 
                               : strValue;
            }
            if (TmdbIdRaw is long longValue) return longValue.ToString(CultureInfo.InvariantCulture);
            if (TmdbIdRaw is int intValue) return intValue.ToString(CultureInfo.InvariantCulture);
            if (TmdbIdRaw is double doubleValue) return doubleValue.ToString(CultureInfo.InvariantCulture);
            if (TmdbIdRaw is decimal decimalValue) return decimalValue.ToString(CultureInfo.InvariantCulture);

            string? fallbackString = TmdbIdRaw.ToString();
            return string.IsNullOrEmpty(fallbackString) || "NULL".Equals(fallbackString, StringComparison.OrdinalIgnoreCase)
                   ? null
                   : fallbackString;
        }
    }

    [JsonPropertyName("total_appearances")] 
    public int? TotalAppearances { get; set; }
}
