using System.Numerics;
using System.Reflection;
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
Console.WriteLine($"{passed} FNA integration checks passed.");
