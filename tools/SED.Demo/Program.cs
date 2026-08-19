using System.Drawing.Imaging;
using PKHeX.Core;
using SED.Core;
using SED.UI;

namespace SED.Demo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string output = args.Length == 0
            ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "work", "demo-frames"))
            : Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);

        var save = new SAV3E
        {
            OT = "DEMO",
            TID16 = 32837,
            SID16 = 48749,
            Gender = 0,
            Language = (int)LanguageID.English,
        };
        PKM current = save.BlankPKM;
        current.Species = (ushort)Species.Abra;
        var provider = new DemoSaveProvider(save);
        var editor = new DemoPokemonView(current);

        using var form = new SeedEncounterDatabaseForm(provider, editor)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-30_000, -30_000),
            Size = new Size(1280, 760),
            ShowInTaskbar = false,
        };
        form.ConfigureDemonstration((ushort)Species.Abra, 0, 0, 1_000_000, ShinySearchFilter.ShinyOnly);
        form.Show();
        Application.DoEvents();

        SaveFrame(form, Path.Combine(output, "01-configured.png"), FindComboBox(form, "Shiny only"));

        Button search = FindControl<Button>(form, z => z.Text == "Search")
            ?? throw new InvalidOperationException("The SED search button was not found.");
        search.PerformClick();
        SaveFrame(form, Path.Combine(output, "02-searching.png"), search);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!search.Enabled && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Application.DoEvents();
        if (!search.Enabled)
            throw new TimeoutException("The demonstration seed search did not finish within thirty seconds.");

        DataGridView grid = FindControl<DataGridView>(form, _ => true)
            ?? throw new InvalidOperationException("The SED result grid was not found.");
        if (grid.Rows.Count == 0)
            throw new InvalidOperationException("The demonstration seed range did not produce a shiny Abra.");
        SaveFrame(form, Path.Combine(output, "03-results.png"), grid);

        Button view = FindControl<Button>(form, z => z.Text == "View in Editor")
            ?? throw new InvalidOperationException("The SED editor button was not found.");
        SaveFrame(form, Path.Combine(output, "04-view-in-editor.png"), view);
        view.PerformClick();
        File.WriteAllBytes(Path.Combine(output, "shiny-abra.pk3"), editor.Data.Data);
        form.Hide();

        Console.WriteLine($"Rendered {grid.Rows.Count} independently validated shiny Abra results to {output}");
        return 0;
    }

    private static ComboBox? FindComboBox(Control root, string selectedText) =>
        FindControl<ComboBox>(root, z => string.Equals(z.SelectedItem?.ToString(), selectedText, StringComparison.Ordinal));

    private static T? FindControl<T>(Control root, Predicate<T> predicate) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T candidate && predicate(candidate))
                return candidate;
            if (FindControl<T>(child, predicate) is { } nested)
                return nested;
        }
        return null;
    }

    private static void SaveFrame(Form form, string path, Control? focus)
    {
        using var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        if (focus is not null)
        {
            Point origin = form.PointToClient(focus.PointToScreen(Point.Empty));
            int border = (form.Width - form.ClientSize.Width) / 2;
            int title = form.Height - form.ClientSize.Height - border;
            origin.Offset(border, title);
            var highlight = Rectangle.Inflate(new Rectangle(origin, focus.Size), 5, 5);
            using var graphics = Graphics.FromImage(bitmap);
            using var pen = new Pen(Color.FromArgb(230, 208, 88, 0), 4);
            graphics.DrawRectangle(pen, highlight);
        }
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
