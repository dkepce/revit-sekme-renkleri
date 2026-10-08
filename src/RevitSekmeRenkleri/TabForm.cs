using System.Windows;
using System.Windows.Media;

namespace RevitSekmeRenkleri;

/// <summary>
/// Gezeichnete Tab-Formen: Parallelogramm und Ordner-Reiter.
///
/// **Der Winkel ist fest, der Versatz folgt der Hoehe.** Eine feste Versatz-
/// Breite (etwa 12 px) ergibt bei einem hoeheren aktiven Tab einen anderen
/// Winkel - die Flanken laufen dann nicht mehr parallel. Hier wird der Versatz
/// aus der Hoehe des jeweiligen Tabs gerechnet: gleiche Steigung bei jeder
/// Hoehe, und der Abstand zwischen zwei Tabs ist ueberall gleich breit.
///
/// Zwei Zeichenarten: Flaeche (geschlossene Form, gefuellt) und Kontur
/// (offene Linie ohne Unterkante, nach innen versetzt, damit die Strichmitte
/// nicht ueber die Tab-Grenze ragt).
/// </summary>
internal static class TabForm
{
    /// <summary>Neigung der Flanken gegen die Senkrechte.</summary>
    public const double WinkelGrad = 18;

    /// <summary>Waagerechte Luecke zwischen zwei Tabs, an jeder Stelle gleich.</summary>
    public const double Zwischenraum = 2;

    /// <summary>Hoehe des Ordner-Reiters ueber dem Koerper.</summary>
    public const double OhrHoehe = 6;

    private static readonly double Tan = Math.Tan(WinkelGrad * Math.PI / 180);
    private static readonly double Cos = Math.Cos(WinkelGrad * Math.PI / 180);

    /// <summary>Waagerechter Versatz einer Flanke bei dieser Hoehe.</summary>
    public static double Versatz(double hoehe) => hoehe * Tan;

    /// <summary>Parallelogramm der Groesse w x h, nach rechts geneigt (/).</summary>
    public static Geometry Parallelogramm(double w, double h, double kontur, bool flaeche)
    {
        if (w <= 0 || h <= 0) return Geometry.Empty;

        double s = Versatz(h);
        var geo = new StreamGeometry();
        using (var c = geo.Open())
        {
            if (flaeche)
            {
                c.BeginFigure(new Point(s, 0), true, true);
                c.LineTo(new Point(w, 0), true, false);
                c.LineTo(new Point(w - s, h), true, false);
                c.LineTo(new Point(0, h), true, false);
            }
            else
            {
                double i = kontur / 2;
                double ix = i / Cos;   // waagerechter Versatz einer um i verschobenen Flanke
                double ys = s / h;     // Flanke: x aendert sich um s ueber die Hoehe h
                c.BeginFigure(new Point(ix, h), false, false);
                c.LineTo(new Point(s + ix - ys * i, i), true, false);
                c.LineTo(new Point(w - ix - ys * i, i), true, false);
                c.LineTo(new Point(w - s - ix, h), true, false);
            }
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>
    /// Ordner-Form: Koerper mit einem kleinen Reiter oben links. h ist die
    /// GESAMTE Hoehe samt Reiter. schraeg = Flanken wie beim Parallelogramm.
    /// </summary>
    public static Geometry Ordner(double w, double h, double ohr, double ohrBreite,
        bool schraeg, double kontur, bool flaeche)
    {
        if (w <= 0 || h <= ohr) return Geometry.Empty;

        double t = schraeg ? Tan : 0;
        double wr = Math.Min(ohrBreite, w * 0.6);
        var geo = new StreamGeometry();
        using (var c = geo.Open())
        {
            if (flaeche)
            {
                c.BeginFigure(new Point(t * h, 0), true, true);
                c.LineTo(new Point(t * h + wr, 0), true, false);
                c.LineTo(new Point(t * h + wr - t * ohr, ohr), true, false);
                c.LineTo(new Point(w - t * ohr, ohr), true, false);
                c.LineTo(new Point(w - t * h, h), true, false);
                c.LineTo(new Point(0, h), true, false);
            }
            else
            {
                double i = kontur / 2;
                double ix = schraeg ? i / Cos : i;
                c.BeginFigure(new Point(ix, h), false, false);
                c.LineTo(new Point(t * (h - i) + ix, i), true, false);
                c.LineTo(new Point(t * (h - i) + wr - ix, i), true, false);
                c.LineTo(new Point(t * h + wr - t * (ohr + i) - ix, ohr + i), true, false);
                c.LineTo(new Point(w - t * (ohr + i) - ix, ohr + i), true, false);
                c.LineTo(new Point(w - t * h - ix, h), true, false);
            }
        }
        geo.Freeze();
        return geo;
    }
}
