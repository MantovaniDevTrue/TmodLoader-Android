using System.Text;
using System.Text.Json;

namespace Tml.Mobile;

public sealed record LocalModEntry(string Name, string Version, string Target, string FileName, long Bytes, string? Error);

public static class LocalModLibrary
{
    public static LocalModEntry[] Scan(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory).Where(p => Path.GetExtension(p).Equals(".tmod", StringComparison.OrdinalIgnoreCase))
            .Select(Read).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenByDescending(m => System.Version.TryParse(m.Version, out var v) ? v : new Version()).ToArray();
    }

    public static LocalModEntry Read(string path)
    {
        string file = Path.GetFileName(path);
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (!reader.ReadBytes(4).AsSpan().SequenceEqual("TMOD"u8)) throw new InvalidDataException("Invalid TMOD header");
            string target = ReadString(reader, 64);
            if (!Version.TryParse(target, out var version) || version < new Version(0, 11)) throw new InvalidDataException("Unsupported package format");
            stream.Seek(276, SeekOrigin.Current);
            int length = reader.ReadInt32();
            if (length != stream.Length - stream.Position) throw new InvalidDataException("Incomplete package");
            string name = ReadString(reader, 160), modVersion = ReadString(reader, 64);
            if (name.Length == 0 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') || !Version.TryParse(modVersion, out _))
                throw new InvalidDataException("Invalid mod identity");
            return new(name, modVersion, target, file, stream.Length, null);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or ArgumentException or OverflowException)
        { return new(file, "", "", file, 0, e.Message); }
    }

    private static string ReadString(BinaryReader reader, int maximum)
    {
        int length = reader.Read7BitEncodedInt();
        if (length < 0 || length > maximum) throw new InvalidDataException("Package string exceeds limit");
        byte[] data = reader.ReadBytes(length);
        if (data.Length != length) throw new EndOfStreamException();
        return new UTF8Encoding(false, true).GetString(data);
    }

    public static HashSet<string> Enabled(string directory)
    {
        string path = Path.Combine(directory, "enabled.json");
        if (!File.Exists(path)) return new(StringComparer.Ordinal);
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("enabled.json exceeds limit");
        return new(JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? [], StringComparer.Ordinal);
    }

    public static void Save(string directory, IEnumerable<string> names)
    {
        Directory.CreateDirectory(directory);
        Write(Path.Combine(directory, "enabled.json"), JsonSerializer.Serialize(names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    }

    public static void DisableAll(string directory)
    {
        var enabled = Enabled(directory);
        if (enabled.Count > 0) Write(Path.Combine(directory, "launcher-selection.json"), JsonSerializer.Serialize(enabled));
        Save(directory, []);
    }

    public static bool CanRestore(string directory) => File.Exists(Path.Combine(directory, "launcher-selection.json"));

    public static void Restore(string directory)
    {
        string path = Path.Combine(directory, "launcher-selection.json");
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Selection backup exceeds limit");
        var names = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? [];
        Save(directory, names);
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".launcher-tmp";
        try { File.WriteAllText(temporary, text); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
