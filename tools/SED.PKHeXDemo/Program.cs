using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using PKHeX.Core;
using PKHeX.WinForms;

namespace SED.PKHeXDemo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Type program = typeof(Main).Assembly.GetType("PKHeX.WinForms.Program")
            ?? throw new TypeLoadException("PKHeX.WinForms.Program was not found.");
        RuntimeHelpers.RunClassConstructor(program.TypeHandle);

        if (args.Length != 2)
            throw new ArgumentException("usage: SED.PKHeXDemo SHINY_ABRA_PK3 OUTPUT_PNG");

        string pokemonPath = Path.GetFullPath(args[0]);
        string outputPath = Path.GetFullPath(args[1]);

        var save = new SAV3E
        {
            OT = "DEMO",
            TID16 = 32837,
            SID16 = 48749,
            Gender = 0,
            Language = (int)LanguageID.English,
        };
        PKM pokemon = EntityFormat.GetFromBytes(File.ReadAllBytes(pokemonPath), EntityContext.Gen3)
            ?? throw new InvalidOperationException("PKHeX could not parse the generated Abra file.");

        var startup = new StartupArguments();
        SetStartupValue(startup, nameof(StartupArguments.SAV), save);
        SetStartupValue(startup, nameof(StartupArguments.Entity), pokemon);

        using var main = new Main
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-30_000, -30_000),
            Size = new Size(1080, 720),
            ShowInTaskbar = false,
        };
        main.LoadInitialFiles(startup);
        main.Show();
        Application.DoEvents();
        InvokePrivate(main, "PKME_Tabs_UpdatePreviewSprite", main, EventArgs.Empty);
        Application.DoEvents();

        using var bitmap = new Bitmap(main.Width, main.Height, PixelFormat.Format32bppArgb);
        main.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        DrawPreviewSprite(main, bitmap);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        bitmap.Save(outputPath, ImageFormat.Png);
        main.Hide();

        Console.WriteLine($"Rendered the generated shiny Abra inside PKHeX 26.07.07 to {outputPath}");
        return 0;
    }

    private static void SetStartupValue(StartupArguments startup, string propertyName, object value)
    {
        var property = typeof(StartupArguments).GetProperty(propertyName)
            ?? throw new MissingMemberException(typeof(StartupArguments).FullName, propertyName);
        property.SetValue(startup, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        method.Invoke(target, arguments);
    }

    private static void DrawPreviewSprite(Main main, Bitmap bitmap)
    {
        FieldInfo field = typeof(Main).GetField("dragout", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Main).FullName, "dragout");
        if (field.GetValue(main) is not PictureBox { Image: { } sprite } preview)
            throw new InvalidOperationException("PKHeX did not generate an Abra preview sprite.");

        Point origin = main.PointToClient(preview.PointToScreen(Point.Empty));
        int border = (main.Width - main.ClientSize.Width) / 2;
        int title = main.Height - main.ClientSize.Height - border;
        origin.Offset(border + (preview.Width - sprite.Width) / 2, title + (preview.Height - sprite.Height) / 2);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.DrawImage(sprite, origin);
    }
}
