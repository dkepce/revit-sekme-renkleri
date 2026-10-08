using System.IO;

namespace RevitSekmeRenkleri;

/// <summary>
/// Kleines Protokoll nach %LOCALAPPDATA%\RevitSekmeRenkleri\TabColor-Log.txt, fuer das,
/// was sich vom Schreibtisch aus nicht beobachten laesst: Revit-Oberflaeche
/// reagiert auf Klicks in der Dokumentzeile, nicht jeder Wechsel ist sofort
/// sichtbar. Nur seltene Ereignisse (Klicks, Wiederholungen), nie der Takt -
/// die Datei bleibt klein und wird bei 200 KB neu begonnen.
/// </summary>
internal static class TabLog
{
    private static readonly object _sperre = new();

    private static readonly string Pfad = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RevitSekmeRenkleri", "TabColor-Log.txt");

    public static void Schreibe(string text)
    {
        try
        {
            lock (_sperre)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Pfad)!);
                if (File.Exists(Pfad) && new FileInfo(Pfad).Length > 200_000) File.Delete(Pfad);
                File.AppendAllText(Pfad, $"{DateTime.Now:HH:mm:ss.fff}  {text}{Environment.NewLine}");
            }
        }
        catch { /* ein Protokoll darf nie stoeren */ }
    }
}
