using System.Text.Json;

namespace SED.Core;

public sealed record ManipulationPreset(string Name, SeedSearchFilters Filters);

public sealed class ManipulationPresetStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string FilePath = filePath;

    public IReadOnlyList<ManipulationPreset> Load()
    {
        if (!File.Exists(FilePath))
            return [];
        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<ManipulationPreset[]>(json, Options) ?? [];
    }

    public void Save(string name, SeedSearchFilters filters)
    {
        name = name.Trim();
        if (name.Length == 0)
            throw new ArgumentException("Preset name cannot be empty.", nameof(name));
        var presets = Load().Where(z => !z.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        presets.Add(new ManipulationPreset(name, filters));
        Write(presets.OrderBy(z => z.Name).ToArray());
    }

    public bool Delete(string name)
    {
        var presets = Load().Where(z => !z.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (presets.Length == Load().Count)
            return false;
        Write(presets);
        return true;
    }

    private void Write(IReadOnlyList<ManipulationPreset> presets)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(presets, Options));
        File.Move(temporary, FilePath, true);
    }
}
