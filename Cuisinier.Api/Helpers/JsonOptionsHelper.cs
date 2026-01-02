using System.Text.Json;
using Cuisinier.Api.JsonConverters;

namespace Cuisinier.Api.Helpers;

public static class JsonOptionsHelper
{
    /// <summary>
    /// Gets the default JSON serializer options with TimeSpan converters.
    /// Use this for serialization and deserialization of DTOs.
    /// </summary>
    public static JsonSerializerOptions GetDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        
        options.Converters.Add(new TimeSpanConverter());
        options.Converters.Add(new NullableTimeSpanConverter());
        
        return options;
    }

    /// <summary>
    /// Gets JSON options for serialization only (without PropertyNameCaseInsensitive).
    /// </summary>
    public static JsonSerializerOptions GetSerializationOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        options.Converters.Add(new TimeSpanConverter());
        options.Converters.Add(new NullableTimeSpanConverter());
        
        return options;
    }
}

