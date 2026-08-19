using FluentAssertions;
using SED.Core;
using Xunit;

namespace SED.Tests;

public sealed class ManipulationPresetStoreTests
{
    [Fact]
    public void PresetsRoundTripOverwriteAndDelete()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sed-presets-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "presets.json");
        try
        {
            var store = new ManipulationPresetStore(path);
            store.Save("Safari Abra", new SeedSearchFilters(Nature: 3, ExactFrame: 1234));
            store.Save("Safari Abra", new SeedSearchFilters(Nature: 7, ExactFrame: 5678));

            ManipulationPreset preset = store.Load().Single();
            preset.Name.Should().Be("Safari Abra");
            preset.Filters.Nature.Should().Be(7);
            preset.Filters.ExactFrame.Should().Be(5678);
            store.Delete("safari abra").Should().BeTrue();
            store.Load().Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
