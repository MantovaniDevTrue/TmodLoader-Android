using System;
using Tml.Mobile;

namespace Microsoft.Xna.Framework.Input
{
    // Compiled inside FNA, sharing its real KeyboardState and MouseState types.
    public static class MobileInputEXT
    {
        public static TouchRouter Source { get; set; }
        public static InputFrame Frame { get; private set; }
        public static float AimOriginX { get; set; } = .5f;
        public static float AimOriginY { get; set; } = .5f;
        public static float AimRadius { get; set; } = .22f;
        private static bool hasAim;
        private static float aimX, aimY;
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
            if (Source == null) { Frame = default(InputFrame); hasAim = false; return; }
            Frame = Source.ConsumeFrame();
            if (Source.Context == InputContext.Suspended || Source.Context == InputContext.TextEntry)
            { hasAim = false; return; }
            if (Frame.HasPointer) { aimX = Frame.Pointer.X; aimY = Frame.Pointer.Y; hasAim = true; }
            if (Frame.Aim.LengthSquared() > 0)
            {
                float w = Math.Max(1, Mouse.INTERNAL_BackBufferWidth);
                float h = Math.Max(1, Mouse.INTERNAL_BackBufferHeight);
                float distance = Math.Min(w, h) * AimRadius;
                aimX = Math.Clamp(AimOriginX + Frame.Aim.X * distance / w, 0, 1);
                aimY = Math.Clamp(AimOriginY + Frame.Aim.Y * distance / h, 0, 1);
                hasAim = true;
            }
            if ((Frame.Pressed & MobileAction.HotbarPrevious) != 0) wheel += 120;
            if ((Frame.Pressed & MobileAction.HotbarNext) != 0) wheel -= 120;
        }

        internal static KeyboardState MergeKeyboard(KeyboardState physical)
        {
            if (Source == null) return physical;
            for (int i = 0; i < bindings.Length; i++)
                if (Frame.IsHeld(bindings[i].Action)) physical.AddPressedKey((int)bindings[i].Key);
            return physical;
        }

        internal static MouseState MergeMouse(MouseState physical)
        {
            if (Source == null) return physical;
            if (hasAim)
            {
                physical.X = (int)(aimX * Math.Max(0, Mouse.INTERNAL_BackBufferWidth - 1));
                physical.Y = (int)(aimY * Math.Max(0, Mouse.INTERNAL_BackBufferHeight - 1));
            }
            if (Frame.PointerDown || Frame.IsHeld(MobileAction.Attack)) physical.LeftButton = ButtonState.Pressed;
            if (Frame.IsHeld(MobileAction.Interact)) physical.RightButton = ButtonState.Pressed;
            physical.ScrollWheelValue += wheel;
            return physical;
        }
    }
}
