using System;
using Tml.Mobile;

namespace Microsoft.Xna.Framework.Input
{
    // Compiled inside FNA, sharing its real KeyboardState and MouseState types.
    public static class MobileInputEXT
    {
        public static bool DirectActions { get; set; }
        public static bool NativeTouchEnabled { get; set; }
        public static event Action BeforeUpdate;
        public static Func<TouchPoint, bool> TouchInterceptor { get; set; }
        public static void NativeTouch(int id, float x, float y, TouchPhase phase)
        {
            if (!NativeTouchEnabled || Source == null) return;
            var point = new TouchPoint(id, new System.Numerics.Vector2(x * Math.Max(1, Mouse.INTERNAL_BackBufferWidth), y * Math.Max(1, Mouse.INTERNAL_BackBufferHeight)), phase);
            if (TouchInterceptor == null || !TouchInterceptor(point)) Source.Handle(point);
        }

        private static TouchRouter source;
        public static TouchRouter Source
        {
            get => source;
            set { if (source != value) { aimState.Reset(); physicalSeen = false; } source = value; }
        }
        public static InputFrame Frame { get; private set; }
        public static float AimOriginX { get; set; } = .5f;
        public static float AimOriginY { get; set; } = .5f;
        public static float AimRadius { get; set; } = .22f;
        private static readonly TouchAimState aimState = new TouchAimState();
        private static int physicalX, physicalY;
        private static bool physicalSeen;
        private static int wheel;

        private static readonly (MobileAction Action, Keys Key)[] bindings = {
            (MobileAction.Left, Keys.A), (MobileAction.Right, Keys.D),
            (MobileAction.Up, Keys.W), (MobileAction.Down, Keys.S),
            (MobileAction.Jump, Keys.Space), (MobileAction.Grapple, Keys.E),
            (MobileAction.Inventory, Keys.Escape), (MobileAction.Escape, Keys.Escape),
            (MobileAction.Heal, Keys.H), (MobileAction.Mana, Keys.J),
            (MobileAction.Buff, Keys.B), (MobileAction.Mount, Keys.R),
            (MobileAction.SmartCursor, Keys.LeftControl), (MobileAction.Map, Keys.M)
        };

        public static void Bind(MobileAction action, Keys key)
        {
            for (int i = 0; i < bindings.Length; i++)
                if (bindings[i].Action == action) { bindings[i] = (action, key); return; }
            throw new ArgumentException("Action is not a keyboard binding", nameof(action));
        }

        // Called on the game thread once before each fixed/variable update, not each GetState.
        public static void BeginUpdate()
        {
            BeforeUpdate?.Invoke();
            if (Source == null) { Frame = default(InputFrame); aimState.Reset(); return; }
            Frame = Source.ConsumeFrame();
            if (Source.Context == InputContext.Suspended || Source.Context == InputContext.TextEntry)
            { return; }
            aimState.Update(Frame, new System.Numerics.Vector2(AimOriginX, AimOriginY), AimRadius,
                Math.Max(1, Mouse.INTERNAL_BackBufferWidth), Math.Max(1, Mouse.INTERNAL_BackBufferHeight));
            if ((Frame.Pressed & MobileAction.HotbarPrevious) != 0) wheel += 120;
            if ((Frame.Pressed & MobileAction.HotbarNext) != 0) wheel -= 120;
            if (Source.Context == InputContext.Inventory) wheel += Frame.UiScrollSteps * 120;
        }

        internal static KeyboardState MergeKeyboard(KeyboardState physical)
        {
            if (Source == null || DirectActions) return physical;
            for (int i = 0; i < bindings.Length; i++)
                if (Frame.IsHeld(bindings[i].Action)) physical.AddPressedKey((int)bindings[i].Key);
            return physical;
        }

        internal static MouseState MergeMouse(MouseState physical)
        {
            if (Source == null || Source.Context == InputContext.Suspended || Source.Context == InputContext.TextEntry) return physical;
            bool moved = physicalSeen && (physical.X != physicalX || physical.Y != physicalY);
            physicalX = physical.X; physicalY = physical.Y; physicalSeen = true;
            if (moved && !Frame.HasPointer && Frame.Held == MobileAction.None && Frame.Aim.LengthSquared() == 0) aimState.ReleaseToMouse();
            if (aimState.HasPosition)
            {
                physical.X = (int)(aimState.Position.X * Math.Max(0, Mouse.INTERNAL_BackBufferWidth - 1));
                physical.Y = (int)(aimState.Position.Y * Math.Max(0, Mouse.INTERNAL_BackBufferHeight - 1));
            }
            if (Frame.PointerDown || Frame.IsHeld(MobileAction.Attack)) physical.LeftButton = ButtonState.Pressed;
            if (Frame.UiRightDown || Frame.IsHeld(MobileAction.Interact)) physical.RightButton = ButtonState.Pressed;
            physical.ScrollWheelValue += wheel;
            return physical;
        }
    }
}
