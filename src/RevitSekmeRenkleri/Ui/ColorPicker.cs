using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitSekmeRenkleri.Ui;

/// <summary>
/// Kleiner Farbwaehler: Feld fuer Saettigung und Helligkeit, Farbtonleiste,
/// Hex-Eingabe und Vergleich alt/neu. Alles schlichtes WPF - das Feld ist ein
/// farbiges Rechteck unter einem Weiss-zu-transparent- und einem
/// Transparent-zu-Schwarz-Verlauf, und genau das ist HSV; es braucht kein
/// Pixelbild.
/// </summary>
internal sealed class ColorPicker : Window
{
    private const double FeldBreite = 240, FeldHoehe = 150, LeisteHoehe = 16;

    private readonly Color _alt;
    private double _farbton;      // 0-360
    private double _saettigung;   // 0-1
    private double _helligkeit;   // 0-1
    private bool _aendert;        // Hex-Feld und Regler aktualisieren sich nicht gegenseitig

    private readonly SolidColorBrush _farbtonBrush = new();
    private readonly Ellipse _feldGriff;
    private readonly Border _leistenGriff;
    private readonly TextBox _hex;
    private readonly Border _neuFeld;

    /// <summary>Gewaehlte Farbe als #RRGGBB, oder null bei Abbruch.</summary>
    public static string Pick(string startHex, Window besitzer, string untertitel = "")
    {
        if (!TryParseHex(startHex, out var start)) start = Color.FromRgb(128, 128, 128);

        var dialog = new ColorPicker(start, untertitel ?? "");
        if (besitzer != null) dialog.Owner = besitzer;
        return dialog.ShowDialog() == true ? Hex(dialog.Aktuell) : null;
    }

    private Color Aktuell => VonHsv(_farbton, _saettigung, _helligkeit);

    private ColorPicker(Color start, string untertitel)
    {
        _alt = start;
        Title = "Renk seç";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Apply(this);

        (_farbton, _saettigung, _helligkeit) = NachHsv(start);

        // ---- Kopf ----
        var titel = new TextBlock { Text = "Renk seç", FontSize = 16, FontWeight = FontWeights.SemiBold };
        titel.SetResourceReference(TextBlock.ForegroundProperty, "HeaderTextBrush");
        var unter = new TextBlock { Text = untertitel, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) };
        unter.SetResourceReference(TextBlock.ForegroundProperty, "HeaderSubtextBrush");
        var kopf = new Border { Padding = new Thickness(16, 12, 16, 12), Child = new StackPanel { Children = { titel, unter } } };
        kopf.SetResourceReference(Border.BackgroundProperty, "HeaderBackgroundBrush");

