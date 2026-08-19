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
    int WorkerCount = 0);

public sealed record SeedShinyValidation(
    ushort ShinyValue,
    bool IsShiny,
    bool MatchesFilter,
    bool AgreesWithPKHeX)
{
    public bool Valid => MatchesFilter && AgreesWithPKHeX;
}

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
    string LegalityReport);
