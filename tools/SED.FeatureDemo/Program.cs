using System.Drawing.Imaging;
using System.Reflection;
using PKHeX.Core;
using SED.Core;
using SED.UI;

namespace SED.FeatureDemo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var output = args.Length == 0
            ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "work", "feature-frames"))
            : Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);

        RenderSafari(Path.Combine(output, "safari"));
        RenderHiddenPower(Path.Combine(output, "hidden-power"));
        Console.WriteLine($"Rendered SED feature demonstrations to {output}");
        return 0;
    }

    private static void RenderSafari(string output)
    {
        Directory.CreateDirectory(output);
        using var form = CreateForm(Species.Pikachu);
        Configure(form, Species.Pikachu, 150_000, "Wild Method H", "Safari Zone", "Method 1 / H1");
        form.Show();
        Application.DoEvents();
        SaveFrame(form, Path.Combine(output, "01-safari-configured.png"), FindComboBox(form, "Safari Zone"));

        Search(form);
        var grid = RequireControl<DataGridView>(form, _ => true, "result grid");
        SaveFrame(form, Path.Combine(output, "02-safari-results.png"), grid);

        var predictor = RequireControl<Button>(form, z => z.Text == "Safari Predictor", "Safari Predictor button");
        SaveFrame(form, Path.Combine(output, "03-safari-predictor-button.png"), predictor);
        predictor.PerformClick();
        Application.DoEvents();
        var prediction = Application.OpenForms.OfType<SafariPredictionForm>().Single();
        SaveFrame(prediction, Path.Combine(output, "04-safari-prediction.png"), RequireControl<DataGridView>(prediction, _ => true, "prediction grid"));
        prediction.Close();
    }

    private static void RenderHiddenPower(string output)
    {
        Directory.CreateDirectory(output);
        var filters = new SeedSearchFilters(HiddenPowerType: 15, ExactHiddenPower: 70);
        using (var advanced = new AdvancedFilterForm(filters)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-30_000, -30_000),
            ShowInTaskbar = false,
        })
        {
            advanced.Show();
            Application.DoEvents();
            SaveFrame(advanced, Path.Combine(output, "01-hidden-power-filter.png"), FindComboBox(advanced, "70"));
            advanced.Hide();
        }

        using var form = CreateForm(Species.Abra);
        Configure(form, Species.Abra, 200_000, "Wild Method H", "Any environment", "Method 4 / H4");
        SetAdvancedFilters(form, filters);
        form.Show();
        Application.DoEvents();
        SaveFrame(form, Path.Combine(output, "02-method4-configured.png"), FindComboBox(form, "Method 4 / H4"));

        var search = RequireControl<Button>(form, z => z.Text == "Search", "Search button");
        search.PerformClick();
        SaveFrame(form, Path.Combine(output, "03-method4-searching.png"), search);
        WaitForSearch(search);
        var grid = RequireControl<DataGridView>(form, _ => true, "result grid");
        if (grid.Rows.Count == 0)
            throw new InvalidOperationException("The Method H4 Hidden Power demonstration produced no results.");
        SaveFrame(form, Path.Combine(output, "04-method4-results.png"), grid);
    }

    private static SeedEncounterDatabaseForm CreateForm(Species species)
    {
        var save = new SAV3E
        {
            OT = "DEMO",
            TID16 = 32837,
            SID16 = 48749,
            Gender = 0,
            Language = (int)LanguageID.English,
        };
        PKM current = save.BlankPKM;
        current.Species = (ushort)species;
        return new SeedEncounterDatabaseForm(new DemoSaveProvider(save), new DemoPokemonView(current))
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-30_000, -30_000),
            Size = new Size(1280, 760),
            ShowInTaskbar = false,
        };
    }

    private static void Configure(SeedEncounterDatabaseForm form, Species species, int frames, string category, string environment, string method)
    {
        form.ConfigureDemonstration((ushort)species, 0, 0, frames, ShinySearchFilter.Any);
        SelectComboBox(form, category);
        SelectComboBox(form, environment);
        SelectComboBox(form, method);
    }

    private static void SetAdvancedFilters(SeedEncounterDatabaseForm form, SeedSearchFilters filters)
    {
        var field = typeof(SeedEncounterDatabaseForm).GetField("AdvancedFilters", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(SeedEncounterDatabaseForm), "AdvancedFilters");
        field.SetValue(form, filters);
        var update = typeof(SeedEncounterDatabaseForm).GetMethod("UpdateAdvancedFilterLabel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(SeedEncounterDatabaseForm), "UpdateAdvancedFilterLabel");
        update.Invoke(form, null);
    }

    private static void Search(SeedEncounterDatabaseForm form)
    {
        var search = RequireControl<Button>(form, z => z.Text == "Search", "Search button");
        search.PerformClick();
        SaveFrame(form, Path.Combine(Path.GetTempPath(), "sed-searching.png"), search);
        WaitForSearch(search);
        var grid = RequireControl<DataGridView>(form, _ => true, "result grid");
        if (grid.Rows.Count == 0)
            throw new InvalidOperationException("The Safari demonstration produced no results.");
    }

    private static void WaitForSearch(Button search)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!search.Enabled && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Application.DoEvents();
        if (!search.Enabled)
            throw new TimeoutException("The feature demonstration search exceeded thirty seconds.");
    }

    private static void SelectComboBox(Control root, string item)
    {
        var combo = FindComboBox(root, item) ?? throw new InvalidOperationException($"The {item} selector was not found.");
        combo.SelectedIndex = combo.Items.Cast<object>().Select(z => z.ToString()).ToList().IndexOf(item);
    }

    private static ComboBox? FindComboBox(Control root, string item) =>
        FindControl<ComboBox>(root, z => z.Items.Cast<object>().Any(value => value.ToString() == item));

    private static T RequireControl<T>(Control root, Predicate<T> predicate, string description) where T : Control =>
        FindControl(root, predicate) ?? throw new InvalidOperationException($"The {description} was not found.");

    private static T? FindControl<T>(Control root, Predicate<T> predicate) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T candidate && predicate(candidate))
                return candidate;
            if (FindControl(child, predicate) is { } nested)
                return nested;
        }
        return null;
    }

    private static void SaveFrame(Form form, string path, Control? focus)
    {
        Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        if (focus is not null)
        {
            var origin = form.PointToClient(focus.PointToScreen(Point.Empty));
            var border = (form.Width - form.ClientSize.Width) / 2;
            var title = form.Height - form.ClientSize.Height - border;
            origin.Offset(border, title);
            var highlight = Rectangle.Inflate(new Rectangle(origin, focus.Size), 5, 5);
            using var graphics = Graphics.FromImage(bitmap);
            using var pen = new Pen(Color.FromArgb(230, 208, 88, 0), 4);
            graphics.DrawRectangle(pen, highlight);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, ImageFormat.Png);
    }

    private sealed class DemoSaveProvider(SaveFile save) : ISaveFileProvider
    {
        public SaveFile SAV { get; } = save;
        public int CurrentBox => 0;
        public void ReloadSlots() { }
    }

    private sealed class DemoPokemonView(PKM data) : IPKMView
    {
        public PKM Data { get; private set; } = data;
        public bool Unicode => false;
        public bool HaX => false;
        public bool ChangingFields { get; set; }
        public bool EditsComplete => true;
        public PKM PreparePKM(bool click = true) => Data;
        public void PopulateFields(PKM pk, bool focus = true, bool skipConversionCheck = false) => Data = pk;
        public void NotifyWasExported(PKM pk) { }
    }
}
