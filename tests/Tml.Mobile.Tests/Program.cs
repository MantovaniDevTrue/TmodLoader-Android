using System.Numerics;
using Tml.Mobile;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
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
    Touch(17, move.Center, TouchPhase.Down); router.ConsumeFrame(); router.CancelAll(); router.ConsumeFrame();
}
long start = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 10000; i++)
{
    Touch(17, move.Center, TouchPhase.Down);
    Touch(17, move.Center+new Vector2(30, 20), TouchPhase.Move);
    router.ConsumeFrame(); router.CancelAll(); router.ConsumeFrame();
}
Check(GC.GetAllocatedBytesForCurrentThread()-start == 0, "10,000 warmed touch/update cycles allocate zero managed bytes");
var issues = ContentPreflight.Validate(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
Check(issues.Count == 6 && issues.Any(i => i.Asset == "Images/SplashScreens/Splash_1.xnb"), "missing desktop content reports the known splash blocker");
if (args.Length > 0)
{
    var runtime = RuntimeProbe.Run(args[0]);
    Check(runtime.AssemblyLoaded && runtime.EmitSucceeded, "external .NET 10 assembly and Reflection.Emit execute");
}
Console.WriteLine($"{passed} checks passed.");
