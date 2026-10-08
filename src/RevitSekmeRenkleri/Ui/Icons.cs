using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RevitSekmeRenkleri.Ui;

/// <summary>Ein Teil eines Symbols; Ton = Deckkraft der Symbolfarbe (so bleibt die Abstufung in hell und dunkel lesbar).</summary>
internal readonly record struct IconPart(string PathData, double Tone);

/// <summary>
/// Menueband-Symbole als Pfad-Geometrie statt als PNG: scharf bei jeder
/// Skalierung, keine Binaerdateien im Repo, und die Farbe folgt Revits Thema.
///
/// Gezeichnet wird auf einem 24x24-Raster, mittig in die 32x32 des Knopfes
/// gesetzt, mit doppelter Pixeldichte gerendert. Jedes Bild merkt sich, woraus es
/// entstand, damit es beim Themenwechsel in der anderen Farbe neu gezeichnet
/// werden kann (<see cref="NeuZeichnen"/>).
/// </summary>
internal static class Icons
{
    private const double Raster = 24.0;
    private const double Entwurf = 32.0;
    private const double Rand = (Entwurf - Raster) / 2.0;

    /// <summary>
    /// Zwei Tabs oben (links gefuellt, rechts nur als Rahmen), darunter die
    /// Trennlinie der Tab-Leiste und drei Kacheln fuer Ansichten bzw. Vorschau.
    /// Die Rahmenform ist ein EvenOdd-Teil: die innere Kontur stanzt die Mitte
    /// aus, unten offen wie ein echter Tab.
    /// </summary>
    public static readonly IconPart[] TabColor =
    {
        new("F1 M2,11 V8 A3,3 0 0 1 5,5 H9 A3,3 0 0 1 12,8 V11 Z", 1.00),
        new("F0 M13,11 V8 A3,3 0 0 1 16,5 H19 A3,3 0 0 1 22,8 V11 Z "
            + "M15,11 V8.5 A1.5,1.5 0 0 1 16.5,7 H18.5 A1.5,1.5 0 0 1 20,8.5 V11 Z", 1.00),
        new("F1 M2,12.5 H22 V14 H2 Z", 0.55),
        new("F1 M2,16 H8 V21 H2 Z M10,16 H16 V21 H10 Z M18,16 H22 V21 H18 Z", 0.70),
    };

    // Auf hellem Menueband verblassen niedrige Toene zu fast weissen Flaechen.
    private const double HellUntergrenze = 0.55;

    private sealed class Rezept
    {
        public Rezept(IconPart[] teile, int groesse) { Teile = teile; Groesse = groesse; }
        public IconPart[] Teile { get; }
        public int Groesse { get; }
    }

    // ConditionalWeakTable: die Tabelle darf ein abgeloestes Bild nicht am Leben halten.
    private static readonly ConditionalWeakTable<ImageSource, Rezept> _rezepte = new();

    public static ImageSource Render(IconPart[] teile, int groesse = 32) => Zeichnen(groesse, teile);

    /// <summary>Zeichnet ein von hier stammendes Bild im JETZIGEN Thema neu, sonst null.</summary>
    public static ImageSource NeuZeichnen(ImageSource alt)
    {
        if (alt == null || !_rezepte.TryGetValue(alt, out var r)) return null;
        return Zeichnen(r.Groesse, r.Teile);
    }

    private static ImageSource Zeichnen(int groesse, IconPart[] teile)
    {
        try
        {
            bool dunkel = Theme.IsDark;
            var grundfarbe = dunkel ? Color.FromRgb(0xE8, 0xE9, 0xEC) : Color.FromRgb(0x12, 0x14, 0x18);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double massstab = groesse / Entwurf;
                dc.PushTransform(new ScaleTransform(massstab, massstab));
                dc.PushTransform(new TranslateTransform(Rand, Rand));

                foreach (var teil in teile)
                {
                    var form = Geometry.Parse(teil.PathData);
                    form.Freeze();

                    double ton = dunkel ? teil.Tone : HellUntergrenze + (1 - HellUntergrenze) * teil.Tone;
                    var pinsel = new SolidColorBrush(grundfarbe) { Opacity = ton };
                    pinsel.Freeze();
                    dc.DrawGeometry(pinsel, null, form);
                }

                dc.Pop();
                dc.Pop();
            }

            var bild = new RenderTargetBitmap(groesse * 2, groesse * 2, 192, 192, PixelFormats.Pbgra32);
            bild.Render(visual);
            bild.Freeze();

            _rezepte.Add(bild, new Rezept(teile, groesse));
            return bild;
        }
        catch
        {
            // Ein kaputtes Symbol kostet den Knopf sein Bild, nie das Laden des Add-ins.
            return null;
        }
    }
}
