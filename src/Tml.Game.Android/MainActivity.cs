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
public sealed class MainActivity : Activity
{
    private TextView? status;
    private Button? play, import;
    private bool busy;
    private const string TerrariaHash = "960a03bff6050cf7be16dfc1a7b19e10fc2c4f8f835a6a3b135a50dd9e6ba2f3";

    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(40, 20, 40, 20);
        var title = new TextView(this) { Text = "tModLoader Android · experimental", TextSize = 24 };
        status = new TextView(this) { Text = "Importe o ZIP completo do Terraria PC. O teste gráfico funciona sem importar.", TextSize = 16 };
        var graphics = new Button(this) { Text = "Testar gráficos e controles" };
        import = new Button(this) { Text = "Importar Terraria-PC.zip" };
        play = new Button(this) { Text = "Iniciar tModLoader (experimental)" };
        var logs = new Button(this) { Text = "Ver último relatório" };
        graphics.Click += (_, _) => Launch("probe");
        import.Click += (_, _) =>
        {
            if (busy) return;
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            StartActivityForResult(intent, 10);
        };
        play.Click += async (_, _) => await PlayAsync();
        logs.Click += (_, _) =>
        {
            string path = Path.Combine(FilesDir!.AbsolutePath, "host-session.log");
            string message = File.Exists(path) ? File.ReadAllText(path) : "Nenhuma sessão registrada.";
            string gameLogs = Path.Combine(FilesDir!.AbsolutePath, "game/tModLoader-Logs");
            if (Directory.Exists(gameLogs))
            {
                string? latest = Directory.EnumerateFiles(gameLogs, "client*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (latest != null) message += "\n\n" + File.ReadAllText(latest);
            }
            if (message.Length > 24000) message = message[^24000..];
            var dialog = new AlertDialog.Builder(this);
            dialog.SetTitle("Relatório");
            dialog.SetMessage(message);
            dialog.SetPositiveButton("Fechar", (_, _) => { });
            dialog.SetNeutralButton("Copiar", (_, _) =>
            {
                var clipboard = (ClipboardManager)GetSystemService(ClipboardService)!;
                clipboard.PrimaryClip = ClipData.NewPlainText("tML Android", message);
            });
            dialog.Show();
        };
        foreach (var view in new global::Android.Views.View[] { title, status, graphics, import, play, logs }) panel.AddView(view);
        SetContentView(panel);
        global::Android.Util.Log.Info("TmlHost", "HOST_LAUNCHER_READY");
    }

    private void Launch(string mode)
    {
        if (busy) return;
        var intent = new Intent();
        intent.SetClassName(this, "com.mantovani.tmlmobile.SdlGameActivity");
        intent.PutExtra("mode", mode);
        StartActivity(intent);
    }

    private async Task PlayAsync()
    {
        if (busy) return;
        SetBusy(true, "Preparando os arquivos do jogo…");
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
                if (!File.Exists(marker) || File.ReadAllText(marker) != "0.2.0")
                {
                    string cache = Path.Combine(CacheDir!.AbsolutePath, "game-payload.zip");
                    try
                    {
                        using (var source = Assets!.Open("game-payload.zip"))
                        using (var target = File.Create(cache)) source.CopyTo(target);
                        ExtractPayload(cache, Path.Combine(root, "game"));
                        File.WriteAllText(marker, "0.2.0");
                    }
                    finally { if (File.Exists(cache)) File.Delete(cache); }
                }
            });
            SetBusy(false, "Arquivos preparados. Abrindo o jogo…");
            Launch("game");
        }
        catch (Exception error) { SetBusy(false, "Não foi possível iniciar: " + error.Message); }
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
        if (requestCode != 10 || resultCode != Result.Ok || data?.Data == null) return;
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
        status!.Text = message;
        play!.Enabled = import!.Enabled = !value;
    }
}
