using System.Globalization;
using PKHeX.Core;
using SED.Core;

namespace SED.UI;

public sealed class SeedEncounterDatabaseForm : Form
{
    private readonly ISaveFileProvider SaveEditor;
    private readonly IPKMView PokemonEditor;
    private readonly ComboBox SpeciesBox = new();
    private readonly ComboBox CategoryBox = new();
    private readonly ComboBox LeadBox = new();
    private readonly ComboBox LeadNatureBox = new();
    private readonly NumericUpDown LeadLevel = new();
    private readonly ComboBox ShinyBox = new();
    private readonly TextBox SeedBox = new();
    private readonly NumericUpDown StartFrame = new();
    private readonly NumericUpDown FrameCount = new();
    private readonly NumericUpDown WorkerCount = new();
    private readonly NumericUpDown MaximumResults = new();
    private readonly CheckBox LegalOnly = new();
    private readonly Button SearchButton = new();
    private readonly Button CancelSearchButton = new();
    private readonly Button ResetButton = new();
    private readonly Button RecoverButton = new();
    private readonly Button AdvancedButton = new();
    private readonly DataGridView Grid = new();
    private readonly RichTextBox Details = new();
    private readonly Label Status = new();
    private readonly Label Trainer = new();
    private readonly Button ViewButton = new();
    private readonly Button SetBoxButton = new();
    private readonly Button CopyButton = new();
    private readonly Button ProofButton = new();
    private readonly BindingSource ResultSource = new();
    private CancellationTokenSource? SearchCancellation;
    private SeedSearchFilters AdvancedFilters = SeedSearchFilters.Any;

    private SaveFile Save => SaveEditor.SAV;
    private DisplayResult? Selected => Grid.CurrentRow?.DataBoundItem as DisplayResult;

