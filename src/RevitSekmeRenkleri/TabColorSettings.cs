using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitSekmeRenkleri;

/// <summary>Darstellungsstil der Tabs.</summary>
public enum TabStil
{
    /// <summary>Revits eigene Tabs, nichts wird gefaerbt oder veraendert.</summary>
    RevitStandard,
    /// <summary>Eckig und flach: Projekte gefuellt, Familien mit Rahmen.</summary>
    Eckig,
    /// <summary>Obere Ecken rund, kleine Luecke zwischen den Tabs.</summary>
    Abgerundet,
    /// <summary>Vollrund, freistehend.</summary>
    Pille,
    /// <summary>Sanfte Toenung mit farbigem Balken unten.</summary>
    Farbbalken,
    /// <summary>Flach, dicker Strich unten.</summary>
    Unterstrich,
    /// <summary>Neutrale Flaeche, die Farbe steht in einem Streifen links.</summary>
    Seitenstreifen,
    /// <summary>Eine zusammenhaengende Leiste, nur die Enden sind rund.</summary>
    Segmente,
    /// <summary>Zarte Toenung mit feiner Kontur.</summary>
    Leise,
    /// <summary>Alle Tabs gleich geneigt, scharfe Ecken; der Winkel ist fest, der Versatz folgt der Hoehe.</summary>
    Parallelogramm
}

/// <summary>Wie ein Dokumenttyp markiert wird - unabhaengig vom Stil.</summary>
public enum TabMarkierung
{
    /// <summary>Gefuellt, im Ton des gewaehlten Stils.</summary>
    Flaeche,
    /// <summary>Dicker farbiger Rahmen, keine Flaeche.</summary>
    Rahmen
}

/// <summary>
/// Einstellungen der Tab-Farben. Liegen als JSON unter
/// %LOCALAPPDATA%\RevitSekmeRenkleri\TabColor.json und gelten sofort - der Takt in
/// <see cref="TabColorApp"/> liest bei jedem Durchlauf den aktuellen Stand.
/// </summary>
public class TabColorSettings
{
    public const int PaletteGroesse = 20;

    public static readonly string[] StandardPalette =
    {
        "#0082C8", "#E6194B", "#3CB44B", "#FFE119", "#F58230",
        "#911EB4", "#46F0F0", "#F032E6", "#D2F53C", "#FABED4",
        "#008080", "#DCBEFF", "#AA6E28", "#FFFAC8", "#800000",
        "#AAFFC3", "#808000", "#FFD7B4", "#000080", "#808080"
    };

