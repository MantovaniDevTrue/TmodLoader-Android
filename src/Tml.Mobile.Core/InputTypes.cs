using System.Numerics;

namespace Tml.Mobile;

[Flags]
public enum MobileAction : ulong
{
    None = 0,
    Left = 1UL << 0, Right = 1UL << 1, Up = 1UL << 2, Down = 1UL << 3,
    Jump = 1UL << 4, Attack = 1UL << 5, Interact = 1UL << 6,
    Grapple = 1UL << 7, Inventory = 1UL << 8, Escape = 1UL << 9,
    Heal = 1UL << 10, Mana = 1UL << 11, Buff = 1UL << 12,
    Mount = 1UL << 13, SmartCursor = 1UL << 14, Map = 1UL << 15,
    HotbarPrevious = 1UL << 16, HotbarNext = 1UL << 17
}

public enum InputContext { Gameplay, Inventory, Menu, TextEntry, Suspended }
public enum ControlKind { Button, MovementStick, AimStick }
public enum TouchPhase { Down, Move, Up, Cancel }

public readonly record struct TouchPoint(int Id, Vector2 Position, TouchPhase Phase);

// All coordinates use pixels in the overlay's local coordinate system.
public readonly record struct Viewport(float X, float Y, float Width, float Height)
{
    public bool Contains(Vector2 point) => point.X >= X && point.Y >= Y &&
        point.X < X + Width && point.Y < Y + Height;

    public Vector2 Normalize(Vector2 point) => Vector2.Clamp(
        new((point.X - X) / Width, (point.Y - Y) / Height), Vector2.Zero, Vector2.One);
}

public readonly record struct TouchControl(
    string Id, string Label, ControlKind Kind, MobileAction Action,
    Vector2 Center, float Radius)
{
    public bool Contains(Vector2 point) => Vector2.DistanceSquared(Center, point) <= Radius * Radius;
}

public readonly record struct InputFrame(
    long Sequence, MobileAction Held, MobileAction Pressed, MobileAction Released,
    Vector2 Movement, Vector2 Aim, Vector2 Pointer, bool HasPointer,
    bool PointerDown, bool PointerPressed, bool PointerReleased)
{
    public long PointerRevision { get; init; }
    public long AimRevision { get; init; }
    public long GestureEpoch { get; init; }
    public bool UiRightDown { get; init; }
    public int UiScrollSteps { get; init; }
    public bool IsHeld(MobileAction action) => (Held & action) != 0;
}
