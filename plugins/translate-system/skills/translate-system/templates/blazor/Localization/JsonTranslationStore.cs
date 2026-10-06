using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace __NAMESPACE__;

/// <summary>
/// Traducciones en archivos JSON planos: <c>Resources/{es|en|ja}.json</c>. La llave es el
/// texto en inglés tal como está en el código y el valor es su traducción.
/// </summary>
/// <remarks>
/// Orden de búsqueda: el idioma pedido, luego <c>en.json</c> y al final la propia llave,
/// así que un texto sin traducir nunca truena: se ve en inglés. Si alguien edita un JSON en
/// el servidor, el cambio se toma solo en un par de segundos, sin reiniciar el sitio.
/// </remarks>
public sealed class JsonTranslationStore
{
    private static readonly TimeSpan IntervaloRevision = TimeSpan.FromSeconds(2);

    private readonly string _resourcesPath;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CacheEntry(DateTime Stamp, DateTime CheckedAt, IReadOnlyDictionary<string, string> Map);

    public JsonTranslationStore(string resourcesPath) => _resourcesPath = resourcesPath;

    public IReadOnlyDictionary<string, string> GetMap(string culture)
    {
        var code = SupportedCultures.Normalize(culture);
        var ahora = DateTime.UtcNow;

        // Revisar la fecha del archivo en cada texto serían cientos de accesos a disco por página.
        if (_cache.TryGetValue(code, out var cached) && ahora - cached.CheckedAt < IntervaloRevision)
            return cached.Map;

        var file = Path.Combine(_resourcesPath, code + ".json");
        var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;

        var map = cached is not null && cached.Stamp == stamp ? cached.Map : Load(file);
        _cache[code] = new CacheEntry(stamp, ahora, map);
        return map;
    }

    private static IReadOnlyDictionary<string, string> Load(string file)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(file))
            return result;

        try
        {
            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String && prop.Value.GetString() is { Length: > 0 } valor)
                    result[prop.Name] = valor;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Un JSON mal editado no debe tirar el sistema: se ve en inglés hasta que se corrija.
        }

        return result;
    }

    public bool TryGet(string culture, string key, out string value)
    {
        if (GetMap(culture).TryGetValue(key, out var found) || GetMap("en").TryGetValue(key, out found))
        {
            value = found;
            return true;
        }

        value = key;
        return false;
    }

    public string Get(string culture, string key) => TryGet(culture, key, out var value) ? value : key;

    /// <summary>Todos los textos de un idioma, ya con el respaldo en inglés aplicado.</summary>
    public IReadOnlyDictionary<string, string> Merged(string culture)
    {
        var merged = new Dictionary<string, string>(GetMap("en"), StringComparer.Ordinal);
        foreach (var (k, v) in GetMap(culture))
            merged[k] = v;
        return merged;
    }
}

/// <summary>
/// Acceso a las traducciones desde código que no puede inyectar <c>IStringLocalizer</c>:
/// clases estáticas de etiquetas, servicios que arman mensajes para la pantalla, etc.
/// Usa el idioma de la petición o del circuito en curso.
/// </summary>
public static class Loc
{
    internal static JsonTranslationStore? Store { get; set; }

    public static string T(string key) =>
        Store?.Get(CultureInfo.CurrentUICulture.Name, key) ?? key;

    public static string T(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>Diccionario del idioma actual en JSON, para los textos de JavaScript (<c>moT</c>).</summary>
    public static string ClientJson() =>
        JsonSerializer.Serialize(Store?.Merged(CultureInfo.CurrentUICulture.Name) ?? new Dictionary<string, string>());
}
