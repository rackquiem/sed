using PKHeX.Core;
using SED.Core;

namespace SED.UI;

public sealed class SafariPredictionForm : Form
{
    private readonly SeedEncounterResult Result;
    private readonly NumericUpDown OffsetCount = new();
    private readonly DataGridView Grid = new();
    private readonly Label Summary = new();

    public SafariPredictionForm(SeedEncounterResult result)
    {
        Result = result;
        Text = "SED Safari Capture and Flee Predictor";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 520);
        Size = new Size(980, 660);
        BuildInterface();
        RefreshPredictions();
    }

    private void BuildInterface()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        top.Controls.Add(new Label { Text = "Battle RNG offsets", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        OffsetCount.Minimum = 1;
        OffsetCount.Maximum = 100_000;
        OffsetCount.Value = 256;
        OffsetCount.ThousandsSeparator = true;
        top.Controls.Add(OffsetCount);
        var calculate = new Button { Text = "Predict", AutoSize = true };
        calculate.Click += (_, _) => RefreshPredictions();
        top.Controls.Add(calculate);
        Controls.Add(top);

        Summary.Dock = DockStyle.Top;
        Summary.AutoSize = true;
        Summary.Padding = new Padding(12, 4, 12, 8);
        Controls.Add(Summary);

        Grid.Dock = DockStyle.Fill;
        Grid.ReadOnly = true;
        Grid.AllowUserToAddRows = false;
        Grid.AllowUserToDeleteRows = false;
        Grid.RowHeadersVisible = false;
        Grid.AutoGenerateColumns = false;
        Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        AddColumn("Offset", nameof(DisplayPrediction.Offset), 70);
        AddColumn("Starting state", nameof(DisplayPrediction.State), 110);
        AddColumn("Shakes", nameof(DisplayPrediction.Shakes), 65);
        AddColumn("Capture", nameof(DisplayPrediction.Capture), 75);
        AddColumn("Flee roll", nameof(DisplayPrediction.FleeRoll), 75);
        AddColumn("Flee limit", nameof(DisplayPrediction.FleeLimit), 75);
        AddColumn("Flees", nameof(DisplayPrediction.Flees), 65);
        AddColumn("Ending state", nameof(DisplayPrediction.EndState), 110);
        Controls.Add(Grid);
        Grid.BringToFront();
    }

    private void RefreshPredictions()
    {
        var predictions = SafariPredictor.Predict(Result, (int)OffsetCount.Value);
        Grid.DataSource = predictions.Select(DisplayPrediction.Create).ToArray();
        var pk = Result.Pokemon;
        var species = GameInfo.Strings.Species[pk.Species];
        Summary.Text = $"{species} at encounter frame {Result.Frame:N0}. Offsets begin after the final encounter generation call. " +
                       $"Initial catch factor {predictions[0].CatchFactor} and escape factor {predictions[0].EscapeFactor}.";
    }

    private void AddColumn(string header, string property, int width) => Grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        HeaderText = header,
        DataPropertyName = property,
        Width = width,
        SortMode = DataGridViewColumnSortMode.Automatic,
    });

    private sealed record DisplayPrediction(
        int Offset,
        string State,
        int Shakes,
        string Capture,
        string FleeRoll,
        int FleeLimit,
        string Flees,
        string EndState)
    {
        public static DisplayPrediction Create(SafariTurnPrediction value) => new(
            value.BattleOffset,
            $"0x{value.StartingState:X8}",
            value.Shakes,
            value.Captured ? "Yes" : "No",
            value.FleeRoll?.ToString() ?? "—",
            value.EscapeFactor * 5,
            value.Captured ? "—" : value.Flees ? "Yes" : "No",
            $"0x{value.EndingState:X8}");
    }
}
