using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using RevitSekmeRenkleri.Ui;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace RevitSekmeRenkleri;

/// <summary>
/// Faerbt und gruppiert die Ansichts-Tabs von Revit nach ihrem Dokument.
///
/// Revit zeigt die Ansichts-Tabs ueber Xceed AvalonDock: jeder Tab ist ein
/// <see cref="TabItem"/> in einem <see cref="LayoutDocumentPaneControl"/>. Sein
/// ToolTip lautet "Dokumentname.rvt - Ansichtstyp: Ansichtsname"; der Titel des
/// LayoutDocument enthaelt nur den Ansichtsnamen und taugt nicht zur
/// Dokumenterkennung.
///
/// Darstellung:
///   Projekt  (.rvt) - Tab vollflaechig in der Dokumentfarbe, kein Rahmen.
///   Familie  (.rfa) - Tab ohne Hintergrundfarbe, dafuer dicker farbiger Rahmen.
///
/// Wer TabItem.Background setzt, loest Revits eigene Stilbindung. Revit setzt
/// den Stil bei jedem Ansichtswechsel zurueck - die Farben werden deshalb vom
/// Timer in <see cref="TabColorApp"/> laufend neu gesetzt.
/// </summary>
public partial class ViewTabColorizer
{
    private readonly Dictionary<string, int> _farbIndex = new();
    private bool _baumProtokolliert;
    private bool _zurueckgesetzt;
    private int _naechsterIndex;
    private string _letzteTabMenge;

