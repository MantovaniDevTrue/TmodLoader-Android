using System.Diagnostics;
using System.Numerics;

namespace Tml.Mobile;

public sealed class TouchRouter
{
    private struct Finger
    {
        public bool Active;
        public int Id, Control;
        public Vector2 Position;
    }

    private readonly object gate = new();
    private readonly Finger[] fingers = new Finger[16];
    private TouchControl[] controls = [];
    private Viewport viewport;
    private InputContext context = InputContext.Gameplay;
    private MobileAction held, pendingPressed, sampledHeld;
    private Vector2 movement, aim, pointer = new(.5f, .5f);
    private bool pointerDown, pointerPressed, sampledPointerDown;
    private InputFrame lastFrame;
    private long sequence, gestureRevision, pointerRevision, aimRevision, gestureEpoch;
    private long uiSecondaryStart;
    private float uiSecondaryStartY, uiScrollRemainder;
    private int pendingUiScroll;
    private bool uiSecondaryScrolling, uiRightWasHeld, pendingUiRightTap;

    public InputContext Context { get { lock (gate) return context; } }

    public void Configure(Viewport gameViewport, ReadOnlySpan<TouchControl> layout)
    {
        if (!float.IsFinite(gameViewport.X) || !float.IsFinite(gameViewport.Y) ||
            !float.IsFinite(gameViewport.Width) || !float.IsFinite(gameViewport.Height) ||
            gameViewport.Width <= 0 || gameViewport.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(gameViewport));
        foreach (var control in layout)
            if (!float.IsFinite(control.Center.X) || !float.IsFinite(control.Center.Y) ||
                !float.IsFinite(control.Radius) || control.Radius <= 0)
                throw new ArgumentException("Control coordinates and radius must be finite and positive.", nameof(layout));
        lock (gate)
        {
            ResetCore();
            viewport = gameViewport;
            controls = layout.ToArray();
        }
    }

    public void SetContext(InputContext value)
    {
        lock (gate)
        {
            if (context == value) return;
            ResetCore();
            context = value;
        }
    }

    public void CancelAll() { lock (gate) ResetCore(); }

    public InputFrame PeekFrame() { lock (gate) return lastFrame; }

    public bool Handle(TouchPoint point)
    {
        lock (gate)
        {
            if (point.Phase == TouchPhase.Cancel) { ResetCore(); return true; }
            if (context is InputContext.Suspended or InputContext.TextEntry || viewport.Width <= 0)
                return false;
            if (!float.IsFinite(point.Position.X) || !float.IsFinite(point.Position.Y)) return false;
            int slot = Find(point.Id);
            if (point.Phase == TouchPhase.Down)
            {
                // IDs, rather than Android pointer indices, own captures throughout a gesture.
                if (slot >= 0) return true;
                int target = HitTest(point.Position);
                if (target == -2) return false;
                slot = FreeSlot();
                if (slot < 0) return false;
                fingers[slot] = new() { Active = true, Id = point.Id, Control = target, Position = point.Position };
                if (target == -3)
                {
                    uiSecondaryStart = Stopwatch.GetTimestamp();
                    uiSecondaryStartY = point.Position.Y;
                    uiScrollRemainder = 0;
                    uiSecondaryScrolling = uiRightWasHeld = pendingUiRightTap = false;
                }
            }
            else
            {
                if (slot < 0) return false;
                if (fingers[slot].Control == -3 && point.Phase == TouchPhase.Move)
                {
                    float delta = point.Position.Y - fingers[slot].Position.Y;
                    float total = point.Position.Y - uiSecondaryStartY;
                    if (!uiSecondaryScrolling && MathF.Abs(total) >= 24)
                    {
                        uiSecondaryScrolling = true;
                        uiScrollRemainder = total;
                    }
                    else if (uiSecondaryScrolling) uiScrollRemainder += delta;
                    if (uiSecondaryScrolling)
                    {
                        int steps = (int)(uiScrollRemainder / 32f);
                        if (steps != 0)
                        {
                            pendingUiScroll += steps;
                            uiScrollRemainder -= steps * 32f;
                        }
                    }
                }
                if (fingers[slot].Control == -3 && point.Phase == TouchPhase.Up)
                    pendingUiRightTap = !uiSecondaryScrolling && !uiRightWasHeld;
                fingers[slot].Position = point.Position;
                if (point.Phase == TouchPhase.Up) fingers[slot].Active = false;
            }
            int captured = fingers[slot].Control;
            if (captured == -1) pointer = viewport.Normalize(point.Position);
            if (point.Phase is TouchPhase.Down or TouchPhase.Move)
            {
                if (captured == -1) pointerRevision = ++gestureRevision;
                else if (controls[captured].Kind == ControlKind.AimStick) aimRevision = ++gestureRevision;
            }
            Recalculate();
            return true;
        }
    }

