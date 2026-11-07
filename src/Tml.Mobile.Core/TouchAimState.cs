using System.Numerics;

namespace Tml.Mobile;

public sealed class TouchAimState
{
    private long epoch = -1, pointerRevision, aimRevision;
    private bool stickOwns;
    private Vector2 stick;
    public bool HasPosition { get; private set; }
    public Vector2 Position { get; private set; }

    public void Reset() { epoch = -1; pointerRevision = aimRevision = 0; stickOwns = false; stick = default; HasPosition = false; }
    public void ReleaseToMouse() { HasPosition = false; stickOwns = false; }

    public void Update(in InputFrame frame, Vector2 origin, float radius, int width, int height)
    {
        if (frame.GestureEpoch != epoch)
        {
            epoch = frame.GestureEpoch; pointerRevision = aimRevision = 0; stickOwns = false; stick = default;
        }
        bool newPointer = frame.PointerRevision > pointerRevision;
        bool newAim = frame.AimRevision > aimRevision;
        if (newAim && frame.AimRevision > frame.PointerRevision)
        {
            if (!stickOwns && frame.Aim.LengthSquared() == 0) stick = default;
            stickOwns = true;
        }
        else if (newPointer && frame.PointerRevision >= frame.AimRevision) stickOwns = false;
        pointerRevision = Math.Max(pointerRevision, frame.PointerRevision);
        aimRevision = Math.Max(aimRevision, frame.AimRevision);
        if (stickOwns)
        {
            if (frame.Aim.LengthSquared() > 0) stick = frame.Aim;
            if (stick.LengthSquared() > 0)
            {
                float distance = Math.Min(width, height) * radius;
                Position = Vector2.Clamp(origin + new Vector2(stick.X * distance / width, stick.Y * distance / height), Vector2.Zero, Vector2.One);
                HasPosition = true;
            }
        }
        else if (frame.HasPointer && (newPointer || frame.PointerDown || frame.PointerReleased))
        {
            Position = frame.Pointer; HasPosition = true;
        }
    }
}

public enum CaptureScene { Startup, Menu, World, WorldUi, Saving, Paused }
