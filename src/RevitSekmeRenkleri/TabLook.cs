using System.Windows;
using System.Windows.Media;

namespace RevitSekmeRenkleri;

/// <summary>Wie ein Tab (oder ein Dokument-Tab der oberen Zeile) aussehen soll.</summary>
internal sealed class TabLook
{
    /// <summary>Flaechenfarbe; null = keine (Revits Standard bzw. durchsichtig).</summary>
    public Brush Hintergrund;

    public Brush RahmenFarbe;
    public Thickness Rahmen;
    public CornerRadius Radius;
    public Thickness Abstand;

    /// <summary>Gezeichnete Schraegform (Parallelogramm) statt Rechteck mit Rahmen.</summary>
    public bool Schraege;

    /// <summary>Konturstaerke bei Rahmen-Markierung (alle Stile), sonst 0.</summary>
    public double Kontur;
}

/// <summary>
/// Berechnet das Aussehen aus Stil und Markierungsart. Zwei Achsen, die
/// getrennt einstellbar sind:
///
///   Stil       - Form und Abstand (eckig, abgerundet, Pille, Unterstrich, ...)
///   Markierung - Flaeche (gefuellt im Ton des Stils) oder Rahmen (dicker
///                farbiger Rahmen, keine Flaeche); je fuer Projekte und
///                Familien.
///
/// Eine Stelle fuer Tabs der unteren Zeile UND Dokument-Tabs der oberen: beide
/// muessen gleich aussehen, sonst liest sich die Gruppierung als zwei Werkzeuge.
/// </summary>
internal static class TabStilRechner
{
    /// <param name="pos">Platz des Tabs in seiner Zeile, 0-basiert (fuer Segmente).</param>
    /// <param name="anzahl">Anzahl der Tabs in der Zeile.</param>
    /// <param name="dunkel">Revit-Thema, fuer neutrale Flaechen.</param>
    public static TabLook Berechne(TabColorSettings s, Color farbe, bool familie, bool aktiv,
        double hoehe, int pos, int anzahl, bool dunkel)
    {
        double r = s.Eckenradius;
        double d = s.Rahmenstaerke;
        var look = new TabLook();

        // ---- Form und Abstand je Stil ----
        switch (s.Stil)
        {
            case TabStil.Eckig:
                look.Radius = new CornerRadius(0);
                look.Abstand = new Thickness(0);
                break;
            case TabStil.Pille:
                look.Radius = new CornerRadius(hoehe / 2);
                look.Abstand = new Thickness(0, 2, 3, 2);
                break;
            case TabStil.Unterstrich:
                look.Radius = new CornerRadius(0);
                look.Abstand = new Thickness(0, 0, 3, 0);
                break;
            case TabStil.Seitenstreifen:
                look.Radius = new CornerRadius(r);
                look.Abstand = new Thickness(0, 0, 4, 0);
                break;
            case TabStil.Segmente:
                look.Radius = anzahl <= 1 ? new CornerRadius(r)
                    : pos == 0 ? new CornerRadius(r, 0, 0, r)
                    : pos == anzahl - 1 ? new CornerRadius(0, r, r, 0)
                    : new CornerRadius(0);
                look.Abstand = new Thickness(0);
                break;
            case TabStil.Parallelogramm:
                // Gezeichnete Form: Winkel fest, Versatz folgt der Hoehe (TabForm).
                // Der Abstand wird erst beim Anwenden festgelegt, er haengt am Versatz.
                look.Radius = new CornerRadius(0);
                look.Abstand = new Thickness(0, 0, TabForm.Zwischenraum, 0);
                look.Schraege = true;
                break;
            case TabStil.Leise:
                look.Radius = new CornerRadius(r, r, 0, 0);
                look.Abstand = new Thickness(0, 0, 3, 0);
                break;
            default: // Abgerundet, Farbbalken
                look.Radius = new CornerRadius(r, r, 0, 0);
                look.Abstand = new Thickness(0, 0, 2, 0);
                break;
        }

        // ---- Markierung: Rahmen ----
        var markierung = familie ? s.FamilieDarstellung : s.ProjektDarstellung;
        if (markierung == TabMarkierung.Rahmen)
        {
            look.Hintergrund = null;
            look.Rahmen = new Thickness(d);
            look.Kontur = d;
            look.RahmenFarbe = TabColorPalette.Pinsel(farbe, aktiv ? 100 : 86);
            return look;
        }

        // ---- Markierung: Flaeche, im Ton des Stils ----
        int voll = aktiv ? s.FuellungAktiv : s.FuellungInaktiv;
        switch (s.Stil)
        {
            case TabStil.Farbbalken:
                look.Hintergrund = TabColorPalette.Pinsel(farbe,
                    (int)Math.Round(aktiv ? s.FuellungAktiv * 0.4 : s.FuellungInaktiv * 0.3));
                look.Rahmen = new Thickness(0, 0, 0, d);
                look.RahmenFarbe = TabColorPalette.Pinsel(farbe, 100);
                break;

            case TabStil.Unterstrich:
                look.Hintergrund = TabColorPalette.Pinsel(farbe, aktiv ? 20 : 8);
                look.Rahmen = new Thickness(0, 0, 0, d + 1);
                look.RahmenFarbe = TabColorPalette.Pinsel(farbe, aktiv ? 100 : 50);
                break;

            case TabStil.Seitenstreifen:
                // Neutrale Flaeche, die Farbe steht nur im Streifen links.
                look.Hintergrund = TabColorPalette.Pinsel(Colors.White,
                    dunkel ? (aktiv ? 16 : 7) : (aktiv ? 95 : 55));
                look.Rahmen = new Thickness(d + 2, 0, 0, 0);
                look.RahmenFarbe = TabColorPalette.Pinsel(farbe, 100);
                break;

            case TabStil.Segmente:
                look.Hintergrund = TabColorPalette.Pinsel(farbe, voll);
                look.Rahmen = new Thickness(0, 0, 1, 0);
                look.RahmenFarbe = TabColorPalette.Pinsel(dunkel ? Colors.Black : Colors.White, dunkel ? 35 : 70);
                break;

            case TabStil.Leise:
                look.Hintergrund = TabColorPalette.Pinsel(farbe, aktiv ? 28 : 10);
                look.Rahmen = new Thickness(1.5, 1.5, 1.5, 0);
                look.RahmenFarbe = TabColorPalette.Pinsel(farbe, aktiv ? 100 : 55);
                break;

            default: // Eckig, Abgerundet, Pille
                look.Hintergrund = TabColorPalette.Pinsel(farbe, voll);
                look.Rahmen = new Thickness(0);
                break;
        }
        return look;
    }
}
