// Shape of curriculum.json (HANDOFF section 12).
using System.Text.Json;

namespace Nordlys.Core;

public sealed record Unit(string Id, string Title, int[] Tiers, string[] Prereqs);
public sealed record TrackInfo(string Code, string Name, double Share, Unit[] Units);

public sealed record Curriculum(int Version, TrackInfo[] Tracks)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static Curriculum Parse(string text) =>
        JsonSerializer.Deserialize<Curriculum>(text, Json) is { Tracks: not null } c ? c : throw new FormatException("curriculum.json has no \"tracks\" list");

    public IEnumerable<Unit> AllUnits => Tracks.SelectMany(t => t.Units);
    public Unit? UnitById(string id) => AllUnits.FirstOrDefault(u => u.Id == id);
    public string TrackName(string code) => Tracks.FirstOrDefault(t => t.Code == code)?.Name ?? code;
}
