using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Autodesk.Revit.UI;

namespace RevitSekmeRenkleri.Ui;

/// <summary>
/// Hell/Dunkel nach Revits Oberflaeche, fuer Fenster dieses Add-ins.
///
/// Die Farben liegen als Pinsel in den Ressourcen des Fensters, die Stile
/// (Knoepfe, Auswahlliste, Regler) nehmen sie per DynamicResource. Das Thema
/// wird beim Oeffnen gelesen; wer Revit waehrenddessen umschaltet, sieht es
/// beim naechsten Oeffnen.
/// </summary>
internal static class Theme
{
    /// <summary>True, wenn Revit im dunklen Thema laeuft. Faellt auf hell zurueck.</summary>
    public static bool IsDark
    {
        get
        {
            try { return RevitIstDunkel(); }
            catch { return false; }
        }
    }

    // In eigener Methode, damit ein fehlendes RevitAPIUI.dll (Pruefstand ohne
    // Revit) erst hier und nicht schon beim Uebersetzen des Getters scheitert.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool RevitIstDunkel() => UIThemeManager.CurrentTheme == UITheme.Dark;

    private static readonly Dictionary<string, string> Hell = new()
    {
        ["BackgroundBrush"] = "#FFFFFF",
        ["SurfaceBrush"] = "#F5F6F8",
        ["BorderBrush2"] = "#D5D9E0",
        ["HoverBrush"] = "#EAEEF4",
        ["TextPrimaryBrush"] = "#1F2328",
        ["TextSecondaryBrush"] = "#6B7280",
        ["AccentBrush"] = "#2F6FED",
        ["HeaderBackgroundBrush"] = "#1F2A3C",
        ["HeaderTextBrush"] = "#FFFFFF",
        ["HeaderSubtextBrush"] = "#9FB0C8",
    };

    private static readonly Dictionary<string, string> Dunkel = new()
    {
        ["BackgroundBrush"] = "#2D333D",
        ["SurfaceBrush"] = "#252A32",
        ["BorderBrush2"] = "#485060",
        ["HoverBrush"] = "#3A414D",
        ["TextPrimaryBrush"] = "#F2F4F7",
        ["TextSecondaryBrush"] = "#AEB6C2",
        ["AccentBrush"] = "#4C8DF6",
        ["HeaderBackgroundBrush"] = "#1F242B",
        ["HeaderTextBrush"] = "#FFFFFF",
        ["HeaderSubtextBrush"] = "#9AA4B2",
    };

