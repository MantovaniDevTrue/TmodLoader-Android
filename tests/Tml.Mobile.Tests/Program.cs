using System.Numerics;
using Tml.Mobile;

int passed = 0;
string libraryRoot = Path.Combine(Path.GetTempPath(), "tml-library-" + Guid.NewGuid());
try
{
    Directory.CreateDirectory(libraryRoot);
    void Package(string file, string name, string version)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
        writer.Write("TMOD"u8); writer.Write("9999.0"); writer.Write(new byte[276]);
        long size = data.Position; writer.Write(0); long start = data.Position;
        writer.Write(name); writer.Write(version); writer.Write(0);
        long end = data.Position; data.Position = size; writer.Write((int)(end - start)); writer.Flush();
        File.WriteAllBytes(Path.Combine(libraryRoot, file), data.ToArray());
    }
    Package("Renamed_0.1.0.tmod", "SwordTest", "0.1.0");
    Package("OtherFilename.tmod", "SwordTest", "0.1.1");
    var entries = LocalModLibrary.Scan(libraryRoot);
    Check(entries.Length == 2 && entries.All(m => m.Name == "SwordTest" && m.Error == null), "library reads internal names rather than filenames");
    Check(entries[0].Version == "0.1.1", "library orders duplicate package versions");
    LocalModLibrary.Save(libraryRoot, ["SwordTest", "MissingDependency", "SwordTest"]);
    Check(LocalModLibrary.Enabled(libraryRoot).SetEquals(["SwordTest", "MissingDependency"]), "enabled.json compatible unique internal-name selection");
    LocalModLibrary.DisableAll(libraryRoot);
    Check(LocalModLibrary.Enabled(libraryRoot).Count == 0 && File.Exists(Path.Combine(libraryRoot, entries[0].FileName)), "clean launch disables selection without deleting packages");
    LocalModLibrary.DisableAll(libraryRoot);
    LocalModLibrary.Restore(libraryRoot);
    Check(LocalModLibrary.Enabled(libraryRoot).SetEquals(["SwordTest", "MissingDependency"]), "repeated disable preserves recoverable previous selection");
    File.WriteAllText(Path.Combine(libraryRoot, "bad.tmod"), "not a package");
    Check(LocalModLibrary.Scan(libraryRoot).Single(m => m.FileName == "bad.tmod").Error != null, "invalid package isolated as a library error");
    File.WriteAllBytes(Path.Combine(libraryRoot, "huge-string.tmod"), [84, 77, 79, 68, 255, 255, 255, 127]);
    Check(LocalModLibrary.Read(Path.Combine(libraryRoot, "huge-string.tmod")).Error != null, "untrusted package strings are bounded");
    string selected = Path.Combine(libraryRoot, "enabled.json"); File.WriteAllText(selected, "broken-json");
    bool refused = false;
    try { LocalModLibrary.DisableAll(libraryRoot); } catch (System.Text.Json.JsonException) { refused = true; }
    Check(refused && File.ReadAllText(selected) == "broken-json", "corrupt selection not silently overwritten");
    if (args.Length > 1) Check(LocalModLibrary.Read(args[1]) is { Name: "AndroidSwordTest", Version: "0.1.1", Error: null }, "real supplied sword package identity");
}
finally { Directory.Delete(libraryRoot, true); }
var diagnostic = DiagnosticReport.Parse("--- Sessão ---\nSession 2026-11-07\nSTARTUP_EXCEPTION (may be handled) Test\nMANAGED_SDL_EXIT\n--- Desempenho ---\n--- tModLoader ---\n[Main/ERROR] mod load failed\nAndroid: older\nGot a SIGSEGV\nreason=CrashNative status=11");
Check(diagnostic.NormalExit && diagnostic.Errors.Length == 1 && diagnostic.Errors[0].Code == "game", "diagnostic keeps historical crash out of current-session errors");
Check(diagnostic.Warnings is [{ Code: "handled" }] && diagnostic.History.Contains("SIGSEGV"), "handled exceptions remain warnings and history is preserved");
Check(DiagnosticReport.Parse("Session now\nGot a SIGSEGV").Errors is [{ Code: "native" }], "current native failure identified");
Check(!DiagnosticReport.Parse("Session now\nAndroid: old\nstatus=9").NormalExit, "signal 9 history is not interpreted as a normal exit or a known cause");
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
foreach (var (width, height) in new[] { (2400,1080), (1920,1080), (1080,2400), (800,600), (3200,600), (1,1) })
{
    var size = MobileRenderPolicy.Select(width, height);
    Check(size == MobileRenderPolicy.Select(height, width), "render size ignores portrait rotation");
    Check(size.Width >= 800 && size.Height >= 720 && size.Width >= size.Height &&
        size.Width * size.Height <= MobileRenderPolicy.PixelBudget && size.Width % 8 == 0 && size.Height % 8 == 0,
        "render size respects PC minimum, landscape, alignment and pixel budget");
}
Check(MobileRenderPolicy.Select(2400, 1080) == new RenderSize(1600, 720), "phone render size preserves landscape aspect above the PC minimum");
Check(new RenderSize(1416, 640) != new RenderSize(Math.Max(800, 1416), Math.Max(720, 640)),
    "reproduce old render size diverging from Terraria's 800x720 clamp");
