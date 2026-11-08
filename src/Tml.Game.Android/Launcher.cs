using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Tml.Mobile;
using Path = System.IO.Path;
using AColor = Android.Graphics.Color;

namespace Tml.Mobile.Android;

public sealed partial class MainActivity
{
    private ISharedPreferences Preferences => GetSharedPreferences("launcher", FileCreationMode.Private)!;
    private LauncherTheme theme = LauncherTheme.Presets[0];
    private readonly List<View> actions = [];
    private int selectedPage;
    private string language = "pt", pendingReport = "", exportReport = "", notice = "";
    private bool launcherReady;
    private bool compactLayout;
    private string ModDirectory => LocalModImport.DirectoryFor(FilesDir!.AbsolutePath);
    private sealed class PaletteAdapter(Context context, string[] values, AColor foreground, AColor background)
        : ArrayAdapter<string>(context, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, values)
    {
        private View Style(View view)
        {
            view.SetBackgroundColor(background);
            if (view is TextView text) { text.SetTextColor(foreground); text.TextSize = 14; }
            return view;
        }
        public override View GetView(int position, View? convertView, ViewGroup parent) => Style(base.GetView(position, convertView, parent));
        public override View GetDropDownView(int position, View? convertView, ViewGroup? parent) => Style(base.GetDropDownView(position, convertView, parent)!);
    }
    private string L(string pt, string en, string es) => language == "en" ? en : language == "es" ? es : pt;
    private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + .5f);
    private AColor Color(string value) => AColor.ParseColor(value);
    private GradientDrawable Background(string value, int radius = 16)
    {
        var background = new GradientDrawable(); background.SetColor(Color(value)); background.SetCornerRadius(Dp(radius)); return background;
    }
    private TextView Text(string value, int size, bool muted = false)
    {
        var view = new TextView(this) { Text = value, TextSize = size };
        view.SetTextColor(Color(muted ? theme.Muted : theme.Text));
        view.SetPadding(0, Dp(3), 0, Dp(7)); return view;
    }
    private Button Action(string label, Action action, bool primary = false, string? id = null)
    {
        var button = new Button(this) { Text = label, TextSize = 14, ContentDescription = id ?? label };
        button.SetAllCaps(false); button.SetTextColor(Color(primary ? theme.Background : theme.Text));
        button.Background = Background(primary ? theme.Accent : theme.Background, 12);
        button.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        button.Click += (_, _) => { if (!busy) action(); }; actions.Add(button); return button;
    }
    private void AddAction(LinearLayout parent, Button button)
    {
        parent.AddView(button, new LinearLayout.LayoutParams(-1, Dp(52)) { TopMargin = Dp(8) });
    }
    private LinearLayout Panel(LinearLayout parent, string title, string? description = null)
    {
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(20), Dp(16), Dp(20), Dp(20)); panel.Background = Background(theme.Surface);
        parent.AddView(panel, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(14) });
        if (title.Length > 0) { var heading = Text(title, 20); heading.SetTypeface(Typeface.Default, TypefaceStyle.Bold); panel.AddView(heading); }
        if (description != null) panel.AddView(Text(description, 13, true)); return panel;
    }
    private void Navigate(int page) { selectedPage = page; notice = ""; BuildLauncher(); }
    private void Pick(int request, bool multiple = false)
    {
        var intent = new Intent(Intent.ActionOpenDocument); intent.AddCategory(Intent.CategoryOpenable); intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraAllowMultiple, multiple); StartActivityForResult(intent, request);
    }
    private LauncherTheme? CustomTheme()
    {
        string path = Path.Combine(FilesDir!.AbsolutePath, "launcher-theme.json");
        if (!File.Exists(path)) return null;
        try { return LauncherTheme.Parse(File.ReadAllText(path)); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { return null; }
    }
    protected override void OnResume()
    {
        base.OnResume(); if (launcherReady && !busy) BuildLauncher();
    }
    public override void OnBackPressed()
    {
        if (busy) return;
        if (selectedPage != 0) Navigate(0); else base.OnBackPressed();
    }
    private void BuildLauncher()
    {
        actions.Clear(); language = Preferences.GetString("language", "pt")!;
        var custom = CustomTheme();
        var themes = custom == null ? LauncherTheme.Presets : LauncherTheme.Presets.Append(custom).ToArray();
        theme = themes.FirstOrDefault(t => t.Name == Preferences.GetString("theme", "tModLoader")) ?? themes[0];
        bool compact = Resources!.Configuration!.ScreenWidthDp < 700;
        compactLayout = compact;
        var shell = new LinearLayout(this) { Orientation = compact ? Orientation.Vertical : Orientation.Horizontal };
        shell.SetBackgroundColor(Color(theme.Background));
        var rail = new LinearLayout(this) { Orientation = compact ? Orientation.Horizontal : Orientation.Vertical };
        rail.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8)); rail.Background = Background(theme.Surface, 0);
        if (compact) shell.AddView(rail, new LinearLayout.LayoutParams(-1, -2));
        else
        {
            var navigationScroll = new ScrollView(this); navigationScroll.SetBackgroundColor(Color(theme.Surface)); navigationScroll.AddView(rail);
            shell.AddView(navigationScroll, new LinearLayout.LayoutParams(Dp(174), -1));
        }
        if (!compact)
        {
            var icon = new ImageView(this); icon.SetImageResource(global::Tml.Game.Android.Resource.Drawable.tml_icon);
            rail.AddView(icon, new LinearLayout.LayoutParams(Dp(40), Dp(40)) { Gravity = GravityFlags.CenterHorizontal });
            var brand = Text("tModLoader", 17); brand.Gravity = GravityFlags.Center; rail.AddView(brand);
            var version = Text("ANDROID · 0.2.16", 10, true); version.Gravity = GravityFlags.Center; rail.AddView(version);
        }
        string[] labels = [L("Início", "Home", "Inicio"), "Mods", L("Arquivos", "Files", "Archivos"), L("Configurações", "Settings", "Ajustes"), L("Diagnóstico", "Diagnostics", "Diagnóstico")];
        string[] ids = ["launcher-home", "launcher-mods", "launcher-files", "launcher-settings", "launcher-diagnostics"];
        for (int i = 0; i < labels.Length; i++)
        {
            int page = i; var button = Action(labels[i], () => Navigate(page), selectedPage == i, ids[i]);
            var lp = compact ? new LinearLayout.LayoutParams(0, Dp(52), 1) : new LinearLayout.LayoutParams(-1, Dp(44)) { TopMargin = Dp(4) };
            if (compact) button.TextSize = 11; rail.AddView(button, lp);
        }
        var scroll = new ScrollView(this) { FillViewport = true };
        shell.AddView(scroll, compact ? new LinearLayout.LayoutParams(-1, 0, 1) : new LinearLayout.LayoutParams(0, -1, 1));
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.SetPadding(Dp(24), Dp(18), Dp(24), Dp(24)); scroll.AddView(content);
        if (selectedPage != 0)
        {
            var heading = Text(selectedPage == 5 ? L("Registro completo", "Full log", "Registro completo") : labels[selectedPage], 28);
            heading.SetTypeface(Typeface.Default, TypefaceStyle.Bold); content.AddView(heading);
        }
        switch (selectedPage)
        {
            case 0: BuildHome(content); break;
            case 1: BuildMods(content); break;
            case 2: BuildFiles(content); break;
            case 3: BuildSettings(content, themes); break;
            case 4: BuildDiagnostics(content); break;
            case 5:
                AddAction(content, Action(L("Voltar ao resumo", "Back to summary", "Volver al resumen"), () => Navigate(4)));
                var raw = Text(pendingReport, 12); raw.Typeface = Typeface.Monospace; raw.SetTextIsSelectable(true); content.AddView(raw); break;
        }
        status = Text(notice, 13, true); status.ContentDescription = "launcher-status"; content.AddView(status);
        SetContentView(shell); launcherReady = true;
    }
    private void BuildHome(LinearLayout page)
    {
        bool installed = File.Exists(Path.Combine(FilesDir!.AbsolutePath, "Terraria/Terraria.exe"));
        var hero = Panel(page, compactLayout ? "tModLoader · Android" : L("Sua próxima aventura", "Your next adventure", "Tu próxima aventura"), compactLayout ? null : L("Terraria no Android, com o seu jeito de jogar.", "Terraria on Android, your way to play.", "Terraria en Android, a tu manera."));
        hero.AddView(Text(installed ? L("● Arquivos prontos", "● Files ready", "● Archivos listos") : L("○ Importe os arquivos para começar", "○ Import files to get started", "○ Importa los archivos para comenzar"), 14));
        try { hero.AddView(Text($"{LocalModLibrary.Enabled(ModDirectory).Count} " + L("mod(s) selecionado(s)", "mod(s) selected", "mod(s) seleccionado(s)"), 14, true)); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { hero.AddView(Text(L("Seleção de mods ilegível: ", "Unreadable mod selection: ", "Selección ilegible: ") + e.Message, 13)); }
        play = Action(L("Iniciar tModLoader", "Start tModLoader", "Iniciar tModLoader"), () => _ = PlayAsync(), true, "launcher-play"); AddAction(hero, play);
        if (!installed) AddAction(hero, Action(L("Preparar arquivos", "Set up files", "Preparar archivos"), () => Navigate(2)));
        var clean = Panel(page, L("Jogar sem mods", "Play without mods", "Jugar sin mods"), L("Desativa a seleção e mantém os pacotes. Restaure a seleção na biblioteca de mods quando quiser.", "Disables your selection and keeps the packages. Restore your selection in the mod library whenever you want.", "Desactiva la selección y conserva los paquetes. Restaura la selección en la biblioteca cuando quieras."));
        AddAction(clean, Action(L("Iniciar sem mods", "Start without mods", "Iniciar sin mods"), () =>
        {
            try { LocalModLibrary.DisableAll(ModDirectory); _ = PlayAsync(); }
            catch (Exception e) { SetBusy(false, L("Não foi possível desativar: ", "Could not disable mods: ", "No se pudieron desactivar: ") + e.Message); }
        }, id: "launcher-play-clean"));
    }
    private void BuildMods(LinearLayout page)
    {
        var tools = Panel(page, L("Biblioteca local", "Local library", "Biblioteca local"), L("Ative e desative antes de abrir o jogo. O tModLoader verifica as dependências e a compatibilidade.", "Enable or disable before launch. tModLoader checks dependencies and compatibility.", "Activa o desactiva antes de iniciar. tModLoader comprueba dependencias y compatibilidad."));
        AddAction(tools, Action(L("Adicionar mods (.tmod)", "Add mods (.tmod)", "Añadir mods (.tmod)"), () => Pick(11, true), true));
        AddAction(tools, Action(L("Desativar todos", "Disable all", "Desactivar todos"), () => ChangeMods(() => LocalModLibrary.DisableAll(ModDirectory)), id: "launcher-disable-all"));
        if (LocalModLibrary.CanRestore(ModDirectory)) AddAction(tools, Action(L("Restaurar seleção anterior", "Restore previous selection", "Restaurar selección anterior"), () => ChangeMods(() => LocalModLibrary.Restore(ModDirectory))));
        HashSet<string> enabled;
        try { enabled = LocalModLibrary.Enabled(ModDirectory); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { Panel(page, L("Seleção ilegível", "Unreadable selection", "Selección ilegible"), e.Message); return; }
        var groups = LocalModLibrary.Scan(ModDirectory).GroupBy(m => m.Name).ToArray();
        if (groups.Length == 0) Panel(page, L("Sua biblioteca está vazia", "Your library is empty", "Tu biblioteca está vacía"), L("Importe um arquivo .tmod do celular para vê-lo aqui.", "Import a .tmod file from your phone to see it here.", "Importa un archivo .tmod del teléfono para verlo aquí."));
        foreach (var group in groups)
        {
            var mod = group.FirstOrDefault(m => m.Target == "9999.0") ?? group.First();
            var card = Panel(page, mod.Name, mod.Error == null ? $"v{mod.Version}  ·  tML {mod.Target}  ·  {mod.Bytes / 1048576d:F2} MB" : L("Pacote inválido: ", "Invalid package: ", "Paquete inválido: ") + mod.Error);
            card.AddView(Text(mod.FileName + (group.Count() > 1 ? $"  (+{group.Count() - 1})" : ""), 12, true));
            if (mod.Error != null) continue;
            var toggle = new Switch(this) { Text = L("Ativado", "Enabled", "Activado"), Checked = enabled.Contains(mod.Name), ContentDescription = "mod-toggle-" + mod.Name };
            toggle.SetTextColor(Color(theme.Text)); actions.Add(toggle); card.AddView(toggle);
            toggle.CheckedChange += (_, e) => ChangeMods(() => { var names = LocalModLibrary.Enabled(ModDirectory); if (e.IsChecked) names.Add(mod.Name); else names.Remove(mod.Name); LocalModLibrary.Save(ModDirectory, names); });
        }
    }
    private void ChangeMods(Action change)
    {
        try { change(); BuildLauncher(); }
        catch (Exception e) { BuildLauncher(); SetBusy(false, L("Não foi possível salvar: ", "Could not save: ", "No se pudo guardar: ") + e.Message); }
    }
    private void BuildFiles(LinearLayout page)
    {
        bool installed = File.Exists(Path.Combine(FilesDir!.AbsolutePath, "Terraria/Terraria.exe"));
        var files = Panel(page, "Terraria PC 1.4.5.8", installed ? L("Arquivos importados. Pronto para iniciar.", "Files imported. Ready to launch.", "Archivos importados. Listo para iniciar.") : L("Selecione o ZIP completo da sua cópia do Terraria PC. Os arquivos necessários serão verificados.", "Select the complete ZIP from your Terraria PC copy. Required files will be checked.", "Selecciona el ZIP completo de tu copia de Terraria PC. Se comprobarán los archivos necesarios."));
        import = Action(installed ? L("Atualizar Terraria.zip", "Update Terraria.zip", "Actualizar Terraria.zip") : L("Adicionar Terraria.zip", "Add Terraria.zip", "Añadir Terraria.zip"), () => Pick(10), true); AddAction(files, import);
        Panel(page, L("Seus dados", "Your data", "Tus datos"), L("Importar os arquivos não apaga mods, personagens ou mundos. Atualize o aplicativo sem desinstalar para preservar seus dados.", "Importing files keeps mods, players and worlds. Update the app without uninstalling to keep your data.", "Importar archivos conserva mods, jugadores y mundos. Actualiza la aplicación sin desinstalar para conservar tus datos."));
    }
    private void Choice(LinearLayout panel, string title, string[] values, int selected, Action<int> changed, string id)
    {
        panel.AddView(Text(title, 14)); var picker = new Spinner(this) { ContentDescription = id };
        picker.Adapter = new PaletteAdapter(this, values, Color(theme.Text), Color(theme.Background));
        picker.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(Color(theme.Accent)); picker.SetSelection(selected); int current = selected;
        picker.ItemSelected += (_, e) => { if (busy || current == e.Position) return; current = e.Position; changed(e.Position); };
        panel.AddView(picker, new LinearLayout.LayoutParams(-1, Dp(52))); actions.Add(picker);
    }
    private void BuildSettings(LinearLayout page, LauncherTheme[] themes)
    {
        var locale = Panel(page, L("Idioma", "Language", "Idioma"), L("Altera apenas o launcher. O idioma do Terraria é escolhido dentro do jogo.", "Changes the launcher only. Choose Terraria's language inside the game.", "Solo cambia el launcher. El idioma de Terraria se elige dentro del juego."));
        string[] codes = ["pt", "en", "es"];
        Choice(locale, L("Idioma do launcher", "Launcher language", "Idioma del launcher"), ["Português (Brasil)", "English", "Español"], Math.Max(0, Array.IndexOf(codes, language)), i => { Preferences.Edit()!.PutString("language", codes[i])!.Apply(); BuildLauncher(); }, "launcher-language");
        var appearance = Panel(page, L("Aparência", "Appearance", "Apariencia"), L("Temas Calamity e Thorium para o launcher.", "Calamity and Thorium themes for the launcher.", "Temas Calamity y Thorium para el launcher."));
        Choice(appearance, L("Tema", "Theme", "Tema"), themes.Select(t => t.Name).ToArray(), Array.IndexOf(themes, theme), i => { Preferences.Edit()!.PutString("theme", themes[i].Name)!.Apply(); BuildLauncher(); }, "launcher-theme");
        AddAction(appearance, Action(L("Importar tema (.json)", "Import theme (.json)", "Importar tema (.json)"), () => Pick(12)));
        var graphics = Panel(page, L("Gráficos", "Graphics", "Gráficos"), L("OpenGL ES é o padrão validado. Vulkan é experimental e pode falhar neste aparelho.", "OpenGL ES is the validated default. Vulkan is experimental and may fail on this device.", "OpenGL ES es la opción validada. Vulkan es experimental y puede fallar en este dispositivo."));
        Choice(graphics, L("Renderizador", "Renderer", "Renderizador"), [L("OpenGL ES · padrão", "OpenGL ES · default", "OpenGL ES · predeterminado"), "Vulkan · experimental"], Preferences.GetString("renderer", "OpenGL") == "SDLGPU" ? 1 : 0, i => Preferences.Edit()!.PutString("renderer", i == 0 ? "OpenGL" : "SDLGPU")!.Apply(), "Renderizador");
        var game = Panel(page, L("Durante o jogo", "While playing", "Durante el juego"));
        var awake = new Switch(this) { Text = L("Manter tela ligada", "Keep screen awake", "Mantener pantalla encendida"), Checked = Preferences.GetBoolean("keep-screen-on", true) };
        awake.SetTextColor(Color(theme.Text)); awake.CheckedChange += (_, e) => Preferences.Edit()!.PutBoolean("keep-screen-on", e.IsChecked)!.Apply(); game.AddView(awake); actions.Add(awake);
        game.AddView(Text(L("Configure posição, tamanho e ações dos botões touch nas opções dentro do mundo.", "Configure touch button position, size and actions from the in-world options.", "Configura la posición, el tamaño y las acciones táctiles desde las opciones dentro del mundo."), 13, true));
    }
    private void BuildDiagnostics(LinearLayout page)
    {
        pendingReport = HostDiagnostics.ReadReport(this); var report = DiagnosticReport.Parse(pendingReport);
        var summary = Panel(page, L("Última sessão", "Last session", "Última sesión"), !report.HasSession ? L("Ainda não há uma sessão registrada.", "No session recorded yet.", "Aún no hay una sesión registrada.") : report.Errors.Length > 0 ? L("Há erros registrados nesta sessão.", "Errors recorded in this session.", "Hay errores registrados en esta sesión.") : report.NormalExit ? L("O jogo encerrou normalmente.", "The game exited normally.", "El juego se cerró normalmente.") : L("Sem confirmação de encerramento normal. Consulte o histórico.", "No normal-exit confirmation. Check the history.", "No hay confirmación de cierre normal. Consulta el historial."));
        foreach (string line in report.Environment) summary.AddView(Text(line, 12, true));
        var errors = Panel(page, L("Erros da sessão", "Session errors", "Errores de la sesión"));
        if (report.Errors.Length == 0) errors.AddView(Text(L("Nenhum erro fatal identificado no trecho disponível.", "No fatal error identified in the available excerpt.", "No se identificaron errores fatales en el fragmento disponible."), 13, true));
        foreach (var issue in report.Errors) errors.AddView(Text((issue.Code == "native" ? L("Falha nativa", "Native failure", "Fallo nativo") : issue.Code == "game" ? L("Erro do jogo ou mod", "Game or mod error", "Error del juego o mod") : L("Falha de execução", "Execution failure", "Fallo de ejecución")) + $" (×{issue.Count})\n{issue.Detail}", 13));
        var warnings = Panel(page, L("Avisos", "Warnings", "Avisos"), L("Exceções capturadas podem ser tratadas pelo jogo; sozinhas, não comprovam um crash.", "Captured exceptions may be handled by the game; alone, they do not prove a crash.", "El juego puede controlar estas excepciones; por sí solas no prueban un cierre inesperado."));
        if (report.Warnings.Length == 0) warnings.AddView(Text(L("Nenhum aviso identificado.", "No warnings identified.", "No se identificaron avisos."), 13, true));
        foreach (var issue in report.Warnings) warnings.AddView(Text($"×{issue.Count}  {issue.Detail}", 12, true));
        var history = Panel(page, L("Histórico do Android", "Android history", "Historial de Android"), L("Registros anteriores. Sinal 9 indica término forçado, mas não comprova sua causa.", "Earlier records. Signal 9 indicates forced termination but does not establish its cause.", "Registros anteriores. La señal 9 indica terminación forzada, pero no determina la causa."));
        if (report.History.Length == 0) history.AddView(Text(L("Sem histórico disponível.", "No history available.", "Sin historial disponible."), 12, true));
        foreach (string block in report.History.Split("Android:", StringSplitOptions.RemoveEmptyEntries).Take(4))
        {
            string[] lines = block.Trim().Split('\n');
            string reason = lines.FirstOrDefault(l => l.StartsWith("process=", StringComparison.Ordinal)) ?? "";
            string explanation = reason.Contains("reason=CrashNative(") ? L("Falha em código nativo", "Native code failure", "Fallo en código nativo") :
                reason.Contains("reason=LowMemory(") ? L("Android registrou falta de memória", "Android reported low memory", "Android indicó falta de memoria") :
                reason.Contains("reason=PackageUpdated(") ? L("Aplicativo atualizado", "App updated", "Aplicación actualizada") :
                reason.Contains("reason=Signaled(") ? L("Encerrado por sinal; causa não informada", "Terminated by signal; cause not provided", "Terminado por señal; causa no indicada") :
                reason.Contains("reason=UserRequested(") ? L("Encerramento solicitado ao Android", "Termination requested through Android", "Cierre solicitado a Android") :
                L("Encerramento registrado pelo Android", "Termination recorded by Android", "Cierre registrado por Android");
            history.AddView(Text(explanation, 14)); history.AddView(Text(lines[0] + "\n" + reason, 11, true));
        }
        var tools = Panel(page, L("Registro e ferramentas", "Log and tools", "Registro y herramientas"));
        AddAction(tools, Action(L("Salvar diagnóstico (.txt)", "Save diagnostics (.txt)", "Guardar diagnóstico (.txt)"), ExportReport, true, "launcher-export-report"));
        AddAction(tools, Action(L("Ver registro completo", "View full log", "Ver registro completo"), () => Navigate(5)));
        AddAction(tools, Action(L("Copiar registro", "Copy log", "Copiar registro"), () => { ((ClipboardManager)GetSystemService(ClipboardService)!).PrimaryClip = ClipData.NewPlainText("tML Android", pendingReport); status!.Text = L("Registro copiado.", "Log copied.", "Registro copiado."); }));
        AddAction(tools, Action(L("Testar gráficos e controles", "Test graphics and controls", "Probar gráficos y controles"), () => Launch("probe"), id: "launcher-probe"));
    }
    private void ShowReport() => Navigate(4);
    private void ExportReport()
    {
        exportReport = pendingReport;
        var intent = new Intent(Intent.ActionCreateDocument); intent.AddCategory(Intent.CategoryOpenable); intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraTitle, "tML-Android-diagnostico-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"); StartActivityForResult(intent, 13);
    }
    private string DisplayName(global::Android.Net.Uri uri)
    {
        using var cursor = ContentResolver!.Query(uri, new[] { OpenableColumns.DisplayName }, null, null, null);
        return cursor != null && cursor.MoveToFirst() ? cursor.GetString(0) ?? "" : "";
    }
    private async Task ImportModsAsync(Intent data)
    {
        var uris = new List<global::Android.Net.Uri>();
        if (data.ClipData != null) for (int i = 0; i < data.ClipData.ItemCount; i++) { var uri = data.ClipData.GetItemAt(i)?.Uri; if (uri != null) uris.Add(uri); }
        else if (data.Data != null) uris.Add(data.Data);
        if (uris.Count == 0) return;
        SetBusy(true, L("Importando mods…", "Importing mods…", "Importando mods…")); int imported = 0; var errors = new List<string>();
        foreach (var uri in uris)
        {
            string name = "Arquivo";
            try { name = DisplayName(uri); using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Arquivo indisponível."); await LocalModImport.ImportAsync(input, name, ModDirectory); imported++; }
            catch (Exception e) { errors.Add(name + ": " + e.Message); }
        }
        selectedPage = 1; BuildLauncher(); SetBusy(false, $"{imported} " + L("mod(s) importado(s).", "mod(s) imported.", "mod(s) importado(s).") + (errors.Count > 0 ? " " + string.Join("; ", errors) : ""));
    }
    private async Task ImportThemeAsync(global::Android.Net.Uri uri)
    {
        SetBusy(true, L("Importando tema…", "Importing theme…", "Importando tema…"));
        try
        {
            using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Arquivo indisponível.");
            using var buffer = new MemoryStream(); byte[] chunk = new byte[4096]; int n;
            while ((n = await input.ReadAsync(chunk)) > 0) { if (buffer.Length + n > 65536) throw new InvalidDataException("Tema maior que 64 KiB."); buffer.Write(chunk, 0, n); }
            string json = System.Text.Encoding.UTF8.GetString(buffer.ToArray()); var parsed = LauncherTheme.Parse(json);
            if (LauncherTheme.Presets.Any(t => t.Name.Equals(parsed.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Use um nome diferente dos temas predefinidos.");
            string path = Path.Combine(FilesDir!.AbsolutePath, "launcher-theme.json"); File.WriteAllText(path + ".tmp", json); File.Move(path + ".tmp", path, true);
            Preferences.Edit()!.PutString("theme", parsed.Name)!.Apply(); BuildLauncher(); SetBusy(false, L("Tema aplicado: ", "Theme applied: ", "Tema aplicado: ") + parsed.Name);
        }
        catch (Exception e) { SetBusy(false, L("Falha ao importar tema: ", "Theme import failed: ", "Error al importar el tema: ") + e.Message); }
    }
}