    /// <summary>Haengt Farben und Stile ans Fenster. Vor dem ersten Layout aufrufen.</summary>
    public static void Apply(FrameworkElement fenster)
    {
        try
        {
            bool dunkel = IsDark;

            foreach (var (schluessel, hex) in dunkel ? Dunkel : Hell)
            {
                var pinsel = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                pinsel.Freeze();
                fenster.Resources[schluessel] = pinsel;
            }

            // Die Stile kommen NACH den Pinseln, verweisen aber nur per
            // DynamicResource auf sie - die Reihenfolge ist egal.
            fenster.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(StilXaml));

            if (fenster is Window w)
            {
                w.SetResourceReference(Window.BackgroundProperty, "BackgroundBrush");
                w.SetResourceReference(Window.ForegroundProperty, "TextPrimaryBrush");
                DunkleTitelleiste(w, dunkel);
            }
        }
        catch
        {
            // Ein Thema ist Zierde: lieber ein haessliches Fenster als keins.
        }
    }

    // ----------------------------------------------------------------
    //  Titelleiste
    // ----------------------------------------------------------------

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int wert, int groesse);

    /// <summary>
    /// Die Titelleiste gehoert zum Fensterrahmen, nicht zum Inhalt - kein Stil
    /// erreicht sie. Windows laesst sie ueber ein DWM-Attribut umschalten
    /// (Nummer 20, vor Windows 10 20H1 die 19). Braucht ein Fensterhandle; das
    /// gibt es erst bei SourceInitialized, ein Aufruf im Konstruktor verpufft.
    /// </summary>
    private static void DunkleTitelleiste(Window fenster, bool dunkel)
    {
        void Setzen()
        {
            try
            {
                var hwnd = new WindowInteropHelper(fenster).Handle;
                if (hwnd == IntPtr.Zero) return;

                int wert = dunkel ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref wert, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref wert, sizeof(int));
            }
            catch { /* eine Titelleiste in der falschen Farbe ist kein Fehler */ }
        }

        if (new WindowInteropHelper(fenster).Handle != IntPtr.Zero) Setzen();
        else fenster.SourceInitialized += (_, _) => Setzen();
    }

    // ----------------------------------------------------------------
    //  Stile
    // ----------------------------------------------------------------

    private const string StilXaml = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <Style x:Key="SectionCaptionStyle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="10"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="TextWrapping" Value="NoWrap"/>
    <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
  </Style>

  <Style x:Key="SecondaryButtonStyle" TargetType="Button">
    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="b" Background="{DynamicResource SurfaceBrush}"
                  BorderBrush="{DynamicResource BorderBrush2}" BorderThickness="1"
                  CornerRadius="6" Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="b" Property="Background" Value="{DynamicResource HoverBrush}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="PrimaryButtonStyle" TargetType="Button">
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="b" Background="{DynamicResource AccentBrush}"
                  CornerRadius="6" Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="b" Property="Opacity" Value="0.88"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="KompaktSliderStyle" TargetType="Slider">
    <Setter Property="IsMoveToPointEnabled" Value="True"/>
    <Setter Property="MinHeight" Value="18"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Slider">
          <Grid Background="Transparent" MinHeight="18">
            <Border Height="4" CornerRadius="2" VerticalAlignment="Center"
                    Background="{DynamicResource BorderBrush2}"/>
            <Track x:Name="PART_Track" VerticalAlignment="Center">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="Slider.DecreaseLarge" Focusable="False" IsTabStop="False">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton">
                      <Border Height="4" CornerRadius="2" VerticalAlignment="Center"
                              Background="{DynamicResource AccentBrush}"/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.DecreaseRepeatButton>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="Slider.IncreaseLarge" Focusable="False" IsTabStop="False">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton">
                      <Border Background="Transparent" Height="18"/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.IncreaseRepeatButton>
              <Track.Thumb>
                <Thumb Focusable="False">
                  <Thumb.Template>
                    <ControlTemplate TargetType="Thumb">
                      <Ellipse Width="14" Height="14" Fill="{DynamicResource AccentBrush}"
                               Stroke="{DynamicResource BackgroundBrush}" StrokeThickness="2"/>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="ModernComboBoxStyle" TargetType="ComboBox">
    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Padding" Value="8,4,26,4"/>
    <Setter Property="ItemContainerStyle">
      <Setter.Value>
        <Style TargetType="ComboBoxItem">
          <Setter Property="Padding" Value="8,4"/>
          <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="ComboBoxItem">
                <Border x:Name="b" Background="Transparent" CornerRadius="4"
                        Padding="{TemplateBinding Padding}">
                  <ContentPresenter/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsHighlighted" Value="True">
                    <Setter TargetName="b" Property="Background" Value="{DynamicResource HoverBrush}"/>
                  </Trigger>
                  <Trigger Property="IsSelected" Value="True">
                    <Setter TargetName="b" Property="Background" Value="{DynamicResource HoverBrush}"/>
                  </Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
      </Setter.Value>
    </Setter>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBox">
          <Grid>
            <ToggleButton ClickMode="Press" Focusable="False"
                          IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Border Background="{DynamicResource SurfaceBrush}"
                          BorderBrush="{DynamicResource BorderBrush2}" BorderThickness="1" CornerRadius="6">
                    <Path HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,9,0"
                          Data="M0,0 L4,4 L8,0" Stroke="{DynamicResource TextSecondaryBrush}"
                          StrokeThickness="1.5"/>
                  </Border>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter IsHitTestVisible="False"
                              Content="{TemplateBinding SelectionBoxItem}"
                              ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                              Margin="{TemplateBinding Padding}"
                              VerticalAlignment="Center" HorizontalAlignment="Left"/>
            <Popup Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}"
                   AllowsTransparency="True" Focusable="False" PopupAnimation="Slide">
              <Border Background="{DynamicResource BackgroundBrush}"
                      BorderBrush="{DynamicResource BorderBrush2}" BorderThickness="1" CornerRadius="8"
                      MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"
                      MaxHeight="360" Margin="0,4,0,0">
                <ScrollViewer Margin="4">
                  <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>
""";
}
