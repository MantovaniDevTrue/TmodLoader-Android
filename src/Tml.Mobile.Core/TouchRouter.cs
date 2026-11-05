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
    private bool pointerDown, pointerPressed, sampledPointerDown, hasPointer;
    private InputFrame lastFrame;
    private long sequence;

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
            }
            else
            {
                if (slot < 0) return false;
                fingers[slot].Position = point.Position;
                if (point.Phase == TouchPhase.Up) fingers[slot].Active = false;
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
            bool visiblePointer = pointerDown || pointerPressed;
            var frame = new InputFrame(++sequence, visible, pressed, released,
                movement, aim, pointer, hasPointer, visiblePointer,
                visiblePointer && !sampledPointerDown, !visiblePointer && sampledPointerDown);
            sampledHeld = visible;
            sampledPointerDown = visiblePointer;
            pendingPressed = MobileAction.None;
            pointerPressed = false;
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
        if (context == InputContext.Gameplay)
            for (int i = 0; i < controls.Length; i++)
                if (controls[i].Contains(position)) return IsCaptured(i) ? -2 : i;
        // Exactly one free-screen finger owns the pointer; buttons never become clicks.
        return IsCaptured(-1) ? -2 : -1;
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
                hasPointer = nextPointerDown = true;
                continue;
            }
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
        Array.Clear(fingers);
        held = pendingPressed = MobileAction.None;
        movement = aim = Vector2.Zero;
        pointerDown = pointerPressed = hasPointer = false;
        lastFrame = default;
        // Keep sampled state until the consumer sees the release frame.
    }
}