    public SeedEncounterDatabaseForm(ISaveFileProvider saveEditor, IPKMView pokemonEditor)
    {
        SaveEditor = saveEditor;
        PokemonEditor = pokemonEditor;
        Text = "SED - Seed Encounter Database";
        Name = nameof(SeedEncounterDatabaseForm);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1080, 640);
        Size = new Size(1280, 760);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildInterface();
        PopulateFilters();
        RefreshLoadedSave();
    }

    private void BuildInterface()
    {
        var outer = new SplitContainer
        {
            Size = new Size(1260, 720),
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 270,
            Panel1MinSize = 250,
        };
        outer.Panel1.AutoScroll = true;
        Controls.Add(outer);

        var filters = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(12),
        };
        outer.Panel1.Controls.Add(filters);
        AddFilter(filters, "Species", SpeciesBox);
        SpeciesBox.DropDownStyle = ComboBoxStyle.DropDown;
        SpeciesBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        SpeciesBox.AutoCompleteSource = AutoCompleteSource.ListItems;

        AddFilter(filters, "Encounter type", CategoryBox);
        CategoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
        AddFilter(filters, "Lead ability (wild)", LeadBox);
        LeadBox.DropDownStyle = ComboBoxStyle.DropDownList;
        LeadBox.SelectedIndexChanged += (_, _) => UpdateLeadControls();
        AddFilter(filters, "Synchronize nature", LeadNatureBox);
        LeadNatureBox.DropDownStyle = ComboBoxStyle.DropDownList;
        AddFilter(filters, "Lead level", LeadLevel);
        LeadLevel.Minimum = 1;
        LeadLevel.Maximum = 100;
        LeadLevel.Value = 100;
        AddFilter(filters, "Shiny filter", ShinyBox);
        ShinyBox.DropDownStyle = ComboBoxStyle.DropDownList;
        AddFilter(filters, "Initial seed (hex)", SeedBox);
        SeedBox.CharacterCasing = CharacterCasing.Upper;
        SeedBox.Font = new Font(FontFamily.GenericMonospace, Font.Size);
        AddFilter(filters, "Starting frame", StartFrame);
        StartFrame.Maximum = 1_000_000_000;
        StartFrame.ThousandsSeparator = true;
        AddFilter(filters, "Frames to scan", FrameCount);
        FrameCount.Minimum = 1;
        FrameCount.Maximum = 1_000_000_000;
        FrameCount.Value = 100_000;
        FrameCount.ThousandsSeparator = true;
        AddFilter(filters, "Worker threads", WorkerCount);
        WorkerCount.Minimum = 1;
        WorkerCount.Maximum = 64;
        WorkerCount.Value = Math.Clamp(Environment.ProcessorCount, 1, 64);
        AddFilter(filters, "Maximum results", MaximumResults);
        MaximumResults.Minimum = 1;
        MaximumResults.Maximum = 500;
        MaximumResults.Value = 100;

        LegalOnly.Text = "Require PKHeX legality";
        LegalOnly.Checked = true;
        LegalOnly.AutoSize = true;
        LegalOnly.Margin = new Padding(3, 10, 3, 6);
        filters.Controls.Add(LegalOnly);

        var actions = new FlowLayoutPanel { AutoSize = true };
        SearchButton.Text = "Search";
        SearchButton.AutoSize = true;
        SearchButton.Click += Search_Click;
        CancelSearchButton.Text = "Cancel";
        CancelSearchButton.AutoSize = true;
        CancelSearchButton.Enabled = false;
        CancelSearchButton.Click += (_, _) => SearchCancellation?.Cancel();
        ResetButton.Text = "Reset";
        ResetButton.AutoSize = true;
        ResetButton.Click += (_, _) => ResetFilters();
        RecoverButton.Text = "Recover Editor Pokémon";
        RecoverButton.AutoSize = true;
        RecoverButton.Click += (_, _) => RecoverEditorPokemon();
        AdvancedButton.Text = "Advanced Filters";
        AdvancedButton.AutoSize = true;
        AdvancedButton.Click += (_, _) => EditAdvancedFilters();
        actions.Controls.AddRange([SearchButton, CancelSearchButton, ResetButton, AdvancedButton, RecoverButton]);
        filters.Controls.Add(actions);

        Trainer.AutoSize = true;
        Trainer.MaximumSize = new Size(235, 0);
        Trainer.Margin = new Padding(3, 14, 3, 3);
        filters.Controls.Add(Trainer);

        var results = new SplitContainer
        {
            Size = new Size(970, 720),
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
            SplitterDistance = 670,
            Panel2MinSize = 285,
        };
        outer.Panel2.Controls.Add(results);

        ConfigureGrid();
        results.Panel1.Controls.Add(Grid);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        results.Panel2.Controls.Add(right);

        Details.Dock = DockStyle.Fill;
        Details.ReadOnly = true;
        Details.BackColor = SystemColors.Window;
        Details.Font = new Font(FontFamily.GenericMonospace, Font.Size);
        right.Controls.Add(Details, 0, 0);

        var resultActions = new FlowLayoutPanel { AutoSize = true };
        ViewButton.Text = "View in Editor";
        ViewButton.AutoSize = true;
        ViewButton.Click += (_, _) => ViewSelected();
        SetBoxButton.Text = "Set to Box";
        SetBoxButton.AutoSize = true;
        SetBoxButton.Click += (_, _) => SetSelectedToBox();
        CopyButton.Text = "Copy Seed Recipe";
        CopyButton.AutoSize = true;
        CopyButton.Click += (_, _) => CopyRecipe();
        ProofButton.Text = "RNG Proof";
        ProofButton.AutoSize = true;
        ProofButton.Click += (_, _) => ShowRngProof();
        resultActions.Controls.AddRange([ViewButton, SetBoxButton, CopyButton, ProofButton]);
        right.Controls.Add(resultActions, 0, 1);

        Status.AutoSize = true;
        Status.Margin = new Padding(3, 8, 3, 3);
        Status.Text = "Ready.";
        right.Controls.Add(Status, 0, 2);
        ShowSelectedDetails();
    }

    private void ConfigureGrid()
    {
        Grid.Dock = DockStyle.Fill;
        Grid.ReadOnly = true;
        Grid.AllowUserToAddRows = false;
        Grid.AllowUserToDeleteRows = false;
        Grid.AllowUserToResizeRows = false;
        Grid.AutoGenerateColumns = false;
        Grid.MultiSelect = false;
        Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Grid.RowHeadersVisible = false;
        Grid.DataSource = ResultSource;
        Grid.SelectionChanged += (_, _) => ShowSelectedDetails();
        Grid.CellDoubleClick += (_, _) => ViewSelected();
        AddColumn("Frame", nameof(DisplayResult.Frame), 76);
        AddColumn("State", nameof(DisplayResult.State), 105);
        AddColumn("Species", nameof(DisplayResult.Species), 100);
        AddColumn("Lv.", nameof(DisplayResult.Level), 42);
        AddColumn("Nature", nameof(DisplayResult.Nature), 72);
        AddColumn("Lead", nameof(DisplayResult.Lead), 135);
        AddColumn("Shiny", nameof(DisplayResult.Shiny), 50);
        AddColumn("XOR", nameof(DisplayResult.ShinyValue), 48);
        AddColumn("IVs", nameof(DisplayResult.IVs), 145);
        AddColumn("PID", nameof(DisplayResult.PID), 85);
        AddColumn("Encounter", nameof(DisplayResult.Encounter), 145);
        AddColumn("Legal", nameof(DisplayResult.Legality), 58);
    }

    private static void AddFilter(TableLayoutPanel panel, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 2) });
        control.Width = 230;
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        panel.Controls.Add(control);
    }

    private void AddColumn(string header, string property, int width) => Grid.Columns.Add(new DataGridViewTextBoxColumn
    {
        HeaderText = header,
        DataPropertyName = property,
        Width = width,
        SortMode = DataGridViewColumnSortMode.Automatic,
    });

    private void PopulateFilters()
    {
        CategoryBox.DataSource = new[]
        {
            new Choice<SeedEncounterCategory>(SeedEncounterCategory.All, "Wild and static"),
            new Choice<SeedEncounterCategory>(SeedEncounterCategory.Wild, "Wild Method H"),
            new Choice<SeedEncounterCategory>(SeedEncounterCategory.Static, "Static Method 1"),
        };
        ShinyBox.DataSource = new[]
        {
            new Choice<ShinySearchFilter>(ShinySearchFilter.Any, "Any"),
            new Choice<ShinySearchFilter>(ShinySearchFilter.ShinyOnly, "Shiny only"),
            new Choice<ShinySearchFilter>(ShinySearchFilter.NonShinyOnly, "Non-shiny only"),
        };
        LeadNatureBox.DataSource = Enumerable.Range(0, 25)
            .Select(z => new Choice<Nature>((Nature)z, GameInfo.Strings.Natures[z]))
            .ToArray();
        SeedBox.Text = "00000000";
    }

    public void RefreshLoadedSave()
    {
        var names = GameInfo.Strings.Species;
        var choices = Enumerable.Range(1, Math.Min(Save.MaxSpeciesID, names.Count - 1))
            .Select(z => new SpeciesChoice((ushort)z, names[z]))
            .OrderBy(z => z.Name)
            .ToArray();
        var current = PokemonEditor.Data.Species;
        SpeciesBox.DataSource = choices;
        SpeciesBox.DisplayMember = nameof(SpeciesChoice.Name);
        SpeciesBox.ValueMember = nameof(SpeciesChoice.ID);
        if (current != 0)
            SpeciesBox.SelectedValue = current;
        PopulateLeadChoices();

        var source = SupportedPretGames.GetSourceRepository(Save.Version);
        Trainer.Text = SupportedPretGames.IsSupported(Save.Version)
            ? $"Loaded save trainer\nOT: {Save.OT}\nTID: {Save.TID16:00000}\nSID: {Save.SID16:00000}\nGame: {Save.Version}\nSource: {source}"
            : $"Game {Save.Version} is not supported because SED only includes games with matching pret source repositories.";
        SearchButton.Enabled = SupportedPretGames.IsSupported(Save.Version);
    }

    private void PopulateLeadChoices()
    {
        var previous = LeadBox.SelectedItem is Choice<SeedLeadAbility> selected
            ? selected.Value
            : SeedLeadAbility.None;
        var values = Save.Version == GameVersion.E
            ? new[]
            {
                new Choice<SeedLeadAbility>(SeedLeadAbility.None, "None"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.Synchronize, "Synchronize"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.CuteCharmMale, "Cute Charm (male lead)"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.CuteCharmFemale, "Cute Charm (female lead)"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.Static, "Static"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.MagnetPull, "Magnet Pull"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.Pressure, "Pressure"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.Hustle, "Hustle"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.VitalSpirit, "Vital Spirit"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.Intimidate, "Intimidate"),
                new Choice<SeedLeadAbility>(SeedLeadAbility.KeenEye, "Keen Eye"),
            }
            : [new Choice<SeedLeadAbility>(SeedLeadAbility.None, "None — lead effects are unavailable in this game")];
        LeadBox.DataSource = values;
        LeadBox.SelectedIndex = Array.FindIndex(values, z => z.Value == previous) is var index and >= 0 ? index : 0;
        UpdateLeadControls();
    }

    private void UpdateLeadControls()
    {
        var ability = LeadBox.SelectedItem is Choice<SeedLeadAbility> selected
            ? selected.Value
            : SeedLeadAbility.None;
        LeadNatureBox.Enabled = ability == SeedLeadAbility.Synchronize;
        LeadLevel.Enabled = ability is SeedLeadAbility.Intimidate or SeedLeadAbility.KeenEye;
    }

    private void ResetFilters()
    {
        SeedBox.Text = "00000000";
        StartFrame.Value = 0;
        FrameCount.Value = 100_000;
        WorkerCount.Value = Math.Clamp(Environment.ProcessorCount, 1, 64);
        MaximumResults.Value = 100;
        CategoryBox.SelectedIndex = 0;
        LeadBox.SelectedIndex = 0;
        LeadNatureBox.SelectedIndex = 0;
        LeadLevel.Value = 100;
        ShinyBox.SelectedIndex = 0;
        LegalOnly.Checked = true;
        AdvancedFilters = SeedSearchFilters.Any;
        UpdateAdvancedFilterLabel();
        ApplyResults([]);
        Status.Text = "Ready.";
    }

    private async void Search_Click(object? sender, EventArgs e)
    {
        if (!TryParseSeed(SeedBox.Text, out var seed))
        {
            MessageBox.Show(this, "Enter an eight digit hexadecimal Generation III seed.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (SpeciesBox.SelectedItem is not SpeciesChoice species ||
            CategoryBox.SelectedItem is not Choice<SeedEncounterCategory> category ||
            LeadBox.SelectedItem is not Choice<SeedLeadAbility> lead ||
            LeadNatureBox.SelectedItem is not Choice<Nature> leadNature ||
            ShinyBox.SelectedItem is not Choice<ShinySearchFilter> shiny)
            return;

        SearchCancellation?.Dispose();
        SearchCancellation = new CancellationTokenSource();
        ToggleSearching(true);
        Status.Text = shiny.Value == ShinySearchFilter.ShinyOnly
            ? $"Scanning frames with {WorkerCount.Value} workers for independently validated shiny encounters…"
            : $"Scanning deterministic encounter frames with {WorkerCount.Value} workers…";

        try
        {
            var request = new SeedSearchRequest(
                species.ID,
                seed,
                (int)StartFrame.Value,
                (int)FrameCount.Value,
                (int)MaximumResults.Value,
                shiny.Value,
                category.Value,
                LegalOnly.Checked,
                new SeedLeadSettings(lead.Value, leadNature.Value, (byte)LeadLevel.Value),
                (int)WorkerCount.Value,
                AdvancedFilters);
            var found = await Task.Run(() => Gen3SeedSearcher.Search(Save, request, SearchCancellation.Token));
            ApplyResults(found);
            Status.Text = found.Count == 0
                ? "No encounters matched this seed range and validation policy."
                : $"Found {found.Count} validated result(s). Double-click a row to view it in PKHeX.";
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Search cancelled.";
        }
        catch (Exception ex)
        {
            Status.Text = "Search failed.";
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleSearching(false);
        }
    }

    private void ToggleSearching(bool searching)
    {
        SearchButton.Enabled = !searching && SupportedPretGames.IsSupported(Save.Version);
        CancelSearchButton.Enabled = searching;
        ResetButton.Enabled = !searching;
    }

    public void ApplyResults(IReadOnlyList<SeedEncounterResult> results)
    {
        ResultSource.DataSource = results.Select(DisplayResult.Create).ToArray();
        if (Grid.Rows.Count > 0)
            Grid.Rows[0].Selected = true;
        ShowSelectedDetails();
    }

    public void ConfigureDemonstration(ushort species, uint seed, int start, int frames, ShinySearchFilter shiny)
    {
        SpeciesBox.SelectedValue = species;
        SeedBox.Text = seed.ToString("X8", CultureInfo.InvariantCulture);
        StartFrame.Value = start;
        FrameCount.Value = frames;
        ShinyBox.SelectedIndex = shiny switch
        {
            ShinySearchFilter.Any => 0,
            ShinySearchFilter.ShinyOnly => 1,
            _ => 2,
        };
    }

    private static bool TryParseSeed(string text, out uint value)
    {
        value = 0;
        var clean = text.Trim();
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            clean = clean[2..];
        return clean.Length is > 0 and <= 8 && uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private void RecoverEditorPokemon()
    {
        if (!TryParseSeed(SeedBox.Text, out var initialSeed))
        {
            MessageBox.Show(this, "Enter the reference initial seed before recovering a frame.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            var pokemon = PokemonEditor.Data.Clone();
            var recovered = Gen3SeedRecovery.Recover(Save, pokemon, initialSeed);
            var form = new SeedRecoveryForm(pokemon, initialSeed, recovered);
            form.Show(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EditAdvancedFilters()
    {
        using var form = new AdvancedFilterForm(AdvancedFilters);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;
        AdvancedFilters = form.Filters;
        UpdateAdvancedFilterLabel();
    }

    private void UpdateAdvancedFilterLabel() => AdvancedButton.Text = AdvancedFilters.ActiveCount == 0
        ? "Advanced Filters"
        : $"Advanced Filters ({AdvancedFilters.ActiveCount})";

    private void ShowSelectedDetails()
    {
        var selected = Selected;
        Details.Text = selected?.Details ?? string.Empty;
        ViewButton.Enabled = SetBoxButton.Enabled = CopyButton.Enabled = ProofButton.Enabled = selected is not null;
    }

    private void ViewSelected()
    {
        if (Selected is { } selected)
            PokemonEditor.PopulateFields(selected.Result.Pokemon.Clone(), true);
    }

    private void SetSelectedToBox()
    {
        if (Selected is not { } selected)
            return;
        var start = SaveEditor.CurrentBox * Save.BoxSlotCount - 1;
        var slot = Save.NextOpenBoxSlot(start);
        if (slot < 0)
        {
            MessageBox.Show(this, "No empty box slot was found after the current position.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Save.SetBoxSlotAtIndex(selected.Result.Pokemon.Clone(), slot);
        SaveEditor.ReloadSlots();
        Status.Text = $"Placed {selected.Species} into box slot {slot + 1}.";
    }

    private void CopyRecipe()
    {
        if (Selected is not { } selected)
            return;
        Clipboard.SetText(selected.Recipe);
        Status.Text = "Seed recipe copied.";
    }

    private void ShowRngProof()
    {
        if (Selected is { } selected)
            new RngProofForm(selected.Result).Show(this);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SearchCancellation?.Cancel();
        SearchCancellation?.Dispose();
        base.OnFormClosing(e);
    }

    private sealed record SpeciesChoice(ushort ID, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record Choice<T>(T Value, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed class DisplayResult
    {
        public required SeedEncounterResult Result { get; init; }
        public required int Frame { get; init; }
        public required string State { get; init; }
        public required string Species { get; init; }
        public required int Level { get; init; }
        public required string Nature { get; init; }
        public required string Lead { get; init; }
        public required string Shiny { get; init; }
        public required ushort ShinyValue { get; init; }
        public required string IVs { get; init; }
        public required string PID { get; init; }
        public required string Encounter { get; init; }
        public required string Legality { get; init; }
        public required string Recipe { get; init; }
        public required string Details { get; init; }

        public static DisplayResult Create(SeedEncounterResult result)
        {
            var pk = result.Pokemon;
            var species = GameInfo.Strings.Species[pk.Species];
            var nature = GameInfo.Strings.Natures[(int)pk.Nature];
            var encounter = result.Encounter is IEncounterable named ? named.LongName : result.Encounter.GetType().Name;
            var location = GameInfo.GetLocationName(false, pk.MetLocation, pk.Format, pk.Generation, pk.Version);
            var ivs = $"{pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}";
            var recipe = string.Join(Environment.NewLine,
                "SED Seed Recipe",
                $"Game={pk.Version}",
                $"Species={species}",
                $"Encounter={encounter}",
                $"Method={result.Method}",
                $"Lead={result.Lead.Description}",
                $"InitialSeed=0x{result.InitialSeed:X8}",
                $"Frame={result.Frame}",
                $"State=0x{result.State:X8}",
                $"RNGCalls={result.Trace.Count}",
                $"ShinyValue={result.ShinyValidation.ShinyValue}",
                $"OT={pk.OriginalTrainerName}",
                $"TID={pk.TID16}",
                $"SID={pk.SID16}");
            var details = string.Join(Environment.NewLine,
                $"{species} — {encounter}",
                string.Empty,
                recipe,
                string.Empty,
                $"Level: {pk.CurrentLevel}",
                $"Nature: {nature}",
                $"Lead: {result.Lead.Description}",
                $"Shiny: {(result.ShinyValidation.IsShiny ? "Yes" : "No")}",
                $"Independent shiny value: {result.ShinyValidation.ShinyValue}",
                $"PKHeX agreement: {(result.ShinyValidation.AgreesWithPKHeX ? "Yes" : "No")}",
                $"PID: {pk.PID:X8}",
                $"IVs: {ivs}",
                $"Location: {location}",
                string.Empty,
                result.LegalityReport);
            return new DisplayResult
            {
                Result = result,
                Frame = result.Frame,
                State = $"0x{result.State:X8}",
                Species = species,
                Level = pk.CurrentLevel,
                Nature = nature,
                Lead = result.Lead.Description,
                Shiny = result.ShinyValidation.IsShiny ? "Yes" : "No",
                ShinyValue = result.ShinyValidation.ShinyValue,
                IVs = ivs,
                PID = $"{pk.PID:X8}",
                Encounter = encounter,
                Legality = result.IsLegal ? "Valid" : "Invalid",
                Recipe = recipe,
                Details = details,
            };
        }
    }
}
