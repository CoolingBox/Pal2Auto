// 文件：BattlePanel.cs
// 描述：卡组战斗界面 —— 敌我都是牌组：上方敌方 5 张小卡，下方玩家 5 张大卡，
//       各自独立倒计时，到点自动打出并补抽。规则见 Docs/卡组战斗设计.md。

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

    private const float CardW = 150f;
    private const float CardH = 196f;
    private const float CardY = 322f;
    private const float CardGap = 20f;
    private const float CardX0 = 40f;

    private const float MiniW = 80f;
    private const float MiniH = 108f;
    private const float MiniY = 140f;
    private const float MiniGap = 12f;
    private const float MiniX0 = 256f;

    private Control _root;
    private bool _built;

    private DeckBattle _battle;
    private BattleTicker _ticker;
    private readonly CardView[] _cards = new CardView[DeckBattle.HandSize];
    private readonly MiniCardView[] _enemyCards = new MiniCardView[DeckBattle.HandSize];
    private float _accum;
    private float _endDelay = -1f;

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
        BuildEnemyHand();
        BuildLog();
        BuildPlayerHand();
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
        _playerPortrait.Position = new Vector2(20f, 8f);
        _playerPortrait.Size = new Vector2(88f, 68f);

        _playerName = UiKit.MakeLabel("", 16, UiKit.Paper);
        UiKit.Place(_playerName, 116f, 10f, 190f, 24f);
        _root.AddChild(_playerName);

        _playerBar = BuildBar(116f, 36f, 190f, 18f, new Color(0.600f, 0.180f, 0.160f));
        _playerStatus = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_playerStatus, 116f, 58f, 190f, 16f);
        _root.AddChild(_playerStatus);
    }

    private void BuildEnemyBlock()
    {
        _enemyPortrait = new TextureRect();
        _enemyPortrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _enemyPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _enemyPortrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_enemyPortrait);
        _enemyPortrait.Position = new Vector2(852f, 8f);
        _enemyPortrait.Size = new Vector2(88f, 68f);

        _enemyName = UiKit.MakeLabel("", 16, new Color(0.900f, 0.760f, 0.620f));
        UiKit.Place(_enemyName, 654f, 10f, 190f, 24f, HorizontalAlignment.Right);
        _root.AddChild(_enemyName);

        _enemyBar = BuildBar(654f, 36f, 190f, 18f, new Color(0.560f, 0.220f, 0.200f));
        _enemyStatus = UiKit.MakeLabel("", 11, UiKit.PaperDim);
        UiKit.Place(_enemyStatus, 654f, 58f, 190f, 16f, HorizontalAlignment.Right);
        _root.AddChild(_enemyStatus);

        _enemyIntent = UiKit.MakeLabel("", 12, UiKit.Gold);
        UiKit.Place(_enemyIntent, 716f, 96f, 220f, 18f, HorizontalAlignment.Right);
        _root.AddChild(_enemyIntent);

        _enemyPiles = UiKit.MakeLabel("", 11, UiKit.Dim);
        UiKit.Place(_enemyPiles, 716f, 116f, 220f, 16f, HorizontalAlignment.Right);
        _root.AddChild(_enemyPiles);
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

        Label tag = UiKit.MakeLabel("敌方手牌", 11, UiKit.Dim);
        UiKit.Place(tag, MiniX0, MiniY - 18f, 160f, 16f);
        _root.AddChild(tag);
    }

    private void BuildLog()
    {
        Panel panel = new Panel();
        panel.Position = new Vector2(24f, 96f);
        panel.Size = new Vector2(220f, 110f);
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.070f, 0.064f, 0.056f), new Color(0.220f, 0.190f, 0.150f), 1, 4));
        _root.AddChild(panel);

        Label tag = UiKit.MakeLabel("交手记录", 10, UiKit.Dim);
        UiKit.Place(tag, 32f, 100f, 100f, 14f);
        _root.AddChild(tag);

        for (int i = 0; i < _logLines.Length; i++)
        {
            Label line = UiKit.MakeLabel("", 10, UiKit.PaperDim);
            UiKit.Place(line, 32f, 116f + i * 20f, 204f, 18f);
            _root.AddChild(line);
            _logLines[i] = line;
        }

        _playerPiles = UiKit.MakeLabel("", 11, UiKit.Dim);
        UiKit.Place(_playerPiles, 24f, 214f, 220f, 16f);
        _root.AddChild(_playerPiles);

        Label tag2 = UiKit.MakeLabel("我方手牌", 11, UiKit.Dim);
        UiKit.Place(tag2, 40f, CardY - 20f, 160f, 16f);
        _root.AddChild(tag2);
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
        _log.Clear();
        RefreshLog();

        _playerPortrait.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + portraitKey + ".png");
        _enemyPortrait.Texture = UiKit.LoadTexture("res://Resources/Image/Portrait/" + enemy.Portrait + ".png");

        _resultOverlay.Visible = false;
        SyncCards();
        RefreshStatus();
        PushLog("开局：双方各抽 " + DeckBattle.HandSize + " 张，倒计时归零自动出手");
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
    }

    private void HandleEvents(List<BattleEvent> events)
    {
        for (int i = 0; i < events.Count; i++)
        {
            BattleEvent evt = events[i];

            switch (evt.Kind)
            {
                case BattleEventKind.Play:
                    if (evt.Side == BattleSide.Player)
                    {
                        if (evt.Slot >= 0 && evt.Slot < _cards.Length)
                        {
                            _cards[evt.Slot].PlayFlash();
                        }
                        PushLog("你打出「" + evt.Text + "」");
                        AudioManager.PlaySFX("slash", true);
                    }
                    else
                    {
                        PushLog("敌方「" + evt.Text + "」");
                    }
                    break;

                case BattleEventKind.Draw:
                    if (evt.Side == BattleSide.Player)
                    {
                        PushLog("补抽「" + evt.Text + "」");
                    }
                    break;

                case BattleEventKind.Damage:
                    Color dmgColor = evt.Element == SpellElement.None
                        ? UiKit.DamageRed
                        : CardColors.ElementColor(evt.Element);
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(), "-" + evt.Value, dmgColor);
                    if (evt.Side == BattleSide.Enemy)
                    {
                        AudioManager.PlaySFX("hit", true);
                    }
                    break;

                case BattleEventKind.Shield:
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "+" + evt.Value + " 护", UiKit.ShieldGold);
                    break;

                case BattleEventKind.Heal:
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "+" + evt.Value, UiKit.HealGreen);
                    break;

                case BattleEventKind.Poison:
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "-" + evt.Value, UiKit.PoisonGreen);
                    break;

                case BattleEventKind.Expose:
                    ShowFloating(evt.Side == BattleSide.Enemy ? EnemyPoint() : PlayerPoint(),
                        "破绽 " + evt.Value, UiKit.ExposeViolet);
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

        CardConfig intent = _battle.EnemyFastestCard();
        if (intent != null)
        {
            _enemyIntent.Text = "下一手：" + intent.Name;
        }
        else
        {
            _enemyIntent.Text = "下一手：—";
        }

        _playerPiles.Text = "抽牌堆 " + p.DrawPile.Count + "　弃牌堆 " + p.DiscardPile.Count;
        _enemyPiles.Text = "抽牌堆 " + e.DrawPile.Count + "　弃牌堆 " + e.DiscardPile.Count;
    }

    private static string BuildStatus(FighterState unit)
    {
        string status = "护体 " + unit.Shield;
        if (unit.PoisonStacks > 0)
        {
            status += "　中毒 " + unit.PoisonStacks;
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
        return new Vector2(64f, 42f);
    }

    private Vector2 EnemyPoint()
    {
        return new Vector2(896f, 42f);
    }

    private void ShowFloating(Vector2 origin, string text, Color color)
    {
        Label label = UiKit.MakeLabel(text, 20, color);
        label.Position = origin + new Vector2(-40f, -10f);
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
            _resultBody.Text = "你出 " + _battle.PlayerPlayed + " 张，敌方出 " + _battle.EnemyPlayed + " 张，" +
                               "用时 " + _battle.Elapsed.ToString("0.0") + " 秒。\n" +
                               (_battle.TimedOut ? "打到时辰已尽，按剩余气血判。\n" : "") +
                               (won ? "这条街暂时还是你的。" : "牌组还得再改改。");
        }

        _resultOverlay.Visible = true;
    }

    private void OnBackToMenu()
    {
        AudioManager.PlayUISFX("ui_close");
        UIManager.ShowPanel<MainMenuPanel>();
        UIManager.HidePanel<BattlePanel>();
    }
}
