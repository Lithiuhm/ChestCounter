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

    // Nombres guardados en francés (data.json) -> nombre mostrado.
    private static readonly Dictionary<string, (string Es, string En)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Coffre"] = ("Cofre", "Chest"),
        ["Coffre rare"] = ("Cofre raro", "Rare chest"),
        ["Coffre enterré"] = ("Cofre enterrado", "Buried chest"),
        ["Coffre d'extraction / reliques"] = ("Cofre de extracción / reliquias", "Extraction / relic chest"),
        ["Coffre des affiches d'esprit"] = ("Cofre de los carteles de espíritu", "Spirit poster chest"),
        ["Autre coffre"] = ("Otro cofre", "Other chest"),
        ["Canon à butin"] = ("Cañón de botín", "Loot cannon"),
        ["Caisse de munitions"] = ("Caja de munición", "Ammo box"),
        ["Caisse de nourriture"] = ("Caja de comida", "Food box"),
        ["Autre conteneur"] = ("Otro contenedor", "Other container"),
        ["Trésor"] = ("Tesoro", "Treasure"),
        ["Trésor (type inconnu)"] = ("Tesoro (tipo desconocido)", "Treasure (unknown type)"),
        ["Gizmo de trésor"] = ("Artilugio de tesoro", "Treasure gizmo"),
        ["Feu d'artifice doré"] = ("Fuego artificial dorado", "Golden firework"),
        ["Extracteur portable"] = ("Extractor portátil", "Portable extractor"),
        ["Localisateur d'esprit"] = ("Localizador de espíritu", "Spirit locator"),
        ["Localisateur chanceux"] = ("Localizador afortunado", "Lucky locator"),
        ["Accélérateur d'extraction"] = ("Acelerador de extracción", "Extraction accelerator"),
        ["inconnu"] = ("desconocido", "unknown"),
    };

    /// <summary>Nombre mostrado de un tipo de cofre u objeto guardado en francés.</summary>
    public static string Name(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored ?? "";
        }
        return Names.TryGetValue(stored, out var pair) ? (En ? pair.En : pair.Es) : stored;
    }
}
