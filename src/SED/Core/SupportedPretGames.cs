using PKHeX.Core;

namespace SED.Core;

public static class SupportedPretGames
{
    public static bool IsSupported(GameVersion version) => version is
        GameVersion.R or GameVersion.S or GameVersion.E or GameVersion.FR or GameVersion.LG;

    public static string GetSourceRepository(GameVersion version) => version switch
    {
        GameVersion.R or GameVersion.S => "https://github.com/pret/pokeruby",
        GameVersion.E => "https://github.com/pret/pokeemerald",
        GameVersion.FR or GameVersion.LG => "https://github.com/pret/pokefirered",
        _ => string.Empty,
    };
}
