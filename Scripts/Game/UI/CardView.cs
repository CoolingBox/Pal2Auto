// 文件：CardView.cs
// 描述：手牌控件（敌我通用一套外观）—— 图标 + 名字 + 彩色效果标签 + 蓄势倒计时 + 进度条。
//
// 版面按比例算（Layout），所以上场数从 5 变到 8 时牌会整体缩小而不会散架；
// 敌我用同一个类、同一套尺寸规则：数量相同时，两边卡牌大小完全一致。
//
// 颜色编码（卡面标签、边框、飘字共用一套）：
//   场 上场数 → 石青　攻 物理 → 红　术 法术 → 元素色　守 护体 → 黄　疗 回复 → 翠绿　毒 中毒 → 深绿

using Godot;

public partial class CardView : Control
{
    public int SlotIndex;

    private Panel _frame;
    private TextureRect _icon;
    private Label _name;
    private RichTextLabel _effects;
    private RichTextLabel _keywords;
    private Label _cdCaption;
    private Label _countdown;
    private ColorRect _barBg;
    private ColorRect _barFill;
    private ColorRect _flash;
    private bool _built;
    private bool _empty = true;

    // 进度条的当前几何：SetCountdown 每帧都要用，存下来免得重复算
    private float _barX;
    private float _barY;
    private float _barW;
    private float _barH;

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
        _frame.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_frame);

        _icon = new TextureRect();
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_icon);

        _name = UiKit.MakeLabel("", 15, UiKit.Paper);
        AddChild(_name);

        _effects = new RichTextLabel();
        _effects.BbcodeEnabled = true;
        _effects.FitContent = false;
        _effects.ScrollActive = false;
        _effects.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _effects.MouseFilter = MouseFilterEnum.Ignore;
        _effects.AddThemeFontOverride("normal_font", UiKit.GameFont);
        _effects.AddThemeFontSizeOverride("normal_font_size", 18);
        _effects.AddThemeConstantOverride("line_separation", 0);
        AddChild(_effects);

        // 关键字角标压在图标左上角：卡面已经排满，唯一能挂徽标又不挤走数值的位置
        _keywords = new RichTextLabel();
        _keywords.BbcodeEnabled = true;
        _keywords.FitContent = false;
        _keywords.ScrollActive = false;
        _keywords.MouseFilter = MouseFilterEnum.Ignore;
        _keywords.AddThemeFontOverride("normal_font", UiKit.GameFont);
        _keywords.AddThemeFontSizeOverride("normal_font_size", 10);
        _keywords.AddThemeConstantOverride("line_separation", 0);
        AddChild(_keywords);

        _cdCaption = UiKit.MakeLabel("蓄 势", 10, new Color(0.560f, 0.510f, 0.420f));
        AddChild(_cdCaption);

        _countdown = UiKit.MakeLabel("", 26, UiKit.Gold);
        AddChild(_countdown);

        _barBg = new ColorRect();
        _barBg.Color = new Color(0.060f, 0.055f, 0.048f);
        _barBg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_barBg);

        _barFill = new ColorRect();
        _barFill.Color = new Color(0.620f, 0.470f, 0.240f);
        _barFill.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_barFill);

        _flash = new ColorRect();
        _flash.Color = new Color(1f, 0.92f, 0.70f, 0.30f);
        _flash.MouseFilter = MouseFilterEnum.Ignore;
        _flash.Visible = false;
        AddChild(_flash);

        Layout();
        SetEmpty();
    }

    /// <summary>改尺寸并重排。上场数变化时由面板调用。</summary>
    public void FitTo(float width, float height)
    {
        Build();
        Size = new Vector2(width, height);
        Layout();
    }

    /// <summary>全部子件按 0~1 的比例摆，字号跟着宽度缩。</summary>
    private void Layout()
    {
        float w = Size.X;
        float h = Size.Y;
        if (w <= 1f || h <= 1f)
        {
            return;
        }

        // 字号缩放：132 宽为基准；缩到 0.62 以下就不再缩，否则汉字糊成墨点
        float f = Mathf.Clamp(w / 132f, 0.62f, 1f);

        _frame.Position = Vector2.Zero;
        _frame.Size = Size;

        _icon.Position = new Vector2(w * 0.09f, h * 0.012f);
        _icon.Size = new Vector2(w * 0.82f, h * 0.288f);

        _keywords.Position = new Vector2(w * 0.055f, h * 0.022f);
        _keywords.Size = new Vector2(w * 0.62f, h * 0.10f);
        _keywords.AddThemeFontSizeOverride("normal_font_size", Mathf.Max(9, Mathf.RoundToInt(10f * f)));

        _name.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(15f * f) + 1);
        UiKit.Place(_name, w * 0.03f, h * 0.30f, w * 0.94f, h * 0.115f, HorizontalAlignment.Center);

        _effects.Position = new Vector2(w * 0.03f, h * 0.415f);
        _effects.Size = new Vector2(w * 0.94f, h * 0.205f);
        _effects.AddThemeFontSizeOverride("normal_font_size", Mathf.RoundToInt(18f * f) + 1);

        _cdCaption.AddThemeFontSizeOverride("font_size", Mathf.Max(9, Mathf.RoundToInt(10f * f)));
        UiKit.Place(_cdCaption, w * 0.03f, h * 0.622f, w * 0.94f, h * 0.072f, HorizontalAlignment.Center);

        _countdown.AddThemeFontSizeOverride("font_size", Mathf.Max(16, Mathf.RoundToInt(26f * f)));
        UiKit.Place(_countdown, w * 0.03f, h * 0.694f, w * 0.94f, h * 0.235f, HorizontalAlignment.Center);

        _barH = Mathf.Max(4f, h * 0.045f);
        _barY = h - _barH - h * 0.014f;
        _barX = w * 0.09f;
        _barW = w * 0.82f;

        _barBg.Position = new Vector2(_barX, _barY);
        _barBg.Size = new Vector2(_barW, _barH);
        _barFill.Position = new Vector2(_barX, _barY);
        _barFill.Size = new Vector2(0f, _barH);

        _flash.Position = Vector2.Zero;
        _flash.Size = Size;
    }

    public void SetCard(CardConfig card)
    {
        Build();
        _empty = false;

        _icon.Texture = UiKit.LoadTexture("res://Resources/Image/Items/" + card.Icon + ".png");
        _icon.Visible = true;
        _name.Text = card.Name;
        _effects.Text = "[center]" + CardColors.BuildEffectText(card) + "[/center]";
        ApplyKeywordBadges(card);
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
        _cdCaption.Visible = false;
        _keywords.Text = "";
        _keywords.Visible = false;
        _countdown.Text = "";
        _barFill.Size = new Vector2(0f, _barH);
        _barFill.Color = new Color(0.620f, 0.470f, 0.240f);
        TooltipText = "";

        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.075f, 0.068f, 0.058f), new Color(0.240f, 0.210f, 0.170f), 1, 6));
    }

    /// <summary>关键字角标：耗 / 移 / 穿，单字 + 底色，缩到最小卡也读得出。</summary>
    private void ApplyKeywordBadges(CardConfig card)
    {
        string text = "";
        if (card.HasKeyword(CardKeyword.Exhaust))
        {
            text += Badge("#22303a", "#8fb6c9", "耗");
        }
        if (card.HasKeyword(CardKeyword.Remove))
        {
            text += Badge("#3d2020", "#df9d8b", "移");
        }
        if (card.HasKeyword(CardKeyword.Pierce))
        {
            text += Badge("#33301a", "#ddd08a", "穿");
        }

        _keywords.Text = text;
        _keywords.Visible = text.Length > 0;
    }

    private static string Badge(string background, string foreground, string word)
    {
        return "[bgcolor=" + background + "][color=" + foreground + "] " + word + " [/color][/bgcolor] ";
    }

    public void SetCountdown(float progress, float remain)
    {
        if (_empty)
        {
            return;
        }

        _countdown.Text = remain.ToString("0.0");
        _barFill.Size = new Vector2(Mathf.Round(_barW * Mathf.Clamp(progress, 0f, 1f)), _barH);

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
