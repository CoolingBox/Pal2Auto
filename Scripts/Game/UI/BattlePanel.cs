// 文件：BattlePanel.cs
// 描述：卡组战斗界面。版面按"上下对峙"排：
//       上方敌手 → 中间两排牌阵（上小下大）→ 下方我方。
//       交手记录是可选抽屉（默认关闭），明细带时间与真实数值，统计页给出双方出手/伤害/护体等。
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

/// <summary>一条血条：底 + 填充 + 文字。</summary>
public sealed class UiBar
{
    public Panel Root;
    public ColorRect Fill;
    public Label Text;
}

public class BattlePanel : BasePanel
{
    private const float W = 960f;
    private const float H = 540f;

    // —— 我方手牌（大卡）——
    private const float CardW = 150f;
    private const float CardH = 196f;
    private const float CardGap = 18f;
    private const float CardX0 = 69f;
    private const float CardY = 214f;

    // —— 敌方手牌（小卡）——
    private const float MiniW = 80f;
    private const float MiniH = 108f;
    private const float MiniGap = 12f;
    private const float MiniX0 = 256f;
    private const float MiniY = 96f;

    // —— 交手记录抽屉 ——
    private const float LogX = 8f;
    private const float LogY = 44f;
    private const float LogW = 320f;
    private const float LogH = 456f;
    private const int LogKeep = 200;      // 内存里最多留多少行
    private const int LogShow = 60;       // 界面上最多铺多少行

    private Control _root;
    private bool _built;

    private DeckBattle _battle;
    private BattleTicker _ticker;
    private readonly CardView[] _cards = new CardView[DeckBattle.HandSize];
    private readonly MiniCardView[] _enemyCards = new MiniCardView[DeckBattle.HandSize];
    private float _accum;
    private float _endDelay = -1f;
    private float _statRefresh;

    private TextureRect _playerPortrait;
    private TextureRect _enemyPortrait;
    private Label _playerName;
    private Label _enemyName;
    private Label _enemyIntent;
    private Label _playerStatus;
    private Label _enemyStatus;
    private Label _playerPiles;
    private Label _enemyPiles;
    private UiBar _playerBar;
    private UiBar _enemyBar;

    // —— 记录 ——
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
        BuildEnemyHand();
        BuildPlayerHand();
        BuildPlayerBlock();
        BuildLogDrawer();
        BuildLogToggle();
        BuildResultOverlay();

        _ticker = new BattleTicker();
        _ticker.Tick = OnTick;
        _root.AddChild(_ticker);

