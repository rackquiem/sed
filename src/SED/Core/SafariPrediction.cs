using PKHeX.Core;

namespace SED.Core;

public sealed record SafariTurnPrediction(
    int EncounterFrame,
    int BattleOffset,
    uint StartingState,
    uint EndingState,
    int CatchFactor,
    uint ShakeThreshold,
    int Shakes,
    bool Captured,
    int EscapeFactor,
    ushort? FleeRoll,
    bool Flees);

public static class SafariPredictor
{
    private static readonly Dictionary<Species, byte> FireRedLeafGreenFleeRates = new()
    {
        [Species.NidoranF] = 50,
        [Species.Nidorina] = 75,
        [Species.NidoranM] = 50,
        [Species.Nidorino] = 75,
        [Species.Paras] = 50,
        [Species.Parasect] = 75,
        [Species.Venonat] = 50,
        [Species.Venomoth] = 75,
        [Species.Psyduck] = 50,
        [Species.Poliwag] = 50,
        [Species.Slowpoke] = 50,
        [Species.Doduo] = 50,
        [Species.Exeggcute] = 75,
        [Species.Rhyhorn] = 75,
        [Species.Chansey] = 125,
        [Species.Kangaskhan] = 125,
        [Species.Goldeen] = 50,
        [Species.Seaking] = 75,
        [Species.Scyther] = 125,
        [Species.Pinsir] = 125,
        [Species.Tauros] = 125,
        [Species.Magikarp] = 25,
        [Species.Dratini] = 100,
        [Species.Dragonair] = 125,
    };

    public static IReadOnlyList<SafariTurnPrediction> Predict(SeedEncounterResult result, int offsetCount)
    {
        if (result.Encounter is not EncounterSlot3 { IsSafari: true })
            throw new ArgumentException("Safari prediction requires a Safari Zone encounter.", nameof(result));
        if (offsetCount is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(offsetCount));

        var postEncounterState = result.Trace.Count == 0 ? result.State : result.Trace[^1].StateAfter;
        var predictions = new SafariTurnPrediction[offsetCount];
        var state = postEncounterState;
        for (var offset = 0; offset < offsetCount; offset++)
        {
            predictions[offset] = PredictBall(result.Pokemon, result.Frame, offset, state);
            state = LCRNG.Next(state);
        }
        return predictions;
    }

    public static SafariTurnPrediction PredictBall(PK3 pokemon, int encounterFrame, int battleOffset, uint startingState)
    {
        var catchFactor = GetInitialCatchFactor(pokemon.PersonalInfo.CatchRate);
        var effectiveCatchRate = catchFactor * 1275 / 100;
        var catchOdds = effectiveCatchRate * 15 / 10 / 3;
        var shakeThreshold = GetShakeThreshold(catchOdds);
        var state = startingState;
        var shakes = 0;
        while (shakes < 4)
        {
            state = LCRNG.Next(state);
            if ((state >> 16) >= shakeThreshold)
                break;
            shakes++;
        }

        var captured = shakes == 4;
        var escapeFactor = GetInitialEscapeFactor(pokemon.Version, (Species)pokemon.Species);
        ushort? fleeRoll = null;
        var flees = false;
        if (!captured)
        {
            state = LCRNG.Next(state);
            fleeRoll = (ushort)((state >> 16) % 100);
            flees = fleeRoll < escapeFactor * 5;
        }

        return new SafariTurnPrediction(
            encounterFrame,
            battleOffset,
            startingState,
            state,
            catchFactor,
            shakeThreshold,
            shakes,
            captured,
            escapeFactor,
            fleeRoll,
            flees);
    }

    public static int GetInitialCatchFactor(byte catchRate) => catchRate * 100 / 1275;

    public static int GetInitialEscapeFactor(GameVersion version, Species species)
    {
        if (version is GameVersion.R or GameVersion.S or GameVersion.E)
            return 3;
        if (version is not (GameVersion.FR or GameVersion.LG))
            throw new NotSupportedException($"Safari prediction is unavailable for {version}.");

        FireRedLeafGreenFleeRates.TryGetValue(species, out var sourceRate);
        return Math.Max(2, sourceRate * 100 / 1275);
    }

    private static uint GetShakeThreshold(int catchOdds)
    {
        if (catchOdds > 254)
            return ushort.MaxValue + 1u;
        if (catchOdds <= 0)
            return 0;
        var inner = IntegerSquareRoot((uint)(16_711_680 / catchOdds));
        var outer = IntegerSquareRoot(inner);
        return outer == 0 ? ushort.MaxValue + 1u : 1_048_560u / outer;
    }

    private static uint IntegerSquareRoot(uint value) => (uint)Math.Sqrt(value);
}
