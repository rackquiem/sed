using FluentAssertions;
using PKHeX.Core;
using SED.Core;
using Xunit;

namespace SED.Tests;

public sealed class EmulatorBreakpointExporterTests
{
    [Fact]
    public void GeneratedMgbaScriptContainsExactTargetAndDebuggerHooks()
    {
        SeedEncounterResult result = GetResult();

        var lua = EmulatorBreakpointExporter.CreateLua(result, @"C:\targets\abra.ss1", @"C:\targets\abra_generation.ss1");

        lua.Should().Contain("local expectedCode = \"BPEE\"");
        lua.Should().Contain($"local targetState = 0x{result.State:X8}");
        lua.Should().Contain($"local targetFrame = {result.Frame}");
        lua.Should().Contain("setRangeWatchpoint");
        lua.Should().Contain("setBreakpoint");
        lua.Should().Contain("C:/targets/abra.ss1");
    }

    private static SeedEncounterResult GetResult()
    {
        var save = new SAV3E { OT = "TEST", TID16 = 1234, SID16 = 5678, Language = (int)LanguageID.English };
        var request = new SeedSearchRequest(
            (ushort)Species.Abra,
            0,
            0,
            10_000,
            1,
            ShinySearchFilter.Any,
            SeedEncounterCategory.Wild,
            false,
            SeedLeadSettings.None);
        return Gen3SeedSearcher.Search(save, request, TestContext.Current.CancellationToken).Single();
    }
}
