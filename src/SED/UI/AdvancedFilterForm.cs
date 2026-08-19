using System.Globalization;
using PKHeX.Core;
using SED.Core;

namespace SED.UI;

public sealed class AdvancedFilterForm : Form
{
    private readonly ComboBox NatureBox = new();
    private readonly ComboBox GenderBox = new();
    private readonly ComboBox AbilityBox = new();
    private readonly ComboBox HiddenPowerBox = new();
    private readonly NumericUpDown[] IVs = Enumerable.Range(0, 6).Select(_ => CreateNumber(0, 31)).ToArray();
    private readonly TextBox ExactPidBox = new();
    private readonly NumericUpDown[] ExactIVs = Enumerable.Range(0, 6).Select(_ => CreateNumber(-1, 31)).ToArray();
    private readonly NumericUpDown HiddenPowerPower = CreateNumber(0, 70);
    private readonly NumericUpDown MinimumLevel = CreateNumber(1, 100);
    private readonly NumericUpDown MaximumLevel = CreateNumber(1, 100);
    private readonly NumericUpDown LocationFilter = CreateNumber(-1, 255);
    private readonly NumericUpDown EncounterSlot = CreateNumber(-1, 99);
    private readonly NumericUpDown ExactFrame = CreateNumber(-1, 1_000_000_000);

    public SeedSearchFilters Filters { get; private set; }

    public AdvancedFilterForm(SeedSearchFilters current)
    {
        Filters = current;
        Text = "SED Advanced Manipulation Filters";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BuildInterface();
        LoadCurrent(current);
    }

