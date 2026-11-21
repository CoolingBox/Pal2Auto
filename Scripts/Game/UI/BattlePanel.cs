// 文件：BattlePanel.cs
// 描述：卡组战斗界面 —— 上方敌我状态，下方 5 张手牌各自倒计时，到点自动打出并补抽。
//       规则见 Docs/卡组战斗设计.md；这里只管"看得懂"：谁在打、什么时候打、打掉多少。

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

/// <summary>一条血条：底 + 填充 + 文字。血量不满时填充宽度按比例走。</summary>
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

    private const float CardW = 150f;
    private const float CardH = 196f;
    private const float CardY = 322f;
    private const float CardGap = 20f;
    private const float CardX0 = 40f;

    private Control _root;
    private bool _built;

    private DeckBattle _battle;
    private BattleTicker _ticker;
    private readonly CardView[] _cards = new CardView[DeckBattle.HandSize];
    private float _accum;
    private float _endDelay = -1f;

    private TextureRect _playerPortrait;
    private TextureRect _enemyPortrait;
    private Label _playerName;
    private Label _enemyName;
    private Label _enemyIntent;
    private Label _playerStatus;
    private Label _enemyStatus;
    private Label _drawPileLabel;
    private Label _discardPileLabel;
    private UiBar _playerBar;
    private UiBar _enemyBar;

    private readonly Label[] _logLines = new Label[4];
    private readonly Queue<string> _log = new Queue<string>();

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

        BuildPlayerBlock();
        BuildEnemyBlock();
        BuildLog();
        BuildCards();
        BuildResultOverlay();

        _ticker = new BattleTicker();
        _ticker.Tick = OnTick;
        _root.AddChild(_ticker);
    }

    private void BuildPlayerBlock()
    {
        _playerPortrait = new TextureRect();
        _playerPortrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _playerPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _playerPortrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_playerPortrait);
        _playerPortrait.Position = new Vector2(24f, 12f);
        _playerPortrait.Size = new Vector2(110f, 86f);

        _playerName = UiKit.MakeLabel("", 17, UiKit.Paper);
        UiKit.Place(_playerName, 144f, 16f, 200f, 26f);
        _root.AddChild(_playerName);

        _playerBar = BuildBar(144f, 46f, 210f, 20f, new Color(0.600f, 0.180f, 0.160f));
        _playerStatus = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_playerStatus, 144f, 70f, 220f, 20f);
        _root.AddChild(_playerStatus);
    }

    private void BuildEnemyBlock()
    {
        _enemyPortrait = new TextureRect();
        _enemyPortrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _enemyPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _enemyPortrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_enemyPortrait);
        _enemyPortrait.Position = new Vector2(806f, 12f);
        _enemyPortrait.Size = new Vector2(130f, 100f);

        _enemyName = UiKit.MakeLabel("", 19, new Color(0.900f, 0.760f, 0.620f));
        UiKit.Place(_enemyName, 400f, 16f, 380f, 28f, HorizontalAlignment.Right);
        _root.AddChild(_enemyName);

        _enemyBar = BuildBar(400f, 46f, 380f, 20f, new Color(0.560f, 0.220f, 0.200f));
        _enemyStatus = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_enemyStatus, 400f, 70f, 380f, 20f, HorizontalAlignment.Right);
        _root.AddChild(_enemyStatus);

        _enemyIntent = UiKit.MakeLabel("", 13, UiKit.Gold);
        UiKit.Place(_enemyIntent, 400f, 92f, 380f, 22f, HorizontalAlignment.Right);
        _root.AddChild(_enemyIntent);
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

        Label text = UiKit.MakeLabel("", 12, UiKit.Paper);
        UiKit.Place(text, x, y, w, h, HorizontalAlignment.Center);
        _root.AddChild(text);

        bar.Root = bg;
        bar.Fill = fill;
        bar.Text = text;
        return bar;
    }

    private void BuildLog()
    {
        Panel panel = new Panel();
        panel.Position = new Vector2(24f, 190f);
        panel.Size = new Vector2(340f, 108f);
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.070f, 0.064f, 0.056f), new Color(0.220f, 0.190f, 0.150f), 1, 4));
        _root.AddChild(panel);

        Label tag = UiKit.MakeLabel("交手记录", 11, UiKit.Dim);
        UiKit.Place(tag, 34f, 194f, 120f, 16f);
        _root.AddChild(tag);

        for (int i = 0; i < _logLines.Length; i++)
        {
            Label line = UiKit.MakeLabel("", 12, UiKit.PaperDim);
            UiKit.Place(line, 34f, 212f + i * 20f, 320f, 18f);
            _root.AddChild(line);
            _logLines[i] = line;
        }

        _drawPileLabel = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_drawPileLabel, 24f, 302f, 160f, 18f);
        _root.AddChild(_drawPileLabel);

        _discardPileLabel = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_discardPileLabel, 180f, 302f, 160f, 18f);
        _root.AddChild(_discardPileLabel);
    }

    private void BuildCards()
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
        panel.Position = new Vector2(300f, 156f);
        panel.Size = new Vector2(360f, 228f);
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.105f, 0.090f, 0.074f), new Color(0.500f, 0.400f, 0.240f), 2, 8));
        _resultOverlay.AddChild(panel);

        _resultTitle = UiKit.MakeLabel("", 32, UiKit.Gold);
        UiKit.Place(_resultTitle, 300f, 172f, 360f, 46f, HorizontalAlignment.Center);
        _resultOverlay.AddChild(_resultTitle);

        _resultBody = UiKit.MakeLabel("", 13, UiKit.Paper);
        UiKit.Place(_resultBody, 324f, 222f, 312f, 80f, HorizontalAlignment.Center);
        _resultBody.VerticalAlignment = VerticalAlignment.Top;
        _resultBody.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _resultOverlay.AddChild(_resultBody);

        Button back = UiKit.MakeButton("回 主 菜 单", true);
        back.Position = new Vector2(380f, 316f);
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

        List<int> deck = new List<int>();
        for (int id = 1; id <= 10; id++)
        {
            deck.Add(id);
        }

        string portraitKey = character != null ? character.Portrait : "p01";
        string charName = character != null ? character.Name : "侠客";

        _battle = DeckBattle.Create(deck, 120, charName, portraitKey, "泼皮阿三", 150);
        _accum = 0f;
        _endDelay = -1f;
        _log.Clear();
        RefreshLog();

        _playerPortrait.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + portraitKey + ".png");
        _enemyPortrait.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/p03.png");

        _resultOverlay.Visible = false;
        SyncCards();
        RefreshStatus();
        PushLog("开局：抽 " + DeckBattle.HandSize + " 张，倒计时归零自动出手");
    }

    private void OnTick(float delta)
    {
        if (_battle == null || _battle.Finished)
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
    }

    private void HandleEvents(List<BattleEvent> events)
    {
        for (int i = 0; i < events.Count; i++)
        {
            BattleEvent evt = events[i];

            switch (evt.Kind)
            {
                case BattleEventKind.Play:
                    if (evt.Slot >= 0 && evt.Slot < _cards.Length)
                    {
                        _cards[evt.Slot].PlayFlash();
                    }
                    PushLog("打出「" + evt.Text + "」");
                    AudioManager.PlaySFX("slash", true);
                    break;

                case BattleEventKind.Draw:
                    PushLog("补抽「" + evt.Text + "」");
                    break;

                case BattleEventKind.Damage:
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "-" + evt.Value, new Color(0.850f, 0.360f, 0.280f));
                    if (evt.Side == BattleSide.Enemy)
                    {
                        AudioManager.PlaySFX("hit", true);
                    }
                    break;

                case BattleEventKind.Shield:
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "+" + evt.Value + " 护", new Color(0.470f, 0.600f, 0.720f));
                    PushLog(evt.Text);
                    break;

                case BattleEventKind.Heal:
                    ShowFloating(PlayerPoint(), "+" + evt.Value, new Color(0.420f, 0.700f, 0.480f));
                    PushLog(evt.Text);
                    break;

                case BattleEventKind.Poison:
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "-" + evt.Value, new Color(0.520f, 0.680f, 0.360f));
                    PushLog(evt.Text);
                    break;

                case BattleEventKind.EnemyMove:
                    PushLog("敌方「" + evt.Text + "」");
                    break;

                case BattleEventKind.End:
                    _endDelay = 0.8f;
                    break;
            }
        }
    }

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
        }
    }

    private void RefreshStatus()
    {
        if (_battle == null)
        {
            return;
        }

        FighterState p = _battle.Player;
        FighterState e = _battle.Enemy;

        _playerName.Text = p.Name;
        _enemyName.Text = e.Name;

        SetBar(_playerBar, p.Hp, p.MaxHp);
        SetBar(_enemyBar, e.Hp, e.MaxHp);

        string pStatus = "护体 " + p.Shield;
        if (p.PoisonStacks > 0)
        {
            pStatus += "　中毒 " + p.PoisonStacks;
        }
        _playerStatus.Text = pStatus;

        string eStatus = "护体 " + e.Shield;
        if (e.PoisonStacks > 0)
        {
            eStatus += "　中毒 " + e.PoisonStacks;
        }
        _enemyStatus.Text = eStatus;

        if (_battle.CurrentMove != null)
        {
            _enemyIntent.Text = "下一招：" + _battle.CurrentMove.Name + "　" + _battle.MoveTimer.ToString("0.0") + "s";
        }

        _drawPileLabel.Text = "抽牌堆 " + p.DrawPile.Count;
        _discardPileLabel.Text = "弃牌堆 " + p.DiscardPile.Count;
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
        return new Vector2(79f, 60f);
    }

    private Vector2 EnemyPoint()
    {
        return new Vector2(871f, 66f);
    }

    private void ShowFloating(Vector2 origin, string text, Color color)
    {
        Label label = UiKit.MakeLabel(text, 20, color);
        label.Position = origin + new Vector2(-40f, -12f);
        label.Size = new Vector2(80f, 26f);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        _root.AddChild(label);

        Tween tween = _root.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -30f), 0.6f);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.6f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(delegate
        {
            label.QueueFree();
        }));
    }

    private void PushLog(string line)
    {
        _log.Enqueue(line);
        while (_log.Count > _logLines.Length)
        {
            _log.Dequeue();
        }
        RefreshLog();
    }

    private void RefreshLog()
    {
        string[] lines = _log.ToArray();
        for (int i = 0; i < _logLines.Length; i++)
        {
            int index = lines.Length - _logLines.Length + i;
            if (index >= 0 && index < lines.Length)
            {
                _logLines[i].Text = lines[index];
            }
            else
            {
                _logLines[i].Text = "";
            }
        }
    }

    private void ShowResult()
    {
        bool won = _battle != null && _battle.PlayerWon;
        _resultTitle.Text = won ? "胜" : "败";
        _resultTitle.AddThemeColorOverride("font_color", won ? UiKit.Gold : UiKit.Vermilion);

        if (_battle != null)
        {
            _resultBody.Text = "打出 " + _battle.PlayedCount + " 张牌，用时 " + _battle.Elapsed.ToString("0.0") + " 秒。\n" +
                               (_battle.TimedOut ? "打到时辰已尽，按剩余气血判。" : "") +
                               "\n" + (won ? "这条街暂时还是你的。" : "牌组还得再改改。");
        }

        _resultOverlay.Visible = true;
        TinyLog.Debug("Battle", "结束 胜=" + won + " 用时=" + (_battle != null ? _battle.Elapsed.ToString("0.0") : "?") +
                 " 出牌=" + (_battle != null ? _battle.PlayedCount : 0) +
                 " 玩家血=" + (_battle != null ? _battle.Player.Hp : 0) +
                 " 敌方血=" + (_battle != null ? _battle.Enemy.Hp : 0) +
                 " 敌方出手=" + (_battle != null ? _battle.EnemyMoveCount : 0) +
                 " 玩家实受伤=" + (_battle != null ? _battle.DamageToPlayer : 0) +
                 " 护体获得=" + (_battle != null ? _battle.ShieldGained : 0));
    }

    private void OnBackToMenu()
    {
        AudioManager.PlayUISFX("ui_close");
        UIManager.ShowPanel<MainMenuPanel>();
        UIManager.HidePanel<BattlePanel>();
    }
}
