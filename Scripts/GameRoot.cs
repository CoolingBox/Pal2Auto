using Godot;
using TinyFramework;

/// <summary>
/// 全局入口（autoload）。职责三件：
///   1. 搭离屏渲染管线：按内部渲染倍率出图，再按窗口尺寸原样贴上去；
///   2. 按顺序拉起框架管理器（字体 → UI → 音频 → 存档 → 设置）；
///   3. 打开主界面。
/// </summary>
public partial class GameRoot : SingletonNode<GameRoot>
{
    /// <summary>逻辑分辨率。所有面板坐标按这个尺寸写，改它要连版面一起改。</summary>
    public const int LogicWidth = 960;
    public const int LogicHeight = 540;

    /// <summary>
    /// 内部渲染倍率：1 = 960×540（像素风），2 = 1920×1080（高清，1080p 窗口下 1:1 输出）。
    /// 版面坐标不变，只是每个坐标能画到 N 倍多的像素上 —— 立绘/图标被缩小的比例也随之减小。
    /// </summary>
    private int _renderScale = 2;

    private SubViewport _gameViewport;
    private CanvasLayer _screenLayer;
    private TextureRect _screen;
    private ShaderMaterial _postAaMaterial;

    /// <summary>屏幕空间后期抗锯齿：0 关 / 1 FXAA / 2 SMAA。作用于贴屏那一层。</summary>
    private int _postAaMode = 0;

    /// <summary>调试用：2D 多重采样（对应 Unity 的 MSAA）。0 关，4 表示 4x。</summary>
    private int _msaa2D = 0;

    // 调试用：--runlog 跑一局自动推演（不出画面），--runlog=N 跑 N 局，--runchar=N 指定主角
    private bool _runLog;
    private int _runLogCount = 1;
    private int _runLogChar;

    public override void _EnterTree()
    {
        base._EnterTree();   // 设置 Instance,并在运行时自动 Init()
        if (Engine.IsEditorHint())
        {
            return;
        }

        // 字体先就位：后面所有面板都靠它
        UiKit.Init();

        // 设置必须先于渲染管线读出来：内部渲染倍率决定离屏画布有多大。
        // 这两个是纯 C# 单例，没有场景树生命周期，放这么早没有副作用。
        SaveManager.Instance.Init();
        SettingManager.Instance.Init();

        // 内部渲染倍率：存档打底，命令行可覆盖（截对比图用）
        _renderScale = Mathf.Clamp(SettingManager.Data.RenderScale, 1, 2);
        _postAaMode = Mathf.Clamp(SettingManager.Data.PostAa, 0, 2);

        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--render="))
            {
                int parsed;
                if (int.TryParse(args[i].Substring(9), out parsed) && parsed >= 1 && parsed <= 4)
                {
                    _renderScale = parsed;
                }
            }
            // 截对比图用：临时关掉解析抗锯齿（不改存档）
            if (args[i] == "--no-aa")
            {
                SettingManager.Data.AntiAlias = false;
                _postAaMode = 0;
            }
            // 一次只验一档：--aa=off|analytic|fxaa|smaa|msaa（只有 msaa 是管线层，其余都是叠加）
            if (args[i] == "--aa=off")
            {
                SettingManager.Data.AntiAlias = false;
                _postAaMode = 0;
            }
            if (args[i] == "--aa=analytic")
            {
                SettingManager.Data.AntiAlias = true;
                _postAaMode = 0;
            }
            if (args[i] == "--aa=fxaa")
            {
                SettingManager.Data.AntiAlias = true;
                _postAaMode = 1;
            }
            if (args[i] == "--aa=smaa")
            {
                SettingManager.Data.AntiAlias = true;
                _postAaMode = 2;
            }
            if (args[i] == "--aa=msaa")
            {
                SettingManager.Data.AntiAlias = false;
                _postAaMode = 0;
                _msaa2D = 4;
            }

            // 一局流程自检：不画面板，只跑 RunState/RunManager/商店/合成的逻辑
            if (args[i] == "--runlog")
            {
                _runLog = true;
            }
            if (args[i].StartsWith("--runlog="))
            {
                _runLog = true;
                int.TryParse(args[i].Substring(9), out _runLogCount);
                if (_runLogCount <= 0)
                {
                    _runLogCount = 1;
                }
            }
            if (args[i].StartsWith("--runchar="))
            {
                int.TryParse(args[i].Substring(10), out _runLogChar);
            }
        }

        // 先有画布，再把 UI 装进去，否则面板会画在根视口上
        SetupRenderPipeline();

        // 场景树管理器:进入树时由 SingletonNode 自动 Init,此处只需 Create()
        UIManager.Create();
        AdoptUiIntoViewport();
        AudioManager.Create();

        // 全局 2D 辉光环境：引擎原生多尺度 Glow（Godot 版的 bloom 金字塔）。
        // 只注册到 viewport，位置无关，但要早于首帧绘制。
        SetupGlowEnvironment();

        // 入口：主界面（开局 → 选人）
        UIManager.ShowPanel<MainMenuPanel>();

