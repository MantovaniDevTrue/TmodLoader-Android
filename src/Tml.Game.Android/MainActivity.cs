using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Tml.Mobile.Android;

[Activity(Label = "tML Android", MainLauncher = true, Exported = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed partial class MainActivity : Activity
{
    private TextView? status;
    private Button? play, import;
    private bool busy;
    private const string TerrariaHash = "960a03bff6050cf7be16dfc1a7b19e10fc2c4f8f835a6a3b135a50dd9e6ba2f3";

    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        BuildLauncher();
        global::Android.Util.Log.Info("TmlHost", "HOST_LAUNCHER_READY");
    }

    private void Launch(string mode)
    {
        if (busy) return;
        var intent = new Intent();
        intent.SetClassName(this, "com.mantovani.tmlmobile.SdlGameActivity");
        intent.PutExtra("mode", mode);
        intent.PutExtra("renderer", Preferences.GetString("renderer", "OpenGL"));
        intent.PutExtra("keep-screen-on", Preferences.GetBoolean("keep-screen-on", true));
        StartActivity(intent);
    }

    private async Task PlayAsync()
    {
        if (busy) return;
        SetBusy(true, L("Preparando os arquivos do jogo…", "Preparing game files…", "Preparando archivos del juego…"));
        try
        {
            string root = FilesDir!.AbsolutePath;
            await Task.Run(() =>
            {
                string executable = Path.Combine(root, "Terraria/Terraria.exe");
                using var owned = File.OpenRead(executable);
                if (!Convert.ToHexString(SHA256.HashData(owned)).Equals(TerrariaHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O executável precisa ser o Terraria PC 1.4.5.8 importado.");
                string marker = Path.Combine(root, "game/.payload-version");
                if (!File.Exists(marker) || File.ReadAllText(marker) != "0.2.16")
                {
                    string cache = Path.Combine(CacheDir!.AbsolutePath, "game-payload.zip");
                    try
                    {
                        using (var source = Assets!.Open("game-payload.zip"))
                        using (var target = File.Create(cache)) source.CopyTo(target);
                        ExtractPayload(cache, Path.Combine(root, "game"));
                        File.WriteAllText(marker, "0.2.16");
                    }
                    finally { if (File.Exists(cache)) File.Delete(cache); }
                }
            });
            SetBusy(false, L("Abrindo o jogo…", "Opening game…", "Abriendo el juego…"));
            Launch("game");
        }
        catch (Exception error) { SetBusy(false, L("Não foi possível iniciar: ", "Could not start: ", "No se pudo iniciar: ") + error.Message); }
    }

    private static void ExtractPayload(string archive, string target)
    {
        Directory.CreateDirectory(target);
        string prefix = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(archive);
        long bytes = 0;
        foreach (var entry in zip.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith("game/") || name.EndsWith('/')) continue;
            bytes += entry.Length;
            if (bytes > 512L * 1024 * 1024) throw new InvalidDataException("Pacote do jogo excede o tamanho esperado.");
            string path = Path.GetFullPath(Path.Combine(target, name[5..]));
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Caminho inválido no pacote.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, overwrite: true);
        }
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok || data == null) return;
        if (requestCode == 13 && data.Data != null)
        {
            SetBusy(true, L("Salvando diagnóstico…", "Saving diagnostics…", "Guardando diagnóstico…"));
            try
            {
                using var output = ContentResolver!.OpenOutputStream(data.Data, "wt") ?? throw new IOException("Arquivo indisponível.");
                await using (var writer = new StreamWriter(output, new System.Text.UTF8Encoding(false), leaveOpen: true))
                    await writer.WriteAsync(exportReport);
                SetBusy(false, L("Diagnóstico salvo em .txt.", "Diagnostics saved as .txt.", "Diagnóstico guardado en .txt."));
            }
            catch (Exception e) { SetBusy(false, L("Não foi possível salvar: ", "Could not save: ", "No se pudo guardar: ") + e.Message); }
            return;
        }
        if (requestCode == 11) { await ImportModsAsync(data); return; }
        if (requestCode == 12 && data.Data != null) { await ImportThemeAsync(data.Data); return; }
        if (requestCode != 10 || data.Data == null) return;
        SetBusy(true, "Importando Terraria. Mantenha o aplicativo aberto…");
        string cached = Path.Combine(CacheDir!.AbsolutePath, "terraria-import.zip");
        string root = FilesDir!.AbsolutePath;
        string staging = Path.Combine(root, "Terraria.import-" + Guid.NewGuid());
        try
        {
            using (var source = ContentResolver!.OpenInputStream(data.Data)!)
            using (var target = File.Create(cached)) await source.CopyToAsync(target);
            await Task.Run(() => ImportTerraria(cached, staging));
            string destination = Path.Combine(root, "Terraria"), backup = destination + ".previous";
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
            if (Directory.Exists(destination)) Directory.Move(destination, backup);
            try { Directory.Move(staging, destination); }
            catch { if (Directory.Exists(backup)) Directory.Move(backup, destination); throw; }
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
            SetBusy(false, "Terraria 1.4.5.8 importado. Você já pode testar a inicialização.");
        }
        catch (Exception error) { SetBusy(false, "Falha ao importar: " + error.Message); }
        finally
        {
            if (File.Exists(cached)) File.Delete(cached);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static void ImportTerraria(string archive, string target)
    {
        Directory.CreateDirectory(target);
        string prefix = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(archive);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            int content = name.IndexOf("Content/", StringComparison.Ordinal);
            string? relative = content >= 0 ? name[content..] : name.EndsWith("Terraria.exe", StringComparison.Ordinal) ? "Terraria.exe" : null;
            if (relative == null || relative.EndsWith('/')) continue;
            total += entry.Length;
            if (total > 1200L * 1024 * 1024) throw new InvalidDataException("Conteúdo maior que o esperado.");
            string path = Path.GetFullPath(Path.Combine(target, relative));
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Caminho inválido no ZIP.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, overwrite: true);
        }
        using var executable = File.OpenRead(Path.Combine(target, "Terraria.exe"));
        if (!Convert.ToHexString(SHA256.HashData(executable)).Equals(TerrariaHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O ZIP precisa conter o Terraria PC 1.4.5.8 que validamos.");
        var issues = ContentPreflight.Validate(Path.Combine(target, "Content"), Path.Combine(target, "Overrides"));
        if (issues.Count > 0) throw new InvalidDataException(issues[0].Asset + ": " + issues[0].Reason);
    }

    private void SetBusy(bool value, string message)
    {
        busy = value;
        notice = message;
        status!.Text = message;
        foreach (var action in actions) action.Enabled = !value;
    }
}
