using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;

namespace RevitSekmeRenkleri.Ui;

/// <summary>
/// Revits Hauptfenster als Besitzer eines Fensters dieses Add-ins, und das
/// Fenster in die Mitte von Revit.
///
/// Ohne Besitzer hat ein Fenster keine Z-Beziehung zu Revit und verschwindet
/// dahinter, sobald der Fokus wandert. Ein Besitzer sorgt dafuer, dass es immer
/// ueber Revit liegt, ohne ueber andere Programme zu schweben (anders als
/// Topmost). Zentriert wird auf dem Fenster von Revit - WindowStartupLocation
/// CenterScreen wuerde auf dem Hauptbildschirm zentrieren, und wer Revit auf dem
/// zweiten Schirm hat, bekaeme das Fenster auf dem falschen.
/// </summary>
internal static class RevitWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rechteck { public int Links, Oben, Rechts, Unten; }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rechteck rechteck);

    /// <summary>Vor dem Anzeigen aufrufen: danach laesst sich der Besitzer nicht mehr setzen.</summary>
    public static void SetOwner(Window fenster, UIApplication app)
    {
        IntPtr revit = app?.MainWindowHandle ?? IntPtr.Zero;
        if (revit == IntPtr.Zero) return;

        new WindowInteropHelper(fenster) { Owner = revit };
        fenster.WindowStartupLocation = WindowStartupLocation.Manual;
        fenster.Loaded += (_, _) => Zentrieren(fenster, revit);
    }

    private static void Zentrieren(Window fenster, IntPtr revit)
    {
        try
        {
            if (!GetWindowRect(revit, out var r)) return;

            var quelle = PresentationSource.FromVisual(fenster);
            if (quelle?.CompositionTarget == null) return;

            // Pixel -> geraeteunabhaengige Einheiten.
            var matrix = quelle.CompositionTarget.TransformFromDevice;
            var oben = matrix.Transform(new Point(r.Links, r.Oben));
            var unten = matrix.Transform(new Point(r.Rechts, r.Unten));

            double links = oben.X + (unten.X - oben.X - fenster.ActualWidth) / 2;
            double top = oben.Y + (unten.Y - oben.Y - fenster.ActualHeight) / 2;

            // Auf dem sichtbaren Bereich der Schirme halten.
            links = Math.Clamp(links, SystemParameters.VirtualScreenLeft,
                Math.Max(SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - fenster.ActualWidth));
            top = Math.Clamp(top, SystemParameters.VirtualScreenTop,
                Math.Max(SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - fenster.ActualHeight));

            fenster.Left = links;
            fenster.Top = top;
        }
        catch { /* Position ist Komfort - das Fenster bleibt, wo Windows es hinsetzt */ }
    }
}
