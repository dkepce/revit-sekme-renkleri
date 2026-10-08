using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Windows.Media.Color;
using Visibility = System.Windows.Visibility;

namespace RevitSekmeRenkleri;

/// <summary>
/// Vorschaubild beim Ueberfahren eines Tabs. Nach einer einstellbaren
/// Wartezeit erscheint unter dem Tab ein Popup mit dem Bild der Ansicht.
///
/// Das Bild kommt aus <c>Document.ExportImage</c>. Die Revit-API laesst sich
/// nur aus einem gueltigen API-Kontext aufrufen, deshalb geht die Anfrage ueber
/// einen ExternalEvent; bis das Bild da ist, zeigt das Popup "wird erzeugt".
/// Fertige Bilder werden kurz zwischengespeichert, damit wiederholtes
/// Ueberfahren nicht jedes Mal neu rendert.
/// </summary>
internal static class VorschauDienst
{
    private static readonly TimeSpan CacheDauer = TimeSpan.FromSeconds(30);

    private static readonly ConditionalWeakTable<TabItem, object> _angebunden = new();
    private static readonly Dictionary<string, (BitmapSource Bild, DateTime Zeit)> _cache = new();
    private static readonly AnfrageHandler _handler = new();

    private static ExternalEvent _ereignis;
    private static DispatcherTimer _wartezeit;
    private static TabItem _ziel;
    private static string _aktuellerSchluessel;

    private static Popup _popup;
    private static Image _bild;
    private static TextBlock _titel;
    private static TextBlock _status;

    /// <summary>Im API-Kontext aufrufen (OnStartup).</summary>
    internal static void Starten()
    {
        _ereignis = ExternalEvent.Create(_handler);
    }

    /// <summary>Wartezeit abbrechen und ein offenes Popup schliessen (Hauptschalter aus).</summary>
    internal static void Verbergen() => Abbrechen();

    internal static void Beenden()
    {
        Abbrechen();
        _ereignis?.Dispose();
        _ereignis = null;
        _cache.Clear();
    }

    /// <summary>Haengt die Hover-Ereignisse an einen Tab - pro Tab nur einmal.</summary>
    internal static void Anbinden(TabItem tab)
    {
        if (_angebunden.TryGetValue(tab, out _)) return;
        _angebunden.Add(tab, new object());

        tab.MouseEnter += (_, _) => BeiBetreten(tab);
        tab.MouseLeave += (_, _) => Abbrechen();
        tab.PreviewMouseDown += (_, _) => Abbrechen();
    }

    // ----------------------------------------------------------------
    //  Wartezeit und Popup
    // ----------------------------------------------------------------

