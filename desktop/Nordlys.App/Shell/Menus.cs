// Context-aware right-click menus. WPF's ContextMenu provides the native behaviour: opens at the pointer
// (or at the element with the Menu key / Shift+F10), stays on screen, arrow keys and access keys, Esc and
// click-outside close it. These helpers decide WHAT is in the menu from what was right-clicked.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Nordlys.App.Rendering;
using Nordlys.App.UI;

namespace Nordlys.App.Shell;

/// <summary>One menu line. Null action = disabled. A null entry in a list = separator.</summary>
public sealed record MenuEntry(string Header, Action? Run, string? Icon = null, string? Gesture = null);

public static class Menus
{
    public static ContextMenu Build(IEnumerable<MenuEntry?> entries)
    {
        var menu = new ContextMenu();
        var pendingSeparator = false;
        foreach (var e in entries)
        {
            if (e == null) { pendingSeparator = menu.Items.Count > 0; continue; }
            if (pendingSeparator) { menu.Items.Add(new Separator()); pendingSeparator = false; }
            var item = new MenuItem { Header = e.Header, InputGestureText = e.Gesture ?? "", IsEnabled = e.Run != null };
            if (e.Icon != null) item.Icon = Ui.Icon(e.Icon, 14, e.Run != null ? "TextMuted" : "TextFaint");
            if (e.Run is { } run) item.Click += (_, _) => run();
            menu.Items.Add(item);
        }
        return menu;
    }

    /// <summary>The event a menu was already built for: the innermost region wins (a card inside a page, a text box inside a card).</summary>
    private static object? builtFor;

    /// <summary>
    /// Builds the menu when it opens, so it reflects the current selection and what is under the pointer.
    /// Return null or no entries to leave it to an outer region. outermost: nothing anywhere = no menu at all.
    /// </summary>
    public static void Attach(FrameworkElement el, Func<ContextMenuEventArgs, IEnumerable<MenuEntry?>?> entries, bool outermost = false)
    {
        el.ContextMenu ??= new ContextMenu(); // placeholder so WPF raises ContextMenuOpening
        el.SetValue(IsRegionProperty, true);
        el.ContextMenuOpening += (_, e) =>
        {
            if (ReferenceEquals(builtFor, e)) return;
            var owner = Owner(e.OriginalSource as DependencyObject, el);
            var list = entries(e)?.ToList() ?? [];
            // Right-click in a text box inside this region: its edit actions first, then the region's.
            if (owner is TextBox tb && owner != el && tb.Tag as string != "custom-menu") list = [.. ForTextBox(tb), null, .. list];
            if (list.All(x => x == null))
            {
                if (outermost) e.Handled = true; // never show an empty menu
                return;
            }
            builtFor = e;
            owner.ContextMenu = Build(list);
        };
    }

    /// <summary>Marks an element whose menu is built by Attach.</summary>
    private static readonly DependencyProperty IsRegionProperty = DependencyProperty.RegisterAttached("IsRegion", typeof(bool), typeof(Menus), new PropertyMetadata(false));

