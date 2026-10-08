using System.Diagnostics;
using System.Reflection;
using System.Windows.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitSekmeRenkleri.Ui;

namespace RevitSekmeRenkleri;

/// <summary>
/// Einstiegspunkt. Ein DispatcherTimer faerbt die Tabs periodisch neu: Die
/// AvalonDock-Ereignisse (LayoutChanged u. a.) feuern, bevor die Oberflaeche
/// aktualisiert ist, und Revit setzt den Tab-Stil bei jedem Ansichtswechsel
/// zurueck - ein Timer faengt beides zuverlaessig ab.
///
/// Es gibt nur noch EINEN Knopf, den der Einstellungen. Ein- und Ausschalten
/// ist der Hauptschalter dort (<see cref="TabColorSettings.Aktiv"/>).
/// </summary>
public class TabColorApp : IExternalApplication
{
    private static readonly TimeSpan Intervall = TimeSpan.FromMilliseconds(500);

    private static ViewTabColorizer _faerber;
    private static DispatcherTimer _timer;
    private static RibbonItem _knopf;

    // Der Sichtbaum steht beim Start noch nicht - der Timer darf erst nach
    // der ersten aktivierten Ansicht laufen.
    private static bool _timerBereit;

    public Result OnStartup(UIControlledApplication app)
    {
        _faerber = new ViewTabColorizer();
        VorschauDienst.Starten();

        app.ViewActivated += BeiErsterAnsicht;
        app.ThemeChanged += BeiThemenwechsel;
        TabColorSettings.Geaendert += BeiEinstellung;

        ReiterAnlegen(app);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app)
    {
        TabColorSettings.Geaendert -= BeiEinstellung;
        app.ThemeChanged -= BeiThemenwechsel;

        TimerStoppen();
        VorschauDienst.Beenden();
        _faerber?.Reset();
        return Result.Succeeded;
    }

    private static void ReiterAnlegen(UIControlledApplication app)
    {
        try
        {
            // Revits eigener Reiter Zusatzmodule: CreateRibbonPanel ohne Reiter-Namen
            // legt das Panel dort an. Ein eigener Reiter waere fuer einen Knopf zu viel.
            var panel = app.CreateRibbonPanel("Sekme Renkleri");
            string pfad = Assembly.GetExecutingAssembly().Location;

            var daten = new PushButtonData("RevitSekmeRenkleriEinstellungen", "Sekme\nRenkleri", pfad,
                typeof(TabColorEinstellungenCommand).FullName)
            {
                ToolTip = "Sekme Renkleri: açma/kapatma, stil, renkler, gruplama ve önizleme ayarları.",
                AvailabilityClassName = typeof(ImmerVerfuegbar).FullName,
                // Symbole als Pfad-Geometrie, im Thema von Revit gezeichnet
                // (siehe Ui/Icons.cs).
                LargeImage = Icons.Render(Icons.TabColor, 32),
                Image = Icons.Render(Icons.TabColor, 16)
            };

            _knopf = panel.AddItem(daten);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Reiter konnte nicht angelegt werden: {ex.Message}");
        }
    }

    /// <summary>
    /// Die Symbole haengen am Revit-Thema; beim Umschalten zeichnet sich das
    /// Pfad-Symbol selbst in der anderen Farbe neu.
    /// </summary>
    private static void BeiThemenwechsel(object sender, ThemeChangedEventArgs e)
    {
        try
        {
            if (_knopf is not RibbonButton knopf) return;
            knopf.LargeImage = Icons.NeuZeichnen(knopf.LargeImage) ?? knopf.LargeImage;
            knopf.Image = Icons.NeuZeichnen(knopf.Image) ?? knopf.Image;
        }
        catch { /* ein Themenwechsel darf an keinem Symbol scheitern */ }
    }

    // ----------------------------------------------------------------
    //  Takt
    // ----------------------------------------------------------------

    private static void BeiErsterAnsicht(object sender, ViewActivatedEventArgs e)
    {
        if (sender is UIApplication uiApp)
            uiApp.ViewActivated -= BeiErsterAnsicht;

        _timerBereit = true;
        if (TabColorSettings.Aktuell.Aktiv) TimerStarten();
    }

    /// <summary>Hauptschalter geaendert: Takt starten oder anhalten und zuruecksetzen.</summary>
    private static void BeiEinstellung()
    {
        if (TabColorSettings.Aktuell.Aktiv)
        {
            if (_timerBereit) TimerStarten();
        }
        else if (_timer != null)
        {
            TimerStoppen();
            VorschauDienst.Verbergen();
            _faerber?.Reset();
        }
    }

    private static void TimerStarten()
    {
        if (_timer != null) return;

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Intervall };
        _timer.Tick += BeiTakt;
        _timer.Start();
    }

    private static void TimerStoppen()
    {
        if (_timer == null) return;

        _timer.Stop();
        _timer.Tick -= BeiTakt;
        _timer = null;
    }

    // Messung: was kostet ein Takt auf dem UI-Thread? Zusammengefasst alle 60 s
    // ins Protokoll - ein Zeile je Minute, nicht je Takt.
    private static readonly Stopwatch _uhr = new();
    private static readonly Stopwatch _fenster = Stopwatch.StartNew();
    private static int _takte, _langsam;
    private static double _summeMs, _maxMs;

    private static void BeiTakt(object sender, EventArgs e)
    {
        _uhr.Restart();
        try { _faerber?.Colorize(); }
        catch (Exception ex) { TabLog.Schreibe($"Takt-Fehler: {ex.Message}"); }
        _uhr.Stop();

        double ms = _uhr.Elapsed.TotalMilliseconds;
        _takte++;
        _summeMs += ms;
        if (ms > _maxMs) _maxMs = ms;
        if (ms > 20) _langsam++;

        if (_fenster.Elapsed.TotalSeconds >= 60)
        {
            TabLog.Schreibe($"Takt: {_takte} Durchlaeufe in {_fenster.Elapsed.TotalSeconds:0}s, " +
                            $"Mittel {_summeMs / Math.Max(1, _takte):0.0} ms, Spitze {_maxMs:0.0} ms, ueber 20 ms: {_langsam}");
            _fenster.Restart();
            _takte = _langsam = 0;
            _summeMs = _maxMs = 0;
        }
    }
}

/// <summary>Knopf auch ohne geoeffnetes Dokument aktiv.</summary>
public class ImmerVerfuegbar : IExternalCommandAvailability
{
    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories) => true;
}
