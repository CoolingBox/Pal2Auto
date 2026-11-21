// 文件：CharacterCardView.cs
// 描述：选人卡 —— 立绘 + 名号 + 定位。点一下选中。

using System;
using Godot;

public partial class CharacterCardView : Control
{
    public int CharacterId;
    public Action<int> Clicked;

    private Panel _frame;
    private TextureRect _portrait;
    private Label _name;
    private Label _title;
    private Label _style;
    private bool _selected;
    private bool _built;

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

        // 先入树再设尺寸：TextureRect 进树时会把 Size 顶成贴图原始尺寸
        _portrait = new TextureRect();
        _portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _portrait.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_portrait);
        _portrait.Position = new Vector2(10f, 8f);
        _portrait.Size = new Vector2(Size.X - 20f, 100f);

        _name = UiKit.MakeLabel("", 17, UiKit.Paper);
        UiKit.Place(_name, 8f, 112f, Size.X - 16f, 24f, HorizontalAlignment.Center);
        AddChild(_name);

        _title = UiKit.MakeLabel("", 12, UiKit.Gold);
        UiKit.Place(_title, 8f, 136f, Size.X - 16f, 18f, HorizontalAlignment.Center);
        AddChild(_title);

        _style = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_style, 8f, 156f, Size.X - 16f, 18f, HorizontalAlignment.Center);
        AddChild(_style);

        RefreshFrame();
    }

    public void SetCharacter(CharacterConfig cfg)
    {
        Build();
        CharacterId = cfg.Id;

        _portrait.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + cfg.Portrait + ".png");
        _name.Text = cfg.Name;
        _title.Text = cfg.Title;
        _style.Text = cfg.Style;
        TooltipText = cfg.Name + "（" + cfg.Title + "）\n" + cfg.Desc;
    }

    public void SetSelected(bool selected)
    {
        Build();
        _selected = selected;
        RefreshFrame();
    }

    private void RefreshFrame()
    {
        if (_frame == null)
        {
            return;
        }

        Color border = _selected ? UiKit.Gold : new Color(0.330f, 0.290f, 0.220f);
        Color fill = _selected ? new Color(0.150f, 0.128f, 0.100f) : new Color(0.105f, 0.092f, 0.078f);
        _frame.AddThemeStyleboxOverride("panel", UiKit.Box(fill, border, _selected ? 2 : 1, 6));
    }

    public override void _GuiInput(InputEvent @event)
    {
        InputEventMouseButton mouse = @event as InputEventMouseButton;
        if (mouse == null || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        if (Clicked != null)
        {
            Clicked(CharacterId);
        }
        AcceptEvent();
    }
}
