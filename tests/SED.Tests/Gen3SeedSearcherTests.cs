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
            RequireLegal: true,
            Lead: SeedLeadSettings.None);

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
    public void ParallelAndSingleWorkerSearchesProduceIdenticalOrderedResults()
    {
        SAV3E save = CreateEmeraldSave();
        var request = new SeedSearchRequest(
            Species: (ushort)Species.Abra,
            InitialSeed: 0x12345678,
            StartFrame: 250,
            FrameCount: 500_000,
            MaximumResults: 10,
            ShinyFilter: ShinySearchFilter.ShinyOnly,
            Category: SeedEncounterCategory.Wild,
            RequireLegal: true,
            Lead: SeedLeadSettings.None);

        IReadOnlyList<SeedEncounterResult> first = Gen3SeedSearcher.Search(save, request with { WorkerCount = 1 }, TestContext.Current.CancellationToken);
        IReadOnlyList<SeedEncounterResult> second = Gen3SeedSearcher.Search(save, request with { WorkerCount = 4 }, TestContext.Current.CancellationToken);

        first.Should().NotBeEmpty();
        first.Select(z => (z.Frame, z.Pokemon.PID, z.Pokemon.IV32, z.Lead.Description))
            .Should().Equal(second.Select(z => (z.Frame, z.Pokemon.PID, z.Pokemon.IV32, z.Lead.Description)));
    }

    [Fact]
    public void SynchronizeActivatedFramesUseLeadNature()
    {
        var lead = new SeedLeadSettings(SeedLeadAbility.Synchronize, Nature.Adamant);

        IReadOnlyList<SeedEncounterResult> results = Gen3SeedSearcher.Search(
            CreateEmeraldSave(),
            CreateLeadRequest(Species.Abra, lead),
            TestContext.Current.CancellationToken);

        SeedEncounterResult[] activated = results.Where(z => z.Lead.Activated).ToArray();
        activated.Should().NotBeEmpty();
        activated.Should().OnlyContain(z => z.Pokemon.Nature == Nature.Adamant);
    }

    [Fact]
    public void CuteCharmActivatedFramesUseOppositeGender()
    {
        var lead = new SeedLeadSettings(SeedLeadAbility.CuteCharmMale);

        IReadOnlyList<SeedEncounterResult> results = Gen3SeedSearcher.Search(
            CreateEmeraldSave(),
            CreateLeadRequest(Species.Abra, lead),
            TestContext.Current.CancellationToken);

        SeedEncounterResult[] activated = results.Where(z => z.Lead.Activated).ToArray();
        activated.Should().NotBeEmpty();
        activated.Should().OnlyContain(z => z.Pokemon.Gender == (byte)Gender.Female);
    }

    [Fact]
    public void PressureActivatedFramesUseEncounterPressureLevel()
    {
        var lead = new SeedLeadSettings(SeedLeadAbility.Pressure);

        IReadOnlyList<SeedEncounterResult> results = Gen3SeedSearcher.Search(
            CreateEmeraldSave(),
            CreateLeadRequest(Species.Poochyena, lead),
            TestContext.Current.CancellationToken);

        SeedEncounterResult[] activated = results.Where(z => z.Lead.Activated).ToArray();
        activated.Should().NotBeEmpty();
        activated.Should().OnlyContain(z => z.Pokemon.MetLevel == ((EncounterSlot3)z.Encounter).PressureLevel);
    }

    [Theory]
    [InlineData(SeedLeadAbility.Static)]
    [InlineData(SeedLeadAbility.MagnetPull)]
    public void TypeAttractionCanSelectEligibleMagnemiteSlots(SeedLeadAbility ability)
    {
        var lead = new SeedLeadSettings(ability);

        IReadOnlyList<SeedEncounterResult> results = Gen3SeedSearcher.Search(
            CreateEmeraldSave(),
            CreateLeadRequest(Species.Magnemite, lead),
            TestContext.Current.CancellationToken);

        results.Should().Contain(z => z.Lead.Activated);
    }

    [Fact]
    public void EncounterModifyingLeadsAreRejectedOutsideEmerald()
    {
        var save = new SAV3FRLG { Version = GameVersion.FR };
        var request = CreateLeadRequest(Species.Pikachu, new SeedLeadSettings(SeedLeadAbility.Synchronize, Nature.Modest));

        Action search = () => Gen3SeedSearcher.Search(save, request, TestContext.Current.CancellationToken);

        search.Should().Throw<NotSupportedException>();
    }

    private static SeedSearchRequest CreateLeadRequest(Species species, SeedLeadSettings lead) => new(
        Species: (ushort)species,
        InitialSeed: 0,
        StartFrame: 0,
        FrameCount: 200_000,
        MaximumResults: 100,
        ShinyFilter: ShinySearchFilter.Any,
        Category: SeedEncounterCategory.Wild,
        RequireLegal: true,
        Lead: lead);

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
