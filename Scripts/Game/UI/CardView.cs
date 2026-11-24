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

    private ColorRect _board;
    private ShaderMaterial _boardMat;
    private TextureRect _icon;
    private Label _name;
    private RichTextLabel _effects;
    private RichTextLabel _keywords;
    private Label _cdCaption;
    private Label _countdown;
    private ColorRect _frost;
    private ColorRect _flash;
    private bool _built;

    // 时机状态：决定倒计时数字的颜色与冻霜覆盖
    private bool _frozen;
    private bool _hasted;
    private bool _slowed;
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

        // 牌面全部交给 card_frame.gdshader：斜面、内框线、底部凹槽和进度条都在里面画，
        // 省掉一堆 ColorRect，也不用为了"看起来有做工"再叠三层 Panel。
        _board = new ColorRect();
        _board.MouseFilter = MouseFilterEnum.Ignore;
        _board.Color = new Color(1f, 1f, 1f, 1f);
        _boardMat = new ShaderMaterial();
        _boardMat.Shader = ResourceLoader.Load<Shader>("res://Resources/Shaders/card_frame.gdshader");
        _board.Material = _boardMat;
        AddChild(_board);

        _icon = new TextureRect();
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        // 图标裁成圆牌：素材是方形画布里的圆形玉牌，不裁就是一块米色硬边方块
        ShaderMaterial iconMat = new ShaderMaterial();
        iconMat.Shader = ResourceLoader.Load<Shader>("res://Resources/Shaders/icon_token.gdshader");
        _icon.Material = iconMat;
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

        // 冻霜：冰冻期间盖一层薄冰蓝，光看数字颜色不够直观
        _frost = new ColorRect();
        _frost.Color = new Color(0.560f, 0.780f, 0.950f, 0.20f);
        _frost.MouseFilter = MouseFilterEnum.Ignore;
        _frost.Visible = false;
        AddChild(_frost);

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

        _board.Position = Vector2.Zero;
        _board.Size = Size;

        _icon.Position = new Vector2(w * 0.30f, h * 0.030f);
        _icon.Size = new Vector2(w * 0.40f, h * 0.275f);

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

        // 凹槽与进度条由着色器画，这里只把几何喂进去
        _boardMat.SetShaderParameter("rect", new Vector2(w, h));
        // 圆角只留 2px：老界面基本是直角，圆角一大就"现代"了
        _boardMat.SetShaderParameter("corner", 2f);
        _boardMat.SetShaderParameter("bar_h", _barH);
        _boardMat.SetShaderParameter("bar_inset", _barX);
        _boardMat.SetShaderParameter("bar_bottom", h - _barY - _barH);

        _frost.Position = Vector2.Zero;
        _frost.Size = Size;

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
        _boardMat.SetShaderParameter("slot_empty", 0f);
        _boardMat.SetShaderParameter("accent", primary);
        _boardMat.SetShaderParameter("bar_color", primary);
        _boardMat.SetShaderParameter("bar", 0f);

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
        _frozen = false;
        _hasted = false;
        _slowed = false;
        _frost.Visible = false;
        // 打出瞬间会闪一下再变空槽；不关掉的话，空槽会顶着一层没退干净的暖光，看着比牌还亮
        _flash.Visible = false;
        _flash.Modulate = new Color(1f, 1f, 1f, 1f);
        _keywords.Text = "";
        _keywords.Visible = false;
        _countdown.Text = "";
        _boardMat.SetShaderParameter("slot_empty", 1f);
        _boardMat.SetShaderParameter("bar", 0f);
        _boardMat.SetShaderParameter("accent", new Color(0.300f, 0.280f, 0.240f));
        _boardMat.SetShaderParameter("bar_color", new Color(0.300f, 0.280f, 0.240f));
        TooltipText = "";
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

    /// <summary>时机状态：冰冻 / 加速 / 减速。数字颜色和冻霜跟着变，不看文字也知道它现在的状态。</summary>
    public void SetTiming(float haste, float slow, float freeze)
    {
        if (!_built)
        {
            return;
        }

        _hasted = haste > 0f;
        _slowed = slow > 0f;
        _frozen = freeze > 0f;
        _frost.Visible = _frozen && !_empty;
    }

    public void SetCountdown(float progress, float remain)
    {
        if (_empty)
        {
            return;
        }

        _countdown.Text = remain.ToString("0.0");
        _boardMat.SetShaderParameter("bar", Mathf.Clamp(progress, 0f, 1f));

        Color color = UiKit.Gold;
        if (_frozen)
        {
            color = UiKit.FreezeIce;
        }
        else if (remain <= 0.6f)
        {
            // 快出手时数字转亮，制造"要打了"的预期
            color = new Color(0.945f, 0.870f, 0.680f);
        }
        else if (_hasted)
        {
            color = UiKit.HasteGreen;
        }
        else if (_slowed)
        {
            color = UiKit.SlowBlue;
        }

        _countdown.AddThemeColorOverride("font_color", color);
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
