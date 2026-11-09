using System.Globalization;

namespace ChestCounter;

/// <summary>
/// Idioma de la aplicación: español si Windows está en español, inglés en cualquier otro caso.
/// Los textos se escriben en línea: <c>L.T("español", "English")</c>. Los nombres guardados en data.json (tipos de
/// cofre, objetos del tesoro) siguen en francés por compatibilidad y se traducen al mostrarlos (<see cref="Name"/>).
/// </summary>
public static class L
{
    /// <summary>True cuando la interfaz va en inglés (Windows no está en español).</summary>
    /// <remarks>
    /// Para probar: la variable CHESTCOUNTER_LANG (en o es) fuerza el idioma. Sin ella, manda Windows.
    /// </remarks>
    public static bool En { get; } = Environment.GetEnvironmentVariable("CHESTCOUNTER_LANG") is { Length: > 0 } forced
        ? !forced.StartsWith("es", StringComparison.OrdinalIgnoreCase)
        : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "es";

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo(En ? "en-US" : "es-ES");

    /// <summary>Código del idioma para los atributos lang de las páginas HTML.</summary>
    public static string Code => En ? "en" : "es";

    public static string T(string es, string en) => En ? en : es;

    /// <summary>Entero sin separador de miles ("1409"): las cifras son cantidades, no importes.</summary>
    public static string N(int n) => n.ToString("0", Culture);

    /// <summary>"3 cofres" / "1 chest" (plural según la cifra, en el idioma actual).</summary>
    public static string Count(int n, string esOne, string esMany, string enOne, string enMany) =>
        $"{N(n)} {(n == 1 ? T(esOne, enOne) : T(esMany, enMany))}";

    /// <summary>"12.º" / "12th".</summary>
    public static string Ordinal(int n)
    {
        if (!En)
        {
            return $"{n}.º";
        }
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    /// <summary>Fecha corta + hora de una partida ("05/11 22:54" / "Nov 5 22:54").</summary>
    public static string DateTimeShort(DateTime d) => d.ToString(En ? "MMM d HH:mm" : "dd/MM HH:mm", Culture);

    // Nombres internos guardados en data.json (en inglés) -> nombre mostrado.
    private static readonly Dictionary<string, (string Es, string En)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Chest"] = ("Cofre", "Chest"),
        ["Rare chest"] = ("Cofre raro", "Rare chest"),
        ["Buried chest"] = ("Cofre enterrado", "Buried chest"),
        ["Extraction / relic chest"] = ("Cofre de extracción / reliquias", "Extraction / relic chest"),
        ["Spirit poster chest"] = ("Cofre de los carteles de espíritu", "Spirit poster chest"),
        ["Other chest"] = ("Otro cofre", "Other chest"),
        ["Loot cannon"] = ("Cañón de botín", "Loot cannon"),
        ["Ammo box"] = ("Caja de munición", "Ammo box"),
        ["Food box"] = ("Caja de comida", "Food box"),
        ["Other container"] = ("Otro contenedor", "Other container"),
        ["Treasure"] = ("Tesoro", "Treasure"),
        ["Treasure (unknown type)"] = ("Tesoro (tipo desconocido)", "Treasure (unknown type)"),
        ["Treasure gizmo"] = ("Artilugio de tesoro", "Treasure gizmo"),
        ["Golden firework"] = ("Fuego artificial dorado", "Golden firework"),
        ["Portable extractor"] = ("Extractor portátil", "Portable extractor"),
        ["Spirit locator"] = ("Localizador de espíritu", "Spirit locator"),
        ["Lucky locator"] = ("Localizador afortunado", "Lucky locator"),
        ["Extraction accelerator"] = ("Acelerador de extracción", "Extraction accelerator"),
        ["unknown"] = ("desconocido", "unknown"),
    };

    /// <summary>
    /// Nombres internos de versiones anteriores a la 2.0.9 (estaban en francés) -> nombre interno actual.
    /// Solo se usa para convertir el historial antiguo al abrirlo.
    /// </summary>
    private static readonly Dictionary<string, string> Legacy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Coffre"] = "Chest",
        ["Coffre rare"] = "Rare chest",
        ["Coffre enterr\u00e9"] = "Buried chest",
        ["Coffre d'extraction / reliques"] = "Extraction / relic chest",
        ["Coffre des affiches d'esprit"] = "Spirit poster chest",
        ["Autre coffre"] = "Other chest",
        ["Canon \u00e0 butin"] = "Loot cannon",
        ["Caisse de munitions"] = "Ammo box",
        ["Caisse de nourriture"] = "Food box",
        ["Autre conteneur"] = "Other container",
        ["Tr\u00e9sor"] = "Treasure",
        ["Tr\u00e9sor (type inconnu)"] = "Treasure (unknown type)",
        ["Gizmo de tr\u00e9sor"] = "Treasure gizmo",
        ["Feu d'artifice dor\u00e9"] = "Golden firework",
        ["Extracteur portable"] = "Portable extractor",
        ["Localisateur d'esprit"] = "Spirit locator",
        ["Localisateur chanceux"] = "Lucky locator",
        ["Acc\u00e9l\u00e9rateur d'extraction"] = "Extraction accelerator",
        ["inconnu"] = "unknown",
    };

    /// <summary>Nombre interno actual de un nombre guardado por una versión anterior (o el mismo si no cambió).</summary>
    public static string Upgrade(string stored) => Legacy.TryGetValue(stored, out var current) ? current : stored;

    /// <summary>Nombre mostrado de un tipo de cofre u objeto guardado en data.json.</summary>
    public static string Name(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored ?? "";
        }
        return Names.TryGetValue(Upgrade(stored), out var pair) ? (En ? pair.En : pair.Es) : stored;
    }
}
