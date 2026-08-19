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
        first.Select(z => z.Trace).Should().BeEquivalentTo(second.Select(z => z.Trace), options => options.WithStrictOrdering());
    }

    [Fact]
    public void RngProofEntriesFormAReplayableStateChain()
    {
        SeedEncounterResult result = Gen3SeedSearcher.Search(
            CreateEmeraldSave(),
            CreateLeadRequest(Species.Abra, SeedLeadSettings.None) with { MaximumResults = 1 },
            TestContext.Current.CancellationToken).Single();

        result.Trace.Should().NotBeEmpty();
        result.Trace[0].StateBefore.Should().Be(result.State);
        for (var index = 0; index < result.Trace.Count; index++)
        {
            SeedRngTraceEntry entry = result.Trace[index];
            entry.StateAfter.Should().Be(LCRNG.Next(entry.StateBefore));
            entry.Output.Should().Be((ushort)(entry.StateAfter >> 16));
            if (index != 0)
                entry.StateBefore.Should().Be(result.Trace[index - 1].StateAfter);
        }
    }

    [Fact]
    public void MethodOneProofContainsPidAndIvCalls()
    {
        var request = new SeedSearchRequest(
            (ushort)Species.Rayquaza,
            0,
            0,
            1_000,
            1,
            ShinySearchFilter.Any,
            SeedEncounterCategory.Static,
            false,
            SeedLeadSettings.None);

        SeedEncounterResult result = Gen3SeedSearcher.Search(CreateEmeraldSave(), request, TestContext.Current.CancellationToken).Single();

        result.Trace.Should().HaveCount(4);
        result.Trace.Select(z => z.Operation).Should().Equal("PID low", "PID high", "IV word 1", "IV word 2");
    }

    [Theory]
    [InlineData(SeedRngMethod.Method2, PIDType.Method_2)]
    [InlineData(SeedRngMethod.Method4, PIDType.Method_4)]
    public void AlternateMethodsMatchPkhexCorrelationAnalysis(SeedRngMethod method, PIDType expected)
    {
        var request = new SeedSearchRequest(
            (ushort)Species.Rayquaza,
            0,
            0,
            1_000,
            1,
            ShinySearchFilter.Any,
            SeedEncounterCategory.Static,
            false,
            SeedLeadSettings.None,
            RngMethod: method);

        SeedEncounterResult result = Gen3SeedSearcher.Search(CreateEmeraldSave(), request, TestContext.Current.CancellationToken).Single();

        MethodFinder.Analyze(result.Pokemon).Type.Should().Be(expected);
        result.Trace.Should().HaveCount(5);
        result.Trace.Should().ContainSingle(z => z.Operation == "VBlank interruption");
    }

    [Fact]
    public void AllMethodsRetainTheirDistinctFrameAndMethodLabels()
    {
        var request = new SeedSearchRequest(
            (ushort)Species.Rayquaza,
            0,
            0,
            1,
            10,
            ShinySearchFilter.Any,
            SeedEncounterCategory.Static,
            false,
            SeedLeadSettings.None,
            RngMethod: SeedRngMethod.Any);

        IReadOnlyList<SeedEncounterResult> results = Gen3SeedSearcher.Search(CreateEmeraldSave(), request, TestContext.Current.CancellationToken);

        results.Select(z => z.Frame).Should().OnlyContain(z => z == 0);
        results.Select(z => z.Method).Should().BeEquivalentTo("Method 1", "Method 2", "Method 4");
    }

    [Fact]
    public void AdvancedFiltersConstrainManipulationResults()
    {
        var filters = new SeedSearchFilters(
            Nature: (int)Nature.Timid,
            Gender: (int)Gender.Female,
            AbilitySlot: 0,
            MinimumHP: 10,
            MinimumSpeed: 20);
        var request = CreateLeadRequest(Species.Abra, SeedLeadSettings.None) with
        {
            FrameCount = 500_000,
            MaximumResults = 10,
            Filters = filters,
        };

        IReadOnlyList<SeedEncounterResult> results = Gen3SeedSearcher.Search(CreateEmeraldSave(), request, TestContext.Current.CancellationToken);

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(z => z.Pokemon.Nature == Nature.Timid);
        results.Should().OnlyContain(z => z.Pokemon.Gender == (byte)Gender.Female);
        results.Should().OnlyContain(z => (z.Pokemon.PID & 1) == 0 && z.Pokemon.IV_HP >= 10 && z.Pokemon.IV_SPE >= 20);
    }

    [Fact]
    public void ReverseSolverPreservesExactCalculatedFrames()
    {
        SAV3E save = CreateEmeraldSave();
        var request = CreateLeadRequest(Species.Abra, SeedLeadSettings.None) with
        {
            InitialSeed = 0x12345678,
            StartFrame = 250,
            FrameCount = 20_000,
            MaximumResults = 1,
            RequireLegal = false,
        };
        SeedEncounterResult target = Gen3SeedSearcher.Search(save, request, TestContext.Current.CancellationToken).Single();
        var pk = target.Pokemon;
        var filters = new SeedSearchFilters(
            ExactPID: pk.PID,
            ExactHP: pk.IV_HP,
            ExactAttack: pk.IV_ATK,
            ExactDefense: pk.IV_DEF,
            ExactSpecialAttack: pk.IV_SPA,
            ExactSpecialDefense: pk.IV_SPD,
            ExactSpeed: pk.IV_SPE);

        IReadOnlyList<SeedEncounterResult> solved = Gen3SeedSearcher.Search(save, request with
        {
            MaximumResults = 20,
            Filters = filters,
        }, TestContext.Current.CancellationToken);

        solved.Should().NotBeEmpty();
        solved.Should().OnlyContain(z => z.Pokemon.PID == pk.PID && z.Pokemon.IV32 == pk.IV32);
        solved.Should().OnlyContain(z => LCRNG.Advance(request.InitialSeed, z.Frame) == z.State);
    }

    [Fact]
    public void SafariEnvironmentAndTextSearchReturnExactFrames()
    {
        var filters = new SeedSearchFilters(
            Environment: SeedEncounterEnvironment.SafariZone,
            EncounterSearch: "Safari Zone");
        var request = CreateLeadRequest(Species.Pikachu, SeedLeadSettings.None) with
        {
            FrameCount = 100_000,
            MaximumResults = 5,
            RequireLegal = false,
            Filters = filters,
        };

        IReadOnlyList<SeedEncounterResult> results = Gen3SeedSearcher.Search(CreateEmeraldSave(), request, TestContext.Current.CancellationToken);

        results.Should().NotBeEmpty();
        results.All(z => z.Encounter is EncounterSlot3 { IsSafari: true }).Should().BeTrue();
        results.Should().OnlyContain(z => LCRNG.Advance(request.InitialSeed, z.Frame) == z.State);
    }

    [Fact]
    public void ExactFrameFilterSearchesOnlyTheRequestedFrame()
    {
        SAV3E save = CreateEmeraldSave();
        var request = CreateLeadRequest(Species.Abra, SeedLeadSettings.None) with
        {
            InitialSeed = 0x12345678,
            StartFrame = 250,
            FrameCount = 20_000,
            MaximumResults = 1,
            RequireLegal = false,
        };
        SeedEncounterResult target = Gen3SeedSearcher.Search(save, request, TestContext.Current.CancellationToken).Single();

        IReadOnlyList<SeedEncounterResult> exact = Gen3SeedSearcher.Search(save, request with
        {
            StartFrame = 0,
            FrameCount = 1,
            MaximumResults = 100,
            Filters = new SeedSearchFilters(ExactFrame: target.Frame),
        }, TestContext.Current.CancellationToken);

        exact.Should().NotBeEmpty();
        exact.Should().OnlyContain(z => z.Frame == target.Frame);
        exact.Should().Contain(z => z.Pokemon.PID == target.Pokemon.PID && z.Pokemon.IV32 == target.Pokemon.IV32);
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

    [Fact]
    public void RecoveryFindsTheEncounterSeedAndFrameOfAGeneratedWildPokemon()
    {
        SAV3E save = CreateEmeraldSave();
        var request = CreateLeadRequest(Species.Abra, SeedLeadSettings.None) with
        {
            InitialSeed = 0x12345678,
            StartFrame = 250,
            FrameCount = 20_000,
            MaximumResults = 1,
        };
        SeedEncounterResult generated = Gen3SeedSearcher.Search(save, request, TestContext.Current.CancellationToken).Single();

        IReadOnlyList<SeedRecoveryResult> recovered = Gen3SeedRecovery.Recover(save, generated.Pokemon, request.InitialSeed);

        SeedRecoveryResult candidate = recovered.First(z => z.LocationMatches && z.LevelMatches && z.Lead == LeadRequired.None);
        IReadOnlyList<SeedEncounterResult> replayed = Gen3SeedSearcher.Search(save, request with
        {
            StartFrame = (int)candidate.Frame,
            FrameCount = 1,
            MaximumResults = 20,
        }, TestContext.Current.CancellationToken);

        replayed.Should().Contain(z => z.Pokemon.PID == generated.Pokemon.PID && z.Pokemon.IV32 == generated.Pokemon.IV32);
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
