using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Localization;

namespace __NAMESPACE__;

/// <summary>Marcador del recurso único del sistema: <c>IStringLocalizer&lt;SharedResource&gt;</c>.</summary>
public sealed class SharedResource;

/// <summary><c>IStringLocalizer</c> sobre <see cref="JsonTranslationStore"/>, en el idioma de la petición o del circuito.</summary>
public sealed class JsonStringLocalizer(JsonTranslationStore store) : IStringLocalizer
{
    private static string Culture => CultureInfo.CurrentUICulture.Name;

    public LocalizedString this[string name]
    {
        get
        {
            var found = store.TryGet(Culture, name, out var value);
            return new LocalizedString(name, value, resourceNotFound: !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var found = store.TryGet(Culture, name, out var value);
            var texto = arguments is { Length: > 0 } ? string.Format(CultureInfo.CurrentCulture, value, arguments) : value;
            return new LocalizedString(name, texto, resourceNotFound: !found);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        store.Merged(Culture).Select(kv => new LocalizedString(kv.Key, kv.Value, resourceNotFound: false));
}

/// <summary>Todas las clases comparten el mismo diccionario, sin importar el tipo que se pida.</summary>
public sealed class JsonStringLocalizerFactory(JsonTranslationStore store) : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource) => new JsonStringLocalizer(store);

    public IStringLocalizer Create(string baseName, string location) => new JsonStringLocalizer(store);
}
