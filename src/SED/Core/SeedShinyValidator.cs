using PKHeX.Core;

namespace SED.Core;

/// <summary>
/// Implements the Generation III trainer shiny value independently from PKHeX.
/// </summary>
public static class SeedShinyValidator
{
    public const ushort Generation3ShinyThreshold = 8;

    public static ushort GetTrainerShinyValue(uint pid, ushort tid, ushort sid)
    {
        var low = (ushort)pid;
        var high = (ushort)(pid >> 16);
        return (ushort)(tid ^ sid ^ low ^ high);
    }

    public static bool IsGeneration3Shiny(uint pid, ushort tid, ushort sid) =>
        GetTrainerShinyValue(pid, tid, sid) < Generation3ShinyThreshold;

    public static SeedShinyValidation Validate(PK3 pokemon, ShinySearchFilter filter)
    {
        var value = GetTrainerShinyValue(pokemon.PID, pokemon.TID16, pokemon.SID16);
        var shiny = value < Generation3ShinyThreshold;
        var matches = filter switch
        {
            ShinySearchFilter.Any => true,
            ShinySearchFilter.ShinyOnly => shiny,
            ShinySearchFilter.NonShinyOnly => !shiny,
            _ => false,
        };
        return new SeedShinyValidation(value, shiny, matches, shiny == pokemon.IsShiny);
    }
}