foreach (var display in new[] { (2400,1080), (1080,2400), (1920,1080), (3200,1440), (800,600) })
{
    var selected = MobileRenderPolicy.Select(display.Item1, display.Item2);
    var pc = new RenderSize(Math.Max(800, selected.Width), Math.Max(720, selected.Height));
    Check(pc == selected, "render policy is unchanged by Terraria's minimum-size clamp");
}
try { MobileRenderPolicy.Select(0, 1080); throw new Exception("accepted invalid display"); }
catch (ArgumentOutOfRangeException) { Check(true, "reject invalid display dimensions"); }
var area = new Viewport(0, 0, 1800, 900);
var layout = ControlLayout.CreateDefault(area);
var router = new TouchRouter();
router.Configure(area, layout);
TouchControl Find(string id) => layout.Single(c => c.Id == id);
void Touch(int id, Vector2 at, TouchPhase phase) => router.Handle(new(id, at, phase));
var jump = Find("jump"); var attack = Find("attack"); var move = Find("move"); var aim = Find("aim");
Touch(41, jump.Center, TouchPhase.Down);
Touch(90, attack.Center, TouchPhase.Down);
Touch(7, move.Center, TouchPhase.Down);
Touch(7, move.Center + new Vector2(move.Radius, 0), TouchPhase.Move);
Touch(210, aim.Center, TouchPhase.Down);
Touch(210, aim.Center + new Vector2(0, -aim.Radius), TouchPhase.Move);
var frame = router.ConsumeFrame();
Check(frame.IsHeld(MobileAction.Jump | MobileAction.Attack | MobileAction.Right) &&
    (frame.Held & (MobileAction.Jump | MobileAction.Attack | MobileAction.Right)) ==
    (MobileAction.Jump | MobileAction.Attack | MobileAction.Right), "four independent fingers move, aim, jump and attack");
Check(frame.Movement.X == 1 && frame.Aim.Y == -1 && !frame.PointerDown, "controls do not click through into the world");
Touch(41, jump.Center, TouchPhase.Up);
frame = router.ConsumeFrame();
Check(!frame.IsHeld(MobileAction.Jump) && frame.IsHeld(MobileAction.Attack) &&
    (frame.Released & MobileAction.Jump) != 0, "pointer IDs survive other pointer releases");
