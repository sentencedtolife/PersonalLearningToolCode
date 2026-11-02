// Ask: copies generator prompt + learner context + request, to paste into Claude.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Nordlys.App.Shell;
using Nordlys.App.UI;
using static Nordlys.App.Views.Routes;

namespace Nordlys.App.Views;

public sealed class AskView : View
{
    private readonly ScrollViewer root;
    private readonly TextBox request = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 };
    private readonly StackPanel picker = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock status = Ui.Text("", "FontSmall");
    private int energy;

    public override string Title => "Ask";
    public override FrameworkElement Root => root;

    public AskView(string? unitId, string? energyQuery, string? requestQuery)
    {
        energy = int.TryParse(energyQuery, out var e) && e is >= 1 and <= 3 ? e : Data.Energy ?? Data.Settings.LastEnergy;
        if (unitId != null && Data.Curriculum.UnitById(unitId) is { } unit) request.Text = $"Teach me the unit {unit.Id} ({unit.Title}).";
        if (requestQuery != null) request.Text = requestQuery;
        System.Windows.Automation.AutomationProperties.SetName(request, "What do you want to learn?");
        DrawPicker();

        var copy = Ui.Btn("Copy prompt for Claude", () => CopyAsync().Forget(), "BtnPrimary", big: true);
        copy.HorizontalAlignment = HorizontalAlignment.Center;
        var toImport = Ui.Btn("Go to Import →", () => Go("#/import"), "BtnFlat");
        toImport.HorizontalAlignment = HorizontalAlignment.Center;
        status.HorizontalAlignment = HorizontalAlignment.Center;
        System.Windows.Automation.AutomationProperties.SetLiveSetting(status, System.Windows.Automation.AutomationLiveSetting.Polite);

        root = Ui.Page(Ui.Stack(
            Ui.H1("Ask Claude for a lesson"),
            Ui.Text("What do you want to learn? (leave empty for 'next in curriculum')", "FontBase", "Text", FontWeights.SemiBold),
            request,
            Ui.Text("Energy", "FontBase", "Text", FontWeights.SemiBold),
            picker,
            copy,
            Ui.With(Ui.Muted("Paste into Claude. Copy the lesson it gives you. Then go to Import."), t => t.HorizontalAlignment = HorizontalAlignment.Center),
            status,
            toImport), 640);

        var promptFile = Data.Store.Root is { } r ? Path.Combine(r, "generator-prompt.md") : null;
        Menus.Attach(root, _ =>
        [
            new MenuEntry("Copy prompt for Claude", () => CopyAsync().Forget(), Icons.Copy, "Ctrl+Enter"),
            new MenuEntry("Go to Import", () => Go("#/import"), Icons.Import),
            null,
            new MenuEntry("Clear request", request.Text != "" ? () => request.Clear() : null, Icons.Clear),
            new MenuEntry("Edit the generator prompt", promptFile != null ? () => OpenInEditor(promptFile) : null, Icons.Edit),
        ], outermost: true);
    }

    private void DrawPicker()
    {
        picker.Children.Clear();
        foreach (var (n, desc) in new[] { (1, "Low · 5–10 min"), (2, "Medium · ~25 min"), (3, "High · 45–90 min") })
        {
            var b = HomeView.EnergyButton(n, desc, n == energy, () => SetEnergy(n));
            b.Margin = new Thickness(0, 0, 12, 0);
            picker.Children.Add(b);
        }
    }

    private void SetEnergy(int n)
    {
        energy = n;
        Data.Energy = n;
        Data.Settings.LastEnergy = n;
        Data.SaveSettingsAsync().Forget();
        DrawPicker();
    }

    private async Task CopyAsync()
    {
        try
        {
            Clipboard.SetText(await Data.BuildPromptAsync(request.Text, energy));
            status.Text = "Copied. Now paste it into a new Claude chat.";
            status.Res(TextBlock.ForegroundProperty, "Correct");
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            status.Text = $"Could not copy: {ex.Message}";
            status.Res(TextBlock.ForegroundProperty, "NotYet");
        }
    }

    public override void Shown() => request.Focus();

    public override bool OnKey(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { CopyAsync().Forget(); return true; }
        if (Keyboard.FocusedElement is not TextBox && Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.D1 or Key.D2 or Key.D3)
        {
            SetEnergy(e.Key - Key.D0);
            return true;
        }
        return false;
    }
}
