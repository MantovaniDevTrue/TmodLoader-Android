using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Tml.Mobile;
using AColor = Android.Graphics.Color;

namespace Tml.Mobile.Android;

public sealed partial class MainActivity
{
    private ISharedPreferences Preferences => GetSharedPreferences("launcher", FileCreationMode.Private)!;
    private LauncherTheme theme = LauncherTheme.Presets[0];
    private readonly List<View> actions = [];
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
        view.SetPadding(0, Dp(3), 0, Dp(6)); return view;
    }
    private Button Action(string label, Action action, bool primary = false)
    {
        var button = new Button(this) { Text = label, TextSize = 14, AllCaps = false };
        button.SetTextColor(Color(primary ? theme.Background : theme.Text));
        button.Background = Background(primary ? theme.Accent : theme.Background, 12);
        button.SetPadding(Dp(14), Dp(8), Dp(14), Dp(8));
        button.Click += (_, _) => { if (!busy) action(); };
        actions.Add(button);
        return button;
    }
    private void AddAction(LinearLayout parent, Button button)
    {
        var layout = new LinearLayout.LayoutParams(-1, Dp(48)); layout.TopMargin = Dp(8); parent.AddView(button, layout);
    }
    private LinearLayout Card(LinearLayout row, string number, string title, string description)
    {
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.SetPadding(Dp(18), Dp(14), Dp(18), Dp(18)); card.Background = Background(theme.Surface);
        var lp = new LinearLayout.LayoutParams(0, -1, 1); lp.SetMargins(Dp(5), 0, Dp(5), 0); row.AddView(card, lp);
        var badge = Text(number, 12); badge.SetTextColor(Color(theme.Accent)); card.AddView(badge);
        var heading = Text(title, 21); heading.SetTypeface(Typeface.Default, TypefaceStyle.Bold); card.AddView(heading);
        card.AddView(Text(description, 13, true)); return card;
    }
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
    private void BuildLauncher()
    {
        actions.Clear();
        var custom = CustomTheme();
        var themes = custom == null ? LauncherTheme.Presets : LauncherTheme.Presets.Append(custom).ToArray();
        string selected = Preferences.GetString("theme", "tModLoader")!;
        theme = themes.FirstOrDefault(t => t.Name == selected) ?? themes[0];
        var scroll = new ScrollView(this) { FillViewport = true }; scroll.SetBackgroundColor(Color(theme.Background));
        var page = new LinearLayout(this) { Orientation = Orientation.Vertical }; page.SetPadding(Dp(22), Dp(14), Dp(22), Dp(18)); scroll.AddView(page);
        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.CenterVertical };
        var icon = new ImageView(this); icon.SetImageResource(Resource.Drawable.tml_icon); header.AddView(icon, new LinearLayout.LayoutParams(Dp(64), Dp(64)));
        var name = new LinearLayout(this) { Orientation = Orientation.Vertical }; name.SetPadding(Dp(14), 0, 0, 0);
        var title = Text("tModLoader", 28); title.SetTypeface(Typeface.Default, TypefaceStyle.Bold); name.AddView(title); name.AddView(Text("ANDROID  /  0.2.13  ·  Experimental", 11, true)); header.AddView(name, new LinearLayout.LayoutParams(0, -2, 1));
        var palette = new Spinner(this); var adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, themes.Select(t => t.Name).ToArray()); palette.Adapter = adapter;
        palette.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(Color(theme.Accent)); palette.SetSelection(Array.IndexOf(themes, theme));
        palette.ItemSelected += (_, e) => { if (themes[e.Position].Name == theme.Name) return; Preferences.Edit()!.PutString("theme", themes[e.Position].Name)!.Apply(); BuildLauncher(); };
        header.AddView(palette, new LinearLayout.LayoutParams(Dp(155), Dp(48))); page.AddView(header);
        page.AddView(Text("Seu Terraria. Seus mundos. Seus mods.", 15, true));
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; page.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        bool installed = File.Exists(Path.Combine(FilesDir!.AbsolutePath, "Terraria/Terraria.exe"));
        var setup = Card(row, "01  /  ARQUIVOS", "Terraria PC", installed ? "Arquivos importados. Pronto para iniciar." : "Importe o ZIP completo do Terraria PC 1.4.5.8 uma vez.");
        import = Action(installed ? "Atualizar Terraria.zip" : "Adicionar Terraria.zip", () => Pick(10)); AddAction(setup, import);
        var launch = Card(row, "02  /  JOGAR", "Tudo pronto?", "Controles touch ajustáveis nas opções dentro do mundo.");
        play = Action("Iniciar tModLoader", () => _ = PlayAsync(), true); AddAction(launch, play);
        var mods = Card(row, "03  /  MODS LOCAIS", "Biblioteca de mods", "Adicione .tmod do celular e ative em Gerenciar mods no jogo.");
        int count = Directory.Exists(LocalModImport.DirectoryFor(FilesDir.AbsolutePath)) ? Directory.GetFiles(LocalModImport.DirectoryFor(FilesDir.AbsolutePath), "*.tmod").Length : 0;
        mods.AddView(Text($"{count} pacote(s) instalado(s)", 12, true)); AddAction(mods, Action("Adicionar mods (.tmod)", () => Pick(11, true)));
        var tools = new LinearLayout(this) { Orientation = Orientation.Horizontal }; tools.SetPadding(Dp(5), Dp(12), Dp(5), 0);
        foreach (var button in new[] { Action("Testar gráficos e controles", () => Launch("probe")), Action("Relatório", ShowReport), Action("Importar tema", () => Pick(12)) })
        { var lp = new LinearLayout.LayoutParams(0, Dp(48), 1); lp.SetMargins(0, 0, Dp(8), 0); tools.AddView(button, lp); }
        page.AddView(tools);
        var rendererRow = new LinearLayout(this) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.CenterVertical }; rendererRow.SetPadding(Dp(5), Dp(8), Dp(5), 0); rendererRow.AddView(Text("Renderizador", 13, true));
        var renderer = new Spinner(this); renderer.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, new[] { "OpenGL ES · padrão", "Vulkan · experimental" });
        renderer.SetSelection(Preferences.GetString("renderer", "OpenGL") == "SDLGPU" ? 1 : 0); renderer.ItemSelected += (_, e) => Preferences.Edit()!.PutString("renderer", e.Position == 0 ? "OpenGL" : "SDLGPU")!.Apply(); rendererRow.AddView(renderer, new LinearLayout.LayoutParams(Dp(255), Dp(48))); actions.Add(renderer); page.AddView(rendererRow);
        status = Text("Temas Calamity e Thorium alteram a aparência do launcher. Mods precisam ser compatíveis com esta versão do tML.", 12, true); page.AddView(status);
        SetContentView(scroll);
    }
    private void ShowReport()
    {
        string message = HostDiagnostics.ReadReport(this);
        var dialog = new AlertDialog.Builder(this); dialog.SetTitle("Relatório"); dialog.SetMessage(message); dialog.SetPositiveButton("Fechar", (_, _) => { });
        dialog.SetNeutralButton("Copiar", (_, _) => { var clipboard = (ClipboardManager)GetSystemService(ClipboardService)!; clipboard.PrimaryClip = ClipData.NewPlainText("tML Android", message); }); dialog.Show();
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
        SetBusy(true, "Importando mods…"); int imported = 0; var errors = new List<string>();
        foreach (var uri in uris)
        {
            string name = "Arquivo";
            try { name = DisplayName(uri); using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Arquivo indisponível."); await LocalModImport.ImportAsync(input, name, LocalModImport.DirectoryFor(FilesDir!.AbsolutePath)); imported++; }
            catch (Exception e) { errors.Add(name + ": " + e.Message); }
        }
        BuildLauncher(); SetBusy(false, $"{imported} mod(s) importado(s). Ative em Gerenciar mods no jogo." + (errors.Count > 0 ? " Falhas: " + string.Join("; ", errors) : ""));
    }
    private async Task ImportThemeAsync(global::Android.Net.Uri uri)
    {
        SetBusy(true, "Importando tema…");
        try
        {
            using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Arquivo indisponível.");
            using var buffer = new MemoryStream(); byte[] chunk = new byte[4096]; int n;
            while ((n = await input.ReadAsync(chunk)) > 0) { if (buffer.Length + n > 65536) throw new InvalidDataException("Tema maior que 64 KiB."); buffer.Write(chunk, 0, n); }
            string json = System.Text.Encoding.UTF8.GetString(buffer.ToArray()); var parsed = LauncherTheme.Parse(json);
            if (LauncherTheme.Presets.Any(t => t.Name.Equals(parsed.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Use um nome diferente dos temas predefinidos.");
            string path = Path.Combine(FilesDir!.AbsolutePath, "launcher-theme.json"); File.WriteAllText(path + ".tmp", json); File.Move(path + ".tmp", path, true);
            Preferences.Edit()!.PutString("theme", parsed.Name)!.Apply(); BuildLauncher(); SetBusy(false, "Tema aplicado: " + parsed.Name);
        }
        catch (Exception e) { SetBusy(false, "Falha ao importar tema: " + e.Message); }
    }
}
