using PKHeX.Core;
using SED.Core;

namespace SED.UI;

public sealed class RngProofForm : Form
{
    private readonly SeedEncounterResult Result;
    private readonly DataGridView Grid = new();

    public RngProofForm(SeedEncounterResult result)
    {
        Result = result;
        Text = "SED RNG Proof";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 650);
        MinimumSize = new Size(900, 480);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = $"Frame {result.Frame}   State 0x{result.State:X8}   PID 0x{result.Pokemon.PID:X8}   {result.Trace.Count} consumed calls",
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(3, 3, 3, 10),
        });
        ConfigureGrid();
        layout.Controls.Add(Grid);
        var copy = new Button { Text = "Copy RNG Proof", AutoSize = true };
        copy.Click += (_, _) => Clipboard.SetText(BuildText());
        layout.Controls.Add(copy);
        Grid.DataSource = result.Trace.Select(z => new ProofRow(
            z.Call,
            z.Operation,
            $"0x{z.StateBefore:X8}",
            $"0x{z.StateAfter:X8}",
            $"0x{z.Output:X4}",
            z.Decision)).ToArray();
    }

    private void ConfigureGrid()
    {
        Grid.Dock = DockStyle.Fill;
        Grid.ReadOnly = true;
        Grid.AllowUserToAddRows = false;
        Grid.AllowUserToDeleteRows = false;
        Grid.AutoGenerateColumns = false;
        Grid.RowHeadersVisible = false;
        Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        AddColumn("Call", nameof(ProofRow.Call), 55);
        AddColumn("Operation", nameof(ProofRow.Operation), 170);
        AddColumn("State before", nameof(ProofRow.Before), 112);
        AddColumn("State after", nameof(ProofRow.After), 112);
        AddColumn("Output", nameof(ProofRow.Output), 75);
        AddColumn("Interpretation", nameof(ProofRow.Decision), 510);
    }

    private void AddColumn(string header, string property, int width) => Grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        HeaderText = header,
        DataPropertyName = property,
        Width = width,
        SortMode = DataGridViewColumnSortMode.Automatic,
    });

    private string BuildText() => string.Join(Environment.NewLine,
        new[]
        {
            "SED RNG Proof",
            $"InitialSeed=0x{Result.InitialSeed:X8}",
            $"Frame={Result.Frame}",
            $"EncounterState=0x{Result.State:X8}",
            $"PID=0x{Result.Pokemon.PID:X8}",
            $"IV32=0x{Result.Pokemon.IV32:X8}",
            $"Calls={Result.Trace.Count}",
            string.Empty,
            "Call\tOperation\tBefore\tAfter\tOutput\tInterpretation",
        }.Concat(Result.Trace.Select(z => $"{z.Call}\t{z.Operation}\t{z.StateBefore:X8}\t{z.StateAfter:X8}\t{z.Output:X4}\t{z.Decision}")));

    private sealed record ProofRow(int Call, string Operation, string Before, string After, string Output, string Decision);
}
