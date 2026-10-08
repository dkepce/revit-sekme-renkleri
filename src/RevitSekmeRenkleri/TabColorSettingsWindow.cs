using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitSekmeRenkleri.Ui;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using Ellipse = System.Windows.Shapes.Ellipse;
using Grid = System.Windows.Controls.Grid;
using Orientation = System.Windows.Controls.Orientation;

namespace RevitSekmeRenkleri;

/// <summary>
/// Einstellungsfenster, nicht-modal, mit eigenem Hell/Dunkel-Thema (Ui/Theme.cs)
/// nach Revits Oberflaeche. Jede Aenderung gilt sofort - der Takt der App liest
/// die Einstellungen bei jedem Durchlauf neu, das Fenster ruft nur
/// <see cref="TabColorSettings.Uebernehmen"/>. Die Revit-API wird hier nicht
/// angefasst, deshalb braucht es keinen ExternalEvent.
///
/// Im Code statt in XAML gebaut: die Felder der Palette entstehen aus einer
/// Schleife, und so bleibt alles in einer Datei. Die Stile kommen ueber
/// <see cref="Theme.Apply"/>.
/// </summary>
public class TabColorSettingsWindow : Window
{
    private static TabColorSettingsWindow _offen;

    private readonly StackPanel _inhalt = new() { Margin = new Thickness(16, 12, 16, 12) };
    private bool _baueAuf;

    private static readonly (string Name, string Text)[] Stile =
    {
        ("Revit varsayılanı", "Hiçbir değişiklik yapılmaz, Revit'in kendi sekmeleri"),
        ("Köşeli",          "Düz görünüm: projeler dolgulu, aileler çerçeveli"),
        ("Yuvarlak köşeli",     "Üst köşeler yuvarlak, sekmeler arasında küçük boşluk"),
        ("Oval",          "Tamamen yuvarlak, birbirinden ayrı duran sekmeler"),
        ("Renk çubuklu",     "Hafif renk tonu, altta renkli çubuk"),
        ("Alt çizgili",    "Düz görünüm, altta kalın çizgi"),
        ("Yan şeritli", "Nötr zemin, solda renkli şerit"),
        ("Bitişik bölümler",       "Tek parça bir çubuk, yalnızca uçlar yuvarlak"),
        ("Yumuşak",          "Hafif renk tonu ve ince kenar çizgisi"),
        ("Paralelkenar", "Tüm sekmeler aynı açıyla eğik, köşeler keskin"),
    };

    private static readonly string[] Markierungen = { "Dolgulu", "Çerçeveli (dolgusuz)" };

    private TabColorSettingsWindow()
    {
        Title = "Sekme Renkleri";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height * 0.92;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        // Stile und Hell/Dunkel nach Revit - vor dem ersten Layout.
        Theme.Apply(this);

        var wurzel = new Grid();
        wurzel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        wurzel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        wurzel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        wurzel.Children.Add(Kopf());

        var rolle = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _inhalt
        };
        Grid.SetRow(rolle, 1);
        wurzel.Children.Add(rolle);

        var fuss = Fuss();
        Grid.SetRow(fuss, 2);
        wurzel.Children.Add(fuss);

        Content = wurzel;

        Aufbauen();
        // Die Stil-Bilder folgen den anderen Einstellungen (Markierung, Rahmen ...).
        // Gebuendelt, damit ein Regler nicht bei jedem Schritt neun Bilder zeichnet.
        var vorschauUhr = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        vorschauUhr.Tick += (_, _) =>
        {
            vorschauUhr.Stop();
            AktualisiereVorschauen();
        };
        void BeiAenderung() { if (!_baueAuf) { vorschauUhr.Stop(); vorschauUhr.Start(); } }
        TabColorSettings.Geaendert += BeiAenderung;

