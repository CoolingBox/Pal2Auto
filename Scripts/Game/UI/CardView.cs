// 文件：CardView.cs
// 描述：手牌控件 —— 图标 + 名字 + 彩色效果标签 + 蓄势倒计时。
//
// 颜色编码（卡面标签、边框、飘字共用一套）：
//   攻 物理伤害 → 红　火 火焰 → 橙黄　守 护体 → 黄　疗 回复 → 翠绿　毒 中毒 → 深绿
// 一张牌可以带多种效果（复合），标签逐个上色，边框取"主效果"色。

using Godot;

public partial class CardView : Control
{
    public int SlotIndex;

    private Panel _frame;
    private TextureRect _icon;
    private Label _name;
    private RichTextLabel _effects;
    private Label _countdown;
    private Label _cdCaption;
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

        _icon = new TextureRect();
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_icon);
        _icon.Position = new Vector2(14f, 2f);
        _icon.Size = new Vector2(Size.X - 28f, 64f);

        _name = UiKit.MakeLabel("", 16, UiKit.Paper);
        UiKit.Place(_name, 6f, 64f, Size.X - 12f, 20f, HorizontalAlignment.Center);
        AddChild(_name);

        _effects = new RichTextLabel();
        _effects.BbcodeEnabled = true;
        _effects.Position = new Vector2(6f, 84f);
        _effects.Size = new Vector2(Size.X - 12f, 38f);
        _effects.FitContent = false;
        _effects.ScrollActive = false;
        _effects.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _effects.MouseFilter = MouseFilterEnum.Ignore;
        _effects.AddThemeFontOverride("normal_font", UiKit.GameFont);
        _effects.AddThemeFontSizeOverride("normal_font_size", 18);
        _effects.AddThemeConstantOverride("line_separation", 0);
        AddChild(_effects);

        // 倒计时上面挂一行小字：光一个 "1.1" 不说明它是多久
        _cdCaption = UiKit.MakeLabel("蓄 势", 11, new Color(0.560f, 0.510f, 0.420f));
        UiKit.Place(_cdCaption, 6f, 122f, Size.X - 12f, 14f, HorizontalAlignment.Center);
        AddChild(_cdCaption);

        _countdown = UiKit.MakeLabel("", 30, UiKit.Gold);
        UiKit.Place(_countdown, 6f, 136f, Size.X - 12f, 40f, HorizontalAlignment.Center);
        AddChild(_countdown);

        _barBg = new ColorRect();
        _barBg.Color = new Color(0.060f, 0.055f, 0.048f);
        _barBg.Position = new Vector2(14f, Size.Y - 16f);
        _barBg.Size = new Vector2(Size.X - 28f, 8f);
        _barBg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_barBg);

        _barFill = new ColorRect();
        _barFill.Color = new Color(0.620f, 0.470f, 0.240f);
        _barFill.Position = new Vector2(14f, Size.Y - 16f);
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
        _icon.Visible = true;
        _name.Text = card.Name;
        _effects.Text = "[center]" + CardColors.BuildEffectText(card) + "[/center]";
        _cdCaption.Visible = true;
        _countdown.Text = card.Countdown.ToString("0.0");

        Color primary = CardColors.PrimaryColor(card);
        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(CardColors.Tint(new Color(0.115f, 0.100f, 0.082f), primary, 0.16f), primary, 1, 6));
        _barFill.Color = primary;

        TooltipText = card.Name + "（蓄势 " + card.Countdown.ToString("0.0") + " 秒）\n" +
                      CardColors.EffectSummary(card) + "\n" + card.Desc;
    }

    public void SetEmpty()
    {
        Build();
        _empty = true;

        _icon.Texture = null;
        _icon.Visible = false;
        _name.Text = "";
        _effects.Text = "";
        _countdown.Text = "";
        _cdCaption.Visible = false;
        _barFill.Size = new Vector2(0f, 8f);
        _barFill.Color = new Color(0.620f, 0.470f, 0.240f);
        TooltipText = "";

        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.075f, 0.068f, 0.058f), new Color(0.240f, 0.210f, 0.170f), 1, 6));
    }

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
            _countdown.AddThemeColorOverride("font_color", new Color(1f, 0.94f, 0.78f));
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
