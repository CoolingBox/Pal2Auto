// 文件：CardView.cs
// 描述：手牌控件 —— 图标 + 名字 + 蓄势倒计时 + 进度条。
//       倒计时归零时闪一下，让"这张牌刚刚出手了"这件事被看见。

using System;
using Godot;

public partial class CardView : Control
{
    public int SlotIndex;

    private Panel _frame;
    private TextureRect _icon;
    private Label _name;
    private Label _countdown;
    private ColorRect _barBg;
    private ColorRect _barFill;
    private ColorRect _flash;
    private bool _built;
    private bool _empty = true;

    public override void _Ready()
    {
        Build();
    }

    private void Build()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        MouseFilter = MouseFilterEnum.Stop;

        _frame = new Panel();
        _frame.Position = Vector2.Zero;
        _frame.Size = Size;
        _frame.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_frame);
        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.115f, 0.100f, 0.082f), new Color(0.400f, 0.340f, 0.240f), 1, 6));

        _icon = new TextureRect();
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_icon);
        _icon.Position = new Vector2(14f, 8f);
        _icon.Size = new Vector2(Size.X - 28f, 82f);

        _name = UiKit.MakeLabel("", 15, UiKit.Paper);
        UiKit.Place(_name, 6f, 94f, Size.X - 12f, 22f, HorizontalAlignment.Center);
        AddChild(_name);

        _countdown = UiKit.MakeLabel("", 26, UiKit.Gold);
        UiKit.Place(_countdown, 6f, 118f, Size.X - 12f, 34f, HorizontalAlignment.Center);
        AddChild(_countdown);

        _barBg = new ColorRect();
        _barBg.Color = new Color(0.060f, 0.055f, 0.048f);
        _barBg.Position = new Vector2(14f, Size.Y - 30f);
        _barBg.Size = new Vector2(Size.X - 28f, 8f);
        _barBg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_barBg);

        _barFill = new ColorRect();
        _barFill.Color = new Color(0.620f, 0.470f, 0.240f);
        _barFill.Position = new Vector2(14f, Size.Y - 30f);
        _barFill.Size = new Vector2(0f, 8f);
        _barFill.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_barFill);

        _flash = new ColorRect();
        _flash.Color = new Color(1f, 0.92f, 0.70f, 0.30f);
        _flash.Size = Size;
        _flash.MouseFilter = MouseFilterEnum.Ignore;
        _flash.Visible = false;
        AddChild(_flash);

        SetEmpty();
    }

    public void SetCard(CardConfig card)
    {
        Build();
        _empty = false;

        _icon.Texture = UiKit.LoadTexture("res://Resources/Image/Items/" + card.Icon + ".png");
        _name.Text = card.Name;
        _countdown.Text = card.Countdown.ToString("0.0");
        TooltipText = card.Name + "（蓄势 " + card.Countdown.ToString("0.0") + " 秒）\n" + card.Desc;

        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.115f, 0.100f, 0.082f), new Color(0.400f, 0.340f, 0.240f), 1, 6));
        _icon.Visible = true;
    }

    public void SetEmpty()
    {
        Build();
        _empty = true;

        _icon.Texture = null;
        _icon.Visible = false;
        _name.Text = "";
        _countdown.Text = "";
        _barFill.Size = new Vector2(0f, 8f);
        TooltipText = "";

        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.075f, 0.068f, 0.058f), new Color(0.240f, 0.210f, 0.170f), 1, 6));
    }

    /// <summary>倒计时刷新：progress 0→1 表示蓄势进度。</summary>
    public void SetCountdown(float progress, float remain)
    {
        if (_empty)
        {
            return;
        }

        _countdown.Text = remain.ToString("0.0");
        float width = Size.X - 28f;
        _barFill.Size = new Vector2(Mathf.Round(width * Mathf.Clamp(progress, 0f, 1f)), 8f);

        // 快出手时数字转亮，制造"要打了"的预期
        if (remain <= 0.6f)
        {
            _countdown.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.72f));
        }
        else
        {
            _countdown.AddThemeColorOverride("font_color", UiKit.Gold);
        }
    }

    public void PlayFlash()
    {
        if (!_built)
        {
            return;
        }

        _flash.Visible = true;
        Tween tween = CreateTween();
        tween.TweenProperty(_flash, "modulate:a", 0.0f, 0.20f);
        tween.TweenCallback(Callable.From(delegate
        {
            _flash.Visible = false;
            _flash.Modulate = new Color(1f, 1f, 1f, 1f);
        }));
    }

    public override void _GuiInput(InputEvent @event)
    {
        InputEventMouseButton mouse = @event as InputEventMouseButton;
        if (mouse == null || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        AcceptEvent();
    }
}
