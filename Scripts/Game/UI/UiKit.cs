// 文件：UiKit.cs
// 描述：UI 的共用皮 —— 字体、配色、底板、按钮样式。全部走代码，换色只改这一个文件。

using Godot;
using TinyFramework;

public static class UiKit
{
    // —— 宣纸 / 墨 / 朱砂 / 旧金 ——
    public static readonly Color Ink = new Color(0.075f, 0.068f, 0.060f);
    public static readonly Color InkSoft = new Color(0.135f, 0.120f, 0.100f);
    public static readonly Color Paper = new Color(0.905f, 0.865f, 0.760f);
    public static readonly Color PaperDim = new Color(0.700f, 0.660f, 0.560f);
    public static readonly Color Gold = new Color(0.800f, 0.640f, 0.340f);
    public static readonly Color Vermilion = new Color(0.690f, 0.220f, 0.180f);
    public static readonly Color Dim = new Color(0.480f, 0.440f, 0.370f);

    public static Font GameFont { get; private set; }

    public static void Init()
    {
        SystemFont font = new SystemFont();
        font.FontNames = new string[]
        {
            "KaiTi", "STKaiti", "Kaiti SC", "SimSun", "Microsoft YaHei", "Noto Sans CJK SC",
        };
        font.Antialiasing = TextServer.FontAntialiasing.Gray;
        font.SubpixelPositioning = TextServer.SubpixelPositioning.Auto;

        GameFont = font;
        ThemeDB.FallbackFont = font;
        ThemeDB.FallbackFontSize = 16;

        TinyLog.Info("UI", "字体就绪：" + font.GetFontName());
    }

    public static Label MakeLabel(string text, int size, Color color)
    {
        Label label = new Label();
        label.Text = text;
        label.AddThemeFontOverride("font", GameFont);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.45f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    public static Label Place(Label label, float x, float y, float w, float h,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        label.Position = new Vector2(x, y);
        label.Size = new Vector2(w, h);
        label.HorizontalAlignment = align;
        label.VerticalAlignment = VerticalAlignment.Center;
        // 中文没有空格，自动换行不可靠；超框时裁掉，而不是溢到隔壁面板上
        label.ClipText = true;
        return label;
    }

    public static StyleBoxFlat Box(Color fill, Color border, int borderWidth, int radius)
    {
        StyleBoxFlat box = new StyleBoxFlat();
        box.BgColor = fill;
        box.BorderColor = border;
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(radius);
        box.ContentMarginLeft = 8;
        box.ContentMarginRight = 8;
        box.ContentMarginTop = 4;
        box.ContentMarginBottom = 4;
        return box;
    }

    public static Button MakeButton(string text, bool primary)
    {
        Button button = new Button();
        button.Text = text;
        button.FocusMode = Control.FocusModeEnum.None;
        button.AddThemeFontOverride("font", GameFont);
        button.AddThemeFontSizeOverride("font_size", 16);

        Color fillNormal;
        Color fillHover;
        Color fillPressed;
        Color border;
        Color textColor;

        if (primary)
        {
            fillNormal = Vermilion;
            fillHover = new Color(0.780f, 0.290f, 0.240f);
            fillPressed = new Color(0.560f, 0.160f, 0.130f);
            border = new Color(0.900f, 0.760f, 0.480f);
            textColor = new Color(0.980f, 0.940f, 0.860f);
        }
        else
        {
            fillNormal = new Color(0.180f, 0.160f, 0.135f);
            fillHover = new Color(0.240f, 0.210f, 0.170f);
            fillPressed = new Color(0.130f, 0.115f, 0.100f);
            border = new Color(0.420f, 0.370f, 0.280f);
            textColor = Paper;
        }

        button.AddThemeStyleboxOverride("normal", Box(fillNormal, border, 1, 4));
        button.AddThemeStyleboxOverride("hover", Box(fillHover, border, 1, 4));
        button.AddThemeStyleboxOverride("pressed", Box(fillPressed, border, 1, 4));
        button.AddThemeStyleboxOverride("disabled",
            Box(new Color(0.140f, 0.130f, 0.115f), new Color(0.260f, 0.240f, 0.200f), 1, 4));
        button.AddThemeStyleboxOverride("focus",
            Box(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), 0, 4));
        button.AddThemeColorOverride("font_color", textColor);
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.960f, 0.860f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.880f, 0.850f, 0.780f));
        button.AddThemeColorOverride("font_disabled_color", new Color(0.400f, 0.380f, 0.340f));
        return button;
    }

    public static Texture2D LoadTexture(string path)
    {
        Texture2D texture = ResManager.Load<Texture2D>(path);
        if (texture == null)
        {
            TinyLog.Warn("UI", "贴图缺失: " + path);
        }
        return texture;
    }
}