    private static void BeiBetreten(TabItem tab)
    {
        Abbrechen();

        var s = TabColorSettings.Aktuell;
        if (!s.Aktiv || !s.VorschauAktiv || _ereignis == null) return;

        _ziel = tab;
        _wartezeit = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(s.VorschauVerzoegerungMs) };
        _wartezeit.Tick += (_, _) =>
        {
            _wartezeit.Stop();
            if (_ziel == tab && tab.IsMouseOver) Anzeigen(tab);
        };
        _wartezeit.Start();
    }

    private static void Abbrechen()
    {
        _wartezeit?.Stop();
        _wartezeit = null;
        _ziel = null;
        _aktuellerSchluessel = null;
        if (_popup != null) _popup.IsOpen = false;
    }

    private static void Anzeigen(TabItem tab)
    {
        string tooltip = tab.ToolTip?.ToString();
        if (string.IsNullOrEmpty(tooltip)) return;

        string dokument = ViewTabColorizer.DokumentName(tooltip);
        string ansicht = AnsichtName(tooltip);
        if (string.IsNullOrEmpty(dokument) || string.IsNullOrEmpty(ansicht)) return;

        string schluessel = dokument + "|" + ansicht;
        _aktuellerSchluessel = schluessel;

        PopupVorbereiten(tab, ansicht);

        if (_cache.TryGetValue(schluessel, out var treffer) && DateTime.Now - treffer.Zeit < CacheDauer)
        {
            BildSetzen(treffer.Bild, null);
            return;
        }

        _status.Text = "Önizleme hazırlanıyor …";
        _status.Visibility = Visibility.Visible;
        _bild.Source = null;
        _bild.Visibility = Visibility.Collapsed;
        _popup.IsOpen = true;

        _handler.Anfrage = new Anfrage(dokument, ansicht, schluessel, TabColorSettings.Aktuell.VorschauBreite);
        _ereignis.Raise();
    }

    private static void PopupVorbereiten(TabItem tab, string ansicht)
    {
        if (_popup == null)
        {
            _titel = new TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6), TextTrimming = TextTrimming.CharacterEllipsis };
            _status = new TextBlock { Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 2) };
            _bild = new Image { Stretch = Stretch.Uniform, SnapsToDevicePixels = true };

            var inhalt = new StackPanel { Children = { _titel, _status, _bild } };
            var rahmen = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x9a, 0x9a, 0x9a)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 8, 8),
                Child = inhalt,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8, ShadowDepth = 2, Opacity = 0.35
                }
            };

            _popup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                Placement = PlacementMode.Bottom,
                VerticalOffset = 2,
                IsHitTestVisible = false,
                Child = rahmen
            };
        }

        _popup.IsOpen = false;
        _popup.PlacementTarget = tab;
        _titel.Text = ansicht;
        ((Border)_popup.Child).Width = TabColorSettings.Aktuell.VorschauBreite + 22;
    }

    private static void BildSetzen(BitmapSource bild, string fehler)
    {
        if (_popup == null) return;

        if (bild != null)
        {
            _bild.Source = bild;
            _bild.Visibility = Visibility.Visible;
            _status.Visibility = Visibility.Collapsed;
        }
        else
        {
            _bild.Source = null;
            _bild.Visibility = Visibility.Collapsed;
            _status.Text = fehler ?? "Bu görünüm için önizleme yok.";
            _status.Visibility = Visibility.Visible;
        }
        _popup.IsOpen = true;
    }

    /// <summary>Vom Handler aufgerufen, sobald das Bild (oder ein Fehler) vorliegt.</summary>
    private static void Ergebnis(string schluessel, BitmapSource bild, string fehler)
    {
        if (bild != null) _cache[schluessel] = (bild, DateTime.Now);

        // Maus ist inzwischen woanders: nichts mehr anzeigen.
        if (_aktuellerSchluessel != schluessel) return;
        BildSetzen(bild, fehler);
    }

    /// <summary>
    /// Ansichtsname aus dem Tab-ToolTip. "Haus.rvt - Grundriss: EG" ergibt "EG".
    /// Der Ansichtstyp davor ist Revit-Sprache und kein Teil des Namens.
    /// </summary>
    private static string AnsichtName(string tooltip)
    {
        int trenner = tooltip.IndexOf(" - ", StringComparison.Ordinal);
        if (trenner < 0) return "";

        string rest = tooltip[(trenner + 3)..].Trim();
        int doppelpunkt = rest.IndexOf(": ", StringComparison.Ordinal);
        return doppelpunkt >= 0 ? rest[(doppelpunkt + 2)..].Trim() : rest;
    }

    // ----------------------------------------------------------------
    //  Revit-Seite
    // ----------------------------------------------------------------

    private sealed record Anfrage(string Dokument, string Ansicht, string Schluessel, int Pixel);

    private sealed class AnfrageHandler : IExternalEventHandler
    {
        internal Anfrage Anfrage;

        public string GetName() => "Sekme Renkleri Onizleme";

        public void Execute(UIApplication app)
        {
            var anfrage = Anfrage;
            if (anfrage == null) return;

            // Rendert die Vorschau eine Ansicht eines anderen Dokuments, macht
            // Revit dieses Dokument aktiv. Den vorherigen Tab merken, danach
            // wiederherstellen.
            var vorher = ViewTabColorizer.AktiveSeite();

            BitmapSource bild = null;
            string fehler = null;
            try
            {
                bild = Erzeugen(app, anfrage, out fehler);
            }
            catch (Exception ex)
            {
                fehler = "Önizleme oluşturulamadı.";
                Debug.WriteLine($"[RevitSekmeRenkleri] Vorschau: {ex.Message}");
            }

            Ergebnis(anfrage.Schluessel, bild, fehler);
            AktivenTabSichern(vorher);
        }

        /// <summary>
        /// Revit stellt den Wechsel des aktiven Dokuments erst nach dem
        /// Ereignis fertig; deshalb nach Leerlauf und noch einmal kurz
        /// danach pruefen und bei Bedarf zurueckschalten.
        /// </summary>
        private static void AktivenTabSichern(Xceed.Wpf.AvalonDock.Layout.LayoutDocument vorher)
        {
            if (vorher == null) return;

            void Pruefen()
            {
                var jetzt = ViewTabColorizer.AktiveSeite();
                if (jetzt != vorher) ViewTabColorizer.SeiteWiederherstellen(vorher);
            }

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Pruefen);

            var nachlauf = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            nachlauf.Tick += (_, _) =>
            {
                nachlauf.Stop();
                Pruefen();
            };
            nachlauf.Start();
        }

        /// <summary>
        /// Stimmt die Ansicht mit dem Text im Tab-ToolTip ueberein? Plaene
        /// (ViewSheet, "Plan:" im ToolTip) heissen im Tab "Nummer - Name",
        /// ihr Name enthaelt aber nur den Namen - ein Vergleich mit dem Namen
        /// allein fand sie nie ("Ansicht nicht gefunden").
        /// </summary>
        private static bool PasstZumTab(View v, string tabText)
        {
            if (v.Name == tabText) return true;
            if (v is ViewSheet blatt && $"{blatt.SheetNumber} - {blatt.Name}" == tabText) return true;
            return v.Title == tabText;
        }

        private static BitmapSource Erzeugen(UIApplication app, Anfrage anfrage, out string fehler)
        {
            fehler = null;

            string gesucht = Path.GetFileNameWithoutExtension(anfrage.Dokument);
            Document doc = null;
            foreach (Document d in app.Application.Documents)
            {
                if (d.IsLinked) continue;
                if (string.Equals(d.Title, gesucht, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(d.Title, anfrage.Dokument, StringComparison.OrdinalIgnoreCase))
                {
                    doc = d;
                    break;
                }
            }
            if (doc == null) { fehler = "Belge bulunamadı."; return null; }

            // Einstellung: kein Dokumentwechsel, also nur das aktive Dokument.
            if (TabColorSettings.Aktuell.VorschauNurAktivesDokument
                && !(app.ActiveUIDocument != null && app.ActiveUIDocument.Document.Equals(doc)))
            {
                fehler = "Önizleme yalnızca etkin belge için gösterilir (ayarlardan değiştirilebilir).";
                return null;
            }

            // Offene Ansichten des aktiven Dokuments bevorzugen: gleiche Namen
            // bei verschiedenen Ansichtstypen (Grundriss "EG", Schnitt "EG").
            var offen = new HashSet<ElementId>();
            if (app.ActiveUIDocument != null && app.ActiveUIDocument.Document.Equals(doc))
                foreach (var uiAnsicht in app.ActiveUIDocument.GetOpenUIViews())
                    offen.Add(uiAnsicht.ViewId);

            var kandidaten = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && PasstZumTab(v, anfrage.Ansicht))
                .ToList();

            var ansicht = kandidaten.FirstOrDefault(v => offen.Contains(v.Id)) ?? kandidaten.FirstOrDefault();
            if (ansicht == null) { fehler = "Görünüm bulunamadı."; return null; }

            if (ansicht is ViewSchedule || ansicht.ViewType == ViewType.Schedule)
            {
                fehler = "Tablolar için önizleme yok.";
                return null;
            }

            string ordner = Path.Combine(Path.GetTempPath(), "RevitSekmeRenkleri_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ordner);
            try
            {
                var optionen = new ImageExportOptions
                {
                    FilePath = Path.Combine(ordner, "vorschau"),
                    ExportRange = ExportRange.SetOfViews,
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ShadowViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_150,
                    ZoomType = ZoomFitType.FitToPage,
                    FitDirection = FitDirectionType.Horizontal,
                    PixelSize = anfrage.Pixel
                };
                optionen.SetViewsAndSheets(new List<ElementId> { ansicht.Id });

                doc.ExportImage(optionen);

                string datei = Directory.GetFiles(ordner, "*.png").FirstOrDefault();
                if (datei == null) { fehler = "Bu görünüm için önizleme yok."; return null; }

                var bild = new BitmapImage();
                bild.BeginInit();
                bild.CacheOption = BitmapCacheOption.OnLoad; // Datei danach freigeben
                bild.UriSource = new Uri(datei);
                bild.EndInit();
                bild.Freeze();
                return bild;
            }
            finally
            {
                try { Directory.Delete(ordner, true); } catch { /* Temp, wird beim naechsten Mal ignoriert */ }
            }
        }
    }
}
