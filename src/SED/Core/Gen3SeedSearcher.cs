using System.Runtime.CompilerServices;
using PKHeX.Core;

namespace SED.Core;

/// <summary>
/// Generates exact no-lead Generation III Method H wild encounters and Method 1 static encounters.
/// </summary>
public static class Gen3SeedSearcher
{
    public static IReadOnlyList<SeedEncounterResult> Search(
        SaveFile save,
        SeedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedPretGames.IsSupported(save.Version))
            throw new NotSupportedException($"{save.Version} is not backed by a supported pret source repository.");
        if (request.FrameCount <= 0 || request.MaximumResults <= 0)
            return [];

        var encounters = GetEncounters(save, request.Species, request.Category);
        var results = new List<SeedEncounterResult>(Math.Min(request.MaximumResults, 256));
        var seen = new HashSet<ResultKey>();

        foreach (var encounter in encounters)
        {
            var state = LCRNG.Advance(request.InitialSeed, request.StartFrame);
            for (int offset = 0; offset < request.FrameCount; offset++, state = LCRNG.Next(state))
            {
                if ((offset & 0x3FFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                var frame = request.StartFrame + offset;
                var generated = GenerateExact(save, encounter, request.InitialSeed, state, frame, request.ShinyFilter);
                if (generated is null)
                    continue;
                if (request.RequireLegal && !generated.IsLegal)
                    continue;

                var key = new ResultKey(frame, generated.Pokemon.PID, generated.Pokemon.IV32, generated.Pokemon.MetLocation);
                if (!seen.Add(key))
                    continue;
                results.Add(generated);
                if (results.Count >= request.MaximumResults)
                    return Sort(results);
            }
        }
        return Sort(results);
    }

    private static IReadOnlyList<SeedEncounterResult> Sort(List<SeedEncounterResult> results) =>
        results.OrderBy(z => z.Frame).ThenBy(z => z.Pokemon.MetLocation).ThenBy(z => z.Pokemon.PID).ToArray();

    private static IEncounterInfo[] GetEncounters(SaveFile save, ushort species, SeedEncounterCategory category)
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
        ShinySearchFilter shinyFilter)
    {
        var raw = encounter.ConvertToPKM(save, EncounterCriteria.Unrestricted);
        if (raw is not PK3 pokemon)
            return null;

        string method;
        switch (encounter)
        {
            case EncounterSlot3 slot:
                if (!ApplyMethodHFrame(slot, pokemon, state))
                    return null;
                method = "Method H";
                break;
            case EncounterStatic3 { IsRoaming: false, IsEgg: false }:
                ApplyMethod1Frame(pokemon, state);
                method = "Method 1";
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
        return new SeedEncounterResult(
            pokemon,
            encounter,
            initialSeed,
            state,
            frame,
            method,
            shiny,
            legality.Valid,
            legality.Report(true));
    }

    private static bool ApplyMethodHFrame(EncounterSlot3 slot, PK3 pokemon, uint state)
    {
        if (MethodH.IsEncounterCheckApplicable(slot.Type) &&
            !MethodH.CheckEncounterActivation(slot, state, LeadRequired.None, out _))
            return false;

        var rng = state;
        var slotRoll = LCRNG.Next16(ref rng) % 100;
        var (minimum, maximum) = SlotMethodH.GetRange(slot.Type, slot.SlotNumber);
        if (slotRoll < minimum || slotRoll > maximum)
            return false;

        var levelRoll = LCRNG.Next16(ref rng);
        var nature = LCRNG.Next16(ref rng) % 25;
        uint pid;
        do
        {
            var first = LCRNG.Next16(ref rng);
            var second = LCRNG.Next16(ref rng);
            pid = slot.Species == (ushort)Species.Unown
                ? GenerateMethodH.GetPIDUnown(first, second)
                : GenerateMethodH.GetPIDRegular(first, second);
        } while (pid % 25 != nature);

        pokemon.MetLevel = pokemon.CurrentLevel = (byte)MethodH.GetRandomLevel(slot, levelRoll, LeadRequired.None);
        pokemon.PID = pid;
        pokemon.IV32 = ClassicEraRNG.GetSequentialIVs(ref rng);
        pokemon.RefreshAbility((int)(pid & 1));
        return true;
    }

    private static void ApplyMethod1Frame(PK3 pokemon, uint state)
    {
        var rng = state;
        var pid = ClassicEraRNG.GetSequentialPID(ref rng);
        pokemon.PID = pid;
        pokemon.IV32 = ClassicEraRNG.GetSequentialIVs(ref rng);
        pokemon.RefreshAbility((int)(pid & 1));
    }

    private readonly record struct ResultKey(int Frame, uint PID, uint IV32, ushort Location);

    private sealed class ReferenceComparer : IEqualityComparer<IEncounterInfo>
    {
        public static readonly ReferenceComparer Instance = new();
        public bool Equals(IEncounterInfo? x, IEncounterInfo? y) => ReferenceEquals(x, y);
        public int GetHashCode(IEncounterInfo obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
