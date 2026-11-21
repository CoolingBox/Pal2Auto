// 文件：MiniCardView.cs
// 描述：敌方手牌（小卡）—— 图标 + 名字 + 倒计时 + 效果色点。
//       敌方也是牌组，界面要能看出"它手上有什么、哪张先出手"。

using Godot;

public partial class MiniCardView : Control
{
    public int SlotIndex;

    private Panel _frame;
    private TextureRect _icon;
    private Label _name;
    private Label _countdown;
    private ColorRect _barBg;
    private ColorRect _barFill;
    private readonly ColorRect[] _dots = new ColorRect[5];
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
        _icon.Position = new Vector2(10f, 4f);
        _icon.Size = new Vector2(Size.X - 20f, 44f);

        _name = UiKit.MakeLabel("", 11, UiKit.Paper);
        UiKit.Place(_name, 2f, 49f, Size.X - 4f, 16f, HorizontalAlignment.Center);
        AddChild(_name);

        _countdown = UiKit.MakeLabel("", 16, UiKit.Gold);
        UiKit.Place(_countdown, 2f, 64f, Size.X - 4f, 20f, HorizontalAlignment.Center);
        AddChild(_countdown);

        for (int i = 0; i < _dots.Length; i++)
        {
            ColorRect dot = new ColorRect();
            dot.Size = new Vector2(7f, 7f);
            dot.Position = new Vector2(9f + i * 9f, 86f);
            dot.Visible = false;
            dot.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(dot);
            _dots[i] = dot;
        }

        _barBg = new ColorRect();
        _barBg.Color = new Color(0.060f, 0.055f, 0.048f);
        _barBg.Position = new Vector2(8f, Size.Y - 8f);
        _barBg.Size = new Vector2(Size.X - 16f, 4f);
        _barBg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_barBg);

        _barFill = new ColorRect();
        _barFill.Color = new Color(0.620f, 0.470f, 0.240f);
        _barFill.Position = new Vector2(8f, Size.Y - 8f);
        _barFill.Size = new Vector2(0f, 4f);
        _barFill.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_barFill);

        SetEmpty();
    }

    public void SetCard(CardConfig card)
    {
        Build();
        _empty = false;

        _icon.Texture = UiKit.LoadTexture("res://Resources/Image/Items/" + card.Icon + ".png");
        _icon.Visible = true;
        _name.Text = card.Name;
        _countdown.Text = card.Countdown.ToString("0.0");

        Color primary = CardColors.PrimaryColor(card);
        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.105f, 0.092f, 0.078f), primary, 1, 5));
        _barFill.Color = primary;

        Color[] effectColors = CardColors.EffectColors(card);
        for (int i = 0; i < _dots.Length; i++)
        {
            if (i < effectColors.Length)
            {
                _dots[i].Color = effectColors[i];
                _dots[i].Visible = true;
            }
            else
            {
                _dots[i].Visible = false;
            }
        }

        TooltipText = card.Name + "（蓄势 " + card.Countdown.ToString("0.0") + " 秒）\n" + card.Desc;
    }

    public void SetEmpty()
    {
        Build();
        _empty = true;

        _icon.Texture = null;
        _icon.Visible = false;
        _name.Text = "";
        _countdown.Text = "";
        _barFill.Size = new Vector2(0f, 4f);
        TooltipText = "";

        for (int i = 0; i < _dots.Length; i++)
        {
            _dots[i].Visible = false;
        }

        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.070f, 0.064f, 0.056f), new Color(0.230f, 0.200f, 0.160f), 1, 5));
    }

    public void SetCountdown(float progress, float remain)
    {
        if (_empty)
        {
            return;
        }

        _countdown.Text = remain.ToString("0.0");
        float width = Size.X - 16f;
        _barFill.Size = new Vector2(Mathf.Round(width * Mathf.Clamp(progress, 0f, 1f)), 4f);

        if (remain <= 0.6f)
        {
            _countdown.AddThemeColorOverride("font_color", new Color(1f, 0.94f, 0.78f));
        }
        else
        {
            _countdown.AddThemeColorOverride("font_color", UiKit.Gold);
        }
    }
}
