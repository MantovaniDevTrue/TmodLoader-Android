using System.Numerics;
using System.Reflection;
using MobilePerformanceEXT = Microsoft.Xna.Framework.MobilePerformanceEXT;
using Microsoft.Xna.Framework.Input;
using Tml.Mobile;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
var type = typeof(MobileInputEXT);
var mergeKeyboard = type.GetMethod("MergeKeyboard", BindingFlags.NonPublic | BindingFlags.Static)!
    .CreateDelegate<Func<KeyboardState, KeyboardState>>();
var mergeMouse = type.GetMethod("MergeMouse", BindingFlags.NonPublic | BindingFlags.Static)!
    .CreateDelegate<Func<MouseState, MouseState>>();
var physicalKeys = new KeyboardState(Keys.F1);
var physicalMouse = new MouseState(20, 30, 240, ButtonState.Released, ButtonState.Pressed,
    ButtonState.Released, ButtonState.Pressed, ButtonState.Released);
var router = new TouchRouter();
var area = new Viewport(0, 0, 1800, 900);
var controls = ControlLayout.CreateDefault(area);
router.Configure(area, controls);
MobileInputEXT.Source = router;
var jump = controls.Single(c => c.Id == "jump");
var attack = controls.Single(c => c.Id == "attack");
router.Handle(new(5, jump.Center, TouchPhase.Down));
router.Handle(new(7, attack.Center, TouchPhase.Down));
MobileInputEXT.BeginUpdate();
var keys = mergeKeyboard(physicalKeys);
var mouse = mergeMouse(physicalMouse);
Check(keys.IsKeyDown(Keys.Space) && keys.IsKeyDown(Keys.F1), "touch keyboard merges without losing hardware keys");
Check(mouse.LeftButton == ButtonState.Pressed && mouse.MiddleButton == ButtonState.Pressed &&
    mouse.XButton1 == ButtonState.Pressed, "touch mouse preserves unrelated hardware buttons");
Check(Keyboard.GetState().IsKeyDown(Keys.Space), "public FNA Keyboard.GetState sees mobile input");
Check(Keyboard.GetState() == Keyboard.GetState(), "repeated polling does not consume input edges");
router.CancelAll();
MobileInputEXT.BeginUpdate();
Check(mergeKeyboard(physicalKeys).IsKeyUp(Keys.Space) && mergeMouse(physicalMouse).LeftButton == ButtonState.Released,
    "mobile release preserves physical state");
MobileInputEXT.Bind(MobileAction.Jump, Keys.Z);
router.Handle(new(5, jump.Center, TouchPhase.Down));
MobileInputEXT.BeginUpdate();
Check(Keyboard.GetState().IsKeyDown(Keys.Z) && Keyboard.GetState().IsKeyUp(Keys.Space), "keyboard binding can follow Terraria input profile");
router.SetContext(InputContext.Menu);
router.Handle(new(15, new(900, 450), TouchPhase.Down));
MobileInputEXT.BeginUpdate();
mouse = mergeMouse(physicalMouse);
Check(mouse.X == 399 && mouse.Y == 239 && mouse.LeftButton == ButtonState.Pressed,
    "menu pointer maps overlay pixels into actual FNA backbuffer");
