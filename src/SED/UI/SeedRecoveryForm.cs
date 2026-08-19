using PKHeX.Core;
using SED.Core;

namespace SED.UI;

public sealed class SeedRecoveryForm : Form
{
    private readonly DataGridView Grid = new();
    private readonly RichTextBox Details = new();
    private readonly Button CopyButton = new();
    private readonly SeedRecoveryResult[] Results;

    public SeedRecoveryForm(PKM pokemon, uint initialSeed, IReadOnlyList<SeedRecoveryResult> results)
    {
        Results = results.ToArray();
        Text = "SED Seed Recovery";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 620);
        MinimumSize = new Size(900, 500);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 760, FixedPanel = FixedPanel.Panel2 };
        Controls.Add(split);
        ConfigureGrid();
        split.Panel1.Controls.Add(Grid);

        var side = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(8) };
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        split.Panel2.Controls.Add(side);
        Details.Dock = DockStyle.Fill;
        Details.ReadOnly = true;
        Details.Font = new Font(FontFamily.GenericMonospace, Font.Size);
        side.Controls.Add(Details);
        CopyButton.Text = "Copy Recovery Recipe";
        CopyButton.AutoSize = true;
        CopyButton.Click += (_, _) => CopySelected(pokemon, initialSeed);
        side.Controls.Add(CopyButton);

        Grid.DataSource = Results.Select(ToRow).ToArray();
        Grid.SelectionChanged += (_, _) => ShowSelected(pokemon, initialSeed);
        ShowSelected(pokemon, initialSeed);
    }

    private void ConfigureGrid()
    {
        Grid.Dock = DockStyle.Fill;
        Grid.ReadOnly = true;
        Grid.AllowUserToAddRows = false;
        Grid.AllowUserToDeleteRows = false;
        Grid.AutoGenerateColumns = false;
        Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Grid.MultiSelect = false;
        Grid.RowHeadersVisible = false;
        AddColumn("Frame", nameof(RecoveryRow.Frame), 92);
        AddColumn("Encounter seed", nameof(RecoveryRow.EncounterSeed), 112);
        AddColumn("PID seed", nameof(RecoveryRow.OriginSeed), 112);
        AddColumn("Method", nameof(RecoveryRow.Method), 90);
        AddColumn("Lead", nameof(RecoveryRow.Lead), 150);
        AddColumn("Location", nameof(RecoveryRow.Location), 70);
        AddColumn("Level", nameof(RecoveryRow.Level), 60);
        AddColumn("Encounter", nameof(RecoveryRow.Encounter), 190);
    }

    private void AddColumn(string header, string property, int width) => Grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        HeaderText = header,
        DataPropertyName = property,
        Width = width,
        SortMode = DataGridViewColumnSortMode.Automatic,
    });

    private SeedRecoveryResult? Selected => Grid.CurrentRow?.Index is int index && index >= 0 && index < Results.Length
        ? Results[index]
        : null;

    private void ShowSelected(PKM pokemon, uint initialSeed)
    {
        Details.Text = Selected is { } result ? BuildRecipe(pokemon, initialSeed, result) : "No recoverable LCRNG correlation was found.";
        CopyButton.Enabled = Selected is not null;
    }

    private void CopySelected(PKM pokemon, uint initialSeed)
    {
        if (Selected is { } result)
            Clipboard.SetText(BuildRecipe(pokemon, initialSeed, result));
    }

    private static string BuildRecipe(PKM pokemon, uint initialSeed, SeedRecoveryResult result) => string.Join(Environment.NewLine,
        "SED Seed Recovery",
        $"Species={GameInfo.Strings.Species[pokemon.Species]}",
        $"PID=0x{pokemon.PID:X8}",
        $"IV32=0x{pokemon.GetIVs():X8}",
        $"Method={result.Method}",
        $"InitialSeed=0x{initialSeed:X8}",
        $"Frame={result.Frame}",
        $"EncounterSeed=0x{result.EncounterSeed:X8}",
        $"PIDSeed=0x{result.OriginSeed:X8}",
        $"Lead={result.Lead}",
        $"Encounter={result.EncounterName}",
        $"LocationMatch={result.LocationMatches}",
        $"LevelMatch={result.LevelMatches}");

    private static RecoveryRow ToRow(SeedRecoveryResult result) => new(
        result.Frame,
        $"0x{result.EncounterSeed:X8}",
        $"0x{result.OriginSeed:X8}",
        result.Method.ToString(),
        result.Lead.ToString(),
        result.LocationMatches ? "match" : "different",
        result.LevelMatches ? "match" : "different",
        result.EncounterName);

    private sealed record RecoveryRow(uint Frame, string EncounterSeed, string OriginSeed, string Method, string Lead, string Location, string Level, string Encounter);
}
