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
        _icon.Position = new Vector2(14f, 6f);
        _icon.Size = new Vector2(Size.X - 28f, 76f);

        _name = UiKit.MakeLabel("", 14, UiKit.Paper);
        UiKit.Place(_name, 6f, 86f, Size.X - 12f, 20f, HorizontalAlignment.Center);
        AddChild(_name);

        _effects = new RichTextLabel();
        _effects.BbcodeEnabled = true;
        _effects.Position = new Vector2(6f, 108f);
        _effects.Size = new Vector2(Size.X - 12f, 26f);
        _effects.FitContent = true;
        _effects.ScrollActive = false;
        _effects.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _effects.MouseFilter = MouseFilterEnum.Ignore;
        _effects.AddThemeFontOverride("normal_font", UiKit.GameFont);
        _effects.AddThemeFontSizeOverride("normal_font_size", 12);
        _effects.AddThemeConstantOverride("line_separation", 0);
        AddChild(_effects);

        _countdown = UiKit.MakeLabel("", 26, UiKit.Gold);
        UiKit.Place(_countdown, 6f, 132f, Size.X - 12f, 32f, HorizontalAlignment.Center);
        AddChild(_countdown);

        _barBg = new ColorRect();
        _barBg.Color = new Color(0.060f, 0.055f, 0.048f);
        _barBg.Position = new Vector2(14f, Size.Y - 22f);
        _barBg.Size = new Vector2(Size.X - 28f, 8f);
        _barBg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_barBg);

        _barFill = new ColorRect();
        _barFill.Color = new Color(0.620f, 0.470f, 0.240f);
        _barFill.Position = new Vector2(14f, Size.Y - 22f);
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
        _effects.Text = BuildEffectText(card);
        _countdown.Text = card.Countdown.ToString("0.0");

        Color primary = PrimaryColor(card);
        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(Tint(new Color(0.115f, 0.100f, 0.082f), primary, 0.16f), primary, 1, 6));
        _barFill.Color = primary;

        TooltipText = card.Name + "（蓄势 " + card.Countdown.ToString("0.0") + " 秒）\n" +
                      EffectSummary(card) + "\n" + card.Desc;
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

    // —————————————————————————— 颜色编码 ——————————————————————————

    /// <summary>彩色效果标签：一条卡面就写清"打多少、加多少、上几层毒"。</summary>
    private static string BuildEffectText(CardConfig card)
    {
        string text = "";

        if (card.Damage > 0)
        {
            text += Tag(UiKit.DamageRed, "攻 " + card.Damage);
        }
        if (card.Fire > 0)
        {
            text += Tag(UiKit.FireOrange, "火 " + card.Fire);
        }
        if (card.Shield > 0)
        {
            text += Tag(UiKit.ShieldGold, "守 " + card.Shield);
        }
        if (card.Heal > 0)
        {
            text += Tag(UiKit.HealGreen, "疗 " + card.Heal);
        }
        if (card.Poison > 0)
        {
            text += Tag(UiKit.PoisonGreen, "毒 " + card.Poison);
        }

        return text.TrimEnd();
    }

    private static string Tag(Color color, string content)
    {
        return "[color=#" + color.ToHtml(false) + "]" + content + "[/color]  ";
    }

    private static string EffectSummary(CardConfig card)
    {
        string summary = "";
        if (card.Damage > 0)
        {
            summary += "攻 " + card.Damage + "　";
        }
        if (card.Fire > 0)
        {
            summary += "火 " + card.Fire + "　";
        }
        if (card.Shield > 0)
        {
            summary += "守 " + card.Shield + "　";
        }
        if (card.Heal > 0)
        {
            summary += "疗 " + card.Heal + "　";
        }
        if (card.Poison > 0)
        {
            summary += "毒 " + card.Poison + "　";
        }
        if (card.IgnoreShield)
        {
            summary += "无视护体　";
        }

        return summary.TrimEnd();
    }

    /// <summary>主色：按卡牌定位（Type）取色，火焰牌一律橙黄。
    /// 不用"最大数值"判定——蛇涎蛊挂了 3 点伤害，但它的身份是毒，边框该是绿。</summary>
    private static Color PrimaryColor(CardConfig card)
    {
        if (card.Fire > 0)
        {
            return UiKit.FireOrange;
        }

        if (card.Type == CardType.Guard)
        {
            return UiKit.ShieldGold;
        }
        if (card.Type == CardType.Heal)
        {
            return UiKit.HealGreen;
        }
        if (card.Type == CardType.Poison)
        {
            return UiKit.PoisonGreen;
        }

        return UiKit.DamageRed;
    }

    private static Color Tint(Color baseColor, Color accent, float amount)
    {
        return new Color(
            baseColor.R + (accent.R - baseColor.R) * amount,
            baseColor.G + (accent.G - baseColor.G) * amount,
            baseColor.B + (accent.B - baseColor.B) * amount,
            baseColor.A);
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
