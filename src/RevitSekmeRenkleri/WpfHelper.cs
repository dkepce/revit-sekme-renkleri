using System.Windows;
using System.Windows.Media;

namespace RevitSekmeRenkleri;

/// <summary>
/// Hilfsmethoden fuer den WPF-Sichtbaum.
/// </summary>
public static class WpfHelper
{
    /// <summary>Alle Nachfahren eines Typs im Sichtbaum.</summary>
    public static List<T> FindDescendants<T>(this DependencyObject parent) where T : DependencyObject
    {
        var results = new List<T>();
        if (parent == null) return results;
        CollectDescendants(parent, results);
        return results;
    }

    /// <summary>Erster Nachfahre eines Typs (Tiefensuche), sonst null.</summary>
    public static T FindFirstDescendant<T>(this DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;

            var result = child.FindFirstDescendant<T>();
            if (result != null) return result;
        }
        return null;
    }

    private static void CollectDescendants<T>(DependencyObject parent, List<T> results)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
                results.Add(typed);
            CollectDescendants(child, results);
        }
    }
}