        // ---- Feld: Saettigung (waagerecht) und Helligkeit (senkrecht) ----
        var feld = new Grid { Width = FeldBreite, Height = FeldHoehe, Cursor = Cursors.Cross, ClipToBounds = true };
        feld.Children.Add(new Rectangle { Fill = _farbtonBrush });
        feld.Children.Add(new Rectangle
        {
            Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0)
        });
        feld.Children.Add(new Rectangle
        {
            Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90)
        });
        _feldGriff = new Ellipse { Width = 14, Height = 14, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
        var feldBrett = new Canvas { IsHitTestVisible = false };
        feldBrett.Children.Add(_feldGriff);
        feld.Children.Add(feldBrett);

        var feldRand = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = feld,
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        feldRand.SetResourceReference(Border.BorderBrushProperty, "BorderBrush2");
        feldRand.Clip = new RectangleGeometry(new Rect(0, 0, FeldBreite + 2, FeldHoehe + 2), 6, 6);

        // ---- Farbtonleiste ----
        var regenbogen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++)
            regenbogen.GradientStops.Add(new GradientStop(VonHsv(i * 60.0, 1, 1), i / 6.0));

        var leiste = new Grid { Width = FeldBreite, Height = LeisteHoehe, Cursor = Cursors.Hand, Background = regenbogen };
        _leistenGriff = new Border
        {
            Width = 6,
            Height = LeisteHoehe + 4,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        leiste.Children.Add(_leistenGriff);
        var leistenRand = new Border
        {
            CornerRadius = new CornerRadius(4),
            Child = leiste,
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 14),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // ---- Hex und Vergleich alt/neu ----
        _hex = new TextBox
        {
            Width = 92,
            Padding = new Thickness(6, 4, 6, 4),
            FontFamily = new FontFamily("Consolas"),
            VerticalContentAlignment = VerticalAlignment.Center,
            CharacterCasing = CharacterCasing.Upper
        };
        _hex.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");
        _hex.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
        _hex.SetResourceReference(Control.BorderBrushProperty, "BorderBrush2");
        _hex.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, "TextPrimaryBrush");

        Border Muster(Color c)
        {
            var b = new Border
            {
                Width = 44, Height = 28, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(c)
            };
            b.SetResourceReference(Border.BorderBrushProperty, "BorderBrush2");
            return b;
        }
        var altFeld = Muster(_alt);
        _neuFeld = Muster(_alt);

        var vergleich = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 0, 0) };
        vergleich.Children.Add(altFeld);
        vergleich.Children.Add(_neuFeld);

        var hexZeile = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        hexZeile.Children.Add(new TextBlock { Text = "Hex", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), FontSize = 11 });
        hexZeile.Children.Add(_hex);
        hexZeile.Children.Add(vergleich);

        // ---- Knoepfe ----
        Button Knopf(string text, bool haupt, bool abbrechen, Action aktion)
        {
            var k = new Button
            {
                Content = text, Padding = new Thickness(14, 5, 14, 5), FontSize = 11, Margin = new Thickness(8, 0, 0, 0),
                IsCancel = abbrechen, IsDefault = haupt,
                Style = (Style)FindResource(haupt ? "PrimaryButtonStyle" : "SecondaryButtonStyle")
            };
            k.Click += (_, _) => aktion();
            return k;
        }
        var knoepfe = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        knoepfe.Children.Add(Knopf("İptal", false, true, () => DialogResult = false));
        knoepfe.Children.Add(Knopf("Tamam", true, false, () => DialogResult = true));

        var inhalt = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
        inhalt.Children.Add(feldRand);
        inhalt.Children.Add(leistenRand);
        inhalt.Children.Add(hexZeile);
        inhalt.Children.Add(knoepfe);

        Content = new StackPanel { Children = { kopf, inhalt } };

        // ---- Ziehen mit der Maus ----
        void FeldZiehen(MouseEventArgs e)
        {
            var p = e.GetPosition(feld);
            _saettigung = Math.Clamp(p.X / FeldBreite, 0, 1);
            _helligkeit = 1 - Math.Clamp(p.Y / FeldHoehe, 0, 1);
            Aktualisieren(vonHex: false);
        }
        feld.MouseLeftButtonDown += (_, e) => { feld.CaptureMouse(); FeldZiehen(e); };
        feld.MouseMove += (_, e) => { if (feld.IsMouseCaptured) FeldZiehen(e); };
        feld.MouseLeftButtonUp += (_, _) => feld.ReleaseMouseCapture();

        void LeisteZiehen(MouseEventArgs e)
        {
            _farbton = Math.Clamp(e.GetPosition(leiste).X / FeldBreite, 0, 1) * 360;
            Aktualisieren(vonHex: false);
        }
        leiste.MouseLeftButtonDown += (_, e) => { leiste.CaptureMouse(); LeisteZiehen(e); };
        leiste.MouseMove += (_, e) => { if (leiste.IsMouseCaptured) LeisteZiehen(e); };
        leiste.MouseLeftButtonUp += (_, _) => leiste.ReleaseMouseCapture();

        _hex.TextChanged += (_, _) =>
        {
            if (_aendert) return;
            if (!TryParseHex(_hex.Text, out var c)) return;
            (_farbton, _saettigung, _helligkeit) = NachHsv(c);
            Aktualisieren(vonHex: true);
        };

        Aktualisieren(vonHex: false);
    }

    /// <summary>Zeigt den Stand in allen Elementen. Die Hex-Eingabe wird dabei nicht ueberschrieben, solange sie getippt wird.</summary>
    private void Aktualisieren(bool vonHex)
    {
        _aendert = true;
        try
        {
            _farbtonBrush.Color = VonHsv(_farbton, 1, 1);
            Canvas.SetLeft(_feldGriff, _saettigung * FeldBreite - 7);
            Canvas.SetTop(_feldGriff, (1 - _helligkeit) * FeldHoehe - 7);
            _leistenGriff.Margin = new Thickness(_farbton / 360 * (FeldBreite - 6), 0, 0, 0);

            var c = Aktuell;
            _neuFeld.Background = new SolidColorBrush(c);
            if (!vonHex) _hex.Text = Hex(c);
        }
        finally { _aendert = false; }
    }

    // ---- Farbrechnung ----

    private static (double H, double S, double V) NachHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;

        double h = d == 0 ? 0
            : max == r ? 60 * (((g - b) / d) % 6)
            : max == g ? 60 * (((b - r) / d) + 2)
            : 60 * (((r - g) / d) + 4);
        if (h < 0) h += 360;

        return (h, max == 0 ? 0 : d / max, max);
    }

    private static Color VonHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    public static bool TryParseHex(string hex, out Color farbe)
    {
        farbe = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        string t = hex.Trim().TrimStart('#');
        if (t.Length != 6 || !int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int wert)) return false;
        farbe = Color.FromRgb((byte)(wert >> 16), (byte)(wert >> 8), (byte)wert);
        return true;
    }

    public static string Hex(Color c) => string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
}
