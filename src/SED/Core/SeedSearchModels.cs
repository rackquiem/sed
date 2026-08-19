using PKHeX.Core;

namespace SED.Core;

public enum ShinySearchFilter
{
    Any,
    ShinyOnly,
    NonShinyOnly,
}

public enum SeedEncounterCategory
{
    All,
    Wild,
    Static,
}

public enum SeedLeadAbility
{
    None,
    Synchronize,
    CuteCharmMale,
    CuteCharmFemale,
    Static,
    MagnetPull,
    Pressure,
    Hustle,
    VitalSpirit,
    Intimidate,
    KeenEye,
}

/// <summary>
/// Encounter-modifying lead behavior implemented by pret/pokeemerald's wild encounter source.
/// The corresponding pret/pokeruby and pret/pokefirered sources do not apply these Generation III branches.
/// </summary>
public sealed record SeedLeadSettings(
    SeedLeadAbility Ability,
    Nature SynchronizeNature = Nature.Hardy,
    byte LeadLevel = 100)
{
    public static SeedLeadSettings None { get; } = new(SeedLeadAbility.None);

    public bool IsSupported(GameVersion version) => Ability == SeedLeadAbility.None || version == GameVersion.E;
}

public sealed record SeedLeadOutcome(
    SeedLeadAbility Ability,
    bool Activated,
    string Description)
{
    public static SeedLeadOutcome None { get; } = new(SeedLeadAbility.None, false, "None");
}

public sealed record SeedSearchRequest(
    ushort Species,
    uint InitialSeed,
    int StartFrame,
    int FrameCount,
    int MaximumResults,
    ShinySearchFilter ShinyFilter,
    SeedEncounterCategory Category,
    bool RequireLegal,
    SeedLeadSettings Lead,
    int WorkerCount = 0,
    SeedSearchFilters? Filters = null);

public sealed record SeedSearchFilters(
    int Nature = -1,
    int Gender = -1,
    int AbilitySlot = -1,
    int MinimumHP = 0,
    int MinimumAttack = 0,
    int MinimumDefense = 0,
    int MinimumSpecialAttack = 0,
    int MinimumSpecialDefense = 0,
    int MinimumSpeed = 0,
    int HiddenPowerType = -1,
    int MinimumHiddenPower = 0,
    int MinimumLevel = 1,
    int MaximumLevel = 100,
    int Location = -1,
    int EncounterSlot = -1,
    uint? ExactPID = null,
    int ExactHP = -1,
    int ExactAttack = -1,
    int ExactDefense = -1,
    int ExactSpecialAttack = -1,
    int ExactSpecialDefense = -1,
    int ExactSpeed = -1)
{
    public static SeedSearchFilters Any { get; } = new();

    public bool Matches(PK3 pokemon, IEncounterInfo encounter)
    {
        if (Nature >= 0 && (int)pokemon.Nature != Nature)
            return false;
        if (Gender >= 0 && pokemon.Gender != Gender)
            return false;
        if (AbilitySlot >= 0 && (pokemon.PID & 1) != AbilitySlot)
            return false;
        if (ExactPID is { } pid && pokemon.PID != pid)
            return false;
        if (pokemon.IV_HP < MinimumHP || pokemon.IV_ATK < MinimumAttack || pokemon.IV_DEF < MinimumDefense ||
            pokemon.IV_SPA < MinimumSpecialAttack || pokemon.IV_SPD < MinimumSpecialDefense || pokemon.IV_SPE < MinimumSpeed)
            return false;
        if (ExactHP >= 0 && pokemon.IV_HP != ExactHP || ExactAttack >= 0 && pokemon.IV_ATK != ExactAttack ||
            ExactDefense >= 0 && pokemon.IV_DEF != ExactDefense || ExactSpecialAttack >= 0 && pokemon.IV_SPA != ExactSpecialAttack ||
            ExactSpecialDefense >= 0 && pokemon.IV_SPD != ExactSpecialDefense || ExactSpeed >= 0 && pokemon.IV_SPE != ExactSpeed)
            return false;
        if (pokemon.CurrentLevel < MinimumLevel || pokemon.CurrentLevel > MaximumLevel)
            return false;
        if (Location >= 0 && pokemon.MetLocation != Location)
            return false;
        if (EncounterSlot >= 0 && (encounter is not EncounterSlot3 slot || slot.SlotNumber != EncounterSlot))
            return false;

        var hpType = GetHiddenPowerType(pokemon);
        if (HiddenPowerType >= 0 && hpType != HiddenPowerType)
            return false;
        return MinimumHiddenPower <= 0 || GetHiddenPowerPower(pokemon) >= MinimumHiddenPower;
    }

    public int ActiveCount => new[]
    {
        Nature >= 0,
        Gender >= 0,
        AbilitySlot >= 0,
        MinimumHP > 0 || MinimumAttack > 0 || MinimumDefense > 0 || MinimumSpecialAttack > 0 || MinimumSpecialDefense > 0 || MinimumSpeed > 0,
        HiddenPowerType >= 0,
        MinimumHiddenPower > 0,
        MinimumLevel > 1 || MaximumLevel < 100,
        Location >= 0,
        EncounterSlot >= 0,
        ExactPID.HasValue,
        HasExactIVs,
    }.Count(z => z);

    public bool HasExactIVs => ExactHP >= 0 && ExactAttack >= 0 && ExactDefense >= 0 &&
                               ExactSpecialAttack >= 0 && ExactSpecialDefense >= 0 && ExactSpeed >= 0;

    public bool CanReverseSolve => ExactPID.HasValue || HasExactIVs;

    private static int GetHiddenPowerType(PK3 pk)
    {
        var bits = (pk.IV_HP & 1) | ((pk.IV_ATK & 1) << 1) | ((pk.IV_DEF & 1) << 2) |
                   ((pk.IV_SPE & 1) << 3) | ((pk.IV_SPA & 1) << 4) | ((pk.IV_SPD & 1) << 5);
        return bits * 15 / 63;
    }

    private static int GetHiddenPowerPower(PK3 pk)
    {
        var bits = ((pk.IV_HP >> 1) & 1) | (((pk.IV_ATK >> 1) & 1) << 1) | (((pk.IV_DEF >> 1) & 1) << 2) |
                   (((pk.IV_SPE >> 1) & 1) << 3) | (((pk.IV_SPA >> 1) & 1) << 4) | (((pk.IV_SPD >> 1) & 1) << 5);
        return bits * 40 / 63 + 30;
    }
}

public sealed record SeedShinyValidation(
    ushort ShinyValue,
    bool IsShiny,
    bool MatchesFilter,
    bool AgreesWithPKHeX)
{
    public bool Valid => MatchesFilter && AgreesWithPKHeX;
}

public sealed record SeedRngTraceEntry(
    int Call,
    string Operation,
    uint StateBefore,
    uint StateAfter,
    ushort Output,
    string Decision);

public sealed record SeedEncounterResult(
    PK3 Pokemon,
    IEncounterInfo Encounter,
    uint InitialSeed,
    uint State,
    int Frame,
    string Method,
    SeedLeadOutcome Lead,
    SeedShinyValidation ShinyValidation,
    bool IsLegal,
    string LegalityReport,
    IReadOnlyList<SeedRngTraceEntry> Trace);