        // 一局流程自检跑完就退出，不进截图流程
        if (_runLog)
        {
            RunSimulator.Run(_runLogChar, _runLogCount, RunSimulator.DefaultWinPercent);
            GetTree().Quit();
            return;
        }

        DevShot.RunIfRequested(this);
    }

    /// <summary>
    /// 离屏画布 + 原样贴屏。
    /// 为什么不让 UI 直接画在根视口：根视口就是窗口分辨率，改内部渲染倍率就得改所有版面坐标。
    /// 先进一块自己定尺寸的 SubViewport，倍率就是一个数字的事。
    /// 显示层不做任何放大算法：整数倍就是最近邻（硬像素），画布比窗口大时线性缩小。
    /// </summary>
    private void SetupRenderPipeline()
    {
        _gameViewport = new SubViewport();
        _gameViewport.Name = "GameViewport";
        _gameViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _gameViewport.HandleInputLocally = true;
        // 对应 Unity 的 MSAA：只对**几何边**多重采样。
        // 这套 UI 的框、卡、面板全是矩形 Quad + 片元着色器画的边，几何边上本来就没锯齿，
        // 所以这个开关实测无效（Unity 的 Screen Space - Overlay Canvas 是同一个成因）。
        _gameViewport.Msaa2D = _msaa2D > 1 ? Viewport.Msaa.Msaa4X : Viewport.Msaa.Disabled;
        AddChild(_gameViewport);

        _screenLayer = new CanvasLayer();
        _screenLayer.Name = "ScreenLayer";
        AddChild(_screenLayer);

        _screen = new TextureRect();
        _screen.Name = "Screen";
        _screen.Texture = _gameViewport.GetTexture();
        _screen.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _screen.StretchMode = TextureRect.StretchModeEnum.Scale;
        _screen.MouseFilter = Control.MouseFilterEnum.Ignore;

        // 后期 AA 挂在贴屏这一层：整幅画面已经合成完，正好是 Unity 里 post-processing AA 的时机
        _postAaMaterial = new ShaderMaterial();
        _postAaMaterial.Shader = ResourceLoader.Load<Shader>("res://Resources/Shaders/post_aa.gdshader");
        _screen.Material = _postAaMaterial;

        _screenLayer.AddChild(_screen);
        // 不设全屏锚点：锚点会与 _Process 里的手动尺寸打架（引擎会警告 size 被覆盖），
        // 直接按根视口尺寸摆，窗口变了在 _Process 里跟。
        _screen.Position = Vector2.Zero;
        _screen.Size = GetViewport().GetVisibleRect().Size;

        ApplyRenderScale(_renderScale);
        ApplyPostAa(_postAaMode);
    }

    /// <summary>
    /// 切后期抗锯齿。mode：0 关 / 1 FXAA / 2 SMAA。
    /// 开了以后贴屏那一层必须用线性过滤 —— FXAA、SMAA 都要做亚像素采样，
    /// 最近邻会把偏移量吸附回整像素，等于没做。
    /// </summary>
    public void ApplyPostAa(int mode)
    {
        _postAaMode = Mathf.Clamp(mode, 0, 2);
        if (_postAaMaterial != null)
        {
            _postAaMaterial.SetShaderParameter("mode", _postAaMode);
        }

        UpdateScreenFilter();
        TinyLog.Info("Render", "后期抗锯齿：" + (_postAaMode == 1 ? "FXAA" : _postAaMode == 2 ? "SMAA（简化）" : "关"));
    }

    /// <summary>
    /// 改内部渲染倍率：离屏画布按倍率放大，画布变换同步放大 —— 所有面板仍然按 960×540 写坐标，
    /// 一行都不用改。运行时改也安全：ViewportTexture 会跟着视口尺寸自动更新。
    /// </summary>
    public void ApplyRenderScale(int scale)
    {
        _renderScale = Mathf.Clamp(scale, 1, 2);
        if (_gameViewport == null)
        {
            return;
        }

        _gameViewport.Size = new Vector2I(LogicWidth * _renderScale, LogicHeight * _renderScale);
        _gameViewport.CanvasTransform = new Transform2D(_renderScale, 0f, 0f, _renderScale, 0f, 0f);
        UpdateScreenFilter();
    }

    /// <summary>画布比窗口大时（4K 屏上选高清）要线性缩小，否则最近邻取样会漏掉像素。</summary>
    private void UpdateScreenFilter()
    {
        if (_screen == null)
        {
            return;
        }

        // 后期 AA 在着色器里自己采样，必须线性；最近邻会让它的亚像素偏移全部白算
        if (_postAaMode != 0)
        {
            _screen.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
            return;
        }

        Vector2 window = GetViewport().GetVisibleRect().Size;
        bool downscale = LogicWidth * _renderScale > window.X + 1f;
        _screen.TextureFilter = downscale
            ? CanvasItem.TextureFilterEnum.Linear
            : CanvasItem.TextureFilterEnum.Nearest;
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
            UpdateScreenFilter();
        }
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

        Vector2 scale = new Vector2(LogicWidth * _renderScale / size.X, LogicHeight * _renderScale / size.Y);

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
