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

public sealed record SeedSearchRequest(
    ushort Species,
    uint InitialSeed,
    int StartFrame,
    int FrameCount,
    int MaximumResults,
    ShinySearchFilter ShinyFilter,
    SeedEncounterCategory Category,
    bool RequireLegal);

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
    SeedShinyValidation ShinyValidation,
    bool IsLegal,
    string LegalityReport);
