using FluentAssertions;
using PKHeX.Core;
using SED.Core;
using Xunit;

namespace SED.Tests;

public sealed class Gen3SeedSearcherTests
{
    [Fact]
    public void AbraShinySearchReturnsTrainerMatchedLegalResults()
    {
        SAV3E save = CreateEmeraldSave();
        var request = new SeedSearchRequest(
            Species: (ushort)Species.Abra,
            InitialSeed: 0,
            StartFrame: 0,
            FrameCount: 1_000_000,
            MaximumResults: 3,
            ShinyFilter: ShinySearchFilter.ShinyOnly,
            Category: SeedEncounterCategory.Wild,
            RequireLegal: true);

        IReadOnlyList<SeedEncounterResult> results = Gen3SeedSearcher.Search(save, request, TestContext.Current.CancellationToken);

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(z => z.ShinyValidation.IsShiny);
        results.Should().OnlyContain(z => z.ShinyValidation.ShinyValue < SeedShinyValidator.Generation3ShinyThreshold);
        results.Should().OnlyContain(z => z.ShinyValidation.AgreesWithPKHeX);
        results.Should().OnlyContain(z => z.IsLegal);
        results.Should().OnlyContain(z => z.Pokemon.TID16 == save.TID16 && z.Pokemon.SID16 == save.SID16);
        results.Should().OnlyContain(z => z.Pokemon.OriginalTrainerName == save.OT);
    }

    [Fact]
    public void RepeatedSearchProducesIdenticalFramesAndPids()
    {
        SAV3E save = CreateEmeraldSave();
        var request = new SeedSearchRequest(
            Species: (ushort)Species.Abra,
            InitialSeed: 0x12345678,
            StartFrame: 250,
            FrameCount: 25_000,
            MaximumResults: 10,
            ShinyFilter: ShinySearchFilter.Any,
            Category: SeedEncounterCategory.Wild,
            RequireLegal: true);

        IReadOnlyList<SeedEncounterResult> first = Gen3SeedSearcher.Search(save, request, TestContext.Current.CancellationToken);
        IReadOnlyList<SeedEncounterResult> second = Gen3SeedSearcher.Search(save, request, TestContext.Current.CancellationToken);

        first.Should().NotBeEmpty();
        first.Select(z => (z.Frame, z.Pokemon.PID, z.Pokemon.IV32))
            .Should().Equal(second.Select(z => (z.Frame, z.Pokemon.PID, z.Pokemon.IV32)));
    }

    private static SAV3E CreateEmeraldSave()
    {
        var save = new SAV3E
        {
            OT = "DEMO",
            TID16 = 32837,
            SID16 = 48749,
            Gender = 0,
            Language = (int)LanguageID.English,
        };
        return save;
    }
}