Touch(90, attack.Center + new Vector2(200, 0), TouchPhase.Move);
Check(!router.ConsumeFrame().IsHeld(MobileAction.Attack), "dragging off button releases it");
Touch(90, attack.Center, TouchPhase.Move);
Check(router.ConsumeFrame().IsHeld(MobileAction.Attack), "dragging back restores captured button");
router.CancelAll();
frame = router.ConsumeFrame();
Check(frame.Held == 0 && frame.Released != 0 && frame.Movement == Vector2.Zero, "cancel clears all fingers and releases previous state");
Touch(3, jump.Center, TouchPhase.Down);
Touch(3, jump.Center, TouchPhase.Up);
frame = router.ConsumeFrame();
Check(frame.IsHeld(MobileAction.Jump) && (frame.Pressed & MobileAction.Jump) != 0, "tap shorter than one frame survives polling");
Check(router.PeekFrame() == frame && router.PeekFrame() == frame, "overlay reads a coherent frame without consuming game input");
Check((router.ConsumeFrame().Released & MobileAction.Jump) != 0, "short tap releases on following frame");
Touch(3, attack.Center, TouchPhase.Down);
router.SetContext(InputContext.Suspended);
Check(router.ConsumeFrame().Held == 0 && !router.Handle(new(1, jump.Center, TouchPhase.Down)), "pause drops pending taps and blocks new touches");
router.SetContext(InputContext.Gameplay);
Touch(3, attack.Center, TouchPhase.Move);
Check(router.ConsumeFrame().Held == 0, "resume does not resurrect stale gesture");
Touch(3, move.Center, TouchPhase.Down);
Touch(3, move.Center + new Vector2(move.Radius*.1f, 0), TouchPhase.Move);
Check(router.ConsumeFrame().Movement == Vector2.Zero, "stick dead zone prevents drift");
router.SetContext(InputContext.Menu);
Touch(4, jump.Center, TouchPhase.Down);
frame = router.ConsumeFrame();
Check(frame.PointerDown && frame.Held == 0, "menu taps become pointer input without gameplay buttons");
Touch(4, new(-50, 1200), TouchPhase.Move);
frame = router.ConsumeFrame();
Check(frame.Pointer == new Vector2(0, 1), "captured pointer clamps outside viewport");
router.Configure(area, layout.Where(c => c.Action is MobileAction.Inventory or MobileAction.Escape).ToArray());
router.SetContext(InputContext.Inventory);
var uiPrimary = new Vector2(500, 210);
var uiSecondary = new Vector2(620, 240);
Touch(601, uiPrimary, TouchPhase.Down);
Touch(602, uiSecondary, TouchPhase.Down);
frame = router.ConsumeFrame();
Check(!frame.PointerDown && !frame.UiRightDown && frame.HasPointer &&
    frame.Pointer == area.Normalize(uiPrimary), "two-finger inventory input waits for context tap instead of producing a phantom left click");
Touch(602, uiSecondary, TouchPhase.Up);
frame = router.ConsumeFrame();
Check(frame.UiRightDown && !frame.PointerDown, "quick two-finger inventory tap produces one right click");
frame = router.ConsumeFrame();
Check(!frame.UiRightDown && !frame.PointerDown, "context tap releases without re-clicking primary finger");
Touch(601, uiPrimary, TouchPhase.Up);
router.ConsumeFrame();
Touch(603, uiPrimary, TouchPhase.Down);
frame = router.ConsumeFrame();
Check(frame.PointerDown && !frame.UiRightDown, "new single finger recovers normal inventory click and drag");
Touch(603, uiPrimary + new Vector2(140, 80), TouchPhase.Move);
frame = router.ConsumeFrame();
Check(frame.PointerDown && frame.Pointer == area.Normalize(uiPrimary + new Vector2(140, 80)),
    "inventory drag retains one live pointer and updates cursor");
Touch(603, uiPrimary, TouchPhase.Up);
router.ConsumeFrame();
Touch(604, uiPrimary, TouchPhase.Down);
Touch(605, uiSecondary, TouchPhase.Down);
Touch(605, uiSecondary + new Vector2(0, -100), TouchPhase.Move);
frame = router.ConsumeFrame();
Check(frame.UiScrollSteps < 0 && !frame.UiRightDown && !frame.PointerDown,
    "two-finger inventory swipe scrolls recipes without activating item buttons");
Touch(605, uiSecondary + new Vector2(0, -100), TouchPhase.Up);
frame = router.ConsumeFrame();
Check(!frame.UiRightDown, "completed scroll cannot synthesize a secondary click");
Touch(604, uiPrimary, TouchPhase.Up);
router.ConsumeFrame();
var inventoryControl = layout.Single(c => c.Action == MobileAction.Inventory);
Touch(606, inventoryControl.Center, TouchPhase.Down);
frame = router.ConsumeFrame();
Check(frame.IsHeld(MobileAction.Inventory) && !frame.PointerDown,
    "inventory close button remains a real UI action and never clicks an item");
Touch(606, inventoryControl.Center, TouchPhase.Up);
router.Configure(area, layout);
router.SetContext(InputContext.TextEntry);
Check(!router.Handle(new(7, jump.Center, TouchPhase.Down)) && router.ConsumeFrame().Held == 0, "text entry suppresses gameplay input");
router.SetContext(InputContext.Gameplay);
Touch(1, attack.Center, TouchPhase.Down);
Check(!router.Handle(new(2, attack.Center, TouchPhase.Down)), "second finger on captured button cannot become world click");
router.ConsumeFrame();
router.Configure(new(100, 50, 1600, 800), layout);
Check(router.ConsumeFrame().Held == 0, "resize cancels old captures");
Check(!router.Handle(new(1, new(float.NaN, 0), TouchPhase.Down)), "nonfinite Android coordinates rejected");
var inset = ControlLayout.CreateDefault(new(100, 60, 1400, 700));
Check(inset.All(c => c.Center.X-c.Radius >= 100 && c.Center.X+c.Radius <= 1500 &&
    c.Center.Y-c.Radius >= 60 && c.Center.Y+c.Radius <= 760), "default controls fit safe area");