    /// <summary>
    /// Hauptschalter. Aus = Revits Tabs unangetastet, kein Takt, keine
    /// Vorschau, keine Dokumentzeile - ersetzt den frueheren Ein/Aus-Knopf.
    /// </summary>
    public bool Aktiv { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TabStil Stil { get; set; } = TabStil.Abgerundet;

    /// <summary>Markierung der Projekte: Flaeche oder Rahmen.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TabMarkierung ProjektDarstellung { get; set; } = TabMarkierung.Flaeche;

    /// <summary>Markierung der Familien: Flaeche oder Rahmen.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TabMarkierung FamilieDarstellung { get; set; } = TabMarkierung.Rahmen;

    /// <summary>Hex-Farben, Index = Reihenfolge der geoeffneten Dokumente.</summary>
    public string[] Palette { get; set; } = (string[])StandardPalette.Clone();

    /// <summary>Deckkraft der Projekt-Flaeche beim aktiven Tab, 0-100 %.</summary>
    public int FuellungAktiv { get; set; } = 75;

    /// <summary>Deckkraft der Projekt-Flaeche bei inaktiven Tabs, 0-100 %.</summary>
    public int FuellungInaktiv { get; set; } = 47;

    /// <summary>Rahmenstaerke der Familien-Tabs (und des Balkens), Pixel.</summary>
    public int Rahmenstaerke { get; set; } = 3;

    /// <summary>Eckenradius der Stile Abgerundet und Farbbalken, Pixel.</summary>
    public int Eckenradius { get; set; } = 9;
    /// <summary>
    /// Zusaetzliche Hoehe der Tabs in Pixeln; 0 = Revits Standardhoehe. Ein
    /// Zuschlag statt einer absoluten Hoehe: Revits Standard ist schon ein
    /// Drittel eines Reglers von 0-60, darunter laesst sich nichts einstellen.
    /// </summary>
    public int TabHoeheZuschlag { get; set; } = 0;

    /// <summary>
    /// Zweizeilige Tab-Leiste: oben ein Tab je Dokument, darunter die offenen
    /// Ansichten des gewaehlten Dokuments.
    /// </summary>
    public bool Gruppieren { get; set; } = false;

    /// <summary>
    /// Dokument-Tabs der oberen Zeile im Ordner-Stil: ein kleiner Reiter oben links,
    /// wie bei einem Ordnersymbol. Wirkt nur mit Gruppierung.
    /// </summary>
    public bool DokumentOrdner { get; set; } = false;

    /// <summary>Vorschaubild beim Ueberfahren eines Tabs anzeigen.</summary>
    public bool VorschauAktiv { get; set; } = true;

    /// <summary>Wartezeit in Millisekunden, bevor die Vorschau erscheint.</summary>
    public int VorschauVerzoegerungMs { get; set; } = 1000;

    /// <summary>
    /// Vorschau nur fuer Ansichten des aktiven Dokuments. Das Rendern einer
    /// Ansicht aus einem anderen Dokument schaltet Revit kurz dorthin um; mit
    /// dieser Einstellung passiert das nie.
    /// </summary>
    public bool VorschauNurAktivesDokument { get; set; } = false;

    /// <summary>Laengste Bildkante der Vorschau in Pixeln.</summary>
    public int VorschauBreite { get; set; } = 360;

    // ---------------------------------------------------------------

    private static readonly string Pfad = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RevitSekmeRenkleri", "TabColor.json");

    private static readonly JsonSerializerOptions JsonOptionen = new() { WriteIndented = true };

    /// <summary>Der aktuell gueltige Stand.</summary>
    public static TabColorSettings Aktuell { get; private set; } = Laden();

    /// <summary>Wird nach jeder Aenderung ueber <see cref="Uebernehmen"/> ausgeloest.</summary>
    public static event Action Geaendert;

    private static TabColorSettings Laden()
    {
        try
        {
            if (File.Exists(Pfad))
            {
                var s = JsonSerializer.Deserialize<TabColorSettings>(File.ReadAllText(Pfad));
                if (s != null)
                {
                    s.Bereinigen();
                    return s;
                }
            }
        }
        catch { /* kaputte Datei: mit Standardwerten weiter */ }

        return new TabColorSettings();
    }

    /// <summary>Haelt Werte in sinnvollen Grenzen, auch bei von Hand bearbeiteter Datei.</summary>
    private void Bereinigen()
    {
        var neu = (string[])StandardPalette.Clone();
        if (Palette != null)
            for (int i = 0; i < Math.Min(Palette.Length, PaletteGroesse); i++)
                if (TabColorPalette.TryParse(Palette[i], out _)) neu[i] = Palette[i];
        Palette = neu;

        FuellungAktiv = Math.Clamp(FuellungAktiv, 0, 100);
        FuellungInaktiv = Math.Clamp(FuellungInaktiv, 0, 100);
        Rahmenstaerke = Math.Clamp(Rahmenstaerke, 1, 8);
        Eckenradius = Math.Clamp(Eckenradius, 0, 20);
        TabHoeheZuschlag = Math.Clamp(TabHoeheZuschlag, 0, 30);
        VorschauVerzoegerungMs = Math.Clamp(VorschauVerzoegerungMs, 100, 5000);
        VorschauBreite = Math.Clamp(VorschauBreite, 200, 800);
    }

    /// <summary>Unabhaengige Kopie, z. B. fuer die Stil-Vorschauen im Einstellungsfenster.</summary>
    internal TabColorSettings Kopie()
    {
        var kopie = (TabColorSettings)MemberwiseClone();
        kopie.Palette = (string[])Palette.Clone();
        return kopie;
    }

    /// <summary>Speichert den aktuellen Stand und meldet die Aenderung.</summary>
    public static void Uebernehmen()
    {
        Aktuell.Bereinigen();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Pfad)!);
            File.WriteAllText(Pfad, JsonSerializer.Serialize(Aktuell, JsonOptionen));
        }
        catch { /* Speichern ist nicht kritisch - die Einstellung gilt trotzdem bis Revit schliesst */ }

        Geaendert?.Invoke();
    }

    /// <summary>Alles auf Werkseinstellung.</summary>
    public static void ZuruecksetzenAlles()
    {
        Aktuell = new TabColorSettings();
        Uebernehmen();
    }

    /// <summary>Nur die Palette auf Werkseinstellung.</summary>
    public static void ZuruecksetzenPalette()
    {
        Aktuell.Palette = (string[])StandardPalette.Clone();
        Uebernehmen();
    }
}
