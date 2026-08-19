using FluentAssertions;
using PKHeX.Core;
using SED.Plugin;
using Xunit;

namespace SED.Tests;

public sealed class SeedEncounterPluginTests
{
    [Fact]
    public void PluginRegistersAfterEncounterDatabase()
    {
        using var menu = new MenuStrip();
        var tools = new ToolStripMenuItem("Tools") { Name = "Menu_Tools" };
        var data = new ToolStripMenuItem("Data") { Name = "Menu_Data" };
        var encounters = new ToolStripMenuItem("Encounter Database") { Name = "Menu_EncDatabase" };
        data.DropDownItems.Add(encounters);
        tools.DropDownItems.Add(data);
        menu.Items.Add(tools);

        var save = new SAV3E();
        PKM pokemon = save.BlankPKM;
        var plugin = new SeedEncounterPlugin();
        plugin.Initialize(new TestSaveProvider(save), new TestPokemonView(pokemon), menu);

        ToolStripItem[] registered = data.DropDownItems.Find("Menu_SED", false);
        registered.Should().ContainSingle();
        data.DropDownItems.IndexOf(registered[0]).Should().Be(data.DropDownItems.IndexOf(encounters) + 1);
    }

    private sealed class TestSaveProvider(SaveFile save) : ISaveFileProvider
    {
        public SaveFile SAV { get; } = save;
        public int CurrentBox => 0;
        public void ReloadSlots() { }
    }

    private sealed class TestPokemonView(PKM data) : IPKMView
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