        Closed += (_, _) =>
        {
            TabColorSettings.Geaendert -= BeiAenderung;
            vorschauUhr.Stop();
            _offen = null;
        };
    }

    // ----------------------------------------------------------------
    //  Stil-Vorschau
    // ----------------------------------------------------------------

    private List<StilEintrag> _stilEintraege = new();

    private sealed class StilEintrag : System.ComponentModel.INotifyPropertyChanged
    {
        private ImageSource _vorschau;

        public StilEintrag(string name, TabStil stil) { Name = name; Stil = stil; }

        public string Name { get; }
        public TabStil Stil { get; }

        public ImageSource Vorschau
        {
            get => _vorschau;
            set
            {
                _vorschau = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Vorschau)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        // Die ComboBox zeigt den ausgewaehlten Eintrag ueber ToString.
        public override string ToString() => Name;
    }

    private void AktualisiereVorschauen()
    {
        foreach (var eintrag in _stilEintraege)
            eintrag.Vorschau = MiniVorschau(eintrag.Stil);
    }

    /// <summary>
    /// Drei kleine Tabs im gegebenen Stil: Projekt aktiv, Projekt inaktiv und
    /// Familie. Gleiche Rechnung wie fuer die echten Tabs, nur Rahmen und
    /// Rundung auf Vorschaugroesse begrenzt.
    /// </summary>
    private static ImageSource MiniVorschau(TabStil stil)
    {
        try
        {
            var s = TabColorSettings.Aktuell.Kopie();
            s.Stil = stil;
            bool dunkel = Theme.IsDark;

            const double H = 20, W = 27;
            var reihe = new StackPanel { Orientation = Orientation.Horizontal, Height = H + 2 };
            var beispiele = new[] { (familie: false, aktiv: true), (familie: false, aktiv: false), (familie: true, aktiv: false) };

            for (int i = 0; i < beispiele.Length; i++)
            {
                var (familie, aktiv) = beispiele[i];
                var farbe = familie ? TabColorPalette.FamilienFarbe(1) : TabColorPalette.ProjektFarbe(0);

                var tab = new Border { Width = W, Height = H, VerticalAlignment = VerticalAlignment.Bottom };

                if (stil == TabStil.Parallelogramm)
                {
                    // Gezeichnete Form, gleiche Rechnung wie fuer die echten Tabs.
                    var look = TabStilRechner.Berechne(s, farbe, familie, aktiv, H, i, beispiele.Length, dunkel);
                    bool flaeche = look.Hintergrund != null;
                    tab.Child = new System.Windows.Shapes.Path
                    {
                        Data = TabForm.Parallelogramm(W, H, Math.Min(look.Kontur, 2.5), flaeche),
                        Fill = flaeche ? look.Hintergrund : null,
                        Stroke = flaeche ? null : look.RahmenFarbe,
                        StrokeThickness = flaeche ? 0 : Math.Min(look.Kontur, 2.5),
                        StrokeLineJoin = PenLineJoin.Miter
                    };
                    tab.Margin = new Thickness(0, 0, TabForm.Zwischenraum - TabForm.Versatz(H), 0);
                }
                else if (stil == TabStil.RevitStandard)
                {
                    tab.Background = TabColorPalette.Pinsel(Colors.Gray, aktiv ? 55 : 28);
                    tab.CornerRadius = new CornerRadius(2, 2, 0, 0);
                    tab.Margin = new Thickness(0, 0, 2, 0);
                }
                else
                {
                    var look = TabStilRechner.Berechne(s, farbe, familie, aktiv, H, i, beispiele.Length, dunkel);
                    tab.Background = look.Hintergrund;
                    tab.BorderBrush = look.RahmenFarbe;
                    tab.BorderThickness = new Thickness(
                        Math.Min(look.Rahmen.Left, 2.5), Math.Min(look.Rahmen.Top, 2.5),
                        Math.Min(look.Rahmen.Right, 2.5), Math.Min(look.Rahmen.Bottom, 2.5));
                    tab.CornerRadius = new CornerRadius(
                        Math.Min(look.Radius.TopLeft, H / 2), Math.Min(look.Radius.TopRight, H / 2),
                        Math.Min(look.Radius.BottomRight, H / 2), Math.Min(look.Radius.BottomLeft, H / 2));
                    tab.Margin = new Thickness(look.Abstand.Left, 0, Math.Min(look.Abstand.Right, 3), 0);
                }
                reihe.Children.Add(tab);
            }

            reihe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            reihe.Arrange(new Rect(reihe.DesiredSize));

            // Mit doppelter Pixeldichte, damit die Bilder auf skalierten Schirmen scharf bleiben.
            var bild = new RenderTargetBitmap(
                (int)Math.Ceiling(reihe.DesiredSize.Width * 2), (int)Math.Ceiling(reihe.DesiredSize.Height * 2),
                192, 192, PixelFormats.Pbgra32);
            bild.Render(reihe);
            bild.Freeze();
            return bild;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Oeffnet das Fenster oder holt das offene nach vorn.</summary>
    internal static void Zeigen(UIApplication app)
    {
        if (_offen != null)
        {
            _offen.Activate();
            return;
        }

        var fenster = new TabColorSettingsWindow();
        RevitWindow.SetOwner(fenster, app); // Besitzer und Position, vor dem Anzeigen

        _offen = fenster;
        fenster.Show();
    }

    // ----------------------------------------------------------------
    //  Rahmen: Kopf und Fuss
    // ----------------------------------------------------------------

    private FrameworkElement Kopf()
    {
        var titel = new TextBlock { Text = "Sekme Renkleri", FontSize = 16, FontWeight = FontWeights.SemiBold };
        titel.SetResourceReference(TextBlock.ForegroundProperty, "HeaderTextBrush");

        var untertitel = new TextBlock
        {
            Text = "Değişiklikler hemen uygulanır ve otomatik kaydedilir",
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0)
        };
        untertitel.SetResourceReference(TextBlock.ForegroundProperty, "HeaderSubtextBrush");

        var kopf = new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Child = new StackPanel { Children = { titel, untertitel } }
        };
        kopf.SetResourceReference(Border.BackgroundProperty, "HeaderBackgroundBrush");
        return kopf;
    }

    private FrameworkElement Fuss()
    {
        var links = new StackPanel { Orientation = Orientation.Horizontal };
        links.Children.Add(Knopf("Paleti sıfırla", false, () =>
        {
            TabColorSettings.ZuruecksetzenPalette();
            Aufbauen();
        }));
        links.Children.Add(Knopf("Tümünü sıfırla", false, () =>
        {
            TabColorSettings.ZuruecksetzenAlles();
            Aufbauen();
        }));

        var schliessen = Knopf("Kapat", true, Close);
        schliessen.HorizontalAlignment = HorizontalAlignment.Right;

        var zeile = new DockPanel();
        DockPanel.SetDock(schliessen, Dock.Right);
        zeile.Children.Add(schliessen);
        zeile.Children.Add(links);

        var fuss = new Border
        {
            Padding = new Thickness(16, 10, 16, 10),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = zeile
        };
        fuss.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        fuss.SetResourceReference(Border.BorderBrushProperty, "BorderBrush2");
        return fuss;
    }

    private Button Knopf(string text, bool haupt, Action aktion)
    {
        var knopf = new Button
        {
            Content = text,
            Padding = new Thickness(10, 5, 10, 5),
            FontSize = 11,
            Margin = new Thickness(0, 0, 6, 0),
            Style = (Style)FindResource(haupt ? "PrimaryButtonStyle" : "SecondaryButtonStyle")
        };
        knopf.Click += (_, _) => aktion();
        return knopf;
    }

    // ----------------------------------------------------------------
    //  Inhalt
    // ----------------------------------------------------------------

    private void Aufbauen()
    {
        _baueAuf = true;
        _inhalt.Children.Clear();
        var s = TabColorSettings.Aktuell;

        // ---- Hauptschalter ----
        _inhalt.Children.Add(Schalter("Sekme renkleri açık", "Kapalıyken Revit'in sekmeleri olduğu gibi kalır.",
            s.Aktiv, v => TabColorSettings.Aktuell.Aktiv = v, fett: true));

        // ---- Stil ----
        _inhalt.Children.Add(Beschriftung("Stil"));
        var stil = new ComboBox
        {
            Style = (Style)FindResource("ModernComboBoxStyle"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Jeder Eintrag zeigt ein kleines Bild des Stils, gezeichnet mit den
        // aktuellen Einstellungen (Markierung, Fuellung, Rahmen) - dieselbe
        // Rechnung wie fuer die echten Tabs, also sieht man das Ergebnis.
        _stilEintraege = new List<StilEintrag>();
        for (int i = 0; i < Stile.Length; i++)
            _stilEintraege.Add(new StilEintrag(Stile[i].Name, (TabStil)i));
        AktualisiereVorschauen();

        stil.ItemTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
            "<StackPanel Orientation='Horizontal'>" +
            "<Image Source='{Binding Vorschau}' Width='92' Height='22' Margin='0,2,10,2' VerticalAlignment='Center'/>" +
            "<TextBlock Text='{Binding Name}' VerticalAlignment='Center' TextWrapping='NoWrap'/>" +
            "</StackPanel></DataTemplate>");
        foreach (var eintrag in _stilEintraege) stil.Items.Add(eintrag);
        stil.SelectedIndex = Math.Clamp((int)s.Stil, 0, Stile.Length - 1);

        var stilHinweis = new TextBlock
        {
            Text = Stile[stil.SelectedIndex].Text,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        };
        stilHinweis.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        stil.SelectionChanged += (_, _) =>
        {
            if (_baueAuf || stil.SelectedIndex < 0) return;
            stilHinweis.Text = Stile[stil.SelectedIndex].Text;
            TabColorSettings.Aktuell.Stil = (TabStil)stil.SelectedIndex;
            TabColorSettings.Uebernehmen();
        };
        _inhalt.Children.Add(stil);
        _inhalt.Children.Add(stilHinweis);

        // ---- Markierung: Projekte und Familien getrennt ----
        _inhalt.Children.Add(Beschriftung("İşaretleme"));
        _inhalt.Children.Add(Paar(
            Auswahl("Projeler", Markierungen, (int)s.ProjektDarstellung,
                i => TabColorSettings.Aktuell.ProjektDarstellung = (TabMarkierung)i),
            Auswahl("Aileler", Markierungen, (int)s.FamilieDarstellung,
                i => TabColorSettings.Aktuell.FamilieDarstellung = (TabMarkierung)i)));

        // ---- Darstellung ----
        _inhalt.Children.Add(Beschriftung("Görünüm"));
        _inhalt.Children.Add(Paar(
            Regler("Etkin sekmenin dolgusu (proje)", 0, 100, s.FuellungAktiv, v => v + " %",
                v => TabColorSettings.Aktuell.FuellungAktiv = v),
            Regler("Pasif sekmenin dolgusu (proje)", 0, 100, s.FuellungInaktiv, v => v + " %",
                v => TabColorSettings.Aktuell.FuellungInaktiv = v)));
        _inhalt.Children.Add(Paar(
            Regler("Çerçeve / çubuk kalınlığı", 1, 8, s.Rahmenstaerke, v => v + " px",
                v => TabColorSettings.Aktuell.Rahmenstaerke = v),
            Regler("Köşe yuvarlaklığı", 0, 20, s.Eckenradius, v => v + " px",
                v => TabColorSettings.Aktuell.Eckenradius = v)));
        _inhalt.Children.Add(Paar(
            Regler("Ek sekme yüksekliği", 0, 30, s.TabHoeheZuschlag, v => v == 0 ? "Varsayılan" : $"+{v} px",
                v => TabColorSettings.Aktuell.TabHoeheZuschlag = v),
            null));

        // ---- Gruppierung ----
        _inhalt.Children.Add(Beschriftung("Gruplama"));
        _inhalt.Children.Add(Schalter("Projeye / aileye göre grupla (iki satırlı)",
            "Üst satırda her belge için bir sekme bulunur. Tıklayınca o belgede en son kullanılan görünüm açılır; " +
            "alt satırda yalnızca bu belgenin açık görünümleri listelenir.",
            s.Gruppieren, v => TabColorSettings.Aktuell.Gruppieren = v));
        _inhalt.Children.Add(Schalter("Belge sekmeleri klasör biçiminde",
            "Üst satırdaki sekmelerin sol üstüne, klasör simgesi gibi küçük bir çıkıntı eklenir. " +
            "Yalnızca gruplama açıkken çalışır.",
            s.DokumentOrdner, v => TabColorSettings.Aktuell.DokumentOrdner = v));

        // ---- Vorschau ----
        _inhalt.Children.Add(Beschriftung("Üzerine gelince önizleme"));
        _inhalt.Children.Add(Schalter("Görünümün önizleme resmini göster",
            "Tablolar (schedule) için önizleme yoktur. Bir görünümün ilk resmi birkaç saniye sürebilir; " +
            "sonrasında 30 saniye boyunca önbellekte tutulur.",
            s.VorschauAktiv, v => TabColorSettings.Aktuell.VorschauAktiv = v));
        _inhalt.Children.Add(Schalter("Yalnızca etkin belgenin görünümleri",
            "Önizleme hazırlanırken başka bir belgeye geçilmez. Büyük modellerde önerilir.",
            s.VorschauNurAktivesDokument, v => TabColorSettings.Aktuell.VorschauNurAktivesDokument = v));
        _inhalt.Children.Add(Paar(
            Regler("Görünme gecikmesi", 1, 50, s.VorschauVerzoegerungMs / 100, v => $"{v / 10.0:0.0} sn",
                v => TabColorSettings.Aktuell.VorschauVerzoegerungMs = v * 100),
            Regler("Resim boyutu (uzun kenar)", 200, 800, s.VorschauBreite, v => v + " px",
                v => TabColorSettings.Aktuell.VorschauBreite = v)));

        // ---- Palette ----
        _inhalt.Children.Add(Beschriftung("Renk paleti"));
        var palette = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        for (int i = 0; i < TabColorSettings.PaletteGroesse; i++)
            palette.Children.Add(Farbfeld(i));
        _inhalt.Children.Add(palette);

        var paletteHinweis = new TextBlock
        {
            Text = "Açılan her belge paletteki bir sonraki rengi alır. Aileler paleti tersten okur; böylece aynı sıradaki proje ile aile farklı renk alır. " +
                   "Rengi değiştirmek için ilgili kutuya tıklayın.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        };
        paletteHinweis.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        _inhalt.Children.Add(paletteHinweis);

        _baueAuf = false;
    }

    private TextBlock Beschriftung(string text)
    {
        var t = new TextBlock
        {
            Text = text,
            Style = (Style)FindResource("SectionCaptionStyle"),
            Margin = new Thickness(0, 14, 0, 6)
        };
        return t;
    }

    /// <summary>Schalter im Win11-Stil: Pille mit gleitendem Knopf, Beschriftung rechts.</summary>
    private FrameworkElement Schalter(string text, string hinweis, bool wert, Action<bool> aenderung, bool fett = false)
    {
        var knopf = new Ellipse
        {
            Width = 12, Height = 12,
            Fill = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 0, 0)
        };
        var spur = new Border
        {
            Width = 34, Height = 18,
            CornerRadius = new CornerRadius(9),
            VerticalAlignment = VerticalAlignment.Center,
            Child = knopf
        };

        void Zeichnen()
        {
            // Ein = Akzent, Aus = gedaempftes Grau; beides folgt dem Thema.
            spur.SetResourceReference(Border.BackgroundProperty, wert ? "AccentBrush" : "TextSecondaryBrush");
            knopf.RenderTransform = new TranslateTransform(wert ? 16 : 0, 0);
        }
        Zeichnen();

        var beschriftung = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = fett ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            TextWrapping = TextWrapping.NoWrap
        };

        var zeile = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 3, 0, 3),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = hinweis,
            Children = { spur, beschriftung }
        };

        zeile.MouseLeftButtonUp += (_, _) =>
        {
            wert = !wert;
            Zeichnen();
            if (_baueAuf) return;
            aenderung(wert);
            TabColorSettings.Uebernehmen();
        };
        return zeile;
    }

    /// <summary>Beschriftung und Auswahlliste untereinander.</summary>
    private FrameworkElement Auswahl(string beschriftung, string[] eintraege, int wahl, Action<int> setzen)
    {
        var name = new TextBlock { Text = beschriftung, FontSize = 11, Margin = new Thickness(0, 0, 0, 3) };
        name.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

        var liste = new ComboBox
        {
            Style = (Style)FindResource("ModernComboBoxStyle"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var e in eintraege) liste.Items.Add(e);
        liste.SelectedIndex = Math.Clamp(wahl, 0, eintraege.Length - 1);
        liste.SelectionChanged += (_, _) =>
        {
            if (_baueAuf || liste.SelectedIndex < 0) return;
            setzen(liste.SelectedIndex);
            TabColorSettings.Uebernehmen();
        };

        return new StackPanel { Children = { name, liste }, Margin = new Thickness(0, 0, 0, 4) };
    }

    /// <summary>Beschriftung mit Wert rechts, darunter der Regler.</summary>
    private FrameworkElement Regler(string beschriftung, int min, int max, int wert,
        Func<int, string> format, Action<int> setzen)
    {
        var anzeige = new TextBlock { Text = format(wert), FontSize = 11, TextAlignment = TextAlignment.Right };
        anzeige.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        var name = new TextBlock { Text = beschriftung, FontSize = 11, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
        name.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

        var kopf = new DockPanel();
        DockPanel.SetDock(anzeige, Dock.Right);
        kopf.Children.Add(anzeige);
        kopf.Children.Add(name);

        var regler = new Slider
        {
            Style = (Style)FindResource("KompaktSliderStyle"),
            Minimum = min,
            Maximum = max,
            Value = wert,
            TickFrequency = 1,
            IsSnapToTickEnabled = true
        };
        regler.ValueChanged += (_, e) =>
        {
            int v = (int)Math.Round(e.NewValue);
            anzeige.Text = format(v);
            if (_baueAuf) return;
            setzen(v);
            TabColorSettings.Uebernehmen();
        };

        return new StackPanel { Children = { kopf, regler }, Margin = new Thickness(0, 0, 0, 6) };
    }

    /// <summary>Zwei Felder nebeneinander statt untereinander (kompakt).</summary>
    private static FrameworkElement Paar(FrameworkElement links, FrameworkElement rechts)
    {
        var gitter = new Grid();
        gitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        gitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(links, 0);
        gitter.Children.Add(links);

        if (rechts != null)
        {
            Grid.SetColumn(rechts, 2);
            gitter.Children.Add(rechts);
        }
        return gitter;
    }

    /// <summary>Ein Palettenfeld: Klick oeffnet den Farbwaehler.</summary>
    private FrameworkElement Farbfeld(int slot)
    {
        string hex = TabColorSettings.Aktuell.Palette[slot];
        TabColorPalette.TryParse(hex, out var farbe);

        var nummer = new TextBlock
        {
            Text = (slot + 1).ToString(),
            FontSize = 9,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        var feld = new Border
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(0, 0, 5, 5),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = nummer
        };
        feld.SetResourceReference(Border.BorderBrushProperty, "BorderBrush2");

        void Zeigen(Color c, string h)
        {
            feld.Background = new SolidColorBrush(c);
            nummer.Foreground = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0 > 0.55 ? Brushes.Black : Brushes.White;
            feld.ToolTip = $"Renk {slot + 1}: {h}\nDeğiştirmek için tıklayın";
        }
        Zeigen(farbe, hex);

        feld.MouseLeftButtonUp += (_, _) =>
        {
            string aktuell = TabColorSettings.Aktuell.Palette[slot];
            string gewaehlt = ColorPicker.Pick(aktuell, this, $"Sekme rengi {slot + 1}");
            if (gewaehlt == null || !TabColorPalette.TryParse(gewaehlt, out var neu)) return;

            TabColorSettings.Aktuell.Palette[slot] = gewaehlt.ToUpperInvariant();
            Zeigen(neu, gewaehlt.ToUpperInvariant());
            TabColorSettings.Uebernehmen();
        };
        return feld;
    }
}

/// <summary>Oeffnet das Einstellungsfenster der Tab-Farben.</summary>
[Transaction(TransactionMode.Manual)]
public class TabColorEinstellungenCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        TabColorSettingsWindow.Zeigen(commandData.Application);
        return Result.Succeeded;
    }
}
