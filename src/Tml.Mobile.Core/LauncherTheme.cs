using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tml.Mobile;

public sealed record LauncherTheme(string Name, string Background, string Surface, string Accent, string Text, string Muted)
{
    public static readonly LauncherTheme[] Presets = [
        new("tModLoader", "#111725", "#202C43", "#9BC55D", "#F1F4FA", "#B1BDD2"),
        new("Calamity", "#1C1018", "#331E29", "#EF7265", "#FFF1EB", "#D8B9C0"),
        new("Thorium", "#0C1D2A", "#163446", "#59D4CB", "#EAFBFF", "#ABD0D9")
    ];
    public static LauncherTheme Parse(string json)
    {
        if (json.Length > 65536) throw new InvalidDataException("Tema maior que 64 KiB.");
        var theme = JsonSerializer.Deserialize<LauncherTheme>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Tema vazio.");
        if (string.IsNullOrWhiteSpace(theme.Name) || theme.Name.Length > 40 || theme.Name.Any(char.IsControl))
            throw new InvalidDataException("Nome de tema inválido.");
        foreach (string color in new[] { theme.Background, theme.Surface, theme.Accent, theme.Text, theme.Muted })
            if (color == null || !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$"))
                throw new InvalidDataException("As cores do tema precisam usar #RRGGBB.");
        return theme;
    }
}
