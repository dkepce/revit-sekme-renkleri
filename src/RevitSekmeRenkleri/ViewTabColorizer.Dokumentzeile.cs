using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RevitSekmeRenkleri.Ui;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace RevitSekmeRenkleri;

/// <summary>
/// Zweizeilige Tab-Leiste: oben ein Tab je Dokument (Projekt oder Familie),
/// darunter Revits eigene Tab-Zeile - gefiltert auf die offenen Ansichten des
/// gewaehlten Dokuments.
///
/// "Gewaehlt" ist immer das Dokument des aktiven Tabs. Ein Klick auf einen
/// Dokument-Tab aktiviert dessen zuletzt benutzte Ansicht; damit folgt die
/// untere Zeile von selbst, und ein Wechsel auf anderem Weg (Strg+Tab, neue
/// Ansicht oeffnen) stimmt immer mit der Anzeige ueberein.
///
/// Die obere Zeile wird in Revits Tab-Leiste eingehaengt: ueber dem Behaelter
/// des Tab-Panels, je nach Vorlage in ein Grid (neue Zeile 0), ein DockPanel
/// oder ein senkrechtes StackPanel. Revits Vorlage ist nicht dokumentiert -
/// welcher Weg gegriffen hat, steht in TabColor-Pane.txt.
/// </summary>
public partial class ViewTabColorizer
{
    private enum Einbau { Grid, Dock, Stapel }

    private sealed class Dokumentzeile
    {
        public Border Leiste;
        public Panel Behaelter;
        public Einbau Art;
        public int NeueGridZeilen;
        public string Signatur;
    }

    private readonly Dictionary<LayoutDocumentPaneControl, Dokumentzeile> _zeilen = new();
    /// <summary>Abstand zwischen Dokumentzeile und Tab-Zeile in Pixeln.</summary>
    private const double ZeilenAbstand = 5;

    private readonly Dictionary<string, LayoutDocument> _letzteAnsicht = new();
    private readonly List<string> _dokumentOrdnung = new();

    /// <summary>Zaehler der Klicks in der Dokumentzeile; nur der letzte darf nachpruefen.</summary>
    private int _klickNr;
    private bool _paneProtokolliert;

