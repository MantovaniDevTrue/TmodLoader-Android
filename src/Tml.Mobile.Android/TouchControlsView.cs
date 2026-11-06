using Android.Content;
using Android.Graphics;
using Android.Views;
using System.Numerics;

namespace Tml.Mobile.Android;

// The same view can be placed above SDL's game surface. This APK only exercises input/runtime.
public sealed class TouchControlsView : View
{
    private readonly TouchRouter router = new();
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private TouchControl[] layout = [];
    private InputFrame frame;
    private bool running = true;
    private bool firstDraw = true;
    private bool firstTouch = true;
    private string[] reportLines = ["Testando o runtime…"];
    private MobileAction shownActions;
    private string heldLabel = "Ativos: None";
    public string Report { set { reportLines = value.Split('\n'); Invalidate(); } }
    public bool ConsumeFrames { get; set; } = true;
    public bool ShowDiagnostics { get; set; } = true;
    public TouchRouter Router => router;

    public TouchControlsView(Context context) : base(context)
    {
        Focusable = FocusableInTouchMode = true;
        SetBackgroundColor(Color.Rgb(14, 21, 30));
    }

    protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
    {
        base.OnSizeChanged(w, h, oldw, oldh);
        Configure();
    }

    public override WindowInsets? OnApplyWindowInsets(WindowInsets? insets)
    {
        var result = base.OnApplyWindowInsets(insets);
        Configure();
        return result;
    }

    private void Configure()
    {
        if (Width <= 0 || Height <= 0) return;
        float margin = 12 * Resources!.DisplayMetrics!.Density;
        float left = margin, top = margin, right = margin, bottom = margin;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var edges = RootWindowInsets?.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
            left += edges?.Left ?? 0; top += edges?.Top ?? 0;
            right += edges?.Right ?? 0; bottom += edges?.Bottom ?? 0;
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            var cutout = RootWindowInsets?.DisplayCutout;
            left += cutout?.SafeInsetLeft ?? 0; top += cutout?.SafeInsetTop ?? 0;
            right += cutout?.SafeInsetRight ?? 0; bottom += cutout?.SafeInsetBottom ?? 0;
        }
        var safe = new Viewport(left, top, Math.Max(1, Width-left-right), Math.Max(1, Height-top-bottom));
        layout = ControlLayout.CreateDefault(safe);
        // Pointer coordinates follow the complete game surface, not the inset control region.
        router.Configure(new(0, 0, Width, Height), layout);
        Invalidate();
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null || !running) return false;
        if (firstTouch)
        {
            firstTouch = false;
            global::Android.Util.Log.Info("TmlMobile", "MOBILE_TOUCH_RECEIVED");
        }
        switch (e.ActionMasked)
        {
            case MotionEventActions.Cancel: router.CancelAll(); break;
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
                Send(e, e.ActionIndex, TouchPhase.Down); break;
            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
                Send(e, e.ActionIndex, TouchPhase.Up); break;
            case MotionEventActions.Move:
                for (int i = 0; i < e.PointerCount; i++) Send(e, i, TouchPhase.Move);
                break;
        }
        Invalidate();
        return true;
    }

    private void Send(MotionEvent e, int i, TouchPhase phase) =>
        router.Handle(new(e.GetPointerId(i), new(e.GetX(i), e.GetY(i)), phase));

    public void Suspend() { running = false; router.SetContext(InputContext.Suspended); Invalidate(); }
    public void Resume() { running = true; router.SetContext(InputContext.Gameplay); Invalidate(); }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        if (firstDraw)
        {
            firstDraw = false;
            global::Android.Util.Log.Info("TmlMobile", "MOBILE_FIRST_DRAW");
        }
        frame = ConsumeFrames ? router.ConsumeFrame() : router.PeekFrame();
        foreach (var control in layout)
        {
            bool active = control.Kind == ControlKind.Button && frame.IsHeld(control.Action);
            paint.Color = active ? Color.Rgb(72, 171, 137) : Color.Rgb(36, 61, 75);
            paint.SetStyle(Paint.Style.Fill);
            canvas.DrawCircle(control.Center.X, control.Center.Y, control.Radius, paint);
            paint.Color = Color.Rgb(136, 198, 205);
            paint.StrokeWidth = 2;
            paint.SetStyle(Paint.Style.Stroke);
            canvas.DrawCircle(control.Center.X, control.Center.Y, control.Radius, paint);
            paint.SetStyle(Paint.Style.Fill);
            if (control.Kind != ControlKind.Button)
            {
                Vector2 stick = control.Kind == ControlKind.MovementStick ? frame.Movement : frame.Aim;
                canvas.DrawCircle(control.Center.X + stick.X*control.Radius*.6f,
                    control.Center.Y + stick.Y*control.Radius*.6f, control.Radius*.28f, paint);
            }
            else
            {
                paint.TextSize = control.Radius * .42f;
                paint.TextAlign = Paint.Align.Center;
                canvas.DrawText(control.Label, control.Center.X, control.Center.Y + paint.TextSize*.35f, paint);
            }
        }
        if (!ShowDiagnostics)
        {
            if (running) PostInvalidateOnAnimation();
            return;
        }
        paint.TextAlign = Paint.Align.Center;
        paint.Color = Color.White;
        float unit = MathF.Min(Width/18f, Height/9f);
        paint.TextSize = unit*.36f;
        canvas.DrawText("tModLoader Android · Etapa 1", Width*.5f, unit*.7f, paint);
        paint.TextSize = unit*.24f;
        canvas.DrawText("Teste de controles e .NET 10 — ainda não inicia o Terraria", Width*.5f, unit*1.25f, paint);
        float line = unit*1.8f;
        foreach (string text in reportLines)
        {
            canvas.DrawText(text, Width*.5f, line, paint);
            line += unit*.37f;
        }
        paint.TextSize = unit*.25f;
        if (shownActions != frame.Held) { shownActions = frame.Held; heldLabel = $"Ativos: {frame.Held}"; }
        canvas.DrawText(heldLabel, Width*.5f, line+unit*.2f, paint);
        if (frame.HasPointer)
        {
            paint.Color = frame.PointerDown ? Color.Rgb(255, 192, 96) : Color.Rgb(136, 198, 205);
            canvas.DrawCircle(frame.Pointer.X*Width, frame.Pointer.Y*Height, unit*.12f, paint);
        }
        if (running) PostInvalidateOnAnimation();
    }

    protected override void OnDetachedFromWindow()
    {
        Suspend();
        base.OnDetachedFromWindow();
    }
}