    /// <summary>Faerbt und gruppiert alle Tabs. Nur im UI-Thread aufrufen.</summary>
    public void Colorize()
    {
        try
        {
            var hauptfenster = UIFramework.MainWindow.getMainWnd();
            if (hauptfenster == null) return;

            // Vorschau beim Ueberfahren gilt unabhaengig vom Stil.
            foreach (var pane in hauptfenster.FindDescendants<LayoutDocumentPaneControl>())
            {
                foreach (var tab in pane.FindDescendants<TabItem>())
                    VorschauDienst.Anbinden(tab);
            }

            // Revit-Standard: Tabs einmal zuruecksetzen, danach nicht mehr
            // anfassen. Die Dokumentzeile ist davon unabhaengig.
            bool standard = TabColorSettings.Aktuell.Stil == TabStil.RevitStandard;
            if (standard)
            {
                if (!_zurueckgesetzt)
                {
                    Reset();
                    _zurueckgesetzt = true;
                }
            }
            else
            {
                _zurueckgesetzt = false;
            }

            foreach (var pane in hauptfenster.FindDescendants<LayoutDocumentPaneControl>())
            {
                if (!standard)
                {
                    // Erst gruppieren, dann faerben - Gruppieren aendert die Reihenfolge.
                    TabsNachDokumentGruppieren(pane);
                    PaneFaerben(pane);
                    InhaltsrahmenAnpassen(pane);
                }

                DokumentzeilePflegen(pane, standard);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Fehler: {ex.Message}");
        }
    }

    /// <summary>Setzt alle Tabs auf Revits Standard zurueck und vergisst die Farbzuordnung.</summary>
    public void Reset()
    {
        try
        {
            var hauptfenster = UIFramework.MainWindow.getMainWnd();
            if (hauptfenster == null) return;

            AlleZeilenEntfernen();

            foreach (var pane in hauptfenster.FindDescendants<LayoutDocumentPaneControl>())
            {
                foreach (var tab in pane.FindDescendants<TabItem>())
                {
                    tab.ClearValue(Control.BackgroundProperty);
                    tab.ClearValue(Control.BorderBrushProperty);
                    tab.ClearValue(Control.BorderThicknessProperty);
                    tab.ClearValue(FrameworkElement.MarginProperty);
                    tab.ClearValue(FrameworkElement.HeightProperty);
                    SchraegEntfernen(tab, vollstaendig: true);
                    tab.FindFirstDescendant<Border>()?.ClearValue(Border.CornerRadiusProperty);
                }

                foreach (var rahmen in pane.FindDescendants<Border>())
                    if (rahmen.Name == "ContentPanel")
                        rahmen.ClearValue(Border.BorderThicknessProperty);
            }

            _farbIndex.Clear();
            _naechsterIndex = 0;
            _letzteTabMenge = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Reset-Fehler: {ex.Message}");
        }
    }

    #region Gruppieren

    /// <summary>
    /// Ordnet die LayoutDocuments so, dass Tabs desselben Dokuments
    /// nebeneinander liegen. Greift nur, wenn sich die MENGE der Tabs aendert
    /// (Ansicht geoeffnet/geschlossen) - sonst wuerde es gegen Revits eigene
    /// Umordnung beim Ansichtswechsel arbeiten und ein Hin und Her ausloesen.
    /// </summary>
    private void TabsNachDokumentGruppieren(LayoutDocumentPaneControl paneControl)
    {
        try
        {
            if (paneControl.Model is not LayoutDocumentPane pane || pane.Children.Count < 2) return;

            var tabs = paneControl.FindDescendants<TabItem>();

            // Das Dokument jedes Tabs direkt vom Tab lesen, nicht ueber die
            // Position: Revits Tab-Leiste haelt nicht dieselbe Reihenfolge wie
            // das AvalonDock-Modell (gemessen: Klick auf Dokument B aktivierte
            // eine Ansicht von Dokument A).
            var nameVon = new Dictionary<LayoutDocument, string>();
            var tooltips = new List<string>(tabs.Count);
            foreach (var tab in tabs)
            {
                string tt = tab.ToolTip?.ToString() ?? "";
                tooltips.Add(tt);

                var doc = DokumentVon(tab);
                if (doc == null) return;
                nameVon[doc] = DokumentName(tt);
            }

            var dokumente = pane.Children.OfType<LayoutDocument>().ToList();
            if (dokumente.Any(d => !nameVon.ContainsKey(d))) return;

            var namen = dokumente.Select(d => nameVon[d]).ToList();

            string tabMenge = string.Join("||", tooltips.OrderBy(t => t, StringComparer.Ordinal));
            if (tabMenge == _letzteTabMenge) return;

            if (ZusammenhaengendNachDokument(namen))
            {
                _letzteTabMenge = tabMenge;
                return;
            }

            // Gruppenreihenfolge = Reihenfolge des ersten Auftretens; die
            // Ansichten innerhalb einer Gruppe behalten ihre Reihenfolge.
            var gruppen = new List<string>();
            foreach (var name in namen)
                if (!gruppen.Contains(name)) gruppen.Add(name);

            var gruppiert = new List<LayoutDocument>();
            foreach (var gruppe in gruppen)
                for (int i = 0; i < dokumente.Count; i++)
                    if (namen[i] == gruppe) gruppiert.Add(dokumente[i]);

            var aktiv = pane.SelectedContent as LayoutDocument;

            foreach (var dok in dokumente) pane.Children.Remove(dok);
            foreach (var dok in gruppiert) pane.Children.Add(dok);

            if (aktiv != null)
                pane.SelectedContentIndex = pane.Children.IndexOf(aktiv);

            _letzteTabMenge = tabMenge;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Gruppieren-Fehler: {ex.Message}");
        }
    }

    /// <summary>True, wenn die Tabs jedes Dokuments schon lueckenlos nebeneinander stehen.</summary>
    private static bool ZusammenhaengendNachDokument(List<string> namen)
    {
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string zuletzt = null;
        foreach (var name in namen)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(name, zuletzt)) continue;
            if (!gesehen.Add(name)) return false;
            zuletzt = name;
        }
        return true;
    }

    #endregion

    #region Faerben

    private void PaneFaerben(LayoutDocumentPaneControl pane)
    {
        var s = TabColorSettings.Aktuell;
        bool dunkel = Theme.IsDark;

        // Sichtbare Tabs in Reihenfolge: Segmente brauchen den Platz in der Zeile,
        // und ausgeblendete Tabs der Dokumentzeile zaehlen nicht mit.
        var tabs = pane.FindDescendants<TabItem>().Where(t => !IstAusgeblendet(t)).ToList();

        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            string tooltip = tab.ToolTip?.ToString();
            if (string.IsNullOrEmpty(tooltip)) continue;

            string name = DokumentName(tooltip);
            if (string.IsNullOrEmpty(name)) continue;

            bool familie = IstFamilie(name);
            int index = FarbIndex(name);
            var farbe = familie ? TabColorPalette.FamilienFarbe(index) : TabColorPalette.ProjektFarbe(index);
            double hoehe = (_standardHoehe > 0 ? _standardHoehe : 28) + s.TabHoeheZuschlag;

            var look = TabStilRechner.Berechne(s, farbe, familie, tab.IsSelected, hoehe, i, tabs.Count, dunkel);

            if (look.Hintergrund == null) tab.ClearValue(Control.BackgroundProperty);
            else tab.Background = look.Hintergrund;

            tab.BorderThickness = look.Rahmen;
            if (look.RahmenFarbe != null) tab.BorderBrush = look.RahmenFarbe;

            Geometrie(tab, s, look);
        }
    }

    /// <summary>
    /// Form und Abstand je nach Stil. Die Rundung gehoert nicht dem TabItem
    /// selbst, sondern dem aeusseren Border in dessen Vorlage - der zeichnet
    /// Flaeche und Rahmen. Wird bei jedem Takt vollstaendig gesetzt, damit ein
    /// Stilwechsel in den Einstellungen sofort greift.
    /// </summary>
    private void Geometrie(TabItem tab, TabColorSettings s, TabLook look)
    {
        // Ausgeblendete Tabs (Dokumentzeile) bleiben ohne Abstand.
        if (IstAusgeblendet(tab))
        {
            tab.Margin = new Thickness(0);
        }
        else if (look.Schraege)
        {
            SchraegAnwenden(tab, look); // setzt auch den Abstand: er haengt am Versatz
        }
        else
        {
            SchraegEntfernen(tab);
            tab.Margin = look.Abstand;
        }

        TabHoeheSetzen(tab, s);

        var rahmen = tab.FindFirstDescendant<Border>();
        if (rahmen == null) return;
        rahmen.CornerRadius = look.Radius;

        if (!_baumProtokolliert)
        {
            _baumProtokolliert = true;
            SichtbaumProtokollieren(tab);
        }
    }

    /// <summary>Revits eigene Tab-Hoehe in Pixeln; 0, solange noch nicht gemessen.</summary>
    private double _standardHoehe;

    /// <summary>
    /// Hoehe = Revits Standard + Zuschlag. Der Standard wird gemessen, sobald
    /// ein Tab ohne gesetzte Hoehe vorliegt: ActualHeight plus die senkrechten
    /// Raender (ein gestreckter Tab ist um seine Raender niedriger als die
    /// Leiste, bei Pille sind das 4 px). Ist schon eine Hoehe gesetzt, wenn
    /// der Zuschlag erstmals gebraucht wird, wird kurz zurueckgesetzt und
    /// gemessen.
    /// </summary>
    private void TabHoeheSetzen(TabItem tab, TabColorSettings s)
    {
        if (s.TabHoeheZuschlag <= 0)
        {
            tab.ClearValue(FrameworkElement.HeightProperty);
            if (tab.ActualHeight > 0 && !IstAusgeblendet(tab))
                _standardHoehe = tab.ActualHeight + tab.Margin.Top + tab.Margin.Bottom;
            return;
        }

        if (_standardHoehe <= 0)
        {
            tab.ClearValue(FrameworkElement.HeightProperty);
            tab.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _standardHoehe = tab.DesiredSize.Height; // enthaelt den Rand bereits
        }

        // Der Standard enthaelt den Rand des Stils, der Tab selbst nicht.
        double rand = tab.Margin.Top + tab.Margin.Bottom;
        tab.Height = Math.Max(1, _standardHoehe - rand + s.TabHoeheZuschlag);
    }


    /// <summary>
    /// Schreibt den Sichtbaum des ersten Tabs nach
    /// %LOCALAPPDATA%\RevitSekmeRenkleri\TabColor-Sichtbaum.txt. Reine Diagnose: zeigt,
    /// welches Element Revits Vorlage wirklich zeichnet, falls die Rundung
    /// nicht sichtbar wird.
    /// </summary>
    private static void SichtbaumProtokollieren(TabItem tab)
    {
        try
        {
            var zeilen = new List<string>();
            void Gehe(DependencyObject knoten, int tiefe)
            {
                string name = (knoten as FrameworkElement)?.Name;
                string extra = knoten is Border b
                    ? $"  Radius={b.CornerRadius} Dicke={b.BorderThickness} Hintergrund={b.Background} Rahmen={b.BorderBrush}"
                    : "";
                zeilen.Add($"{new string(' ', tiefe * 2)}{knoten.GetType().FullName}{(string.IsNullOrEmpty(name) ? "" : " [" + name + "]")}{extra}");

                int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(knoten);
                for (int i = 0; i < n; i++)
                    Gehe(System.Windows.Media.VisualTreeHelper.GetChild(knoten, i), tiefe + 1);
            }
            Gehe(tab, 0);

            string ordner = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitSekmeRenkleri");
            Directory.CreateDirectory(ordner);
            File.WriteAllLines(Path.Combine(ordner, "TabColor-Sichtbaum.txt"), zeilen);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Sichtbaum-Protokoll fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Der ContentPanel-Rahmen von AvalonDock uebernimmt denselben Stil wie die
    /// Tabs und wirkt bei gefaerbten Projekt-Tabs unruhig. Bei Projekten wird er
    /// entfernt; im Familien-Editor bleibt ein duenner Rahmen stehen.
    /// </summary>
    private void InhaltsrahmenAnpassen(LayoutDocumentPaneControl pane)
    {
        var aktiverTab = pane.FindDescendants<TabItem>().FirstOrDefault(t => t.IsSelected);
        bool familie = IstFamilie(DokumentName(aktiverTab?.ToolTip?.ToString() ?? ""));

        foreach (var rahmen in pane.FindDescendants<Border>())
        {
            if (rahmen.Name == "ContentPanel")
                rahmen.BorderThickness = familie ? new Thickness(1) : new Thickness(0);
        }
    }

    #endregion

    #region Hilfsmethoden

    /// <summary>
    /// True, wenn der Dokumentname eine Familie ist: Endung .rfa oder, bei
    /// einer neuen ungespeicherten Familie, der Titel, den Revit vergibt
    /// (tuerkisch "Aile1", deutsch "Familie1", englisch "Family1"). Der Titel
    /// zaehlt nur OHNE Dateiendung: ein gespeichertes Projekt, das zufaellig
    /// "Aile..." heisst, hat eine Endung und wird nicht verwechselt.
    /// </summary>
    internal static bool IstFamilie(string dokumentName)
    {
        if (string.IsNullOrEmpty(dokumentName)) return false;
        if (dokumentName.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase)) return true;

        return string.IsNullOrEmpty(Path.GetExtension(dokumentName))
            && (dokumentName.StartsWith("Aile", StringComparison.OrdinalIgnoreCase)
                || dokumentName.StartsWith("Familie", StringComparison.OrdinalIgnoreCase)
                || dokumentName.StartsWith("Family", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Dokumentname aus dem Tab-ToolTip: "Name.rvt - Typ: Ansicht" ergibt "Name.rvt".
    /// </summary>
    internal static string DokumentName(string tooltip)
    {
        if (string.IsNullOrEmpty(tooltip)) return "";
        int idx = tooltip.IndexOf(" - ", StringComparison.Ordinal);
        return idx > 0 ? tooltip[..idx].Trim() : tooltip.Trim();
    }

    private int FarbIndex(string dokumentName)
    {
        if (!_farbIndex.TryGetValue(dokumentName, out int index))
        {
            index = _naechsterIndex++;
            _farbIndex[dokumentName] = index;
        }
        return index;
    }

    #endregion
}
