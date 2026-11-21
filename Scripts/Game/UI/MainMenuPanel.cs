// 文件：MainMenuPanel.cs
// 描述：主界面 —— 标题、立绘、开局入口。
//       版面按 960x540 逻辑分辨率写死（见 project.godot 的 canvas_items 拉伸）。

using Godot;
using TinyFramework;

public class MainMenuPanel : BasePanel
{
    private const float W = 960f;
    private const float H = 540f;

    private Control _root;
    private bool _built;

    public override void OnBind(Control control)
    {
        base.OnBind(control);
        _root = control;
        // 版面按 960x540 写；工程画布是 1920x1080，整块按 2 倍放大铺满
        _root.Position = Vector2.Zero;
        _root.Size = new Vector2(960f, 540f);
        _root.Scale = new Vector2(2f, 2f);
        Build();
    }

    public override void OnShow()
    {
        base.OnShow();
        AudioManager.PlayBGM("market");
    }

    private void Build()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        ColorRect ink = new ColorRect();
        ink.Color = new Color(0.050f, 0.045f, 0.040f);
        ink.Size = new Vector2(W, H);
        ink.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(ink);

        Gradient gradient = new Gradient();
        gradient.SetColor(0, new Color(0.160f, 0.135f, 0.100f));
        gradient.SetColor(1, new Color(0.045f, 0.040f, 0.036f));
        GradientTexture2D gradientTexture = new GradientTexture2D();
        gradientTexture.Gradient = gradient;
        gradientTexture.Width = 8;
        gradientTexture.Height = 540;
        gradientTexture.FillFrom = new Vector2(0f, 0f);
        gradientTexture.FillTo = new Vector2(0f, 1f);

        TextureRect backdrop = new TextureRect();
        backdrop.Texture = gradientTexture;
        backdrop.Size = new Vector2(W, H);
        backdrop.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        backdrop.StretchMode = TextureRect.StretchModeEnum.Scale;
        backdrop.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(backdrop);

        // 屋顶瓦片压在下缘：给画面一个"站在街上"的落点，而不是纯渐变
        TextureRect roof = new TextureRect();
        roof.Texture = UiKit.LoadTexture("res://Resources/Image/Env/roof.png");
        roof.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        roof.StretchMode = TextureRect.StretchModeEnum.Scale;
        roof.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(roof);
        roof.Position = new Vector2(0f, 340f);
        roof.Size = new Vector2(W, 200f);
        roof.Modulate = new Color(0.55f, 0.50f, 0.44f, 0.55f);

        ColorRect roofFade = new ColorRect();
        roofFade.Color = new Color(0.045f, 0.040f, 0.036f, 0.55f);
        roofFade.Position = new Vector2(0f, 340f);
        roofFade.Size = new Vector2(W, 200f);
        roofFade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(roofFade);

        // 主立绘：下沿用 alpha 淡出，避免原图的矩形硬边砸在地面上
        TextureRect hero = new TextureRect();
        hero.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        hero.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        hero.MouseFilter = Control.MouseFilterEnum.Ignore;

        ShaderMaterial heroMaterial = new ShaderMaterial();
        heroMaterial.Shader = ResourceLoader.Load<Shader>("res://Resources/Shaders/fade_bottom.gdshader");
        heroMaterial.SetShaderParameter("fade_start", 0.66f);
        heroMaterial.SetShaderParameter("fade_end", 0.98f);
        hero.Material = heroMaterial;

        _root.AddChild(hero);
        hero.Position = new Vector2(566f, 132f);
        hero.Size = new Vector2(360f, 282f);
        hero.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/p01.png");

        Panel seal = new Panel();
        seal.Position = new Vector2(52f, 96f);
        seal.Size = new Vector2(46f, 46f);
        seal.MouseFilter = Control.MouseFilterEnum.Ignore;
        seal.AddThemeStyleboxOverride("panel",
            UiKit.Box(UiKit.Vermilion, new Color(0.85f, 0.60f, 0.42f), 1, 4));
        _root.AddChild(seal);

        Label sealText = UiKit.MakeLabel("棋", 24, new Color(0.98f, 0.92f, 0.82f));
        UiKit.Place(sealText, 52f, 96f, 46f, 46f, HorizontalAlignment.Center);
        _root.AddChild(sealText);

        Label title = UiKit.MakeLabel("仙 剑 2 自 走 棋", 40, UiKit.Gold);
        UiKit.Place(title, 108f, 94f, 440f, 54f);
        _root.AddChild(title);

        Label subtitle = UiKit.MakeLabel("江湖这条街，谁拳头硬，谁占好位置。", 15, UiKit.PaperDim);
        UiKit.Place(subtitle, 110f, 148f, 420f, 24f);
        _root.AddChild(subtitle);

        Button start = UiKit.MakeButton("开 局", true);
        start.Position = new Vector2(56f, 210f);
        start.Size = new Vector2(216f, 52f);
        start.AddThemeFontSizeOverride("font_size", 22);
        start.Pressed += OnStartPressed;
        _root.AddChild(start);

        Button quit = UiKit.MakeButton("离 开", false);
        quit.Position = new Vector2(56f, 276f);
        quit.Size = new Vector2(216f, 40f);
        quit.Pressed += OnQuitPressed;
        _root.AddChild(quit);

        Label hint = UiKit.MakeLabel("选一位侠客，决定开局携带的那件家伙。\n摆摊与打擂还在施工中。",
            12, UiKit.Dim);
        UiKit.Place(hint, 56f, 330f, 300f, 46f);
        hint.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        hint.VerticalAlignment = VerticalAlignment.Top;
        _root.AddChild(hint);

        Label footer = UiKit.MakeLabel(
            "素材：仙剑奇侠传二（原作版权归原权利方，仅供个人学习研究）",
            11, new Color(0.560f, 0.500f, 0.410f));
        UiKit.Place(footer, 24f, 512f, 700f, 20f);
        _root.AddChild(footer);
    }

    private void OnStartPressed()
    {
        AudioManager.PlayUISFX("ui_open");
        UIManager.ShowPanel<CharacterSelectPanel>();
        UIManager.HidePanel<MainMenuPanel>();
    }

    private void OnQuitPressed()
    {
        AudioManager.PlayUISFX("ui_close");
        GameRoot.Instance.GetTree().Quit();
    }
}
