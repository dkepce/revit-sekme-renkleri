using System.Windows.Media;

namespace RevitSekmeRenkleri;

/// <summary>
/// Farben der Dokument-Tabs, gelesen aus den Einstellungen. Projekte nutzen die
/// Palette vorwaerts, Familien rueckwaerts - damit Projekt und Familie mit
/// gleichem Index nicht dieselbe Farbe tragen.
/// </summary>
public static class TabColorPalette
{
    /// <summary>Grundfarbe fuer Projekte.</summary>
    public static Color ProjektFarbe(int index)
        => Lesen(index % TabColorSettings.PaletteGroesse);

    /// <summary>Grundfarbe fuer Familien (Palette rueckwaerts).</summary>
    public static Color FamilienFarbe(int index)
        => Lesen((TabColorSettings.PaletteGroesse - 1) - (index % TabColorSettings.PaletteGroesse));

    // Pinsel gleicher Farbe sind derselbe Pinsel. Ein neuer Pinsel gilt fuer
    // WPF als Aenderung - ein Takt alle 500 ms haette die Tabs jedes Mal
    // neu zeichnen lassen, auch wenn sich nichts geaendert hat.
    private static readonly Dictionary<uint, SolidColorBrush> _pinsel = new();

    /// <summary>Eingefrorener Pinsel in der Farbe mit der Deckkraft 0-100 %.</summary>
    public static SolidColorBrush Pinsel(Color farbe, int prozent)
    {
        byte alpha = (byte)Math.Round(Math.Clamp(prozent, 0, 100) * 255 / 100.0);
        uint schluessel = ((uint)alpha << 24) | ((uint)farbe.R << 16) | ((uint)farbe.G << 8) | farbe.B;

        if (_pinsel.TryGetValue(schluessel, out var vorhanden)) return vorhanden;

        var pinsel = new SolidColorBrush(Color.FromArgb(alpha, farbe.R, farbe.G, farbe.B));
        pinsel.Freeze();
        _pinsel[schluessel] = pinsel;
        return pinsel;
    }

    /// <summary>Liest "#RRGGBB" (auch ohne Raute); false bei ungueltiger Eingabe.</summary>
    public static bool TryParse(string hex, out Color farbe)
    {
        farbe = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        string h = hex.Trim().TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out int wert))
            return false;

        farbe = Color.FromRgb((byte)(wert >> 16), (byte)(wert >> 8), (byte)wert);
        return true;
    }

    private static Color Lesen(int slot)
    {
        var palette = TabColorSettings.Aktuell.Palette;
        return TryParse(palette[slot], out var farbe)
            ? farbe
            : (TryParse(TabColorSettings.StandardPalette[slot], out var standard) ? standard : Colors.Gray);
    }
}
