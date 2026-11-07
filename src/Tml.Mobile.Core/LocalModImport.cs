namespace Tml.Mobile;

public static class LocalModImport
{
    public const long MaximumBytes = 1024L * 1024 * 1024;
    public static string DirectoryFor(string files) => Path.Combine(files, "Saves", "tModLoader", "Mods");

    public static async Task<string> ImportAsync(Stream input, string displayName, string directory)
    {
        string name = displayName.Replace('\\', '/').Split('/')[^1];
        if (!name.EndsWith(".tmod", StringComparison.OrdinalIgnoreCase) || name.Length > 160 ||
            name.Length <= 5 || name.Any(c => char.IsControl(c) || ":*?\"<>|".Contains(c)))
            throw new InvalidDataException("Selecione um arquivo .tmod válido.");
        name = name[..^5] + ".tmod";
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, name);
        string staging = Path.Combine(directory, ".import-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] header = new byte[4];
            await input.ReadExactlyAsync(header);
            if (!header.AsSpan().SequenceEqual("TMOD"u8))
                throw new InvalidDataException("O arquivo não é um pacote tModLoader (.tmod).");
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                await output.WriteAsync(header);
                byte[] buffer = new byte[65536];
                long total = 4;
                int count;
                while ((count = await input.ReadAsync(buffer)) != 0)
                {
                    total += count;
                    if (total > MaximumBytes) throw new InvalidDataException("Mod maior que o limite de 1 GiB.");
                    await output.WriteAsync(buffer.AsMemory(0, count));
                }
                if (total < 32) throw new InvalidDataException("Pacote .tmod incompleto.");
                await output.FlushAsync();
            }
            if (File.Exists(destination)) File.Copy(destination, destination + ".previous", true);
            File.Move(staging, destination, true);
            return name;
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }
}
