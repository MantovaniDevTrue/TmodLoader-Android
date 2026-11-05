using System.Numerics;

namespace Tml.Mobile;

public static class ControlLayout
{
    public static TouchControl[] CreateDefault(Viewport safeArea, float scale = 1)
    {
        if (!float.IsFinite(scale) || scale < 0.5f || scale > 1.5f)
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (!float.IsFinite(safeArea.Width) || !float.IsFinite(safeArea.Height) ||
            safeArea.Width <= 0 || safeArea.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(safeArea));

        // A short-axis unit keeps circles round on both phones and tablets.
        float u = MathF.Min(safeArea.Width / 18f, safeArea.Height / 9f) * scale;
        float l = safeArea.X, r = l + safeArea.Width;
        float t = safeArea.Y, b = t + safeArea.Height;
        TouchControl Button(string id, string label, MobileAction action, float x, float y) =>
            new(id, label, ControlKind.Button, action, new(x, y), u * .43f);

        return [
            new("move", "Mover", ControlKind.MovementStick, MobileAction.None, new(l + 2.2f*u, b - 2.1f*u), 1.25f*u),
            new("aim", "Mira", ControlKind.AimStick, MobileAction.None, new(r - 3.8f*u, b - 2.1f*u), 1.05f*u),
            Button("jump", "Pular", MobileAction.Jump, r-u, b-1.1f*u),
            Button("attack", "Usar", MobileAction.Attack, r-u, b-2.4f*u),
            Button("interact", "Interagir", MobileAction.Interact, r-u, b-3.7f*u),
            Button("grapple", "Gancho", MobileAction.Grapple, r-2.3f*u, b-4.4f*u),
            Button("inventory", "Bolsa", MobileAction.Inventory, l+u, t+u),
            Button("escape", "Menu", MobileAction.Escape, r-u, t+u),
            Button("heal", "Vida", MobileAction.Heal, l+u, b-4.5f*u),
            Button("mana", "Mana", MobileAction.Mana, l+2.2f*u, b-4.5f*u),
            Button("buff", "Buffs", MobileAction.Buff, l+3.4f*u, b-4.5f*u),
            Button("mount", "Montar", MobileAction.Mount, r-5.1f*u, b-4.4f*u),
            Button("smart", "Cursor", MobileAction.SmartCursor, r-3.7f*u, b-4.4f*u),
            Button("map", "Mapa", MobileAction.Map, l+2.3f*u, t+u),
            Button("previous", "Item -", MobileAction.HotbarPrevious, l+4.5f*u, b-u),
            Button("next", "Item +", MobileAction.HotbarNext, l+5.7f*u, b-u)
        ];
    }
}
