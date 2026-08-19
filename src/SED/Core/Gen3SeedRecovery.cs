using PKHeX.Core;

namespace SED.Core;

public sealed record SeedRecoveryResult(
    IEncounterInfo? Encounter,
    PIDType Method,
    uint OriginSeed,
    uint EncounterSeed,
    uint Frame,
    LeadRequired Lead,
    bool LocationMatches,
    bool LevelMatches)
{
    public string EncounterName => Encounter is IEncounterable named ? named.LongName : "PID and IV correlation only";
}

/// <summary>
/// Reconstructs Generation III LCRNG origins without requiring legality or an unmodified capture.
/// </summary>
public static class Gen3SeedRecovery
{
    public static IReadOnlyList<SeedRecoveryResult> Recover(SaveFile save, PKM pokemon, uint initialSeed)
    {
        if (pokemon.Format != 3)
            throw new NotSupportedException("Seed recovery requires a Generation III Pokémon in the PKHeX editor.");
        if (!SupportedPretGames.IsSupported(save.Version))
            throw new NotSupportedException($"{save.Version} is not backed by a supported pret source repository.");

        var correlations = GetCorrelations(pokemon);
        if (correlations.Length == 0)
            return [new SeedRecoveryResult(null, PIDType.None, 0, 0, 0, LeadRequired.Invalid, false, false)];

        var candidates = Gen3SeedSearcher.GetEncounters(save, pokemon.Species, SeedEncounterCategory.All);
        var results = new List<SeedRecoveryResult>();
        foreach (var correlation in correlations)
        {
            foreach (var encounter in candidates)
            {
                switch (encounter)
                {
                    case EncounterSlot3 slot:
                        AddWildCandidate(results, pokemon, slot, correlation, initialSeed, save.Version == GameVersion.E);
                        break;
                    case EncounterStatic3 { IsRoaming: false, IsEgg: false } stat:
                        AddStaticCandidate(results, pokemon, stat, correlation, initialSeed);
                        break;
                }
            }
        }

        if (results.Count == 0)
        {
            foreach (var correlation in correlations)
            {
                results.Add(new SeedRecoveryResult(
                    null,
                    correlation.Type,
                    correlation.OriginSeed,
                    correlation.OriginSeed,
                    LCRNG.GetDistance(initialSeed, correlation.OriginSeed),
                    LeadRequired.Invalid,
                    false,
                    false));
            }
        }

        return results
            .DistinctBy(z => (z.EncounterSeed, z.Lead, z.EncounterName))
            .OrderByDescending(z => z.LocationMatches)
            .ThenByDescending(z => z.LevelMatches)
            .ThenBy(z => z.Frame)
            .ToArray();
    }

    private static PIDIV[] GetCorrelations(PKM pokemon)
    {
        var results = new List<PIDIV>();
        var pid = pokemon.EncryptionConstant;
        var first = pid << 16;
        var second = pid & 0xFFFF0000;
        Span<uint> seeds = stackalloc uint[LCRNG.MaxCountSeedsIV];
        var count = pokemon.Species == (ushort)Species.Unown
            ? LCRNGReversal.GetSeeds(seeds, second, first)
            : LCRNGReversal.GetSeeds(seeds, first, second);
        var iv32 = pokemon.GetIVs();
        var iv1 = iv32 & 0x7FFF;
        var iv2 = iv32 >> 15;
        foreach (var seed in seeds[..count])
        {
            var state = LCRNG.Next2(seed);
            if (iv1 != LCRNG.Next15(ref state) || iv2 != LCRNG.Next15(ref state))
                continue;
            results.Add(new PIDIV(PIDType.Method_1, seed));
        }

        var analyzed = MethodFinder.Analyze(pokemon);
        if (!analyzed.NoSeed && results.All(z => z.Type != analyzed.Type || z.OriginSeed != analyzed.OriginSeed))
            results.Add(analyzed);
        return results.ToArray();
    }

    private static void AddWildCandidate(
        ICollection<SeedRecoveryResult> results,
        PKM pokemon,
        EncounterSlot3 slot,
        PIDIV correlation,
        uint initialSeed,
        bool emerald)
    {
        var levelMatches = slot.LevelMin <= pokemon.MetLevel && pokemon.MetLevel <= slot.LevelMax;
        var evo = new EvoCriteria
        {
            Species = slot.Species,
            Form = slot.Form,
            LevelMin = pokemon.MetLevel,
            LevelMax = pokemon.MetLevel,
        };
        var lead = LeadFinder.GetLeadInfo3(slot, correlation, evo, emerald, pokemon.Gender, pokemon.Format);
        if (!lead.IsValid)
            return;

        results.Add(new SeedRecoveryResult(
            slot,
            correlation.Type,
            correlation.OriginSeed,
            lead.Seed,
            LCRNG.GetDistance(initialSeed, lead.Seed),
            lead.Lead,
            pokemon.MetLocation == slot.Location,
            levelMatches));
    }

    private static void AddStaticCandidate(
        ICollection<SeedRecoveryResult> results,
        PKM pokemon,
        EncounterStatic3 encounter,
        PIDIV correlation,
        uint initialSeed)
    {
        if (correlation.Type != PIDType.Method_1)
            return;
        results.Add(new SeedRecoveryResult(
            encounter,
            correlation.Type,
            correlation.OriginSeed,
            correlation.OriginSeed,
            LCRNG.GetDistance(initialSeed, correlation.OriginSeed),
            LeadRequired.None,
            pokemon.MetLocation == encounter.Location,
            pokemon.MetLevel == encounter.Level));
    }
}
