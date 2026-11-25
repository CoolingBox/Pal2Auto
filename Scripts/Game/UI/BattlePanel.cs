// 文件：BattlePanel.cs
// 描述：卡组战斗界面。版面按"上下对峙"排：
//       上方敌手 → 中间两排牌阵（敌上我下，同一套卡面、同样的尺寸规则）→ 下方我方。
//       双方各有抽牌堆与弃牌堆；上场数可被卡牌改写，牌数变了牌阵整体按比例缩放。
//       交手记录是可选抽屉（默认关闭），明细带时间与真实数值，统计页给出双方汇总。
//       规则见 Docs/卡组战斗设计.md。

using System;
using System.Collections.Generic;
using Godot;
using TinyFramework;

/// <summary>每帧回调用的空控件：BasePanel 是纯 C# 类，借它拿帧。</summary>
public partial class BattleTicker : Control
{
    public Action<float> Tick;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        if (Tick != null)
        {
            Tick((float)delta);
        }
    }
}

/// <summary>一条血条：槽 + 填充 + 文字。</summary>
public sealed class UiBar
{
    public Panel Root;
    public Control Fill;
    public Label Text;
}

public class BattlePanel : BasePanel
{
    private const float W = 960f;
    private const float H = 540f;

    // 卡面基准尺寸与牌阵约束
    private const float CardBaseW = 132f;
    private const float CardBaseH = 160f;
    private const float CardGap = 12f;
    private const float RowMaxW = 788f;      // 两侧还要留给抽/弃牌堆

    private const float EnemyBandCenter = 164f;
    private const float PlayerBandCenter = 332f;

    // 交手记录抽屉
    private const float LogX = 8f;
    private const float LogY = 44f;
    private const float LogW = 320f;
    private const float LogH = 456f;
    private const int LogKeep = 200;
    private const int LogShow = 60;

    private Control _root;
    private bool _built;

    private DeckBattle _battle;
    private BattleTicker _ticker;
    private readonly CardView[] _playerCards = new CardView[DeckBattle.HandMax];
    private readonly CardView[] _enemyCards = new CardView[DeckBattle.HandMax];
    private int _lastPlayerCount = -1;
    private int _lastEnemyCount = -1;

    private float _accum;
    private float _endDelay = -1f;
    private float _statRefresh;

    private PortraitFrame _playerPortrait;
    private PortraitFrame _enemyPortrait;
    private Label _playerName;
    private Label _enemyName;
    private Label _playerStatus;
    private Label _enemyStatus;
    private Label _playerMeta;
    private Label _enemyIntent;
    private UiBar _playerBar;
    private UiBar _enemyBar;

    private PileView _enemyDraw;
    private PileView _enemyDiscard;
    private PileView _enemyExhaust;
    private PileView _playerDraw;
    private PileView _playerDiscard;
    private PileView _playerExhaust;

    private readonly List<string> _logLines = new List<string>();
    private bool _showLog;
    private bool _showStats;
    private Panel _logPanel;
    private RichTextLabel _logView;
    private Button _logToggle;
    private Button _tabDetail;
    private Button _tabStats;

    /// <summary>正在拼的一行明细：打出牌之后的那串效果都并到同一行，不刷屏。</summary>
    private sealed class PendingLine
    {
        public bool Active;
        public float Time;
        public string Who = "";
        public string Card = "";
        public string Detail = "";
    }

    private readonly PendingLine _pending = new PendingLine();

    private Control _resultOverlay;
    private Label _resultTitle;
    private Label _resultBody;

    public override void OnBind(Control control)
    {
        base.OnBind(control);
        _root = control;
        _root.Position = Vector2.Zero;
        _root.Size = new Vector2(W, H);
        Build();
    }

    public override void OnShow()
    {
        base.OnShow();
        // 面板会复用：设置里改过抗锯齿要在这里重新同步，否则要重启才生效
        bool antiAlias = SettingManager.Data.AntiAlias;
        if (_playerPortrait != null)
        {
            _playerPortrait.SetAntiAlias(antiAlias);
        }
        if (_enemyPortrait != null)
        {
            _enemyPortrait.SetAntiAlias(antiAlias);
        }
        StartBattle();
    }

    private void Build()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        ColorRect ink = new ColorRect();
        ink.Color = new Color(0.048f, 0.043f, 0.038f);
        ink.Size = new Vector2(W, H);
        ink.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(ink);

        Gradient gradient = new Gradient();
        gradient.SetColor(0, new Color(0.120f, 0.100f, 0.080f));
        gradient.SetColor(1, new Color(0.038f, 0.034f, 0.030f));
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

        BuildEnemyBlock();
        BuildDivider();
        BuildHands();
        BuildPlayerBlock();
        BuildPiles();
        BuildLogDrawer();
        BuildLogToggle();
        BuildResultOverlay();