    private static bool InsideRegion(DependencyObject d)
    {
        for (var p = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d); p != null; p = p is Visual ? VisualTreeHelper.GetParent(p) ?? LogicalTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p))
            if ((bool)p.GetValue(IsRegionProperty)) return true;
        return false;
    }

    /// <summary>
    /// The element whose menu WPF will open: the nearest one from where the menu was asked for (pointer target or
    /// focused element) up to the region, that already has a ContextMenu. Text boxes and documents have one by style.
    /// </summary>
    private static FrameworkElement Owner(DependencyObject? from, FrameworkElement region)
    {
        for (var d = from; d != null && d != region; d = d is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is FrameworkElement { ContextMenu: not null } fe) return fe;
        return region;
    }

    public static void Copy(string text)
    {
        try { Clipboard.SetText(text); } catch (System.Runtime.InteropServices.ExternalException) { /* clipboard busy; Windows retries are not worth a dialog */ }
    }

    public static void SearchWeb(string text) =>
        Markdown.OpenInBrowser(new Uri($"https://www.bing.com/search?q={Uri.EscapeDataString(text.Trim())}"));

    private static string Short(string s, int max = 28)
    {
        s = string.Join(' ', s.Split((char[])['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    /// <summary>
    /// Menu entries for a read-only document (paper, prompt, explanation): text actions when text is selected,
    /// glossary actions on a term, "Copy code" inside a code block.
    /// </summary>
    public static List<MenuEntry?> ForDocument(RichTextBox box, Action<string>? lookUpTerm = null)
    {
        var entries = new List<MenuEntry?>();
        var selected = box.Selection.Text;
        var hasSelection = !string.IsNullOrWhiteSpace(selected);
        var pointer = box.GetPositionFromPoint(Mouse.GetPosition(box), snapToText: true);
        var run = pointer?.Parent as Run;

        if (hasSelection)
        {
            entries.Add(new("Copy", () => Copy(selected), Icons.Copy, "Ctrl+C"));
            entries.Add(new($"Search the web for \"{Short(selected)}\"", () => SearchWeb(selected), Icons.Search));
            var term = selected.Trim().ToLowerInvariant();
            if (lookUpTerm != null && App.State.Progress.Glossary.ContainsKey(term))
                entries.Add(new($"Look up \"{Short(selected)}\" in the Glossary", () => lookUpTerm(term), Icons.Glossary));
        }

        if (run != null && Glossary.DefinitionOf(run) is { } def)
        {
            entries.Add(null);
            entries.Add(new($"Copy definition of \"{run.Text}\"", () => Copy($"{run.Text}: {def}"), Icons.Copy));
            if (lookUpTerm != null) entries.Add(new("Open in Glossary", () => lookUpTerm(run.Text.ToLowerInvariant()), Icons.Glossary));
        }

        if (pointer != null && CodeBlockAt(pointer) is { } code)
        {
            entries.Add(null);
            entries.Add(new("Copy code block", () => Copy(Markdown.PlainText(code).TrimEnd()), Icons.Copy));
        }
        else if ((run?.Tag as string) == Markdown.CodeTag && !hasSelection)
        {
            entries.Add(null);
            entries.Add(new($"Copy \"{Short(run!.Text)}\"", () => Copy(run.Text), Icons.Copy));
        }

        entries.Add(null);
        entries.Add(new("Select all", () => { box.Focus(); box.SelectAll(); }, Icons.SelectAll, "Ctrl+A"));
        return entries;
    }

    private static Paragraph? CodeBlockAt(TextPointer p)
    {
        for (DependencyObject? d = p.Parent; d != null; d = (d as TextElement)?.Parent)
            if (d is Paragraph para && (para.Tag as string) == Markdown.CodeTag) return para;
        return null;
    }

    /// <summary>Standard edit menu for text boxes, with only the actions that can run right now.</summary>
    public static List<MenuEntry?> ForTextBox(TextBox t)
    {
        var sel = t.SelectedText;
        var canPaste = !t.IsReadOnly && Clipboard.ContainsText();
        return
        [
            new("Undo", t.CanUndo && !t.IsReadOnly ? () => t.Undo() : null, Icons.Undo, "Ctrl+Z"),
            new("Redo", t.CanRedo && !t.IsReadOnly ? () => t.Redo() : null, Icons.Redo, "Ctrl+Y"),
            null,
            new("Cut", sel != "" && !t.IsReadOnly ? () => t.Cut() : null, Icons.Cut, "Ctrl+X"),
            new("Copy", sel != "" ? () => t.Copy() : null, Icons.Copy, "Ctrl+C"),
            new("Paste", canPaste ? () => t.Paste() : null, Icons.Paste, "Ctrl+V"),
            new("Delete", sel != "" && !t.IsReadOnly ? () => t.SelectedText = "" : null, Icons.Delete, "Del"),
            null,
            new("Select all", t.Text != "" ? () => { t.Focus(); t.SelectAll(); } : null, Icons.SelectAll, "Ctrl+A"),
            sel.Trim() != "" ? new MenuEntry($"Search the web for \"{Short(sel)}\"", () => SearchWeb(sel), Icons.Search) : null,
        ];
    }

    /// <summary>Gives every TextBox the edit menu, and every document view the text menu (regions may add to it).</summary>
    public static void RegisterTextBoxMenus()
    {
        EventManager.RegisterClassHandler(typeof(TextBox), FrameworkElement.ContextMenuOpeningEvent, new ContextMenuEventHandler((s, e) =>
        {
            // Text boxes inside a region get the region's menu (which includes the edit actions); text boxes with
            // a richer menu of their own (Tag "custom-menu") build it themselves.
            if (s is not TextBox t || ReferenceEquals(builtFor, e) || t.Tag as string == "custom-menu" || InsideRegion(t)) return;
            builtFor = e;
            t.ContextMenu = Build(ForTextBox(t));
        }));
        EventManager.RegisterClassHandler(typeof(RichTextBox), FrameworkElement.ContextMenuOpeningEvent, new ContextMenuEventHandler((s, e) =>
        {
            if (s is RichTextBox r) r.ContextMenu = Build(ForDocument(r)); // a surrounding region usually replaces this
        }));
    }
}
