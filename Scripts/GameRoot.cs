using Godot;
using TinyFramework;

/// <summary>
/// 全局入口（autoload）。职责三件：
///   1. 搭离屏渲染管线：960x540 的 SubViewport 出图，xBRZ 着色器放大到窗口；
///   2. 按顺序拉起框架管理器（字体 → UI → 音频 → 存档 → 设置）；
///   3. 打开主界面。
/// </summary>
public partial class GameRoot : SingletonNode<GameRoot>
{
    /// <summary>逻辑分辨率。所有面板坐标按这个尺寸写，改它要连版面一起改。</summary>
    public const int LogicWidth = 960;
    public const int LogicHeight = 540;

    private SubViewport _gameViewport;
    private CanvasLayer _screenLayer;
    private TextureRect _screen;
    private ShaderMaterial _xbrzMaterial;

    public override void _EnterTree()
    {
        base._EnterTree();   // 设置 Instance,并在运行时自动 Init()
        if (Engine.IsEditorHint())
        {
            return;
        }

        // 字体先就位：后面所有面板都靠它
        UiKit.Init();

        // 先有画布，再把 UI 装进去，否则面板会画在根视口上，xBRZ 就无从谈起
        SetupRenderPipeline();

        // 场景树管理器:进入树时由 SingletonNode 自动 Init,此处只需 Create()
        UIManager.Create();
        AdoptUiIntoViewport();
        AudioManager.Create();

        // 全局 2D 辉光环境：引擎原生多尺度 Glow（Godot 版的 bloom 金字塔）。
        // 只注册到 viewport，位置无关，但要早于首帧绘制。
        SetupGlowEnvironment();

        // 纯 C# 单例无场景树生命周期,需显式初始化一次
        SaveManager.Instance.Init();
        SettingManager.Instance.Init();

        bool xbrz = SettingManager.Data.XbrzEnabled;
        // 调试开关：--no-xbrz 强关滤镜，用来截对比图（不改存档）
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--no-xbrz")
            {
                xbrz = false;
            }
        }
        ApplyXbrz(xbrz);

        // 入口：主界面（开局 → 选人）
        UIManager.ShowPanel<MainMenuPanel>();

        DevShot.RunIfRequested(this);
    }

    /// <summary>
    /// 离屏画布 + 放大显示。
    /// 为什么不让 UI 直接画在根视口：根视口就是窗口分辨率，那样 xBRZ 没有低分辨率可放大，
    /// 只能退化成一次普通的采样。所有 UI 都必须先进 960x540 的 SubViewport。
    /// </summary>
    private void SetupRenderPipeline()
    {
        _gameViewport = new SubViewport();
        _gameViewport.Name = "GameViewport";
        _gameViewport.Size = new Vector2I(LogicWidth, LogicHeight);
        _gameViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _gameViewport.HandleInputLocally = true;
        AddChild(_gameViewport);

        _xbrzMaterial = new ShaderMaterial();
        _xbrzMaterial.Shader = ResourceLoader.Load<Shader>("res://Resources/Shaders/xbrz.gdshader");
        _xbrzMaterial.SetShaderParameter("source_size", new Vector2(LogicWidth, LogicHeight));

        _screenLayer = new CanvasLayer();
        _screenLayer.Name = "ScreenLayer";
        AddChild(_screenLayer);

        _screen = new TextureRect();
        _screen.Name = "Screen";
        _screen.Texture = _gameViewport.GetTexture();
        _screen.Material = _xbrzMaterial;
        _screen.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _screen.StretchMode = TextureRect.StretchModeEnum.Scale;
        // 着色器自己做取样，这一层必须最近邻，否则先被双线性糊一遍再放大
        _screen.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _screen.MouseFilter = Control.MouseFilterEnum.Ignore;
        _screenLayer.AddChild(_screen);
        // 不设全屏锚点：锚点会与 _Process 里的手动尺寸打架（引擎会警告 size 被覆盖），
        // 直接按根视口尺寸摆，窗口变了在 _Process 里跟。
        _screen.Position = Vector2.Zero;
        _screen.Size = GetViewport().GetVisibleRect().Size;
    }

    /// <summary>UIManager 默认挂在 GameRoot 下（即根视口），这里挪进离屏画布。</summary>
    private void AdoptUiIntoViewport()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null || _gameViewport == null)
        {
            return;
        }

        if (ui.GetParent() == this)
        {
            RemoveChild(ui);
        }
        _gameViewport.AddChild(ui);
    }

    public override void _Process(double delta)
    {
        if (_screen == null)
        {
            return;
        }

        // 窗口尺寸变化时把显示层拉满：Control 在 CanvasLayer 下不会自动跟着根视口改
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (_screen.Size != size)
        {
            _screen.Position = Vector2.Zero;
            _screen.Size = size;
        }
    }

    /// <summary>开关 xBRZ。关闭时退化为原像素取样（硬边），由着色器的 enabled 控制。</summary>
    public void ApplyXbrz(bool enabled)
    {
        if (_xbrzMaterial == null)
        {
            return;
        }

        _xbrzMaterial.SetShaderParameter("enabled", enabled ? 1.0f : 0.0f);
        TinyLog.Info("Render", enabled ? "xBRZ 放大：开（960x540 → 窗口）" : "xBRZ 放大：关（原像素）");
    }

    /// <summary>
    /// 鼠标事件从窗口坐标换算到逻辑坐标，再塞给离屏画布。
    /// 用 TextureRect 显示子视口时不会自动转发输入，这一步不能省。
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_gameViewport == null)
        {
            return;
        }

        InputEventMouse mouse = @event as InputEventMouse;
        if (mouse == null)
        {
            return;
        }

        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0f || size.Y <= 0f)
        {
            return;
        }

        Vector2 scale = new Vector2(LogicWidth / size.X, LogicHeight / size.Y);

        InputEventMouse copy = (InputEventMouse)mouse.Duplicate();
        copy.Position = mouse.Position * scale;
        copy.GlobalPosition = copy.Position;

        _gameViewport.PushInput(copy);
        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// 全局 2D 辉光（引擎原生 Glow，等价于 bloom 金字塔）。
    /// 只在 HDR 阈值 1.0 以上的像素上生效：常规 UI 与立绘都 &lt;= 1，不会发光，
    /// 留给以后战斗特效抬亮度用。强度 0.70、只开前 4 级——5~7 级是全屏级，
    /// 开了整幅画面会发灰。要关掉就注释掉 _EnterTree 里的调用。
    /// </summary>
    void SetupGlowEnvironment()
    {
        Godot.Environment env = new Godot.Environment();
        env.BackgroundMode = Godot.Environment.BGMode.Canvas;
        env.BackgroundCanvasMaxLayer = 10;
        env.GlowEnabled = true;
        env.GlowHdrThreshold = 1.0f;
        env.GlowHdrScale = 1.0f;
        env.GlowIntensity = 0.70f;
        env.GlowStrength = 1.0f;
        env.GlowBloom = 0.0f;
        env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen;
        for (int i = 0; i < 7; i++)
        {
            env.SetGlowLevel(i, i < 4 ? 1.0f : 0.0f);
        }

        WorldEnvironment we = new WorldEnvironment();
        we.Name = "FxGlowEnvironment";
        we.Environment = env;
        AddChild(we);
    }
}