        ApplyLogVisibility();
    }

    // —————————————————————————— 对峙的两个角色 ——————————————————————————

    private void BuildEnemyBlock()
    {
        _enemyPortrait = new TextureRect();
        _enemyPortrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _enemyPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _enemyPortrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_enemyPortrait);
        _enemyPortrait.Position = new Vector2(418f, 6f);
        _enemyPortrait.Size = new Vector2(124f, 86f);

        _enemyName = UiKit.MakeLabel("", 17, new Color(0.900f, 0.760f, 0.620f));
        UiKit.Place(_enemyName, 556f, 2f, 230f, 22f);
        _root.AddChild(_enemyName);

        _enemyBar = BuildBar(556f, 26f, 230f, 18f, new Color(0.560f, 0.220f, 0.200f));

        _enemyStatus = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_enemyStatus, 556f, 46f, 230f, 15f);
        _root.AddChild(_enemyStatus);

        _enemyPiles = UiKit.MakeLabel("", 11, UiKit.Dim);
        UiKit.Place(_enemyPiles, 556f, 61f, 230f, 14f);
        _root.AddChild(_enemyPiles);

        _enemyIntent = UiKit.MakeLabel("", 12, UiKit.Gold);
        UiKit.Place(_enemyIntent, 556f, 75f, 230f, 16f);
        _root.AddChild(_enemyIntent);

    }

    private void BuildPlayerBlock()
    {
        _playerPortrait = new TextureRect();
        _playerPortrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _playerPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _playerPortrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_playerPortrait);
        _playerPortrait.Position = new Vector2(418f, 450f);
        _playerPortrait.Size = new Vector2(124f, 84f);

        _playerName = UiKit.MakeLabel("", 17, UiKit.Paper);
        UiKit.Place(_playerName, 556f, 448f, 230f, 22f);
        _root.AddChild(_playerName);

        _playerBar = BuildBar(556f, 472f, 230f, 18f, new Color(0.600f, 0.180f, 0.160f));

        _playerStatus = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_playerStatus, 556f, 492f, 230f, 15f);
        _root.AddChild(_playerStatus);

        _playerPiles = UiKit.MakeLabel("", 11, UiKit.Dim);
        UiKit.Place(_playerPiles, 556f, 507f, 230f, 14f);
        _root.AddChild(_playerPiles);

    }

    /// <summary>两排牌阵之间的细分割线：让"上面是敌、下面是我"不靠猜。</summary>
    private void BuildDivider()
    {
        ColorRect line = new ColorRect();
        line.Color = new Color(0.380f, 0.330f, 0.250f, 0.55f);
        line.Position = new Vector2(120f, 206f);
        line.Size = new Vector2(720f, 1f);
        line.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(line);
    }

    private UiBar BuildBar(float x, float y, float w, float h, Color fillColor)
    {
        UiBar bar = new UiBar();

        Panel bg = new Panel();
        bg.Position = new Vector2(x, y);
        bg.Size = new Vector2(w, h);
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        bg.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.070f, 0.064f, 0.056f), new Color(0.300f, 0.260f, 0.200f), 1, 3));
        _root.AddChild(bg);

        ColorRect fill = new ColorRect();
        fill.Position = new Vector2(x + 2f, y + 2f);
        fill.Size = new Vector2(w - 4f, h - 4f);
        fill.Color = fillColor;
        fill.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(fill);

        Label text = UiKit.MakeLabel("", 11, UiKit.Paper);
        UiKit.Place(text, x, y, w, h, HorizontalAlignment.Center);
        _root.AddChild(text);

        bar.Root = bg;
        bar.Fill = fill;
        bar.Text = text;
        return bar;
    }

    private void BuildEnemyHand()
    {
        for (int i = 0; i < _enemyCards.Length; i++)
        {
            MiniCardView card = new MiniCardView();
            card.SlotIndex = i;
            card.Position = new Vector2(MiniX0 + i * (MiniW + MiniGap), MiniY);
            card.Size = new Vector2(MiniW, MiniH);
            _root.AddChild(card);
            _enemyCards[i] = card;
        }
    }

    private void BuildPlayerHand()
    {
        for (int i = 0; i < _cards.Length; i++)
        {
            CardView card = new CardView();
            card.SlotIndex = i;
            card.Position = new Vector2(CardX0 + i * (CardW + CardGap), CardY);
            card.Size = new Vector2(CardW, CardH);
            _root.AddChild(card);
            _cards[i] = card;
        }
    }

    // —————————————————————————— 交手记录（可选抽屉）——————————————————————————

    private void BuildLogToggle()
    {
        _logToggle = UiKit.MakeButton("交战记录 · 关", false);
        _logToggle.Position = new Vector2(796f, 6f);
        _logToggle.Size = new Vector2(152f, 30f);
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

        Button back = UiKit.MakeButton("回 主 菜 单", true);
        back.Position = new Vector2(380f, 332f);
        back.Size = new Vector2(200f, 46f);
        back.Pressed += OnBackToMenu;
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

        EnemyConfig enemy = ConfigManager.GetOne<EnemyConfig>(1);
        if (enemy == null)
        {
            TinyLog.Error("Battle", "缺少 EnemyConfig Id=1，无法开战");
            return;
        }

        List<int> deck = new List<int>();
        if (character != null && character.Deck != null && character.Deck.Length > 0)
        {
            for (int i = 0; i < character.Deck.Length; i++)
            {
                deck.Add(character.Deck[i]);
            }
        }
        else
        {
            // 兜底：没有配牌组就用前 12 张，避免开不了战
            for (int id = 1; id <= 12; id++)
            {
                deck.Add(id);
            }
        }

        string portraitKey = character != null ? character.Portrait : "p01";
        string charName = character != null ? character.Name : "侠客";

        _battle = DeckBattle.Create(charName, portraitKey, 120, deck,
            enemy.Name, enemy.Portrait, enemy.Hp, enemy.Deck);

        _accum = 0f;
        _endDelay = -1f;
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

        _playerPortrait.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + portraitKey + ".png");
        _enemyPortrait.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + enemy.Portrait + ".png");

        _resultOverlay.Visible = false;
        SyncCards();
        RefreshStatus();
        AddLogLine("开局：双方各抽 " + DeckBattle.HandSize + " 张，倒计时归零自动出手");
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
                        if (evt.Slot >= 0 && evt.Slot < _cards.Length)
                        {
                            _cards[evt.Slot].PlayFlash();
                        }
                        AudioManager.PlaySFX("slash", true);
                    }
                    break;

                case BattleEventKind.Draw:
                    // 补抽太密，明细里不记（牌堆数在两侧信息栏）；但要把上一手收尾
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
                "　残血 " + C(UiKit.PaperDim, unit.Hp + " / " + unit.MaxHp) + "\n";
        return text;
    }

    // —————————————————————————— 同步 ——————————————————————————

    private void SyncCards()
    {
        if (_battle == null)
        {
            return;
        }

        for (int i = 0; i < _cards.Length; i++)
        {
            HandSlot slot = _battle.Player.Hand[i];
            if (slot.Occupied)
            {
                _cards[i].SetCard(slot.Card);
                _cards[i].SetCountdown(slot.Progress, slot.Countdown);
            }
            else
            {
                _cards[i].SetEmpty();
            }

            HandSlot enemySlot = _battle.Enemy.Hand[i];
            if (enemySlot.Occupied)
            {
                _enemyCards[i].SetCard(enemySlot.Card);
                _enemyCards[i].SetCountdown(enemySlot.Progress, enemySlot.Countdown);
            }
            else
            {
                _enemyCards[i].SetEmpty();
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

        _playerPiles.Text = "抽 " + p.DrawPile.Count + " / 弃 " + p.DiscardPile.Count +
                            "　已出 " + p.Stats.Played;
        _enemyPiles.Text = "抽 " + e.DrawPile.Count + " / 弃 " + e.DiscardPile.Count +
                           "　已出 " + e.Stats.Played;

        CardConfig intent = _battle.EnemyFastestCard();
        if (intent != null)
        {
            _enemyIntent.Text = "下一手：" + intent.Name;
        }
        else
        {
            _enemyIntent.Text = "下一手：—";
        }
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
        return new Vector2(480f, 490f);
    }

    private Vector2 EnemyPoint()
    {
        return new Vector2(480f, 46f);
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
                _resultBody.Text += "\n（右上「交战记录」可看逐手明细）";
            }
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
}