router.Configure(area, layout);
for (int i = 0; i < 1000; i++)
{
    Touch(17, move.Center, TouchPhase.Down);
    Touch(17, move.Center+new Vector2(30, 20), TouchPhase.Move);
    router.ConsumeFrame(); router.CancelAll(); router.ConsumeFrame();
}
long MeasureRouting()
{
    long start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10000; i++)
    {
        Touch(17, move.Center, TouchPhase.Down);
        Touch(17, move.Center+new Vector2(30, 20), TouchPhase.Move);
        router.ConsumeFrame(); router.CancelAll(); router.ConsumeFrame();
    }
    return GC.GetAllocatedBytesForCurrentThread()-start;
}
MeasureRouting();
long measuredAlloc = MeasureRouting();
Console.WriteLine($"Router allocation measurement: {measuredAlloc} bytes");
Check(measuredAlloc == 0, "10,000 warmed touch/update cycles allocate zero managed bytes");
var issues = ContentPreflight.Validate(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
Check(issues.Count == 6 && issues.Any(i => i.Asset == "Images/SplashScreens/Splash_1.xnb"), "missing desktop content reports the known splash blocker");
string contentTest = Path.Combine(Path.GetTempPath(), "tml-content-" + Guid.NewGuid());
try
{
    string vanilla = Path.Combine(contentTest, "vanilla"), overrides = Path.Combine(contentTest, "overrides");
    foreach (string asset in new[] { "Images/SplashScreens/Splash_1.xnb", "Images/Item_1.xnb", "Images/Tiles_0.xnb",
        "PixelShader.xnb", "TileShader.xnb", "ScreenShader.xnb" })
    {
        string path = Path.Combine(vanilla, asset);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [(byte)'X', (byte)'N', (byte)'B', 0, 0, 0, 0, 0, 0, 0]);
    }
    Check(ContentPreflight.Validate(vanilla, overrides).Count == 0,
        "complete vanilla content does not require duplicate shader overrides");
    Directory.CreateDirectory(overrides);
    File.WriteAllText(Path.Combine(overrides, "PixelShader.xnb"), "broken");
    var overrideIssues = ContentPreflight.Validate(vanilla, overrides);
    Check(overrideIssues.Count == 1 && overrideIssues[0].Root == overrides,
        "preflight reports a broken selected override instead of silently choosing vanilla");
}
finally { Directory.Delete(contentTest, recursive: true); }
if (args.Length > 0)
{
    var runtime = RuntimeProbe.Run(args[0]);
    Check(runtime.AssemblyLoaded && runtime.EmitSucceeded, "external .NET 10 assembly and Reflection.Emit execute");
}
Console.WriteLine($"{passed} checks passed.");

var profileDir = Path.Combine(Path.GetTempPath(), "touch-profile-" + Guid.NewGuid());
try
{
    string path = Path.Combine(profileDir, "layout.json");
    var profile = new TouchProfile { Opacity = .4f, AttackWithAim = true };
    profile.Buttons["jump"] = new(.92f, .75f, 1.4f, false);
    profile.Save(path);
    var loaded = TouchProfile.Load(path);
    Check(loaded.AttackWithAim && loaded.Opacity == .4f && loaded.Buttons["jump"].Size == 1.4f, "touch profile round trip preserves positions, sizing and options");
    var bounds = new Viewport(10, 10, 900, 500);
    Check(loaded.Layout(bounds).All(c => c.Id != "jump") && loaded.Layout(bounds, true).Any(c => c.Id == "jump"), "hidden buttons remain editable without gameplay captures");
    loaded.Buttons["move"] = new(0, 1, 2);
    Check(loaded.Layout(bounds, true).All(c => c.Center.X-c.Radius >= bounds.X && c.Center.Y+c.Radius <= bounds.Y+bounds.Height), "custom buttons and sticks stay inside viewport after resize");
    loaded.Buttons["move"] = new(float.NaN, 0);
    bool invalid = false;
    try { loaded.Save(path); } catch (InvalidDataException) { invalid = true; }
    Check(invalid && TouchProfile.Load(path).Buttons["jump"].Size == 1.4f, "invalid layout cannot replace the last saved profile");
}
finally { if (Directory.Exists(profileDir)) Directory.Delete(profileDir, true); }