        _ticker = new BattleTicker();
        _ticker.Tick = OnTick;
        _root.AddChild(_ticker);
    }

    // —————————————————————————— 对峙的两个角色 ——————————————————————————

    private void BuildEnemyBlock()
    {
        _enemyPortrait = new PortraitFrame();
        _enemyPortrait.Position = new Vector2(418f, 2f);
        _enemyPortrait.Size = new Vector2(124f, 78f);
        _root.AddChild(_enemyPortrait);
        _enemyPortrait.SetShape(0.22f, 0.088f);
        _enemyPortrait.SetGem(new Color(0.620f, 0.300f, 0.250f), true);
        _enemyPortrait.SetAccent(new Color(0.520f, 0.352f, 0.228f));
        _enemyPortrait.SetAntiAlias(SettingManager.Data.AntiAlias);

        _enemyName = UiKit.MakeLabel("", 17, new Color(0.900f, 0.760f, 0.620f));
        UiKit.Place(_enemyName, 556f, 4f, 230f, 20f);
        _root.AddChild(_enemyName);

        _enemyBar = BuildBar(556f, 26f, 230f, 16f, new Color(0.560f, 0.220f, 0.200f));

        _enemyStatus = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_enemyStatus, 556f, 44f, 230f, 14f);
        _root.AddChild(_enemyStatus);

        _enemyIntent = UiKit.MakeLabel("", 11, UiKit.Gold);
        UiKit.Place(_enemyIntent, 556f, 60f, 230f, 14f);
        _root.AddChild(_enemyIntent);
    }

    private void BuildPlayerBlock()
    {
        _playerPortrait = new PortraitFrame();
        _playerPortrait.Position = new Vector2(418f, 424f);
        _playerPortrait.Size = new Vector2(124f, 86f);
        _root.AddChild(_playerPortrait);
        _playerPortrait.SetShape(0.22f, 0.088f);
        _playerPortrait.SetGem(new Color(0.400f, 0.520f, 0.600f), true);
        _playerPortrait.SetAccent(new Color(0.600f, 0.470f, 0.268f));
        _playerPortrait.SetAntiAlias(SettingManager.Data.AntiAlias);

        _playerName = UiKit.MakeLabel("", 17, UiKit.Paper);
        UiKit.Place(_playerName, 556f, 420f, 230f, 20f);
        _root.AddChild(_playerName);

        _playerBar = BuildBar(556f, 442f, 230f, 16f, new Color(0.600f, 0.180f, 0.160f));

        _playerStatus = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_playerStatus, 556f, 460f, 230f, 14f);
        _root.AddChild(_playerStatus);

        _playerMeta = UiKit.MakeLabel("", 11, UiKit.Dim);
        UiKit.Place(_playerMeta, 556f, 476f, 230f, 14f);
        _root.AddChild(_playerMeta);
    }

    /// <summary>两排牌阵之间的细分割线：让"上面是敌、下面是我"不靠猜。</summary>
    private void BuildDivider()
    {
        ColorRect line = new ColorRect();
        line.Color = new Color(0.380f, 0.330f, 0.250f, 0.55f);
        line.Position = new Vector2(110f, 248f);
        line.Size = new Vector2(740f, 1f);
        line.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(line);
    }

    private UiBar BuildBar(float x, float y, float w, float h, Color fillColor)
    {
        UiBar bar = new UiBar();

        // 槽：近黑的凹槽 + 一圈暗金边，血条才不会像贴在背景上的一条色带
        Panel bg = new Panel();
        bg.Position = new Vector2(x, y);
        bg.Size = new Vector2(w, h);
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        bg.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.048f, 0.042f, 0.035f), new Color(0.230f, 0.190f, 0.140f), 1, 3));
        _root.AddChild(bg);

        // 填充：顶边一道亮线当高光，底边不描 —— 槽底本来就暗，压一条线反而脏
        Panel fill = new Panel();
        fill.Position = new Vector2(x + 2f, y + 2f);
        fill.Size = new Vector2(w - 4f, h - 4f);
        fill.MouseFilter = Control.MouseFilterEnum.Ignore;
        StyleBoxFlat fillBox = new StyleBoxFlat();
        fillBox.BgColor = fillColor;
        fillBox.BorderColor = fillColor.Lightened(0.30f);
        fillBox.BorderWidthTop = 1;
        fillBox.SetCornerRadiusAll(2);
        fill.AddThemeStyleboxOverride("panel", fillBox);
        _root.AddChild(fill);

        // 数字不压在条上：满血时条是实心红色，11px 白字+描边会糊成一团。
        // 挪到名字那一行的右端（名字左、血量右），条本身留干净。
        Label text = UiKit.MakeLabel("", 12, UiKit.Paper);
        text.AddThemeConstantOverride("outline_size", 3);
        text.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.03f, 0.02f, 0.70f));
        UiKit.Place(text, x, y - 20f, w, 18f, HorizontalAlignment.Right);
        _root.AddChild(text);

        bar.Root = bg;
        bar.Fill = fill;
        bar.Text = text;
        return bar;
    }

    private void BuildHands()
    {
        for (int i = 0; i < DeckBattle.HandMax; i++)
        {
            CardView enemy = new CardView();
            enemy.SlotIndex = i;
            enemy.Position = new Vector2(126f, EnemyBandCenter - CardBaseH * 0.5f);
            enemy.Size = new Vector2(CardBaseW, CardBaseH);
            enemy.Visible = false;
            _root.AddChild(enemy);
            _enemyCards[i] = enemy;

            CardView player = new CardView();
            player.SlotIndex = i;
            player.Position = new Vector2(126f, PlayerBandCenter - CardBaseH * 0.5f);
            player.Size = new Vector2(CardBaseW, CardBaseH);
            player.Visible = false;
            _root.AddChild(player);
            _playerCards[i] = player;
        }
    }

    /// <summary>牌数少时用基准尺寸，牌数多时按行宽反算；敌我共用这一套，张数相同则尺寸必然相同。</summary>
    private static float CardWidthFor(int count)
    {
        if (count < 1)
        {
            count = 1;
        }

        float byRow = (RowMaxW - CardGap * (count - 1)) / count;
        return Mathf.Min(CardBaseW, byRow);
    }

    private static float CardHeightFor(int count)
    {
        return CardWidthFor(count) * (CardBaseH / CardBaseW);
    }

    private void LayoutHand(CardView[] views, int count, float bandCenter)
    {
        float w = CardWidthFor(count);
        float h = CardHeightFor(count);
        float total = count * w + (count - 1) * CardGap;
        float x0 = (W - total) * 0.5f;
        float y = bandCenter - h * 0.5f;

        for (int i = 0; i < views.Length; i++)
        {
            if (i >= count)
            {
                views[i].Visible = false;
                continue;
            }

            views[i].Visible = true;
            views[i].Position = new Vector2(x0 + i * (w + CardGap), y);
            views[i].FitTo(w, h);
        }
    }

    /// <summary>
    /// 四堆牌：右侧上下各两堆（上敌下我）。
    /// 牌堆必须"看得见"——只写一行"抽 3 / 弃 5"，玩家没法判断还剩几张、弃了什么。
    /// </summary>
    private void BuildPiles()
    {
        // 右侧放抽/弃两堆，左侧放消耗堆：消耗是本场"死了"的牌，和循环的两堆分开摆
        _enemyExhaust = MakePile(10f, 88f);
        _enemyDraw = MakePile(882f, 88f);
        _enemyDiscard = MakePile(882f, 168f);
        _playerExhaust = MakePile(10f, 256f);
        _playerDraw = MakePile(882f, 256f);
        _playerDiscard = MakePile(882f, 336f);
    }

    private PileView MakePile(float x, float y)
    {
        PileView pile = new PileView();
        pile.Position = new Vector2(x, y);
        pile.Size = new Vector2(68f, 76f);
        _root.AddChild(pile);
        return pile;
    }

    // —————————————————————————— 交手记录（可选抽屉）——————————————————————————

    private void BuildLogToggle()
    {
        _logToggle = UiKit.MakeButton("交战记录 · 关", false);
        _logToggle.Position = new Vector2(8f, 4f);
        _logToggle.Size = new Vector2(132f, 30f);
        _logToggle.AddThemeFontSizeOverride("font_size", 13);
        _logToggle.Pressed += OnToggleLog;
        _root.AddChild(_logToggle);
    }

    private void BuildLogDrawer()
    {
        _logPanel = new Panel();
        _logPanel.Position = new Vector2(LogX, LogY);
        _logPanel.Size = new Vector2(LogW, LogH);
        _logPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        _logPanel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.062f, 0.056f, 0.048f, 0.95f), new Color(0.340f, 0.300f, 0.240f), 1, 6));
        _root.AddChild(_logPanel);

        Label title = UiKit.MakeLabel("交手记录", 15, UiKit.Gold);
        UiKit.Place(title, 16f, 8f, 180f, 22f);
        _logPanel.AddChild(title);

        Label hint = UiKit.MakeLabel("默认关闭，开一次就记住", 10, UiKit.Dim);
        UiKit.Place(hint, 16f, 28f, 288f, 14f);
        _logPanel.AddChild(hint);

        _tabDetail = UiKit.MakeButton("明 细", false);
        _tabDetail.Position = new Vector2(16f, 46f);
        _tabDetail.Size = new Vector2(88f, 26f);
        _tabDetail.AddThemeFontSizeOverride("font_size", 13);
        _tabDetail.Pressed += OnTabDetail;
        _logPanel.AddChild(_tabDetail);

        _tabStats = UiKit.MakeButton("统 计", false);
        _tabStats.Position = new Vector2(112f, 46f);
        _tabStats.Size = new Vector2(88f, 26f);
        _tabStats.AddThemeFontSizeOverride("font_size", 13);
        _tabStats.Pressed += OnTabStats;
        _logPanel.AddChild(_tabStats);

        _logView = new RichTextLabel();
        _logView.BbcodeEnabled = true;
        _logView.Position = new Vector2(14f, 80f);
        _logView.Size = new Vector2(LogW - 28f, 340f);
        _logView.FitContent = false;
        _logView.ScrollActive = true;
        _logView.ScrollFollowing = true;
        _logView.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _logView.MouseFilter = Control.MouseFilterEnum.Stop;
        _logView.AddThemeFontOverride("normal_font", UiKit.GameFont);
        _logView.AddThemeFontSizeOverride("normal_font_size", 13);
        _logView.AddThemeConstantOverride("line_separation", 3);
        _logView.AddThemeColorOverride("default_color", UiKit.PaperDim);
        _logPanel.AddChild(_logView);

        Label footer = UiKit.MakeLabel("数字为真实掉血　「挡」= 被护体吸收", 10, UiKit.Dim);
        UiKit.Place(footer, 16f, 424f, LogW - 32f, 20f);
        _logPanel.AddChild(footer);

        RefreshTabState();
        RefreshLogView();
    }

    private void OnToggleLog()
    {
        _showLog = !_showLog;
        SettingManager.Data.BattleLogEnabled = _showLog;
        SaveManager.Save(SettingManager.Data);

        AudioManager.PlayUISFX(_showLog ? "ui_open" : "ui_close");
        ApplyLogVisibility();
    }

    private void ApplyLogVisibility()
    {
        _showLog = SettingManager.Data.BattleLogEnabled;

        if (_logToggle != null)
        {
            _logToggle.Text = _showLog ? "交战记录 · 开" : "交战记录 · 关";
        }

        if (_logPanel != null)
        {
            _logPanel.Visible = _showLog;
        }

        RefreshLogView();
    }

    private void OnTabDetail()
    {
        _showStats = false;
        AudioManager.PlayUISFX("ui_click");
        RefreshTabState();
        RefreshLogView();
    }

    private void OnTabStats()
    {
        _showStats = true;
        AudioManager.PlayUISFX("ui_click");
        RefreshTabState();
        RefreshLogView();
    }

    private void RefreshTabState()
    {
        if (_tabDetail == null || _tabStats == null)
        {
            return;
        }

        _tabDetail.AddThemeColorOverride("font_color", _showStats ? UiKit.Dim : UiKit.Gold);
        _tabStats.AddThemeColorOverride("font_color", _showStats ? UiKit.Gold : UiKit.Dim);
    }

    private void RefreshLogView()
    {
        if (_logView == null || !_showLog)
        {
            return;
        }

        if (_showStats)
        {
            _logView.Text = BuildStatsText();
            return;
        }

        int start = _logLines.Count - LogShow;
        if (start < 0)
        {
            start = 0;
        }

        string text = "";
        for (int i = start; i < _logLines.Count; i++)
        {
            text += _logLines[i];
            if (i < _logLines.Count - 1)
            {
                text += "\n";
            }
        }

        if (text.Length == 0)
        {
            text = "[color=#6d675d]还没有交手。倒计时归零，牌自己会打出去。[/color]";
        }

        _logView.Text = text;
        _logView.ScrollToLine(_logView.GetLineCount());
    }

    // —————————————————————————— 结算 ——————————————————————————

    private void BuildResultOverlay()
    {
        _resultOverlay = new Control();
        _resultOverlay.Position = Vector2.Zero;
        _resultOverlay.Size = new Vector2(W, H);
        _resultOverlay.Visible = false;
        _resultOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(_resultOverlay);

        ColorRect dim = new ColorRect();
        dim.Color = new Color(0.02f, 0.02f, 0.02f, 0.74f);
        dim.Size = new Vector2(W, H);
        dim.MouseFilter = Control.MouseFilterEnum.Ignore;
        _resultOverlay.AddChild(dim);

        Panel panel = new Panel();
        panel.Position = new Vector2(288f, 130f);
        panel.Size = new Vector2(384f, 268f);
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.105f, 0.090f, 0.074f), new Color(0.500f, 0.400f, 0.240f), 2, 8));
        _resultOverlay.AddChild(panel);

        _resultTitle = UiKit.MakeLabel("", 32, UiKit.Gold);
        UiKit.Place(_resultTitle, 288f, 144f, 384f, 46f, HorizontalAlignment.Center);
        _resultOverlay.AddChild(_resultTitle);

        _resultBody = UiKit.MakeLabel("", 13, UiKit.Paper);
        UiKit.Place(_resultBody, 312f, 196f, 336f, 120f, HorizontalAlignment.Center);
        _resultBody.VerticalAlignment = VerticalAlignment.Top;
        _resultBody.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _resultOverlay.AddChild(_resultBody);

        Button back = UiKit.MakeButton(GameFlow.InRun ? "回 日 程" : "回 主 菜 单", true);
        back.Position = new Vector2(380f, 332f);
        back.Size = new Vector2(200f, 46f);
        back.Pressed += OnResultConfirmed;
        _resultOverlay.AddChild(back);
    }

    // —————————————————————————— 战斗推进 ——————————————————————————

    private void StartBattle()
    {
        GameFlow.EnsureRunStarted();

        CharacterConfig character = ConfigManager.GetOne<CharacterConfig>(GameFlow.SelectedCharacterId);
        if (character == null)
        {
            character = ConfigManager.GetOne<CharacterConfig>(1);
        }

        EnemyConfig enemy = ConfigManager.GetOne<EnemyConfig>(GameFlow.InRun ? GameFlow.PendingEnemyId : 1);
        if (enemy == null)
        {
            enemy = ConfigManager.GetOne<EnemyConfig>(1);
        }
        if (enemy == null)
        {
            TinyLog.Error("Battle", "缺少 EnemyConfig Id=1，无法开战");
            return;
        }

        // 牌组来源：局内用 RunState 的牌组（买来的、合成过的都在里面）；
        // 直接从主菜单进战斗时退回配置表，并扣掉 DeckProgress 里永久移除的卡。
        // 注意：这里传的是「烘好数值的副本」，不是 CardId ——
        // 传 id 会让合成升阶与淬炉附魔在战斗里完全失效（15 两一次的合成变成纯捐款）。
        List<CardConfig> deck = new List<CardConfig>();
        RunManager run = RunManager.Instance;
        if (GameFlow.InRun && run.HasActiveRun)
        {
            for (int i = 0; i < run.Current.Deck.Count; i++)
            {
                deck.Add(run.Current.Deck[i].ToBattleConfig());
            }
        }
        else if (character != null && character.Deck != null && character.Deck.Length > 0)
        {
            // 配置表只读；「移除」关键字造成的永久损失记在 DeckProgress 里，这里扣掉。
            // 局外没有阶位概念，统一按起步铜阶烘成战斗卡。
            List<int> ids = DeckStore.BuildDeck(character.Id, character.Deck);
            for (int i = 0; i < ids.Count; i++)
            {
                deck.Add(new RunCard(ids[i], CardTier.Copper).ToBattleConfig());
            }
        }
        else
        {
            // 兜底：没有配牌组就用前 12 张，避免开不了战
            for (int id = 1; id <= 12; id++)
            {
                deck.Add(new RunCard(id, CardTier.Copper).ToBattleConfig());
            }
        }

        string portraitKey = character != null ? character.Portrait : "p01";
        string charName = character != null ? character.Name : "侠客";

        // 局内气血 = 基础 + 等级加成 + 铁骨奖励；局外（直接进战斗）退回配置值。
        int playerHp = 200;
        int playerSlots = 0;
        if (GameFlow.InRun && run.HasActiveRun)
        {
            playerHp = run.Current.MaxHp;
            playerSlots = run.Current.HandCapacity;
        }
        else if (character != null && character.Hp > 0)
        {
            playerHp = character.Hp;
        }

        // 局内敌血按天数算，敌人表里的 Hp 只是图鉴默认值。
        int enemyHp = enemy.Hp;
        if (GameFlow.PendingEnemyHp > 0)
        {
            enemyHp = GameFlow.PendingEnemyHp;
        }

        _battle = DeckBattle.CreateWithCards(charName, portraitKey, playerHp, deck,
            enemy.Name, enemy.Portrait, enemyHp, enemy.Deck, playerSlots);

        _accum = 0f;
        _endDelay = -1f;
        _lastPlayerCount = -1;
        _lastEnemyCount = -1;
        _logLines.Clear();
        _pending.Active = false;
        _showStats = false;

        // 截图/调试：--log 强制展开记录，--log=stats 直接落在统计页（都只改内存，不落盘）
        if (DevShot.HasArg("--log") || DevShot.HasArg("--log=stats"))
        {
            SettingManager.Data.BattleLogEnabled = true;
        }
        if (DevShot.HasArg("--log=stats"))
        {
            _showStats = true;
        }
        ApplyLogVisibility();
        RefreshTabState();
        RefreshLogView();

        _playerPortrait.SetPortrait(portraitKey);
        _enemyPortrait.SetPortrait(enemy.Portrait);

        _resultOverlay.Visible = false;
        SyncCards();
        RefreshStatus();
        AddLogLine("开局：双方各抽 " + DeckBattle.HandBase + " 张（我方牌库 " + deck.Count +
                   " 张），倒计时归零自动出手");
        RefreshLogView();
    }

    private void OnTick(float delta)
    {
        if (_battle == null)
        {
            return;
        }

        if (_battle.Finished)
        {
            if (_endDelay > 0f)
            {
                _endDelay -= delta;
                if (_endDelay <= 0f)
                {
                    _endDelay = -1f;
                    ShowResult();
                }
            }
            return;
        }

        float speed = SettingManager.Data.BattleSpeed;
        if (speed <= 0f)
        {
            speed = 1f;
        }

        _accum += delta * speed;
        int guard = 0;
        while (_accum >= 0.05f && guard < 40)
        {
            _accum -= 0.05f;
            guard++;

            List<BattleEvent> events = _battle.Step(0.05f);
            HandleEvents(events);

            if (_battle.Finished)
            {
                _endDelay = 0.8f;
                break;
            }
        }

        SyncCards();
        RefreshStatus();

        // 统计页要跟着打斗走，但每帧重建富文本太重，限到 4 次/秒
        if (_showLog && _showStats)
        {
            _statRefresh += delta;
            if (_statRefresh >= 0.25f)
            {
                _statRefresh = 0f;
                RefreshLogView();
            }
        }
    }

    private void HandleEvents(List<BattleEvent> events)
    {
        for (int i = 0; i < events.Count; i++)
        {
            BattleEvent evt = events[i];

            switch (evt.Kind)
            {
                case BattleEventKind.Play:
                    FlushPending();
                    BeginPlayLog(evt);

                    if (evt.Side == BattleSide.Player)
                    {
                        if (evt.Slot >= 0 && evt.Slot < _playerCards.Length && _playerCards[evt.Slot].Visible)
                        {
                            _playerCards[evt.Slot].PlayFlash();
                        }
                        AudioManager.PlaySFX("slash", true);
                    }
                    else if (evt.Slot >= 0 && evt.Slot < _enemyCards.Length && _enemyCards[evt.Slot].Visible)
                    {
                        _enemyCards[evt.Slot].PlayFlash();
                    }
                    break;

                case BattleEventKind.Draw:
                    // 补抽太密，明细里不记（牌堆在右侧）；但要把上一手收尾
                    FlushPending();
                    break;

                case BattleEventKind.Damage:
                    AppendDamageLog(evt);

                    Color dmgColor = evt.Element == SpellElement.None
                        ? UiKit.DamageRed
                        : CardColors.ElementColor(evt.Element);
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "-" + evt.Value, dmgColor);

                    if (evt.Side == BattleSide.Enemy)
                    {
                        AudioManager.PlaySFX("hit", true);
                    }
                    break;

                case BattleEventKind.Shield:
                    AppendDetail(C(UiKit.ShieldGold, "+" + evt.Value + "护"));
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "+" + evt.Value + " 护", UiKit.ShieldGold);
                    break;

                case BattleEventKind.Heal:
                    AppendDetail(C(UiKit.HealGreen, "+" + evt.Value + "气"));
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "+" + evt.Value, UiKit.HealGreen);
                    break;

                case BattleEventKind.Poison:
                    if (evt.Slot < 0)
                    {
                        // 中毒跳伤：自己一行，不挂在某张牌上
                        FlushPending();
                        AddLogLine(Stamp(_battle.Elapsed) + "  " + WhoOf(evt.Side) + " 中毒 " +
                                   C(new Color(0.470f, 0.430f, 0.360f), "→") + " " +
                                   C(UiKit.PoisonGreen, "-" + evt.Value));
                    }
                    else
                    {
                        AppendDetail(C(UiKit.PoisonGreen, "毒+" + evt.Value));
                    }

                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "-" + evt.Value, UiKit.PoisonGreen);
                    break;

                case BattleEventKind.Expose:
                    AppendDetail(C(UiKit.ExposeViolet, "破" + evt.Value));
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "破绽 " + evt.Value, UiKit.ExposeViolet);
                    break;

                case BattleEventKind.Haste:
                    AppendDetail(C(UiKit.HasteGreen, WhoTag(evt.Side) + "速" +
                        CardColors.Amount(evt.Value) + "·" + ShortTime(evt.Duration) + "s"));
                    break;

                case BattleEventKind.Slow:
                    AppendDetail(C(UiKit.SlowBlue, WhoTag(evt.Side) + "缓" +
                        CardColors.Amount(evt.Value) + "·" + ShortTime(evt.Duration) + "s"));
                    break;

                case BattleEventKind.Freeze:
                    AppendDetail(C(UiKit.FreezeIce, WhoTag(evt.Side) + "冻" +
                        CardColors.Amount(evt.Value) + "·" + ShortTime(evt.Duration) + "s"));
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "冰冻 " + CardColors.Amount(evt.Value) + " 张", UiKit.FreezeIce);
                    break;

                case BattleEventKind.Charge:
                    AppendDetail(C(UiKit.ChargeGold, WhoTag(evt.Side) + "充" +
                        CardColors.Amount(evt.Value)));
                    break;

                case BattleEventKind.HandMod:
                    if (_pending.Active)
                    {
                        AppendDetail(C(UiKit.HandTeal, "上" + evt.Value));
                    }
                    else
                    {
                        // 到期收回不挂在某张牌上，自己起一行，否则记录里会看到数值莫名变回去
                        string tail = evt.Revert ? " 限时到期 " : " ";
                        AddLogLine(Stamp(_battle.Elapsed) + "  " + WhoOf(evt.Side) + tail +
                                   C(new Color(0.470f, 0.430f, 0.360f), "→") + " " +
                                   C(UiKit.HandTeal, "上场 " + evt.Value));
                    }
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "上场 " + evt.Value, UiKit.HandTeal);
                    break;

                case BattleEventKind.End:
                    FlushPending();
                    _endDelay = 0.8f;
                    break;
            }
        }

        FlushPending();
    }

    // —————————————————————————— 明细拼装 ——————————————————————————

    private static string C(Color color, string text)
    {
        return "[color=#" + color.ToHtml(false) + "]" + text + "[/color]";
    }

    /// <summary>记录里代表"谁中了"：站在玩家视角写 我 / 敌。</summary>
    private static string WhoTag(BattleSide side)
    {
        return side == BattleSide.Player ? "我" : "敌";
    }

    private static string ShortTime(float seconds)
    {
        return seconds.ToString("0.#");
    }

    private static string Stamp(float seconds)
    {
        return C(new Color(0.480f, 0.440f, 0.370f), seconds.ToString("0.0") + "s");
    }

    private static string WhoOf(BattleSide side)
    {
        return side == BattleSide.Player ? "你" : "敌";
    }

    private void BeginPlayLog(BattleEvent evt)
    {
        _pending.Active = true;
        _pending.Time = _battle.Elapsed;
        _pending.Who = WhoOf(evt.Side);
        _pending.Card = "「" + evt.Text + "」";
        if (evt.Value > 1)
        {
            _pending.Card += C(UiKit.MultiMagenta, "×" + evt.Value);
        }
        if (evt.Card != null)
        {
            if (evt.Card.HasKeyword(CardKeyword.Exhaust))
            {
                _pending.Card += C(new Color(0.560f, 0.700f, 0.780f), "耗");
            }
            else if (evt.Card.HasKeyword(CardKeyword.Remove))
            {
                _pending.Card += C(new Color(0.870f, 0.600f, 0.520f), "移");
            }
        }
        _pending.Detail = "";
    }

    private void AppendDetail(string token)
    {
        if (!_pending.Active)
        {
            return;
        }

        if (_pending.Detail.Length > 0)
        {
            _pending.Detail += "  ";
        }
        _pending.Detail += token;
    }

    private void AppendDamageLog(BattleEvent evt)
    {
        Color color = evt.Element == SpellElement.None
            ? UiKit.DamageRed
            : CardColors.ElementColor(evt.Element);

        string token = C(color, "-" + evt.HpLoss);
        if (evt.Absorbed > 0)
        {
            token += C(new Color(0.520f, 0.490f, 0.430f), "(挡 " + evt.Absorbed + ")");
        }

        AppendDetail(token);
    }

    private void FlushPending()
    {
        if (!_pending.Active)
        {
            return;
        }

        _pending.Active = false;

        string line = Stamp(_pending.Time) + "  " + _pending.Who + " " + _pending.Card;
        if (_pending.Detail.Length > 0)
        {
            line += " " + C(new Color(0.470f, 0.430f, 0.360f), "→") + " " + _pending.Detail;
        }

        AddLogLine(line);
    }

    private void AddLogLine(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > LogKeep)
        {
            _logLines.RemoveAt(0);
        }

        if (_showLog && !_showStats)
        {
            RefreshLogView();
        }
    }

    private string BuildStatsText()
    {
        if (_battle == null)
        {
            return "";
        }

        FighterState p = _battle.Player;
        FighterState e = _battle.Enemy;

        string text = C(UiKit.Gold, "我方　") + p.Name + "\n";
        text += BuildStatBlock(p);
        text += "\n" + C(new Color(0.900f, 0.760f, 0.620f), "敌方　") + e.Name + "\n";
        text += BuildStatBlock(e);
        text += "\n" + C(UiKit.Dim, "用时 ") + _battle.Elapsed.ToString("0.0") + " 秒";

        return text;
    }

    private static string BuildStatBlock(FighterState unit)
    {
        FightStats st = unit.Stats;

        string first = "—";
        if (st.FirstPlayAt >= 0f)
        {
            first = st.FirstPlayAt.ToString("0.0") + " 秒";
        }

        string text = "";
        text += "出手 " + C(UiKit.Paper, st.Played + " 次") +
                "　均伤 " + C(UiKit.DamageRed, st.AverageDamage.ToString("0.0")) + "\n";
        text += "直伤 " + C(UiKit.DamageRed, st.DamageDealt + "") +
                "　承伤 " + C(new Color(0.780f, 0.420f, 0.340f), st.DamageTaken + "") + "\n";
        text += "护体 " + C(UiKit.ShieldGold, st.ShieldGained + "") +
                "　回复 " + C(UiKit.HealGreen, st.Healed + "") + "\n";
        text += "毒伤 " + C(UiKit.PoisonGreen, st.PoisonDamage + "") +
                "　破绽 " + C(UiKit.ExposeViolet, st.ExposeApplied + "") + "\n";
        text += "首手 " + C(UiKit.PaperDim, first) +
                "　上场 " + C(UiKit.HandTeal, unit.ActiveSlots + "") +
                "　耗 " + C(new Color(0.560f, 0.700f, 0.780f), unit.ExhaustPile.Count + "") + "\n";
        return text;
    }

    // —————————————————————————— 同步 ——————————————————————————

    private void SyncCards()
    {
        if (_battle == null)
        {
            return;
        }

        int playerCount = _battle.Player.ActiveSlots;
        if (playerCount != _lastPlayerCount)
        {
            _lastPlayerCount = playerCount;
            LayoutHand(_playerCards, playerCount, PlayerBandCenter);
        }

        int enemyCount = _battle.Enemy.ActiveSlots;
        if (enemyCount != _lastEnemyCount)
        {
            _lastEnemyCount = enemyCount;
            LayoutHand(_enemyCards, enemyCount, EnemyBandCenter);
        }

        for (int i = 0; i < DeckBattle.HandMax; i++)
        {
            if (i < playerCount)
            {
                HandSlot slot = _battle.Player.Hand[i];
                if (slot.Occupied)
                {
                    _playerCards[i].SetCard(slot.Card);
                    _playerCards[i].SetCountdown(slot.Progress, slot.Countdown);
                    _playerCards[i].SetTiming(slot.HasteTime, slot.SlowTime, slot.FreezeTime);
                }
                else
                {
                    _playerCards[i].SetEmpty();
                }
            }

            if (i < enemyCount)
            {
                HandSlot enemySlot = _battle.Enemy.Hand[i];
                if (enemySlot.Occupied)
                {
                    _enemyCards[i].SetCard(enemySlot.Card);
                    _enemyCards[i].SetCountdown(enemySlot.Progress, enemySlot.Countdown);
                    _enemyCards[i].SetTiming(enemySlot.HasteTime, enemySlot.SlowTime, enemySlot.FreezeTime);
                }
                else
                {
                    _enemyCards[i].SetEmpty();
                }
            }
        }
    }

    private void RefreshStatus()
    {
        FighterState p = _battle.Player;
        FighterState e = _battle.Enemy;

        _playerName.Text = p.Name;
        _enemyName.Text = e.Name;

        SetBar(_playerBar, p.Hp, p.MaxHp);
        SetBar(_enemyBar, e.Hp, e.MaxHp);

        _playerStatus.Text = BuildStatus(p);
        _enemyStatus.Text = BuildStatus(e);

        _playerMeta.Text = "已出 " + p.Stats.Played + " 张　上场 " + p.ActiveSlots +
                           (p.PendingHandRevert ? "（出牌时收）" : "");

        CardConfig intent = _battle.EnemyFastestCard();
        string next = intent != null ? intent.Name : "—";
        _enemyIntent.Text = "上场 " + e.ActiveSlots + (e.PendingHandRevert ? "（待收）" : "") +
                            "　下一手：" + next;

        // 抽牌堆不露牌面（牌序由内核决定，露了就剧透）；弃牌堆露出最后进去那张
        _enemyDraw.SetPile("抽牌堆", e.DrawPile.Count, e.DrawPile, false, new Color(0.520f, 0.420f, 0.260f));
        _enemyDiscard.SetPile("弃牌堆", e.DiscardPile.Count, e.DiscardPile, true, new Color(0.360f, 0.320f, 0.260f));
        _playerDraw.SetPile("抽牌堆", p.DrawPile.Count, p.DrawPile, false, new Color(0.560f, 0.460f, 0.280f));
        _playerDiscard.SetPile("弃牌堆", p.DiscardPile.Count, p.DiscardPile, true, new Color(0.400f, 0.350f, 0.280f));
        _enemyExhaust.SetPile("消耗堆", e.ExhaustPile.Count, e.ExhaustPile, true, new Color(0.330f, 0.330f, 0.360f));
        _playerExhaust.SetPile("消耗堆", p.ExhaustPile.Count, p.ExhaustPile, true, new Color(0.380f, 0.380f, 0.410f));
    }

    private static string BuildStatus(FighterState unit)
    {
        string status = "护体 " + unit.Shield;
        if (unit.PoisonStacks > 0)
        {
            status += "　中毒 " + unit.PoisonStacks;
        }
        if (unit.ExposeStacks > 0)
        {
            status += "　破绽 " + unit.ExposeStacks;
        }
        return status;
    }

    private static void SetBar(UiBar bar, int hp, int maxHp)
    {
        float ratio = 0f;
        if (maxHp > 0)
        {
            ratio = Mathf.Clamp((float)hp / maxHp, 0f, 1f);
        }

        float fullWidth = bar.Root.Size.X - 4f;
        bar.Fill.Size = new Vector2(Mathf.Round(fullWidth * ratio), bar.Root.Size.Y - 4f);
        bar.Text.Text = hp + " / " + maxHp;
    }

    private Vector2 PlayerPoint()
    {
        return new Vector2(480f, 470f);
    }

    private Vector2 EnemyPoint()
    {
        return new Vector2(480f, 40f);
    }

    private void ShowFloating(Vector2 origin, string text, Color color)
    {
        Label label = UiKit.MakeLabel(text, 22, color);
        label.Position = origin + new Vector2(-60f, -12f);
        label.Size = new Vector2(120f, 28f);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        _root.AddChild(label);

        Tween tween = _root.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -34f), 0.6f);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.6f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(delegate
        {
            label.QueueFree();
        }));
    }

    private void ShowResult()
    {
        bool won = _battle != null && _battle.PlayerWon;
        _resultTitle.Text = won ? "胜" : "败";
        _resultTitle.AddThemeColorOverride("font_color", won ? UiKit.Gold : UiKit.Vermilion);

        if (_battle != null)
        {
            FightStats mine = _battle.Player.Stats;
            FightStats theirs = _battle.Enemy.Stats;

            _resultBody.Text =
                "你　出 " + mine.Played + " 张　直伤 " + mine.DamageDealt +
                    PoisonTail(mine) + "\n" +
                "敌　出 " + theirs.Played + " 张　直伤 " + theirs.DamageDealt +
                    PoisonTail(theirs) + "\n" +
                "用时 " + _battle.Elapsed.ToString("0.0") + " 秒" +
                (_battle.TimedOut ? "（时辰已尽，按气血比例判）" : "") + "\n" +
                (won ? "这条街暂时还是你的。" : "牌组还得再改改。");

            // 记录默认关闭，结算时也别把人晾着：给一行提示去哪看明细
            if (!_showLog)
            {
                _resultBody.Text += "\n（左上「交战记录」可看逐手明细）";
            }
        }

        // 「移除」的牌在这里落盘：一场结束后从牌库永久去掉，下一场不再出现
        if (_battle != null && _battle.RemovedCardIds.Count > 0)
        {
            DeckStore.AddRemoved(GameFlow.SelectedCharacterId, _battle.RemovedCardIds);
            _battle.RemovedCardIds.Clear();
        }

        _resultOverlay.Visible = true;
        _resultOverlay.MoveToFront();
    }

    /// <summary>中毒为 0 时就不写这一栏：写"中毒 -0"既占地方又像出错。</summary>
    private static string PoisonTail(FightStats stats)
    {
        if (stats.PoisonDamage <= 0)
        {
            return "";
        }
        return "　中毒 -" + stats.PoisonDamage;
    }

    private void OnBackToMenu()
    {
        AudioManager.PlayUISFX("ui_close");
        UIManager.ShowPanel<MainMenuPanel>();
        UIManager.HidePanel<BattlePanel>();
    }

    /// <summary>
    /// 结算确认：局内就回日程并把这个时辰结算掉（胜负决定银两/声望/空过），
    /// 不在局内（直接从主菜单进战斗调试）就回主菜单。
    /// </summary>
    private void OnResultConfirmed()
    {
        AudioManager.PlayUISFX("ui_click");

        RunManager run = RunManager.Instance;
        if (GameFlow.InRun && run.HasActiveRun)
        {
            bool won = _battle != null && _battle.PlayerWon;
            DayPhase phase = run.CurrentPhase;
            if (phase != null && PhaseTypeExtensions.IsCombat(phase.Type))
            {
                run.ResolveCurrentPhase(won);
            }

            UIManager.ShowPanel<RunPanel>();
            UIManager.HidePanel<BattlePanel>();
            return;
        }

        OnBackToMenu();
    }
}
