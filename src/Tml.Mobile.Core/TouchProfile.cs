using System.Text.Json;
using System.Numerics;

namespace Tml.Mobile;

public sealed record ButtonPlacement(float X, float Y, float Size = 1, bool Visible = true);

public sealed class TouchProfile
{
    public int Version { get; set; } = 1;
    public float Scale { get; set; } = .8f;
    public float Opacity { get; set; } = .55f;
    public bool AttackWithAim { get; set; } = true;
    public Dictionary<string, ButtonPlacement> Buttons { get; set; } = new();

    public void Validate()
    {
        if (Version != 1 || !float.IsFinite(Scale) || !float.IsFinite(Opacity) || Scale < .5f || Scale > 1.5f || Opacity < .15f || Opacity > 1 || Buttons == null || Buttons.Count > 32)
            throw new InvalidDataException("Invalid touch profile");
        foreach (var p in Buttons.Values)
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Size) || p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1 || p.Size < .5f || p.Size > 2)
                throw new InvalidDataException("Invalid touch button position");
    }

    public TouchControl[] Layout(Viewport viewport, bool showHidden = false)
    {
        Validate();
        var defaults = ControlLayout.CreateDefault(viewport, Scale);
        var result = new List<TouchControl>(defaults.Length);
        foreach (var original in defaults)
        {
            var c = original;
            if (Buttons.TryGetValue(c.Id, out var p))
            {
                if (!p.Visible && !showHidden) continue;
                c = c with { Center = new(viewport.X + p.X * viewport.Width, viewport.Y + p.Y * viewport.Height), Radius = c.Radius * p.Size };
            }
            float radius = MathF.Min(c.Radius, MathF.Min(viewport.Width, viewport.Height) / 2);
            c = c with { Radius = radius, Center = Vector2.Clamp(c.Center,
                new(viewport.X + radius, viewport.Y + radius), new(viewport.X + viewport.Width - radius, viewport.Y + viewport.Height - radius)) };
            result.Add(c);
        }
        return result.ToArray();
    }

    public static TouchProfile Load(string path)
    {
        if (!File.Exists(path)) return new();
        var p = JsonSerializer.Deserialize<TouchProfile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty touch profile");
        p.Validate();
        return p;
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this));
        File.Move(temp, path, true);
    }
}