router.SetContext(InputContext.Suspended);
MobileInputEXT.BeginUpdate();
Check(mergeMouse(physicalMouse) == physicalMouse, "suspending clears synthetic mouse position and buttons");
router.SetContext(InputContext.Gameplay);
var next = controls.Single(c => c.Id == "next");
router.Handle(new(2, next.Center, TouchPhase.Down));
MobileInputEXT.BeginUpdate();
int wheel = mergeMouse(physicalMouse).ScrollWheelValue;
MobileInputEXT.BeginUpdate();
Check(wheel == 120 && mergeMouse(physicalMouse).ScrollWheelValue == wheel, "held hotbar button produces one scroll step");
router.CancelAll();
MobileInputEXT.BeginUpdate();
var stick = controls.Single(c => c.Id == "aim");
router.Handle(new(8, stick.Center, TouchPhase.Down));
router.Handle(new(8, stick.Center+new Vector2(stick.Radius, 0), TouchPhase.Move));
MobileInputEXT.BeginUpdate();
mouse = mergeMouse(physicalMouse);
Check(mouse.X > 399 && mouse.Y == 239, "aim stick updates cursor without a fake mouse device");
void RunBatch()
{
    for (int i = 0; i < 10000; i++) { MobileInputEXT.BeginUpdate(); mergeKeyboard(physicalKeys); mergeMouse(physicalMouse); }
}
RunBatch();
long before = GC.GetAllocatedBytesForCurrentThread();
RunBatch();
long allocated = GC.GetAllocatedBytesForCurrentThread()-before;
Console.WriteLine($"FNA allocation measurement: {allocated} bytes");
Check(allocated == 0, "10,000 FNA merge/update cycles allocate zero managed bytes");
MobilePerformanceEXT.Destination = null;
MobilePerformanceEXT.BeginFrame();
Check(MobilePerformanceEXT.BeginSection() == 0, "performance capture defaults to disabled");
MobilePerformanceEXT.EndUpdate(0); MobilePerformanceEXT.EndFrame();
var capture = new FrameTimingWindow();
MobilePerformanceEXT.Destination = capture;
MobilePerformanceEXT.BeginFrame();
long section = MobilePerformanceEXT.BeginSection();
MobilePerformanceEXT.EndUpdate(section);
section = MobilePerformanceEXT.BeginSection(); MobilePerformanceEXT.EndUpdate(section);
section = MobilePerformanceEXT.BeginSection(); MobilePerformanceEXT.EndDraw(section);
section = MobilePerformanceEXT.BeginSection(); MobilePerformanceEXT.EndPresent(section);
MobilePerformanceEXT.EndFrame();
MobilePerformanceEXT.EndFrame();
var timing = capture.Snapshot();
Check(timing.TotalRecorded == 1 && timing.Samples[0].UpdatePasses == 2 && timing.PresentedFrames == 1,
    "FNA timing adapter aggregates catch-up passes and records one presentation per completed tick");
Check(timing.Samples[0].TickTicks >= timing.Samples[0].UpdateTicks + timing.Samples[0].DrawTicks + timing.Samples[0].PresentTicks,
    "phase timers fit inside measured tick duration");
MobilePerformanceEXT.BeginFrame();
section = MobilePerformanceEXT.BeginSection(); MobilePerformanceEXT.EndUpdate(section);
MobilePerformanceEXT.EndFrame();
Check(capture.Snapshot().Samples[1].PresentationTimestamp == 0, "suppressed drawing does not create a presentation timestamp");
MobilePerformanceEXT.BeginFrame();
MobilePerformanceEXT.Destination = null;
MobilePerformanceEXT.EndFrame();
Check(capture.Snapshot().TotalRecorded == 2, "detaching capture discards a partially measured frame");
MobilePerformanceEXT.Destination = capture;
void CaptureBatch()
{
    for (int i = 0; i < 10000; i++)
    {
        MobilePerformanceEXT.BeginFrame();
        long started = MobilePerformanceEXT.BeginSection();
        MobilePerformanceEXT.EndUpdate(started);
        started = MobilePerformanceEXT.BeginSection(); MobilePerformanceEXT.EndDraw(started);
        started = MobilePerformanceEXT.BeginSection(); MobilePerformanceEXT.EndPresent(started);
        MobilePerformanceEXT.EndFrame();
    }
}
CaptureBatch();
long captureAllocationStart = GC.GetAllocatedBytesForCurrentThread();
CaptureBatch();
Check(GC.GetAllocatedBytesForCurrentThread() - captureAllocationStart == 0,
    "10,000 warmed enabled FNA capture cycles allocate zero managed bytes");
MobilePerformanceEXT.Destination = null;

bool rejectedSound = false, rejectedWave = false;
try { _ = new Microsoft.Xna.Framework.Audio.SoundBank(null!, "unused.xsb"); }
catch (ArgumentNullException) { rejectedSound = true; }
try { _ = new Microsoft.Xna.Framework.Audio.WaveBank(null!, "unused.xwb"); }
catch (ArgumentNullException) { rejectedWave = true; }
GC.Collect();
GC.WaitForPendingFinalizers();
Check(rejectedSound && rejectedWave, "failed audio bank constructors finalize without accessing native null handles");

Console.WriteLine($"{passed} FNA integration checks passed.");
