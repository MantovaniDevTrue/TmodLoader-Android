namespace Tml.Mobile;

public sealed record DiagnosticIssue(string Code, string Detail, int Count);
public sealed record DiagnosticReport(bool HasSession, bool NormalExit, DiagnosticIssue[] Errors, DiagnosticIssue[] Warnings, string[] Environment, string History)
{
    public static DiagnosticReport Parse(string report)
    {
        int historyAt = report.IndexOf("\nAndroid:", StringComparison.Ordinal);
        string current = historyAt < 0 ? report : report[..historyAt];
        var lines = current.Split('\n').Select(s => s.Trim()).ToArray();
        var errors = new List<(string Code, string Detail)>();
        var warnings = new List<(string Code, string Detail)>();
        foreach (string line in lines)
        {
            if (line.Contains("MANAGED_UNHANDLED", StringComparison.Ordinal) || line.StartsWith("HOST_ERROR ", StringComparison.Ordinal)) errors.Add(("managed", line));
            else if (line.Contains("Got a SIGSEGV", StringComparison.Ordinal) || line.Contains("Got a SIGABRT", StringComparison.Ordinal)) errors.Add(("native", line));
            else if (line.Contains("/ERROR]", StringComparison.Ordinal)) errors.Add(("game", line));
            else if (line.Contains("STARTUP_EXCEPTION (may be handled)", StringComparison.Ordinal)) warnings.Add(("handled", line));
            else if (line.Contains("/WARN]", StringComparison.Ordinal) || line.StartsWith("WARNING:", StringComparison.Ordinal)) warnings.Add(("warning", line));
        }
        DiagnosticIssue[] Group(List<(string Code, string Detail)> values) => values.GroupBy(v => v)
            .Select(g => new DiagnosticIssue(g.Key.Code, g.Key.Detail, g.Count())).Take(30).ToArray();
        string[] environment = lines.Where(l => l.StartsWith("ANDROID_CPU_EXECUTION ", StringComparison.Ordinal) ||
            l.StartsWith("ANDROID_SYSTEM_MEMORY ", StringComparison.Ordinal) || l.StartsWith("ANDROID_RENDER_POLICY ", StringComparison.Ordinal) ||
            l.StartsWith("ANDROID_RENDERER_REQUEST ", StringComparison.Ordinal) || l.Contains("OpenGL Renderer:", StringComparison.Ordinal) ||
            l.StartsWith("Vulkan Device:", StringComparison.Ordinal)).Distinct().ToArray();
        return new(lines.Any(l => l.StartsWith("Session ", StringComparison.Ordinal)), current.Contains("MANAGED_SDL_EXIT", StringComparison.Ordinal),
            Group(errors), Group(warnings), environment, historyAt < 0 ? "" : report[historyAt..].Trim());
    }
}
