// 文件：PileView.cs
// 描述：牌堆控件 —— 一叠牌的示意：数量 + 名称，弃牌堆再露出最上面那张的图标。
//       鼠标悬停给出这一堆里到底有什么（抽牌堆不露牌面，只列名字；牌序由内核决定，界面不剧透）。
//
// 版面：三层错位的"纸"叠在一起表示"一叠"，最上面那张带主色边框 + 一圈内细线（与卡面同一套语言）。

using System.Collections.Generic;
using Godot;

public partial class PileView : Control
{
    private Panel _sheetC;
    private Panel _sheetB;
    private Panel _sheetA;
    private Panel _inner;
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

        // 三层错位：越靠后越暗，一眼看出这是"一叠"而不是一块单薄的面板
        _sheetC = MakeSheet(new Vector2(6f, 6f), new Color(0.048f, 0.043f, 0.037f), new Color(0.170f, 0.150f, 0.120f));
        _sheetB = MakeSheet(new Vector2(3f, 3f), new Color(0.062f, 0.056f, 0.048f), new Color(0.220f, 0.195f, 0.150f));

        _sheetA = new Panel();
        _sheetA.Position = Vector2.Zero;
        _sheetA.Size = FaceSize();
        _sheetA.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_sheetA);

        // 内圈细线：和卡面同一条"印刷内框线"，牌堆才不会比牌面寒酸
        _inner = new Panel();
        _inner.Position = new Vector2(3f, 3f);
        _inner.Size = new Vector2(FaceSize().X - 6f, FaceSize().Y - 6f);
        _inner.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_inner);

        // 先入树再设尺寸：TextureRect 进树时会把 Size 顶成贴图原始尺寸
        _icon = new TextureRect();
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        ShaderMaterial iconMat = new ShaderMaterial();
        iconMat.Shader = ResourceLoader.Load<Shader>("res://Resources/Shaders/icon_token.gdshader");
        _icon.Material = iconMat;
        AddChild(_icon);
        _icon.Position = new Vector2(13.5f, 4f);
        _icon.Size = new Vector2(32f, 26f);

        _count = UiKit.MakeLabel("0", 22, UiKit.Paper);
        UiKit.Place(_count, 0f, 6f, FaceSize().X, 34f, HorizontalAlignment.Center);
        AddChild(_count);

        _title = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_title, 0f, 44f, FaceSize().X, 20f, HorizontalAlignment.Center);
        AddChild(_title);

        SetPile("", 0, new List<CardConfig>(), false, UiKit.Gold);
    }

    /// <summary>最上面那张的尺寸：右下留 9px 给下面两层错位。</summary>
    private Vector2 FaceSize()
    {
        return new Vector2(Size.X - 9f, Size.Y - 9f);
    }

    private Panel MakeSheet(Vector2 offset, Color fill, Color border)
    {
        Panel sheet = new Panel();
        sheet.Position = offset;
        sheet.Size = FaceSize();
        sheet.MouseFilter = MouseFilterEnum.Ignore;
        sheet.AddThemeStyleboxOverride("panel", UiKit.Box(fill, border, 1, 4));
        AddChild(sheet);
        return sheet;
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
            _icon.Modulate = new Color(1f, 1f, 1f, 0.32f);
        }
        else
        {
            // 抽牌堆是背面：不露牌，只留数量
            _icon.Visible = false;
        }

        _sheetA.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.078f, 0.070f, 0.058f), accent, 1, 4));
        _inner.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0f, 0f, 0f, 0f), new Color(accent.R, accent.G, accent.B, 0.28f), 1, 2));

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
