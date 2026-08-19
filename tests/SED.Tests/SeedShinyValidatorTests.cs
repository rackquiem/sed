using FluentAssertions;
using PKHeX.Core;
using SED.Core;
using Xunit;

namespace SED.Tests;

public sealed class SeedShinyValidatorTests
{
    [Fact]
    public void Gen3CalculationRecognizesKnownShinyPid()
    {
        const ushort tid = 12345;
        const ushort sid = 54321;
        const ushort low = 0xABCD;
        ushort high = (ushort)(tid ^ sid ^ low);
        uint pid = (uint)(high << 16) | low;

        SeedShinyValidator.GetTrainerShinyValue(pid, tid, sid).Should().Be(0);
        SeedShinyValidator.IsGeneration3Shiny(pid, tid, sid).Should().BeTrue();
    }

    [Fact]
    public void Gen3CalculationRejectsValueAtThreshold()
    {
        const ushort tid = 12345;
        const ushort sid = 54321;
        const ushort low = 0xABCD;
        ushort high = (ushort)(tid ^ sid ^ low ^ SeedShinyValidator.Generation3ShinyThreshold);
        uint pid = (uint)(high << 16) | low;

        SeedShinyValidator.GetTrainerShinyValue(pid, tid, sid)
            .Should().Be(SeedShinyValidator.Generation3ShinyThreshold);
        SeedShinyValidator.IsGeneration3Shiny(pid, tid, sid).Should().BeFalse();
    }

    [Fact]
    public void ValidatorAgreesWithPkhexForGen3Pokemon()
    {
        var pokemon = new PK3
        {
            TID16 = 12345,
            SID16 = 54321,
        };

        const ushort low = 0x2468;
        ushort high = (ushort)(pokemon.TID16 ^ pokemon.SID16 ^ low ^ 3);
        pokemon.PID = (uint)(high << 16) | low;

        SeedShinyValidation validation = SeedShinyValidator.Validate(pokemon, ShinySearchFilter.ShinyOnly);

        validation.IsShiny.Should().BeTrue();
        validation.MatchesFilter.Should().BeTrue();
        validation.AgreesWithPKHeX.Should().BeTrue();
        validation.ShinyValue.Should().Be(3);
    }
}