    private void BuildInterface()
    {
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        Controls.Add(table);
        PopulateChoices();
        var reverseHint = new Label
        {
            Text = "Exact PID or all six exact IVs enables reverse solving while preserving the calculated frame.",
            AutoSize = true,
            MaximumSize = new Size(390, 0),
            Margin = new Padding(3, 3, 3, 10),
        };
        table.Controls.Add(reverseHint, 0, table.RowCount);
        table.SetColumnSpan(reverseHint, 2);
        ExactPidBox.CharacterCasing = CharacterCasing.Upper;
        ExactPidBox.Font = new Font(FontFamily.GenericMonospace, Font.Size);
        Add(table, "Exact PID hex (blank means any)", ExactPidBox);
        Add(table, "Exact HP IV (-1 means any)", ExactIVs[0]);
        Add(table, "Exact Attack IV (-1 means any)", ExactIVs[1]);
        Add(table, "Exact Defense IV (-1 means any)", ExactIVs[2]);
        Add(table, "Exact Special Attack IV (-1 means any)", ExactIVs[3]);
        Add(table, "Exact Special Defense IV (-1 means any)", ExactIVs[4]);
        Add(table, "Exact Speed IV (-1 means any)", ExactIVs[5]);
        Add(table, "Exact frame (-1 uses frame range)", ExactFrame);
        Add(table, "Nature", NatureBox);
        Add(table, "Gender", GenderBox);
        Add(table, "Ability slot", AbilityBox);
        Add(table, "Minimum HP IV", IVs[0]);
        Add(table, "Minimum Attack IV", IVs[1]);
        Add(table, "Minimum Defense IV", IVs[2]);
        Add(table, "Minimum Special Attack IV", IVs[3]);
        Add(table, "Minimum Special Defense IV", IVs[4]);
        Add(table, "Minimum Speed IV", IVs[5]);
        Add(table, "Hidden Power type", HiddenPowerBox);
        Add(table, "Minimum Hidden Power", HiddenPowerPower);
        Add(table, "Minimum encounter level", MinimumLevel);
        Add(table, "Maximum encounter level", MaximumLevel);
        Add(table, "Location ID (-1 means any)", LocationFilter);
        Add(table, "Encounter slot (-1 means any)", EncounterSlot);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var ok = new Button { Text = "Apply", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var clear = new Button { Text = "Clear", AutoSize = true };
        ok.Click += (_, _) => ApplyAndClose();
        clear.Click += (_, _) => LoadCurrent(SeedSearchFilters.Any);
        buttons.Controls.AddRange([ok, cancel, clear]);
        table.Controls.Add(buttons, 0, table.RowCount);
        table.SetColumnSpan(buttons, 2);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void PopulateChoices()
    {
        NatureBox.DropDownStyle = GenderBox.DropDownStyle = AbilityBox.DropDownStyle = HiddenPowerBox.DropDownStyle = ComboBoxStyle.DropDownList;
        NatureBox.Items.Add("Any");
        foreach (var nature in GameInfo.Strings.Natures)
            NatureBox.Items.Add(nature);
        GenderBox.Items.AddRange(["Any", "Male", "Female"]);
        AbilityBox.Items.AddRange(["Any", "Slot 0", "Slot 1"]);
        HiddenPowerBox.Items.AddRange(["Any", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel", "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark"]);
    }

    private void LoadCurrent(SeedSearchFilters filters)
    {
        ExactPidBox.Text = filters.ExactPID?.ToString("X8", CultureInfo.InvariantCulture) ?? string.Empty;
        ExactIVs[0].Value = filters.ExactHP;
        ExactIVs[1].Value = filters.ExactAttack;
        ExactIVs[2].Value = filters.ExactDefense;
        ExactIVs[3].Value = filters.ExactSpecialAttack;
        ExactIVs[4].Value = filters.ExactSpecialDefense;
        ExactIVs[5].Value = filters.ExactSpeed;
        ExactFrame.Value = filters.ExactFrame;
        NatureBox.SelectedIndex = filters.Nature + 1;
        GenderBox.SelectedIndex = filters.Gender switch { (int)Gender.Male => 1, (int)Gender.Female => 2, _ => 0 };
        AbilityBox.SelectedIndex = filters.AbilitySlot + 1;
        IVs[0].Value = filters.MinimumHP;
        IVs[1].Value = filters.MinimumAttack;
        IVs[2].Value = filters.MinimumDefense;
        IVs[3].Value = filters.MinimumSpecialAttack;
        IVs[4].Value = filters.MinimumSpecialDefense;
        IVs[5].Value = filters.MinimumSpeed;
        HiddenPowerBox.SelectedIndex = filters.HiddenPowerType + 1;
        HiddenPowerPower.Value = filters.MinimumHiddenPower;
        MinimumLevel.Value = filters.MinimumLevel;
        MaximumLevel.Value = filters.MaximumLevel;
        LocationFilter.Value = filters.Location;
        EncounterSlot.Value = filters.EncounterSlot;
    }

    private void ApplyAndClose()
    {
        var text = ExactPidBox.Text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        uint? exactPid = null;
        if (text.Length != 0)
        {
            if (text.Length > 8 || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
            {
                MessageBox.Show(this, "Exact PID must be an eight digit hexadecimal value.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            exactPid = parsed;
        }
        Filters = ReadFilters(exactPid);
        DialogResult = DialogResult.OK;
        Close();
    }

    private SeedSearchFilters ReadFilters(uint? exactPid) => new(
        NatureBox.SelectedIndex - 1,
        GenderBox.SelectedIndex switch { 1 => (int)Gender.Male, 2 => (int)Gender.Female, _ => -1 },
        AbilityBox.SelectedIndex - 1,
        (int)IVs[0].Value,
        (int)IVs[1].Value,
        (int)IVs[2].Value,
        (int)IVs[3].Value,
        (int)IVs[4].Value,
        (int)IVs[5].Value,
        HiddenPowerBox.SelectedIndex - 1,
        (int)HiddenPowerPower.Value,
        (int)MinimumLevel.Value,
        (int)MaximumLevel.Value,
        (int)LocationFilter.Value,
        (int)EncounterSlot.Value,
        exactPid,
        (int)ExactIVs[0].Value,
        (int)ExactIVs[1].Value,
        (int)ExactIVs[2].Value,
        (int)ExactIVs[3].Value,
        (int)ExactIVs[4].Value,
        (int)ExactIVs[5].Value,
        (int)ExactFrame.Value);

    private static NumericUpDown CreateNumber(int minimum, int maximum) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Width = 160,
    };

    private static void Add(TableLayoutPanel table, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 3) });
        control.Width = 180;
        table.Controls.Add(control);
    }
}
