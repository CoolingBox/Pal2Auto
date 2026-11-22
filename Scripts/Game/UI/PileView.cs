// 文件：PileView.cs
// 描述：牌堆控件 —— 一叠牌的示意：数量 + 名称，弃牌堆再露出最上面那张的图标。
//       鼠标悬停给出这一堆里到底有什么（抽牌堆不露牌面，只列名字；牌序由内核决定，界面不剧透）。

using System.Collections.Generic;
using Godot;

public partial class PileView : Control
{
    private Panel _shadow;
    private Panel _frame;
    private TextureRect _icon;
    private Label _count;
    private Label _title;
    private bool _built;

    /// <summary>上次刷新的内容指纹：数量 + 顶牌，避免每帧重建富文本与提示串。</summary>
    private string _lastKey = "";

    public override void _Ready()
    {
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        MouseFilter = MouseFilterEnum.Stop;

        // 底下一层深色卡影，让"一叠"这件事有厚度，而不是一块单薄的面板
        _shadow = new Panel();
        _shadow.Position = new Vector2(7f, 2f);
        _shadow.Size = new Vector2(Size.X - 9f, Size.Y - 9f);
        _shadow.MouseFilter = MouseFilterEnum.Ignore;
        _shadow.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.055f, 0.050f, 0.043f), new Color(0.280f, 0.250f, 0.200f), 1, 4));
        AddChild(_shadow);

        _frame = new Panel();
        _frame.Position = Vector2.Zero;
        _frame.Size = new Vector2(Size.X - 9f, Size.Y - 9f);
        _frame.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_frame);

        // 先入树再设尺寸：TextureRect 进树时会把 Size 顶成贴图原始尺寸
        _icon = new TextureRect();
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_icon);
        _icon.Position = new Vector2(18f, 5f);
        _icon.Size = new Vector2(32f, 28f);

        _count = UiKit.MakeLabel("0", 22, UiKit.Paper);
        UiKit.Place(_count, 0f, 8f, Size.X - 9f, 34f, HorizontalAlignment.Center);
        AddChild(_count);

        _title = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_title, 0f, 42f, Size.X - 9f, 20f, HorizontalAlignment.Center);
        AddChild(_title);

        SetPile("", 0, new List<CardConfig>(), false, UiKit.Gold);
    }

    /// <summary>刷新一堆牌。showTopIcon = 弃牌堆，露出最后进去那张。</summary>
    public void SetPile(string title, int count, List<CardConfig> cards, bool showTopIcon, Color accent)
    {
        EnsureBuilt();

        CardConfig top = null;
        if (cards != null && cards.Count > 0)
        {
            top = cards[cards.Count - 1];
        }

        string key = title + "|" + count + "|" + (top != null ? top.Id : 0) + "|" + (showTopIcon ? "1" : "0");
        if (key == _lastKey)
        {
            return;
        }
        _lastKey = key;

        _count.Text = count + "";

        _title.Text = title;
        _count.AddThemeColorOverride("font_color", count > 0 ? UiKit.Paper : new Color(0.420f, 0.390f, 0.330f));

        if (showTopIcon && top != null)
        {
            // 弃牌堆露出最后进去那张，但压到 32% 不透明度当底纹：
            // 满不透明会跟数量数字叠成一坨，反而看不清。
            _icon.Visible = true;
            _icon.Texture = UiKit.LoadTexture("res://Resources/Image/Items/" + top.Icon + ".png");
            _icon.Modulate = new Color(1f, 1f, 1f, 0.22f);
        }
        else
        {
            // 抽牌堆是背面：不露牌，只留数量
            _icon.Visible = false;
        }

        _frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.085f, 0.077f, 0.065f), accent, 1, 4));

        TooltipText = BuildTooltip(title, count, cards);
    }

    private static string BuildTooltip(string title, int count, List<CardConfig> cards)
    {
        if (count <= 0 || cards == null)
        {
            return title + "：空";
        }

        string text = title + "：" + count + " 张";
        for (int i = 0; i < cards.Count; i++)
        {
            text += "\n· " + cards[i].Name;
        }
        return text;
    }
}