    /// <summary>
    /// Haelt die Dokumentzeile eines Panes auf dem Stand der Einstellungen:
    /// einhaengen, aktualisieren oder wieder entfernen.
    /// </summary>
    private void DokumentzeilePflegen(LayoutDocumentPaneControl pane, bool standardStil)
    {
        try
        {
            var s = TabColorSettings.Aktuell;
            if (!s.Gruppieren)
            {
                ZeileEntfernen(pane);
                return;
            }

            var tabs = pane.FindDescendants<TabItem>();
            if (tabs.Count == 0) return;

            var namen = tabs.Select(t => DokumentName(t.ToolTip?.ToString() ?? "")).ToList();

            // Aktives Dokument und dessen zuletzt benutzte Ansicht merken. Das
            // LayoutDocument kommt vom Tab selbst, nicht aus der Position.
            string aktiv = null;
            for (int i = 0; i < tabs.Count; i++)
            {
                if (!tabs[i].IsSelected) continue;
                aktiv = namen[i];

                var doc = DokumentVon(tabs[i]);
                if (doc != null) _letzteAnsicht[namen[i]] = doc;
            }
            aktiv ??= namen[0];

            // Untere Zeile: nur die Ansichten des aktiven Dokuments.
            for (int i = 0; i < tabs.Count; i++)
                TabAusblenden(tabs[i], namen[i] != aktiv);

            var zeile = Einhaengen(pane, tabs[0]);
            if (zeile == null) return;

            // Feste Reihenfolge der Dokument-Tabs: so, wie sie zuerst gesehen wurden.
            // Die Reihenfolge der TabItems wechselt mit der Auswahl; ohne eigene
            // Ordnung spraengen die Dokument-Tabs beim Klicken.
            _dokumentOrdnung.RemoveAll(d => !namen.Contains(d));
            foreach (var d in namen)
                if (!_dokumentOrdnung.Contains(d)) _dokumentOrdnung.Add(d);
            var reihenfolge = _dokumentOrdnung.ToList();
            string signatur = string.Join("|", reihenfolge.Select(d => d + "*" + namen.Count(n => n == d)))
                + "#" + aktiv + "#" + standardStil + "#" + EinstellungsAbdruck(s);
            if (signatur == zeile.Signatur) return;
            zeile.Signatur = signatur;

            var textvorlage = tabs[0].FindDescendants<TextBlock>().FirstOrDefault(t => t.Name == "mDocumentText");
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var d in reihenfolge)
            {
                panel.Children.Add(Chip(pane, d, namen.Count(n => n == d), d == aktiv, standardStil, s, textvorlage,
                    reihenfolge.IndexOf(d), reihenfolge.Count));
            }
            zeile.Leiste.Child = panel;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Dokumentzeile: {ex.Message}");
        }
    }

    /// <summary>
    /// Tabs, die gerade ausgeblendet sind. Ausgeblendet wird ueber die Breite,
    /// nicht ueber Visibility: Das Tab-Panel von AvalonDock verwaltet die
    /// Visibility seiner Tabs selbst (Ueberlauf) und setzt sie beim naechsten
    /// Layout wieder auf sichtbar zurueck.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TabItem, object> _ausgeblendet = new();

    private static bool IstAusgeblendet(TabItem tab) => _ausgeblendet.TryGetValue(tab, out _);

    private static void TabAusblenden(TabItem tab, bool aus)
    {
        bool schon = IstAusgeblendet(tab);

        if (aus)
        {
            if (!schon) _ausgeblendet.Add(tab, new object());

            // Jeden Takt neu setzen ist billig: gleiche Werte loesen nichts aus.
            tab.Width = 0;
            tab.MinWidth = 0;
            tab.MaxWidth = 0;
            tab.Margin = new Thickness(0);
            tab.ClipToBounds = true;
            tab.IsHitTestVisible = false;
        }
        else if (schon)
        {
            _ausgeblendet.Remove(tab);
            tab.ClearValue(FrameworkElement.WidthProperty);
            tab.ClearValue(FrameworkElement.MinWidthProperty);
            tab.ClearValue(FrameworkElement.MaxWidthProperty);
            tab.ClearValue(FrameworkElement.MarginProperty); // Stil setzt sie im naechsten Takt neu
            tab.ClearValue(UIElement.ClipToBoundsProperty);
            tab.ClearValue(UIElement.IsHitTestVisibleProperty);
        }
    }

    private static string EinstellungsAbdruck(TabColorSettings s)
        => string.Join(",", s.Stil, s.DokumentOrdner, s.ProjektDarstellung, s.FamilieDarstellung, s.FuellungAktiv, s.FuellungInaktiv, s.Rahmenstaerke, s.Eckenradius,
            s.TabHoeheZuschlag, string.Join(";", s.Palette));

    // ----------------------------------------------------------------
    //  Dokument-Tab
    // ----------------------------------------------------------------

    private Border Chip(LayoutDocumentPaneControl pane, string dokument, int anzahl, bool aktiv,
        bool standardStil, TabColorSettings s, TextBlock textvorlage, int pos, int chips)
    {
        double hoehe = (_standardHoehe > 0 ? _standardHoehe : 26) + s.TabHoeheZuschlag;
        bool familie = IstFamilie(dokument);
        int index = FarbIndex(dokument);
        var farbe = familie ? TabColorPalette.FamilienFarbe(index) : TabColorPalette.ProjektFarbe(index);

        Brush text = textvorlage?.Foreground ?? Brushes.White;

        var chip = new Border
        {
            Height = hoehe,
            Padding = new Thickness(10, 0, 10, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Bottom,
            ToolTip = dokument,
            Background = Brushes.Transparent
        };

        TabLook look = null;

        if (standardStil)
        {
            // Revit-Standard: neutrale Chips, die Farben gehoeren zum Stil.
            chip.CornerRadius = new CornerRadius(s.Eckenradius, s.Eckenradius, 0, 0);
            chip.Margin = new Thickness(0, 0, 2, 0);
            chip.Background = TabColorPalette.Pinsel(Colors.Gray, aktiv ? 60 : 25);
        }
        else
        {
            // Dasselbe Aussehen wie die Tabs der unteren Zeile.
            look = TabStilRechner.Berechne(s, farbe, familie, aktiv, hoehe, pos, chips, Theme.IsDark);
            chip.Background = look.Hintergrund ?? Brushes.Transparent;
            chip.BorderThickness = look.Rahmen;
            if (look.RahmenFarbe != null) chip.BorderBrush = look.RahmenFarbe;
            chip.CornerRadius = look.Radius;
            chip.Margin = new Thickness(look.Abstand.Left, 0, look.Abstand.Right, 0);

            // Helle Grundfarbe unter kraeftiger Flaeche: dunkle Schrift.
            bool flaeche = (familie ? s.FamilieDarstellung : s.ProjektDarstellung) == TabMarkierung.Flaeche;
            if (flaeche && aktiv && s.FuellungAktiv >= 60 && Helligkeit(farbe) > 0.62
                && s.Stil is TabStil.Eckig or TabStil.Abgerundet or TabStil.Pille or TabStil.Segmente)
                text = Brushes.Black;
        }

        // Gezeichnete Form: Parallelogramm und/oder Ordner-Reiter. Beides sind
        // Formen, die sich mit Rahmen und Eckenradius nicht darstellen lassen.
        bool schraeg = look != null && look.Schraege;
        bool ordner = s.DokumentOrdner;
        System.Windows.Shapes.Path formPfad = null;
        Thickness innenRand = new Thickness(0);

        if (schraeg || ordner)
        {
            double gesamt = hoehe + (ordner ? TabForm.OhrHoehe : 0);
            double versatz = schraeg ? TabForm.Versatz(gesamt) : 0;

            bool flaeche;
            Brush fuell = null, strich = null;
            double kontur = 0;
            if (standardStil || look == null)
            {
                flaeche = true;
                fuell = TabColorPalette.Pinsel(Colors.Gray, aktiv ? 60 : 25);
            }
            else
            {
                flaeche = look.Hintergrund != null;
                fuell = look.Hintergrund;
                strich = look.RahmenFarbe;
                kontur = look.Kontur;
            }

            chip.Height = gesamt;
            chip.Background = Brushes.Transparent;
            chip.BorderThickness = new Thickness(0);
            chip.CornerRadius = new CornerRadius(0);
            chip.Padding = new Thickness(0);
            chip.Margin = new Thickness(0, 0, TabForm.Zwischenraum - versatz, 0);

            innenRand = new Thickness(versatz + 8, ordner ? TabForm.OhrHoehe : 0, versatz + 8, 0);

            formPfad = new System.Windows.Shapes.Path
            {
                Fill = flaeche ? fuell : null,
                Stroke = flaeche ? null : strich,
                StrokeThickness = flaeche ? 0 : kontur,
                StrokeLineJoin = PenLineJoin.Miter,
                StrokeMiterLimit = 8,
                IsHitTestVisible = false
            };

            chip.SizeChanged += (_, _) =>
            {
                double w = chip.ActualWidth, h = chip.ActualHeight;
                formPfad.Data = ordner
                    ? TabForm.Ordner(w, h, TabForm.OhrHoehe, Math.Min(46, w * 0.5), schraeg, kontur, flaeche)
                    : TabForm.Parallelogramm(w, h, kontur, flaeche);
            };
        }

        string name = Path.GetFileNameWithoutExtension(dokument);
        if (string.IsNullOrEmpty(name)) name = dokument;

        var inhalt = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        inhalt.Children.Add(new TextBlock
        {
            Text = name,
            Foreground = text,
            FontFamily = textvorlage?.FontFamily ?? new FontFamily("Segoe UI"),
            FontSize = textvorlage?.FontSize ?? 12,
            FontWeight = aktiv ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        });
        inhalt.Children.Add(new TextBlock
        {
            Text = anzahl.ToString(),
            Foreground = text,
            Opacity = 0.65,
            FontFamily = textvorlage?.FontFamily ?? new FontFamily("Segoe UI"),
            FontSize = (textvorlage?.FontSize ?? 12) - 1,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        if (formPfad != null)
        {
            inhalt.Margin = innenRand;
            var gitter = new Grid();
            gitter.Children.Add(formPfad);
            gitter.Children.Add(inhalt);
            chip.Child = gitter;
        }
        else
        {
            chip.Child = inhalt;
        }

        chip.MouseLeftButtonDown += (_, e) =>
        {
            Aktivieren(pane, dokument);
            e.Handled = true;
        };
        return chip;
    }

    /// <summary>
    /// Das aktive Dokument-Tab (Ansicht) im Hauptfenster, sonst null. Dient der
    /// Vorschau, um nach dem Rendern den vorherigen Tab wiederherzustellen.
    /// </summary>
    internal static LayoutDocument AktiveSeite()
    {
        var hauptfenster = UIFramework.MainWindow.getMainWnd();
        if (hauptfenster == null) return null;

        LayoutDocument ersatz = null;
        foreach (var pane in hauptfenster.FindDescendants<LayoutDocumentPaneControl>())
        {
            if (pane.Model is not LayoutDocumentPane modell) continue;
            foreach (var dok in modell.Children.OfType<LayoutDocument>())
            {
                if (dok.IsActive) return dok;
            }
            ersatz ??= modell.SelectedContent as LayoutDocument;
        }
        return ersatz;
    }

    /// <summary>Macht eine zuvor aktive Seite wieder aktiv - falls sie noch offen ist.</summary>
    internal static void SeiteWiederherstellen(LayoutDocument seite)
    {
        try
        {
            if (seite == null || seite.Parent == null) return;
            seite.IsSelected = true;
            seite.IsActive = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Wiederherstellen: {ex.Message}");
        }
    }

    private static double Helligkeit(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    /// <summary>
    /// Das LayoutDocument, das ein Tab darstellt - vom Tab selbst gelesen. Die
    /// Position taugt nicht: Revits Tab-Leiste haelt nicht dieselbe Reihenfolge
    /// wie das AvalonDock-Modell.
    /// </summary>
    internal static LayoutDocument DokumentVon(TabItem tab)
        => tab.FindFirstDescendant<LayoutDocumentTabItem>()?.Model as LayoutDocument
           ?? tab.Content as LayoutDocument
           ?? tab.DataContext as LayoutDocument;

    /// <summary>Aktiviert die zuletzt benutzte Ansicht des Dokuments (sonst die erste).</summary>
    private void Aktivieren(LayoutDocumentPaneControl pane, string dokument)
    {
        try
        {
            var tabs = pane.FindDescendants<TabItem>();

            // Alle Ansichten des Dokuments, mit ihrem Tab.
            var ansichten = new List<(TabItem Tab, LayoutDocument Dok)>();
            foreach (var tab in tabs)
            {
                if (DokumentName(tab.ToolTip?.ToString() ?? "") != dokument) continue;
                var dok = DokumentVon(tab);
                if (dok != null) ansichten.Add((tab, dok));
            }

            if (ansichten.Count == 0)
            {
                TabLog.Schreibe($"Klick {dokument}: keine Ansicht gefunden ({tabs.Count} Tabs)");
                return;
            }

            // Zuletzt benutzte Ansicht, sofern sie noch zu diesem Dokument gehoert.
            LayoutDocument ziel = null;
            if (_letzteAnsicht.TryGetValue(dokument, out var gemerkt) && ansichten.Any(a => a.Dok == gemerkt))
                ziel = gemerkt;
            ziel ??= ansichten[0].Dok;

            // Erst das Layout dem Ziel angleichen. Ein ausgewaehlter Tab mit
            // Breite 0 ist fuer das Tab-Panel von AvalonDock ein unsichtbarer
            // Tab; die Auswahl blieb dann haengen, bis ein weiterer Klick
            // das Panel neu anordnete.
            for (int i = 0; i < tabs.Count; i++)
                TabAusblenden(tabs[i], DokumentName(tabs[i].ToolTip?.ToString() ?? "") != dokument);
            pane.UpdateLayout();

            // Wie ein Klick auf den Tab: Auswahl und Aktivierung.
            ziel.IsSelected = true;
            ziel.IsActive = true;
            TabLog.Schreibe($"Klick {dokument}: '{ziel.Title}' aktiviert");

            NachpruefenAktiv(pane, dokument, ziel, 1, ++_klickNr);
        }
        catch (Exception ex)
        {
            TabLog.Schreibe($"Klick {dokument}: Fehler {ex.Message}");
        }
    }

    /// <summary>
    /// Prueft kurz nach einem Klick, ob das Dokument wirklich aktiv ist, und
    /// aktiviert sonst noch einmal. Revit baut beim Ansichtswechsel Fenster
    /// um und braucht dafuer laenger als ein Takt; ein zweiter Versuch kostet
    /// nichts, wenn es schon geklappt hat.
    /// </summary>
    private void NachpruefenAktiv(LayoutDocumentPaneControl pane, string dokument, LayoutDocument ziel, int versuch, int klickNr)
    {
        var uhr = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        uhr.Tick += (_, _) =>
        {
            uhr.Stop();
            try
            {
                // Ein neuerer Klick gilt: die Pruefung des alten darf ihn nicht
                // mit einer Wiederholung ueberstimmen (gemessen: zwei Klicks im
                // Abstand unter einer Sekunde aktivierten sich gegenseitig neu).
                if (klickNr != _klickNr)
                {
                    TabLog.Schreibe($"Klick {dokument}: uebergangen, neuerer Klick");
                    return;
                }

                string aktiv = null;
                foreach (var tab in pane.FindDescendants<TabItem>())
                    if (tab.IsSelected) aktiv = DokumentName(tab.ToolTip?.ToString() ?? "");

                if (aktiv == dokument)
                {
                    TabLog.Schreibe($"Klick {dokument}: aktiv nach {versuch}. Versuch");
                    return;
                }

                if (versuch >= 2 || ziel.Parent == null)
                {
                    TabLog.Schreibe($"Klick {dokument}: nicht umgeschaltet (aktiv: {aktiv}), gebe auf");
                    return;
                }

                TabLog.Schreibe($"Klick {dokument}: noch nicht aktiv (aktiv: {aktiv}), wiederhole");
                ziel.IsSelected = true;
                ziel.IsActive = true;
                NachpruefenAktiv(pane, dokument, ziel, versuch + 1, klickNr);
            }
            catch (Exception ex)
            {
                TabLog.Schreibe($"Klick {dokument}: Nachpruefung Fehler {ex.Message}");
            }
        };
        uhr.Start();
    }

    // ----------------------------------------------------------------
    //  Einhaengen / Entfernen
    // ----------------------------------------------------------------

    private Dokumentzeile Einhaengen(LayoutDocumentPaneControl pane, TabItem erster)
    {
        if (_zeilen.TryGetValue(pane, out var vorhanden))
        {
            // Vorlage von Revit neu aufgebaut: die Leiste ist weg, neu einhaengen.
            if (vorhanden.Leiste.Parent == vorhanden.Behaelter) return vorhanden;
            _zeilen.Remove(pane);
        }

        if (!_paneProtokolliert)
        {
            _paneProtokolliert = true;
            PaneProtokollieren(pane, erster);
        }

        // Unten ein kleiner Abstand: ohne ihn laufen die Stile der oberen und der
        // unteren Zeile ineinander (Rahmen, Streifen und Flaechen stossen an).
        var leiste = new Border { Padding = new Thickness(0, 2, 0, ZeilenAbstand), Background = Brushes.Transparent };

        // Vom Tab nach oben bis zum ersten Behaelter, in den sich eine Zeile
        // einschieben laesst. Der Behaelter ist der Ort des Tab-Panels.
        DependencyObject knoten = erster;
        while (knoten != null && knoten != pane)
        {
            var eltern = VisualTreeHelper.GetParent(knoten);

            if (eltern is Grid grid && knoten is UIElement ankerG)
            {
                int zeilenVorher = grid.RowDefinitions.Count;
                int neu = zeilenVorher == 0 ? 2 : 1;

                // Bestehende Kinder eine Zeile nach unten, dann Leiste in Zeile 0.
                foreach (UIElement kind in grid.Children)
                    Grid.SetRow(kind, Grid.GetRow(kind) + 1);

                grid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });
                if (zeilenVorher == 0)
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(leiste, 0);
                Panel.SetZIndex(leiste, 10);
                grid.Children.Add(leiste);

                return Merken(pane, leiste, grid, Einbau.Grid, neu);
            }

            if (eltern is DockPanel dock && knoten is UIElement)
            {
                DockPanel.SetDock(leiste, Dock.Top);
                dock.Children.Insert(0, leiste);
                return Merken(pane, leiste, dock, Einbau.Dock, 0);
            }

            if (eltern is StackPanel stapel && stapel.Orientation == Orientation.Vertical && knoten is UIElement anker)
            {
                stapel.Children.Insert(Math.Max(0, stapel.Children.IndexOf(anker)), leiste);
                return Merken(pane, leiste, stapel, Einbau.Stapel, 0);
            }

            knoten = eltern;
        }

        Debug.WriteLine("[RevitSekmeRenkleri] Kein Behaelter fuer die Dokumentzeile gefunden.");
        return null;
    }

    private Dokumentzeile Merken(LayoutDocumentPaneControl pane, Border leiste, Panel behaelter, Einbau art, int grid)
    {
        var zeile = new Dokumentzeile { Leiste = leiste, Behaelter = behaelter, Art = art, NeueGridZeilen = grid };
        _zeilen[pane] = zeile;
        ProtokollAnhaengen($"Dokumentzeile eingehaengt: {art} in {behaelter.GetType().Name}" +
                           (art == Einbau.Grid ? $" ({grid} neue Gridzeile(n))" : ""));
        return zeile;
    }

    private void ZeileEntfernen(LayoutDocumentPaneControl pane)
    {
        if (!_zeilen.TryGetValue(pane, out var zeile)) return;
        _zeilen.Remove(pane);

        try
        {
            var behaelter = zeile.Behaelter;
            if (zeile.Leiste.Parent == behaelter)
            {
                behaelter.Children.Remove(zeile.Leiste);

                if (zeile.Art == Einbau.Grid && behaelter is Grid grid)
                {
                    foreach (UIElement kind in grid.Children)
                        Grid.SetRow(kind, Math.Max(0, Grid.GetRow(kind) - 1));

                    for (int i = 0; i < zeile.NeueGridZeilen && grid.RowDefinitions.Count > 0; i++)
                        grid.RowDefinitions.RemoveAt(0);
                }
            }

            foreach (var tab in pane.FindDescendants<TabItem>())
                TabAusblenden(tab, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Zeile entfernen: {ex.Message}");
        }
    }

    /// <summary>Entfernt alle Dokumentzeilen und blendet alle Tabs wieder ein (Reset).</summary>
    private void AlleZeilenEntfernen()
    {
        foreach (var pane in _zeilen.Keys.ToList())
            ZeileEntfernen(pane);
    }

    // ----------------------------------------------------------------
    //  Diagnose
    // ----------------------------------------------------------------

    private static readonly string PanePfad = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RevitSekmeRenkleri", "TabColor-Pane.txt");

    /// <summary>
    /// Schreibt den Aufbau des Panes (ohne das Innere der Tabs) nach
    /// TabColor-Pane.txt: zeigt, in welchen Behaelter die Dokumentzeile gehoert.
    /// </summary>
    private static void PaneProtokollieren(LayoutDocumentPaneControl pane, TabItem erster)
    {
        try
        {
            var zeilen = new List<string>();
            void Gehe(DependencyObject knoten, int tiefe)
            {
                if (tiefe > 9) return;

                string name = (knoten as FrameworkElement)?.Name;
                string extra = knoten switch
                {
                    Grid g => $"  Zeilen={g.RowDefinitions.Count} Spalten={g.ColumnDefinitions.Count}",
                    DockPanel => "  (DockPanel)",
                    StackPanel sp => $"  {sp.Orientation}",
                    _ => ""
                };
                if (knoten is FrameworkElement fe && knoten is not TabItem)
                    extra += $"  Row={Grid.GetRow(fe)} Hoehe={fe.ActualHeight:0}";

                zeilen.Add($"{new string(' ', tiefe * 2)}{knoten.GetType().FullName}{(string.IsNullOrEmpty(name) ? "" : " [" + name + "]")}{extra}");

                if (knoten is TabItem) return; // Inneres der Tabs steht in TabColor-Sichtbaum.txt
                int n = VisualTreeHelper.GetChildrenCount(knoten);
                for (int i = 0; i < n; i++)
                    Gehe(VisualTreeHelper.GetChild(knoten, i), tiefe + 1);
            }
            Gehe(pane, 0);

            Directory.CreateDirectory(Path.GetDirectoryName(PanePfad)!);
            File.WriteAllLines(PanePfad, zeilen);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RevitSekmeRenkleri] Pane-Protokoll: {ex.Message}");
        }
    }

    private static void ProtokollAnhaengen(string zeile)
    {
        try { File.AppendAllText(PanePfad, "\n" + zeile + "\n"); }
        catch { /* Diagnose, nie kritisch */ }
    }
}
