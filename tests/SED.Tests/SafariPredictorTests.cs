using FluentAssertions;
using PKHeX.Core;
using SED.Core;
using Xunit;

namespace SED.Tests;

public sealed class SafariPredictorTests
{
    [Fact]
    public void PretGameProfilesUseTheirSourceSpecificInitialEscapeFactors()
    {
        SafariPredictor.GetInitialEscapeFactor(GameVersion.E, Species.Chansey).Should().Be(3);
        SafariPredictor.GetInitialEscapeFactor(GameVersion.R, Species.Chansey).Should().Be(3);
        SafariPredictor.GetInitialEscapeFactor(GameVersion.FR, Species.Chansey).Should().Be(9);
        SafariPredictor.GetInitialEscapeFactor(GameVersion.LG, Species.Magikarp).Should().Be(2);
    }

    [Fact]
    public void BallPredictionConsumesShakeCallsThenTheFleeCall()
    {
        var pokemon = new PK3 { Species = (ushort)Species.Chansey, Version = GameVersion.FR };
        var prediction = SafariPredictor.PredictBall(pokemon, 123, 7, 0);
        var expectedCalls = prediction.Shakes + 1 + (prediction.Captured ? 0 : 1);

        prediction.EncounterFrame.Should().Be(123);
        prediction.BattleOffset.Should().Be(7);
        prediction.EndingState.Should().Be(LCRNG.Advance(prediction.StartingState, expectedCalls));
        prediction.FleeRoll.HasValue.Should().Be(!prediction.Captured);
        prediction.Flees.Should().Be(prediction.FleeRoll < prediction.EscapeFactor * 5);
    }
}
