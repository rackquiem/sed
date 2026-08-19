using PKHeX.Core;
using SED.UI;

namespace SED.Plugin;

public sealed class SeedEncounterPlugin : IPlugin
{
    public ISaveFileProvider SaveFileEditor { get; private set; } = null!;
    private IPKMView PokemonEditor { get; set; } = null!;
    private SeedEncounterDatabaseForm? OpenForm { get; set; }

    public string Name => "SED - Seed Encounter Database";
    public int Priority => 10;

    public void Initialize(params object[] args)
    {
        SaveFileEditor = args.OfType<ISaveFileProvider>().First();
        PokemonEditor = args.OfType<IPKMView>().First();
        var menu = args.OfType<ToolStrip>().First();
        RegisterMenu(menu);
    }

    private void RegisterMenu(ToolStrip menu)
    {
        if (menu.Items.Find("Menu_Tools", false).FirstOrDefault() is not ToolStripDropDownItem tools)
            return;
        if (tools.DropDownItems.Find("Menu_Data", false).FirstOrDefault() is not ToolStripDropDownItem data)
            return;
        if (data.DropDownItems.Find("Menu_SED", false).Length != 0)
            return;

        var item = new ToolStripMenuItem("SED - Seed Encounter Database")
        {
            Name = "Menu_SED",
            ToolTipText = "Search deterministic Generation III encounters by RNG seed.",
        };
        item.Click += (_, _) => OpenDatabase();
        var encounterDatabase = data.DropDownItems.Find("Menu_EncDatabase", false).FirstOrDefault();
        var index = encounterDatabase is null ? data.DropDownItems.Count : data.DropDownItems.IndexOf(encounterDatabase) + 1;
        data.DropDownItems.Insert(index, item);
    }

    private void OpenDatabase()
    {
        if (OpenForm is { IsDisposed: false })
        {
            OpenForm.Activate();
            return;
        }

        OpenForm = new SeedEncounterDatabaseForm(SaveFileEditor, PokemonEditor);
        OpenForm.FormClosed += (_, _) => OpenForm = null;
        var owner = (SaveFileEditor as Control)?.FindForm();
        if (owner is null)
            OpenForm.Show();
        else
            OpenForm.Show(owner);
    }

    public void NotifySaveLoaded() => OpenForm?.RefreshLoadedSave();
    public bool TryLoadFile(string filePath) => false;
    public void NotifyDisplayLanguageChanged(string language) { }
}
