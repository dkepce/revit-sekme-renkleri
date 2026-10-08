using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RevitSekmeRenkleri;

/// <summary>
/// Gezeichnete Tab-Form (Parallelogramm). Revits Tab ist ein Rechteck mit
/// Rahmen; eine Schraege laesst sich damit nicht darstellen. Deshalb wird in
/// die Vorlage des Tabs (Grid "mGrid") ein eigener Pfad eingehaengt, Revits
/// Rahmen ("mBorder") verschwindet, und der Inhalt ("mContent") rueckt um den
/// Versatz nach innen, damit die Schrift nicht in die Flanken ragt.
/// </summary>
public partial class ViewTabColorizer
{
    private sealed class FormZustand
    {
        public Path Pfad;
        public TabLook Look;
        public bool Aktiv;
        public (double W, double H, bool Flaeche, double Kontur) Letzte;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TabItem, FormZustand> _formen = new();

    private static Grid VorlagenGitter(TabItem tab)
        => tab.FindDescendants<Grid>().FirstOrDefault(g => g.Name == "mGrid");

    private void SchraegAnwenden(TabItem tab, TabLook look)
    {
        var gitter = VorlagenGitter(tab);
        if (gitter == null)
        {
            // Unbekannte Vorlage: lieber das Rechteck als gar kein Tab.
            tab.Margin = look.Abstand;
            return;
        }

        if (!_formen.TryGetValue(tab, out var z))
        {
            z = new FormZustand();
            _formen.Add(tab, z);
        }

        if (z.Pfad == null)
        {
            z.Pfad = new Path
            {
                IsHitTestVisible = false,
                StrokeLineJoin = PenLineJoin.Miter,
                StrokeMiterLimit = 8
            };
            // Wechselt die Breite (Inhalt, Auswahl), wird die Form sofort neu gezeichnet.
            tab.SizeChanged += (_, _) => FormNeuZeichnen(tab);
        }

        // Von Revit neu aufgebaute Vorlage: Pfad wieder einhaengen.
        if (z.Pfad.Parent != gitter)
        {
            (z.Pfad.Parent as Panel)?.Children.Remove(z.Pfad);
            gitter.Children.Insert(Math.Min(1, gitter.Children.Count), z.Pfad);
        }

        z.Aktiv = true;
        z.Look = look;
        z.Pfad.Visibility = Visibility.Visible;

        var rahmen = gitter.Children.OfType<Border>().FirstOrDefault(b => b.Name == "mBorder");
        if (rahmen != null) rahmen.Visibility = Visibility.Collapsed;

        double hoehe = tab.ActualHeight > 0 ? tab.ActualHeight : (_standardHoehe > 0 ? _standardHoehe : 26);
        double versatz = TabForm.Versatz(hoehe);

        var inhalt = gitter.Children.OfType<ContentPresenter>().FirstOrDefault(c => c.Name == "mContent");
        if (inhalt != null) inhalt.Margin = new Thickness(versatz + 6, 0, versatz + 6, 0);

        // Der Abstand haengt am Versatz DIESES Tabs: Unterkanten fluchten, die
        // Flanken laufen parallel, also ist die Luecke ueberall gleich breit.
        tab.Margin = new Thickness(0, 0, TabForm.Zwischenraum - versatz, 0);

        FormZeichnen(tab, z);
    }

    private static void FormNeuZeichnen(TabItem tab)
    {
        if (_formen.TryGetValue(tab, out var z) && z.Aktiv) FormZeichnen(tab, z);
    }

    private static void FormZeichnen(TabItem tab, FormZustand z)
    {
        double w = tab.ActualWidth, h = tab.ActualHeight;
        var look = z.Look;
        if (look == null || z.Pfad == null) return;

        bool flaeche = look.Hintergrund != null;
        var schluessel = (w, h, flaeche, look.Kontur);

        if (!z.Pfad.Data.IsNullOrEmptyGeometry() && schluessel == z.Letzte
            && ReferenceEquals(z.Pfad.Fill, look.Hintergrund) && ReferenceEquals(z.Pfad.Stroke, look.RahmenFarbe))
            return;

        z.Letzte = schluessel;
        z.Pfad.Data = TabForm.Parallelogramm(w, h, look.Kontur, flaeche);
        z.Pfad.Fill = flaeche ? look.Hintergrund : null;
        z.Pfad.Stroke = flaeche ? null : look.RahmenFarbe;
        z.Pfad.StrokeThickness = flaeche ? 0 : look.Kontur;
    }

    /// <summary>Stellt Revits Rechteck-Tab wieder her. vollstaendig = Pfad auch aus der Vorlage nehmen.</summary>
    private static void SchraegEntfernen(TabItem tab, bool vollstaendig = false)
    {
        if (!_formen.TryGetValue(tab, out var z) || (!z.Aktiv && !vollstaendig)) return;

        z.Aktiv = false;

        var gitter = VorlagenGitter(tab);
        if (gitter != null)
        {
            gitter.Children.OfType<Border>().FirstOrDefault(b => b.Name == "mBorder")
                ?.ClearValue(UIElement.VisibilityProperty);
            gitter.Children.OfType<ContentPresenter>().FirstOrDefault(c => c.Name == "mContent")
                ?.ClearValue(FrameworkElement.MarginProperty);
        }

        if (z.Pfad != null)
        {
            if (vollstaendig) (z.Pfad.Parent as Panel)?.Children.Remove(z.Pfad);
            else z.Pfad.Visibility = Visibility.Collapsed;
        }
    }
}

internal static class GeometrieHilfe
{
    public static bool IsNullOrEmptyGeometry(this Geometry g) => g == null || g.IsEmpty();
}