    // Consume once per game update; readers should reuse this frame for all input queries.
    // Short taps are held for one sampled frame so polling APIs do not miss them.
    public InputFrame ConsumeFrame()
    {
        lock (gate)
        {
            MobileAction visible = held | pendingPressed;
            MobileAction pressed = visible & ~sampledHeld;
            MobileAction released = sampledHeld & ~visible;
            bool uiRightHeld = context == InputContext.Inventory && IsCaptured(-1) &&
                IsCaptured(-3) && !uiSecondaryScrolling &&
                Stopwatch.GetTimestamp() - uiSecondaryStart >= Stopwatch.Frequency / 6;
            if (uiRightHeld) uiRightWasHeld = true;
            bool uiRight = uiRightHeld || pendingUiRightTap;
            bool visiblePointer = (pointerDown || pointerPressed) && !uiRight;
            bool hasPosition = pointerDown || pointerPressed || sampledPointerDown || uiRight;
            var frame = new InputFrame(++sequence, visible, pressed, released,
                movement, aim, pointer, hasPosition, visiblePointer,
                visiblePointer && !sampledPointerDown, !visiblePointer && sampledPointerDown)
            {
                PointerRevision = pointerRevision, AimRevision = aimRevision, GestureEpoch = gestureEpoch,
                UiRightDown = uiRight, UiScrollSteps = pendingUiScroll
            };
            sampledHeld = visible;
            sampledPointerDown = visiblePointer;
            pendingPressed = MobileAction.None;
            pointerPressed = false;
            pendingUiRightTap = false;
            pendingUiScroll = 0;
            lastFrame = frame;
            return frame;
        }
    }

    private int Find(int id)
    {
        for (int i = 0; i < fingers.Length; i++) if (fingers[i].Active && fingers[i].Id == id) return i;
        return -1;
    }

    private int FreeSlot()
    {
        for (int i = 0; i < fingers.Length; i++) if (!fingers[i].Active) return i;
        return -1;
    }

    private int HitTest(Vector2 position)
    {
        if (!viewport.Contains(position)) return -2;
        if (context is InputContext.Gameplay or InputContext.Inventory)
            for (int i = 0; i < controls.Length; i++)
                if (controls[i].Contains(position)) return IsCaptured(i) ? -2 : i;
        if (!IsCaptured(-1)) return -1;
        return context == InputContext.Inventory && !IsCaptured(-3) ? -3 : -2;
    }

    private bool IsCaptured(int control)
    {
        foreach (var finger in fingers) if (finger.Active && finger.Control == control) return true;
        return false;
    }

    private void Recalculate()
    {
        MobileAction next = MobileAction.None;
        bool nextPointerDown = false;
        movement = aim = Vector2.Zero;
        foreach (var finger in fingers)
        {
            if (!finger.Active) continue;
            if (finger.Control == -1)
            {
                pointer = viewport.Normalize(finger.Position);
                nextPointerDown = true;
                continue;
            }
            if (finger.Control == -3) continue;
            var control = controls[finger.Control];
            if (control.Kind == ControlKind.Button)
            {
                if (control.Contains(finger.Position)) next |= control.Action;
                continue;
            }
            var delta = (finger.Position - control.Center) / control.Radius;
            float length = delta.Length();
            const float deadZone = .18f;
            var stick = length <= deadZone ? Vector2.Zero :
                delta / length * Math.Clamp((length - deadZone) / (1-deadZone), 0, 1);
            if (control.Kind == ControlKind.AimStick) { aim = stick; continue; }
            movement = stick;
            if (stick.X < -.25f) next |= MobileAction.Left;
            if (stick.X > .25f) next |= MobileAction.Right;
            if (stick.Y < -.25f) next |= MobileAction.Up;
            if (stick.Y > .25f) next |= MobileAction.Down;
        }
        pendingPressed |= next & ~held;
        pointerPressed |= nextPointerDown && !pointerDown;
        pointerDown = nextPointerDown;
        held = next;
    }

    private void ResetCore()
    {
        gestureEpoch++; pointerRevision = aimRevision = 0;
        Array.Clear(fingers);
        held = pendingPressed = MobileAction.None;
        movement = aim = Vector2.Zero;
        pointerDown = pointerPressed = false;
        uiSecondaryStart = 0;
        uiSecondaryStartY = uiScrollRemainder = 0;
        pendingUiScroll = 0;
        uiSecondaryScrolling = uiRightWasHeld = pendingUiRightTap = false;
        lastFrame = default;
        // Keep sampled state until the consumer sees the release frame.
    }
}
