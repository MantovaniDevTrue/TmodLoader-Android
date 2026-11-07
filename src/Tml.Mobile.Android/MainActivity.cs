using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Tml.Mobile.Android;

[Activity(Label = "tML Android — Controles", MainLauncher = true, Exported = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : Activity
{
    private TouchControlsView? controls;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        controls = new TouchControlsView(this);
        SetContentView(controls);
        global::Android.Util.Log.Info("TmlMobile", "MOBILE_STARTUP_READY");
        _ = ProbeAsync();
    }

    private async Task ProbeAsync()
    {
        string path = Path.Combine(FilesDir!.AbsolutePath, "Tml.RuntimeProbe-" + Guid.NewGuid().ToString("N") + ".dll");
        try
        {
            using (var input = Assets!.Open("Tml.RuntimeProbe.dll"))
            using (var output = File.Create(path)) await input.CopyToAsync(output);
            var report = await Task.Run(() => RuntimeProbe.Run(path));
            if (IsDestroyed) return;
            global::Android.Util.Log.Info("TmlMobile", report.Details);
            if (report.AssemblyLoaded && report.EmitSucceeded)
                global::Android.Util.Log.Info("TmlMobile", "MOBILE_RUNTIME_PROBE_OK");
            controls!.Report = $"{report.Framework} · {report.Architecture}\n" +
                $"Código dinâmico: {report.DynamicCodeSupported} · Compilado: {report.DynamicCodeCompiled}\n{report.Details}";
        }
        catch (Exception error)
        {
            global::Android.Util.Log.Error("TmlMobile", "MOBILE_RUNTIME_PROBE_ERROR " + error);
            if (!IsDestroyed) controls!.Report = $"Falha no teste do runtime: {error.GetType().Name}: {error.Message}";
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    protected override void OnPause()
    {
        controls?.Suspend();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        controls?.Resume();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (!hasFocus) controls?.Suspend();
        else controls?.Resume();
    }
}