var aimRouter = new TouchRouter();
var aimArea = new Viewport(0, 0, 1000, 500);
TouchControl[] aimLayout = [new("aim", "Aim", ControlKind.AimStick, MobileAction.None, new(800,350), 80), new("jump", "Jump", ControlKind.Button, MobileAction.Jump, new(950,400), 30)];
aimRouter.Configure(aimArea, aimLayout);
var aimState = new TouchAimState();
void SampleAim() { var f = aimRouter.ConsumeFrame(); aimState.Update(f, new(.5f,.5f), .22f, 1000, 500); }
aimRouter.Handle(new(1,new(200,100),TouchPhase.Down)); aimRouter.Handle(new(1,new(200,100),TouchPhase.Up)); SampleAim();
Check(aimState.Position == new Vector2(.2f,.2f), "world tap sets aim once");
aimRouter.Handle(new(2,new(800,350),TouchPhase.Down)); aimRouter.Handle(new(2,new(880,350),TouchPhase.Move)); SampleAim();
var stickPosition = aimState.Position;
aimRouter.Handle(new(2,new(880,350),TouchPhase.Up)); SampleAim();
for(int i=0;i<20;i++) SampleAim();
Check(aimState.Position == stickPosition && stickPosition.X > .5f, "releasing stick never restores the earlier world tap");
aimRouter.Handle(new(3,new(950,400),TouchPhase.Down)); SampleAim();
Check(aimState.Position == stickPosition, "HUD button cannot claim aim or move the cursor");
aimRouter.Handle(new(3,new(950,400),TouchPhase.Up)); SampleAim();
aimRouter.SetContext(InputContext.Menu); SampleAim();
Check(aimState.Position == stickPosition, "UI context transitions preserve cursor position");
aimRouter.Handle(new(4,new(300,200),TouchPhase.Down)); SampleAim();
Check(aimState.Position == new Vector2(.3f,.4f), "fresh menu touch takes ownership after a stick gesture");

var modRoot = Path.Combine(Path.GetTempPath(), "tml-import-" + Guid.NewGuid());
try
{
    byte[] package = new byte[64]; "TMOD"u8.CopyTo(package);
    string installedMod = LocalModImport.ImportAsync(new MemoryStream(package), "../Example.tmod", modRoot).GetAwaiter().GetResult();
    Check(installedMod == "Example.tmod" && File.ReadAllBytes(Path.Combine(modRoot,installedMod)).SequenceEqual(package), "local mod stream imports to sanitized name");
    byte[] changed = (byte[])package.Clone(); changed[^1] = 8;
    LocalModImport.ImportAsync(new MemoryStream(changed), "Example.tmod", modRoot).GetAwaiter().GetResult();
    Check(File.ReadAllBytes(Path.Combine(modRoot,"Example.tmod.previous")).SequenceEqual(package), "mod update preserves previous package");
    try { LocalModImport.ImportAsync(new MemoryStream(new byte[64]), "Example.tmod", modRoot).GetAwaiter().GetResult(); throw new Exception("Invalid mod accepted"); }
    catch(InvalidDataException) { Check(File.ReadAllBytes(Path.Combine(modRoot,"Example.tmod")).SequenceEqual(changed), "invalid import preserves installed mod"); }
    Check(Directory.GetFiles(modRoot,".import-*").Length == 0, "failed import leaves no staging package");
    Check(LauncherTheme.Parse(System.Text.Json.JsonSerializer.Serialize(LauncherTheme.Presets[1])).Name == "Calamity", "custom palette parses");
    try { LauncherTheme.Parse("{\"Name\":\"Bad\",\"Background\":\"red\"}"); throw new Exception("Invalid palette accepted"); }
    catch(InvalidDataException) { Check(true,"invalid custom palette rejected"); }
}
finally { Directory.Delete(modRoot,true); }

aimRouter.SetContext(InputContext.Gameplay);
aimRouter.Handle(new(8,new(800,350),TouchPhase.Down));
aimRouter.Handle(new(8,new(880,350),TouchPhase.Move)); SampleAim();
aimRouter.Handle(new(8,new(880,350),TouchPhase.Up)); SampleAim();
aimState.ReleaseToMouse();
aimState.Update(aimRouter.ConsumeFrame(),new(.5f,.5f),.22f,1600,720);
Check(!aimState.HasPosition, "hardware mouse takeover does not restore old stick aim on the next update");
