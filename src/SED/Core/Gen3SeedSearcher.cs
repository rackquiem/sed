using System.Runtime.CompilerServices;
using PKHeX.Core;

namespace SED.Core;

/// <summary>
/// Generates exact Generation III Method H wild encounters and Method 1 static encounters.
/// </summary>
public static class Gen3SeedSearcher
{
    private const int FramesPerChunk = 32_768;

    public static IReadOnlyList<SeedEncounterResult> Search(
        SaveFile save,
        SeedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedPretGames.IsSupported(save.Version))
            throw new NotSupportedException($"{save.Version} is not backed by a supported pret source repository.");
        if (!request.Lead.IsSupported(save.Version))
            throw new NotSupportedException("Encounter-modifying lead abilities are implemented by Pokémon Emerald but not Ruby, Sapphire, FireRed, or LeafGreen.");
        if (request.Lead is { Ability: SeedLeadAbility.Synchronize, SynchronizeNature: Nature.Random })
            throw new ArgumentOutOfRangeException(nameof(request), "Synchronize requires a concrete lead nature.");
        if (request.FrameCount <= 0 || request.MaximumResults <= 0)
            return [];
        if (request.StartFrame < 0 || (long)request.StartFrame + request.FrameCount > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request), "The requested frame range exceeds the supported Generation III timeline.");

        var encounters = GetEncounters(save, request.Species, request.Category);
        var results = new Dictionary<ResultKey, SeedEncounterResult>();
        var chunkCount = (int)(((long)request.FrameCount + FramesPerChunk - 1) / FramesPerChunk);
        var workerCount = request.WorkerCount <= 0 ? Environment.ProcessorCount : request.WorkerCount;
        workerCount = Math.Clamp(workerCount, 1, Math.Min(64, chunkCount));
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = workerCount,
        };

        for (int batchStart = 0; batchStart < chunkCount; batchStart += workerCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batchCount = Math.Min(workerCount, chunkCount - batchStart);
            var batchResults = new List<SeedEncounterResult>?[batchCount];
            Parallel.For(0, batchCount, options, index =>
            {
                var chunk = batchStart + index;
                var offset = chunk * FramesPerChunk;
                var count = Math.Min(FramesPerChunk, request.FrameCount - offset);
                batchResults[index] = ScanChunk(save, encounters, request, offset, count, cancellationToken);
            });

            foreach (var chunk in batchResults)
            {
                foreach (var generated in chunk!)
                {
                    var key = GetKey(generated);
                    results.TryAdd(key, generated);
                }
            }
            if (results.Count >= request.MaximumResults)
                break;
        }
        return Sort(results.Values, request.MaximumResults);
    }

    private static List<SeedEncounterResult> ScanChunk(
        SaveFile save,
        IEncounterInfo[] encounters,
        SeedSearchRequest request,
        int frameOffset,
        int frameCount,
        CancellationToken cancellationToken)
    {
        var results = new List<SeedEncounterResult>(Math.Min(request.MaximumResults, 128));
        var seen = new HashSet<ResultKey>();
        var firstFrame = request.StartFrame + frameOffset;
        var state = LCRNG.Advance(request.InitialSeed, firstFrame);

        for (int offset = 0; offset < frameCount; offset++, state = LCRNG.Next(state))
        {
            if ((offset & 0xFFF) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            var frame = firstFrame + offset;
            foreach (var encounter in encounters)
            {
                var generated = GenerateExact(save, encounter, request.InitialSeed, state, frame, request.ShinyFilter, request.Lead);
                if (generated is null || (request.RequireLegal && !generated.IsLegal))
                    continue;
                if (!seen.Add(GetKey(generated)))
                    continue;
                results.Add(generated);
                if (results.Count >= request.MaximumResults)
                    return results;
            }
        }
        return results;
    }

    private static ResultKey GetKey(SeedEncounterResult result) =>
        new(result.Frame, result.Pokemon.PID, result.Pokemon.IV32, result.Pokemon.MetLocation);

    private static IReadOnlyList<SeedEncounterResult> Sort(IEnumerable<SeedEncounterResult> results, int maximum) =>
        results.OrderBy(z => z.Frame).ThenBy(z => z.Pokemon.MetLocation).ThenBy(z => z.Pokemon.PID).Take(maximum).ToArray();

    public static IEncounterInfo[] GetEncounters(SaveFile save, ushort species, SeedEncounterCategory category)
    {
        var template = save.BlankPKM;
        template.Species = species;
        template.Form = 0;
        template.SetGender(template.GetSaneGender());
        EncounterMovesetGenerator.OptimizeCriteria(template, save);
        ReadOnlyMemory<GameVersion> versions = new[] { save.Version };
        return EncounterMovesetGenerator.GenerateEncounters(template, ReadOnlyMemory<ushort>.Empty, versions)
            .Where(z => IsSupportedEncounter(z, category))
            .Distinct(ReferenceComparer.Instance)
            .ToArray();
    }

    private static bool IsSupportedEncounter(IEncounterInfo encounter, SeedEncounterCategory category) => category switch
    {
        SeedEncounterCategory.All => encounter is EncounterSlot3 or EncounterStatic3 { IsRoaming: false, IsEgg: false },
        SeedEncounterCategory.Wild => encounter is EncounterSlot3,
        SeedEncounterCategory.Static => encounter is EncounterStatic3 { IsRoaming: false, IsEgg: false },
        _ => false,
    };

    private static SeedEncounterResult? GenerateExact(
        SaveFile save,
        IEncounterInfo encounter,
        uint initialSeed,
        uint state,
        int frame,
        ShinySearchFilter shinyFilter,
        SeedLeadSettings lead)
    {
        var raw = encounter.ConvertToPKM(save, EncounterCriteria.Unrestricted);
        if (raw is not PK3 pokemon)
            return null;

        string method;
        SeedLeadOutcome leadOutcome;
        switch (encounter)
        {
            case EncounterSlot3 slot:
                if (!ApplyMethodHFrame(slot, pokemon, state, lead, out leadOutcome, null))
                    return null;
                method = "Method H";
                break;
            case EncounterStatic3 { IsRoaming: false, IsEgg: false }:
                ApplyMethod1Frame(pokemon, state, null);
                method = "Method 1";
                leadOutcome = lead.Ability == SeedLeadAbility.None
                    ? SeedLeadOutcome.None
                    : new SeedLeadOutcome(lead.Ability, false, "Not applicable to static Method 1 encounters");
                break;
            default:
                return null;
        }

        pokemon.ResetPartyStats();
        pokemon.RefreshChecksum();
        var shiny = SeedShinyValidator.Validate(pokemon, shinyFilter);
        if (!shiny.Valid)
            return null;

        var legality = new LegalityAnalysis(pokemon);
        var trace = BuildTrace(encounter, pokemon, state, lead);
        return new SeedEncounterResult(
            pokemon,
            encounter,
            initialSeed,
            state,
            frame,
            method,
            leadOutcome,
            shiny,
            legality.Valid,
            legality.Report(true),
            trace);
    }

    private static IReadOnlyList<SeedRngTraceEntry> BuildTrace(
        IEncounterInfo encounter,
        PK3 generated,
        uint state,
        SeedLeadSettings lead)
    {
        var replay = (PK3)generated.Clone();
        var trace = new List<SeedRngTraceEntry>();
        var success = encounter switch
        {
            EncounterSlot3 slot => ApplyMethodHFrame(slot, replay, state, lead, out _, trace),
            EncounterStatic3 { IsRoaming: false, IsEgg: false } => ApplyMethod1Trace(replay, state, trace),
            _ => false,
        };
        if (!success || replay.PID != generated.PID || replay.IV32 != generated.IV32)
            throw new InvalidOperationException("RNG proof replay did not reproduce the selected encounter.");
        return trace.ToArray();
    }

    private static bool ApplyMethod1Trace(PK3 pokemon, uint state, List<SeedRngTraceEntry> trace)
    {
        ApplyMethod1Frame(pokemon, state, trace);
        return true;
    }

    private static bool ApplyMethodHFrame(
        EncounterSlot3 slot,
        PK3 pokemon,
        uint state,
        SeedLeadSettings lead,
        out SeedLeadOutcome outcome,
        List<SeedRngTraceEntry>? trace)
    {
        outcome = SeedLeadOutcome.None;
        if (MethodH.IsEncounterCheckApplicable(slot.Type) &&
            !MethodH.CheckEncounterActivation(slot, state, LeadRequired.None, out _))
            return false;

        var rng = state;
        if (lead.Ability is SeedLeadAbility.Static or SeedLeadAbility.MagnetPull)
        {
            var proc = Next16(ref rng, trace, "Lead activation", $"{GetLeadName(lead.Ability)} activation check");
            var activated = (proc & 1) == 0;
            if (activated)
            {
                var filteredSlot = Next16(ref rng, trace, "Type attraction slot", "Select an eligible ability filtered slot");
                var matches = lead.Ability switch
                {
                    SeedLeadAbility.Static => slot.StaticCount != 0 && filteredSlot % slot.StaticCount == slot.StaticIndex,
                    SeedLeadAbility.MagnetPull => slot.MagnetPullCount != 0 && filteredSlot % slot.MagnetPullCount == slot.MagnetPullIndex,
                    _ => false,
                };
                if (!matches)
                    return false;
            }
            else if (!MatchesRegularSlot(slot, Next16(ref rng, trace, "Encounter slot", $"Regular slot {slot.SlotNumber}")))
            {
                return false;
            }
            outcome = CreateOutcome(lead.Ability, activated);
        }
        else if (!MatchesRegularSlot(slot, Next16(ref rng, trace, "Encounter slot", $"Regular slot {slot.SlotNumber}")))
        {
            return false;
        }

        var levelRoll = Next16(ref rng, trace, "Encounter level", $"Level range {slot.LevelMin} to {slot.LevelMax}");
        var level = (byte)MethodH.GetRandomLevel(slot, levelRoll, LeadRequired.None);
        byte nature;
        byte? requiredGender = null;

        switch (lead.Ability)
        {
            case SeedLeadAbility.Synchronize:
                {
                    var activated = (Next16(ref rng, trace, "Synchronize activation", "Even output activates") & 1) == 0;
                    nature = activated
                        ? (byte)lead.SynchronizeNature
                        : (byte)(Next16(ref rng, trace, "Nature", "Synchronize failed so nature is rolled") % 25);
                    outcome = new SeedLeadOutcome(lead.Ability, activated,
                        activated ? $"Synchronize activated ({lead.SynchronizeNature})" : "Synchronize failed");
                    break;
                }
            case SeedLeadAbility.CuteCharmMale:
            case SeedLeadAbility.CuteCharmFemale:
                {
                    var ratio = pokemon.PersonalInfo.Gender;
                    var applicable = ratio is not (PersonalInfo.RatioMagicGenderless or PersonalInfo.RatioMagicFemale or PersonalInfo.RatioMagicMale);
                    if (applicable)
                    {
                        var activated = Next16(ref rng, trace, "Cute Charm activation", "Output modulo 3 controls activation") % 3 != 0;
                        if (activated)
                            requiredGender = lead.Ability == SeedLeadAbility.CuteCharmMale ? (byte)Gender.Female : (byte)Gender.Male;
                        outcome = CreateOutcome(lead.Ability, activated);
                    }
                    else
                    {
                        outcome = new SeedLeadOutcome(lead.Ability, false, "Cute Charm is inapplicable to this species");
                    }
                    nature = (byte)(Next16(ref rng, trace, "Nature", "Target nature for PID rejection loop") % 25);
                    break;
                }
            case SeedLeadAbility.Pressure:
            case SeedLeadAbility.Hustle:
            case SeedLeadAbility.VitalSpirit:
                {
                    var activated = (Next16(ref rng, trace, "Level lead activation", "Odd output activates") & 1) == 1;
                    level = activated
                        ? slot.PressureLevel
                        : (byte)MethodH.GetRandomLevel(slot, levelRoll, LeadRequired.PressureHustleSpiritFail);
                    nature = (byte)(Next16(ref rng, trace, "Nature", "Target nature for PID rejection loop") % 25);
                    outcome = CreateOutcome(lead.Ability, activated);
                    break;
                }
            case SeedLeadAbility.Intimidate:
            case SeedLeadAbility.KeenEye:
                {
                    if (lead.LeadLevel >= level + 5)
                    {
                        var repelsEncounter = (Next16(ref rng, trace, "Repel check", "Odd output rejects the encounter") & 1) == 1;
                        if (repelsEncounter)
                            return false;
                        outcome = new SeedLeadOutcome(lead.Ability, false, $"{GetLeadName(lead.Ability)} failed to repel");
                    }
                    else
                    {
                        outcome = new SeedLeadOutcome(lead.Ability, false, $"{GetLeadName(lead.Ability)} inactive at lead level {lead.LeadLevel}");
                    }
                    nature = (byte)(Next16(ref rng, trace, "Nature", "Target nature for PID rejection loop") % 25);
                    break;
                }
            default:
                nature = slot.Species == (ushort)Species.Unown
                    ? byte.MaxValue
                    : (byte)(Next16(ref rng, trace, "Nature", "Target nature for PID rejection loop") % 25);
                break;
        }

        uint pid = GeneratePid(slot, pokemon, ref rng, nature, requiredGender, trace);

        pokemon.MetLevel = pokemon.CurrentLevel = level;
        pokemon.PID = pid;
        var iv1 = (uint)(Next16(ref rng, trace, "IV word 1", "HP Attack and Defense IV bits") & 0x7FFF);
        var iv2 = (uint)(Next16(ref rng, trace, "IV word 2", "Speed Special Attack and Special Defense IV bits") & 0x7FFF);
        pokemon.IV32 = (iv2 << 15) | iv1;
        pokemon.RefreshAbility((int)(pid & 1));
        return true;
    }

    private static bool MatchesRegularSlot(EncounterSlot3 slot, uint slotRoll)
    {
        var (minimum, maximum) = SlotMethodH.GetRange(slot.Type, slot.SlotNumber);
        var value = slotRoll % 100;
        return value >= minimum && value <= maximum;
    }

    private static uint GeneratePid(
        EncounterSlot3 slot,
        PK3 pokemon,
        ref uint rng,
        byte nature,
        byte? requiredGender,
        List<SeedRngTraceEntry>? trace)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            var first = Next16(ref rng, trace, $"PID attempt {attempt} low", "First PID half");
            var second = Next16(ref rng, trace, $"PID attempt {attempt} high", "Second PID half");
            var pid = slot.Species == (ushort)Species.Unown
                ? GenerateMethodH.GetPIDUnown(first, second)
                : GenerateMethodH.GetPIDRegular(first, second);
            if (slot.Species == (ushort)Species.Unown)
            {
                if (EntityPID.GetUnownForm3(pid) == slot.Form)
                {
                    AnnotateLast(trace, $"Accepted PID 0x{pid:X8} with Unown form {slot.Form}");
                    return pid;
                }
                AnnotateLast(trace, $"Rejected PID 0x{pid:X8} because the Unown form differs");
                continue;
            }
            if (pid % 25 != nature)
            {
                AnnotateLast(trace, $"Rejected PID 0x{pid:X8} because nature {(Nature)(pid % 25)} differs");
                continue;
            }
            if (requiredGender is not null && EntityGender.GetFromPIDAndRatio(pid, pokemon.PersonalInfo.Gender) != requiredGender)
            {
                AnnotateLast(trace, $"Rejected PID 0x{pid:X8} because gender differs");
                continue;
            }
            AnnotateLast(trace, $"Accepted PID 0x{pid:X8}");
            return pid;
        }
    }

    private static SeedLeadOutcome CreateOutcome(SeedLeadAbility ability, bool activated) =>
        new(ability, activated, $"{GetLeadName(ability)} {(activated ? "activated" : "failed")}");

    private static string GetLeadName(SeedLeadAbility ability) => ability switch
    {
        SeedLeadAbility.CuteCharmMale => "Cute Charm (male lead)",
        SeedLeadAbility.CuteCharmFemale => "Cute Charm (female lead)",
        SeedLeadAbility.MagnetPull => "Magnet Pull",
        SeedLeadAbility.VitalSpirit => "Vital Spirit",
        SeedLeadAbility.KeenEye => "Keen Eye",
        _ => ability.ToString(),
    };

    private static void ApplyMethod1Frame(PK3 pokemon, uint state, List<SeedRngTraceEntry>? trace)
    {
        var rng = state;
        var low = Next16(ref rng, trace, "PID low", "Method 1 first PID half");
        var high = Next16(ref rng, trace, "PID high", "Method 1 second PID half");
        var pid = ((uint)high << 16) | low;
        AnnotateLast(trace, $"PID 0x{pid:X8}");
        pokemon.PID = pid;
        var iv1 = (uint)(Next16(ref rng, trace, "IV word 1", "HP Attack and Defense IV bits") & 0x7FFF);
        var iv2 = (uint)(Next16(ref rng, trace, "IV word 2", "Speed Special Attack and Special Defense IV bits") & 0x7FFF);
        pokemon.IV32 = (iv2 << 15) | iv1;
        pokemon.RefreshAbility((int)(pid & 1));
    }

    private static ushort Next16(
        ref uint state,
        List<SeedRngTraceEntry>? trace,
        string operation,
        string decision)
    {
        var before = state;
        var output = (ushort)LCRNG.Next16(ref state);
        trace?.Add(new SeedRngTraceEntry(trace.Count + 1, operation, before, state, output, decision));
        return output;
    }

    private static void AnnotateLast(List<SeedRngTraceEntry>? trace, string decision)
    {
        if (trace is { Count: > 0 })
            trace[^1] = trace[^1] with { Decision = decision };
    }

    private readonly record struct ResultKey(int Frame, uint PID, uint IV32, ushort Location);

    private sealed class ReferenceComparer : IEqualityComparer<IEncounterInfo>
    {
        public static readonly ReferenceComparer Instance = new();
        public bool Equals(IEncounterInfo? x, IEncounterInfo? y) => ReferenceEquals(x, y);
        public int GetHashCode(IEncounterInfo obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
