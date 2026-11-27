// 文件：RunPanel.cs
// 描述：局内主界面 —— 一天的六个时辰、集市货架、开战入口。
//
// 版面 960×540，与其它面板同一套坐标。
//
//   顶栏：第 N 天 · 第 M 时辰 ／ 六个时辰进度 ／ 银两 · 声望 · 胜场
//   主体：按当前时辰类型切换
//         Free        → 集市 / 奇遇 / 淬炉 三选一
//         Shop        → 四格货架（按阶定价）+ 刷新 + 离开
//         Encounter   → 敌人信息 + 开战
//         Nemesis     → 同上，但输了扣声望
//         Event/Forge → 尚未实装，只能离开（不假装做了）
//   底栏：牌组张数 · 可合成组数 · 一句提示
//
// 规则来源：Docs/局内流程设计.md、Docs/卡牌等阶与商店.md。

using System.Collections.Generic;
using Godot;
using TinyFramework;

public class RunPanel : BasePanel
{
    private const float W = 960f;
    private const float H = 540f;

    private Control _root;
    private Label _title;
    private Label _stats;
    private Control _pipRow;
    private Control _body;
    private Label _foot;

    private readonly List<Panel> _pips = new List<Panel>();
    private readonly List<Label> _pipTexts = new List<Label>();

    private bool _built;

    /// <summary>集市里的子视图：0 = 买卡，1 = 删牌选牌。</summary>
    private int _shopMode;

    /// <summary>百炼台里已经点选的牌（最多 2 张）。</summary>
    private readonly List<RunCard> _forgePicked = new List<RunCard>();

    /// <summary>淬炉里点选的牌（附魔对象）。</summary>
    private RunCard _enchantPicked;

    /// <summary>牌组抽屉开着没有。</summary>
    private bool _deckOpen;

    /// <summary>牌组抽屉里点选的牌（合成用，最多 2 张）。</summary>
    private readonly List<RunCard> _deckPicked = new List<RunCard>();

    public override void OnBind(Control control)
    {
        base.OnBind(control);
        _root = control;
        _root.Position = Vector2.Zero;
        _root.Size = new Vector2(W, H);
    }

    public override void OnShow()
    {
        base.OnShow();

        RunManager run = RunManager.Instance;
        if (!run.HasActiveRun)
        {
            // 直接从别处进这个面板时兜底：按上次选的侠客开一局
            GameFlow.EnsureRunStarted();
            run.StartRun(GameFlow.SelectedCharacterId, 0);
        }

        if (!_built)
        {
            Build();
            _built = true;
        }

        if (GameFlow.PendingOpenRemoval)
        {
            GameFlow.PendingOpenRemoval = false;
            _shopMode = 1;
        }
        Refresh();
    }

    // —————————————————————— 静态框架 ——————————————————————

    private void Build()
    {
        Panel bg = new Panel();
        bg.Position = Vector2.Zero;
        bg.Size = new Vector2(W, H);
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        bg.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.070f, 0.062f, 0.052f), new Color(0.220f, 0.190f, 0.150f), 1, 0));
        _root.AddChild(bg);

        Panel header = new Panel();
        header.Position = new Vector2(8f, 8f);
        header.Size = new Vector2(W - 16f, 52f);
        header.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.098f, 0.086f, 0.070f), new Color(0.260f, 0.220f, 0.160f), 1, 2));
        _root.AddChild(header);

        _title = UiKit.MakeLabel("", 17, UiKit.Paper);
        UiKit.Place(_title, 24f, 16f, 300f, 32f);
        _root.AddChild(_title);

        _stats = UiKit.MakeLabel("", 14, UiKit.PaperDim);
        UiKit.Place(_stats, W - 330f, 16f, 306f, 32f, HorizontalAlignment.Right);
        _root.AddChild(_stats);

        _pipRow = new Control();
        _pipRow.Position = new Vector2(0f, 0f);
        _pipRow.Size = new Vector2(W, 52f);
        _pipRow.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_pipRow);

        for (int i = 0; i < DaySchedule.PhaseCount; i++)
        {
            Panel pip = new Panel();
            pip.Position = new Vector2(352f + i * 36f, 14f);
            pip.Size = new Vector2(28f, 24f);
            pip.MouseFilter = Control.MouseFilterEnum.Ignore;
            _pipRow.AddChild(pip);
            _pips.Add(pip);

            Label text = UiKit.MakeLabel((i + 1).ToString(), 12, UiKit.PaperDim);
            UiKit.Place(text, 0f, 0f, 28f, 24f, HorizontalAlignment.Center);
            pip.AddChild(text);
            _pipTexts.Add(text);
        }

        _body = new Control();
        _body.Position = new Vector2(0f, 68f);
        _body.Size = new Vector2(W, 408f);
        _body.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_body);

        _foot = UiKit.MakeLabel("", 12, UiKit.PaperDim);
        UiKit.Place(_foot, 24f, H - 34f, W - 180f, 24f);
        _root.AddChild(_foot);

        Button deckButton = UiKit.MakeButton("牌　组", false);
        deckButton.Position = new Vector2(W - 128f, H - 38f);
        deckButton.Size = new Vector2(104f, 28f);
        deckButton.Pressed += () =>
        {
            _deckOpen = !_deckOpen;
            _deckPicked.Clear();
            Refresh();
        };
        _root.AddChild(deckButton);
    }

    // —————————————————————— 刷新 ——————————————————————

    private void Refresh()
    {
        RunState state = RunManager.Instance.Current;
        if (state == null)
        {
            _title.Text = "未开局";
            return;
        }

        DayPhase phase = state.Today.Current;
        if (phase == null)
        {
            _title.Text = "第 " + state.Day + " 天 · 已结算";
        }
        else
        {
            _title.Text = "第 " + state.Day + " 天 · 第 " + phase.Index + " 时辰 · " +
                PhaseTypeExtensions.Name(phase.Type);
        }

        _stats.Text = "气血 " + state.MaxHp + "　银两 " + state.Gold + "　声望 " + state.Renown +
            "　胜 " + state.Victories + "/" + RunState.TargetVictories;

        RefreshPips(state);
        BuildBody(state, phase);

        int mergeable = state.MergeableCards().Count;
        _foot.Text = "牌组 " + state.Deck.Count + " 张" +
            (mergeable > 0 ? "　可合成 " + mergeable + " 组（2 张同名同阶 → 高一阶）" : "") +
            "　·　阅历 " + state.Level + " 级（" + state.Experience + "/" + RunState.ExpPerLevel + "）";
    }

    private void RefreshPips(RunState state)
    {
        for (int i = 0; i < _pips.Count; i++)
        {
            DayPhase phase = state.Today.Get(i + 1);
            Color fill = new Color(0.090f, 0.080f, 0.066f);
            Color border = new Color(0.200f, 0.175f, 0.140f);
            Color textColor = UiKit.Dim;

            if (phase != null && phase.Resolved)
            {
                fill = new Color(0.150f, 0.130f, 0.100f);
                border = new Color(0.400f, 0.330f, 0.220f);
                textColor = UiKit.PaperDim;
            }

            DayPhase current = state.Today.Current;
            if (current != null && phase == current)
            {
                fill = new Color(0.280f, 0.210f, 0.120f);
                border = UiKit.Gold;
                textColor = UiKit.Paper;
            }

            // 遭遇与宿敌用红边，一眼看出哪两个时辰要打
            if (phase != null && PhaseTypeExtensions.IsCombat(phase.Type))
            {
                border = new Color(0.480f, 0.260f, 0.220f);
            }

            _pips[i].AddThemeStyleboxOverride("panel", UiKit.Box(fill, border, 1, 2));
            _pipTexts[i].AddThemeColorOverride("font_color", textColor);
        }
    }

    private void ClearBody()
    {
        for (int i = _body.GetChildCount() - 1; i >= 0; i--)
        {
            Node child = _body.GetChild(i);
            _body.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void BuildBody(RunState state, DayPhase phase)
    {
        ClearBody();

        if (state.IsOver)
        {
            BuildOutcome(state);
            return;
        }

        // 升级奖励没选完就不能干别的（连升两级会有多轮）
        if (state.PendingRewards.Count > 0)
        {
            BuildLevelRewards(state);
            return;
        }

        if (phase == null)
        {
            return;
        }

        if (phase.Type == PhaseType.Free)
        {
            BuildFreeChoice();
        }
        else if (phase.Type == PhaseType.Shop)
        {
            BuildShop(state, phase);
        }
        else if (phase.Type == PhaseType.Event)
        {
            BuildForgeEvent(state, phase);
        }
        else if (phase.Type == PhaseType.Forge)
        {
            BuildForgeShop(state, phase);
        }
        else if (phase.Type == PhaseType.Encounter || phase.Type == PhaseType.Nemesis)
        {
            BuildCombat(state, phase);
        }
        else if (phase.Type == PhaseType.Passed)
        {
            BuildPassed(phase);
        }

        if (DevShot.OpenDeckOnStart)
        {
            DevShot.OpenDeckOnStart = false;
            _deckOpen = true;
        }

        if (_deckOpen)
        {
            BuildDeckDrawer(state);
        }
    }

    // —————————————————————— 自由三选一 ——————————————————————

    /// <summary>
    /// 升级三选一。待选列表非空时，主体区只画这三张 —— 玩家必须先选完才能继续当天。
    /// 气血上限的固定收益已经在上面的「气血 → N」里生效，这里选的是额外那一份。
    /// </summary>
    private void BuildLevelRewards(RunState state)
    {
        Label title = UiKit.MakeLabel("阅历 " + state.Level + " 级　选一项", 20, UiKit.Gold);
        UiKit.Place(title, 0f, 34f, W, 32f, HorizontalAlignment.Center);
        _body.AddChild(title);

        int gained = RunState.HpGainOfLevel(state.Level);
        Label note = UiKit.MakeLabel("气血上限 " + state.MaxHp + "（本级 +" + gained + " 已经算进去了）",
            12, UiKit.PaperDim);
        UiKit.Place(note, 0f, 70f, W, 24f, HorizontalAlignment.Center);
        _body.AddChild(note);

        List<LevelReward> rewards = state.PendingRewards;
        for (int i = 0; i < rewards.Count; i++)
        {
            float x = 156f + i * 224f;
            Panel card = new Panel();
            card.Position = new Vector2(x, 118f);
            card.Size = new Vector2(200f, 176f);
            card.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.115f, 0.100f, 0.078f), new Color(0.420f, 0.330f, 0.200f), 1, 3));
            _body.AddChild(card);

            Label name = UiKit.MakeLabel(rewards[i].Name, 22, UiKit.Paper);
            UiKit.Place(name, 0f, 28f, 200f, 34f, HorizontalAlignment.Center);
            card.AddChild(name);

            Label desc = UiKit.MakeLabel(rewards[i].Desc, 12, UiKit.PaperDim);
            UiKit.Place(desc, 12f, 72f, 176f, 44f, HorizontalAlignment.Center);
            desc.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
            card.AddChild(desc);

            Button button = UiKit.MakeButton("选　它", false);
            button.Position = new Vector2(36f, 126f);
            button.Size = new Vector2(128f, 34f);
            LevelReward picked = rewards[i];
            button.Pressed += () => OnPickLevelReward(picked);
            card.AddChild(button);
        }
    }

    private void OnPickLevelReward(LevelReward reward)
    {
        AudioManager.PlayUISFX("ui_click");
        RunManager.Instance.Current.ApplyReward(reward);
        Refresh();
    }

    private void BuildFreeChoice()
    {        Label hint = UiKit.MakeLabel("这个时辰做什么？", 16, UiKit.PaperDim);
        UiKit.Place(hint, 0f, 60f, W, 30f, HorizontalAlignment.Center);
        _body.AddChild(hint);

        string[] names = { "集　市", "奇　遇", "淬　炉" };
        string[] subs =
        {
            "买　卡 · 刷　新",
            "叙　事 · 收　益",
            "免费附魔一次",
        };
        PhaseType[] types = { PhaseType.Shop, PhaseType.Event, PhaseType.Forge };

        for (int i = 0; i < types.Length; i++)
        {
            float x = 156f + i * 224f;
            Panel card = new Panel();
            card.Position = new Vector2(x, 128f);
            card.Size = new Vector2(200f, 168f);
            card.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.105f, 0.092f, 0.074f), new Color(0.300f, 0.250f, 0.180f), 1, 3));
            _body.AddChild(card);

            Label name = UiKit.MakeLabel(names[i], 22, UiKit.Paper);
            UiKit.Place(name, 0f, 34f, 200f, 34f, HorizontalAlignment.Center);
            card.AddChild(name);

            Label sub = UiKit.MakeLabel(subs[i], 11, UiKit.PaperDim);
            UiKit.Place(sub, 12f, 74f, 176f, 40f, HorizontalAlignment.Center);
            sub.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
            card.AddChild(sub);

            Button button = UiKit.MakeButton("选　它", false);
            button.Position = new Vector2(36f, 118f);
            button.Size = new Vector2(128f, 34f);
            PhaseType chosen = types[i];
            button.Pressed += () => OnPickFreePhase(chosen);
            card.AddChild(button);
        }
    }

    private void OnPickFreePhase(PhaseType type)
    {
        AudioManager.PlayUISFX("ui_click");
        if (RunManager.Instance.SelectFreePhase(type))
        {
            Refresh();
        }
    }

    // —————————————————————— 集市 ——————————————————————

    private void BuildShop(RunState state, DayPhase phase)
    {
        if (_shopMode == 1)
        {
            BuildRemovalList(state);
            return;
        }

        ShopStock shop = state.Shop;

        Label shopName = UiKit.MakeLabel(shop.ShopName, 20, UiKit.Gold);
        UiKit.Place(shopName, 0f, 0f, W, 28f, HorizontalAlignment.Center);
        _body.AddChild(shopName);

        Label motto = UiKit.MakeLabel(shop.ShopMotto, 11, UiKit.PaperDim);
        UiKit.Place(motto, 0f, 28f, W, 20f, HorizontalAlignment.Center);
        _body.AddChild(motto);

        for (int i = 0; i < shop.Offers.Count; i++)
        {
            BuildOfferCard(state, shop, shop.Offers[i], i);
        }

        if (shop.Offers.Count == 0)
        {
            Label empty = UiKit.MakeLabel("货架空了（这一格没抽到卡）", 14, UiKit.PaperDim);
            UiKit.Place(empty, 0f, 180f, W, 30f, HorizontalAlignment.Center);
            _body.AddChild(empty);
        }

        int cost = shop.NextRefreshCost;
        string refreshText;
        if (cost < 0)
        {
            refreshText = "今日已刷尽";
        }
        else if (cost == 0)
        {
            refreshText = "刷 新（免费）";
        }
        else
        {
            refreshText = "刷 新（" + cost + " 银两）";
        }

        Button refresh = UiKit.MakeButton(refreshText, false);
        refresh.Position = new Vector2(330f, 280f);
        refresh.Size = new Vector2(180f, 38f);
        refresh.Disabled = cost < 0 || (cost > 0 && !state.CanAfford(cost));
        refresh.Pressed += OnRefreshShop;
        _body.AddChild(refresh);

        Button leave = UiKit.MakeButton("离开集市", true);
        leave.Position = new Vector2(530f, 280f);
        leave.Size = new Vector2(140f, 38f);
        leave.Pressed += OnLeavePhase;
        _body.AddChild(leave);

        string removeText = "删 牌（" + RunState.RemoveServiceCost + "）";
        if (state.RemoveServiceUsed >= RunState.RemoveServiceMax)
        {
            removeText = "删牌已用完";
        }
        Button remove = UiKit.MakeButton(removeText, false);
        remove.Position = new Vector2(690f, 280f);
        remove.Size = new Vector2(150f, 38f);
        remove.Disabled = !state.CanBuyRemoval;
        remove.Pressed += OnOpenRemoval;
        _body.AddChild(remove);

        Label note = UiKit.MakeLabel("合成：两张同名同阶 + " + RunState.MergeCost + " 银两 → 一张高一阶",
            11, UiKit.Dim);
        UiKit.Place(note, 0f, 326f, W, 22f, HorizontalAlignment.Center);
        _body.AddChild(note);
    }

    private void BuildOfferCard(RunState state, ShopStock shop, ShopOffer offer, int index)
    {
        if (offer == null || offer.Card == null)
        {
            return;
        }

        RunCard card = offer.Card;
        Color accent = TierColor(card.Tier);

        // 货格按实际数量居中：3 格与 4 格都能用，不写死坐标。
        int count = shop.Offers.Count;
        if (count < 1)
        {
            count = 1;
        }
        float cardW = 200f;
        float gap = 24f;
        float total = count * cardW + (count - 1) * gap;
        float x = (W - total) * 0.5f + index * (cardW + gap);
        Panel frame = new Panel();
        frame.Position = new Vector2(x, 54f);
        frame.Size = new Vector2(cardW, 208f);
        frame.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.100f, 0.088f, 0.070f), accent, 1, 3));
        _body.AddChild(frame);

        // 阶位条
        Panel tierBar = new Panel();
        tierBar.Position = new Vector2(1f, 1f);
        tierBar.Size = new Vector2(198f, 22f);
        tierBar.MouseFilter = Control.MouseFilterEnum.Ignore;
        tierBar.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(accent.R * 0.35f, accent.G * 0.35f, accent.B * 0.35f), accent, 1, 0));
        frame.AddChild(tierBar);

        Label tierLabel = UiKit.MakeLabel(CardTierRules.Name(card.Tier) + "阶", 12, accent);
        UiKit.Place(tierLabel, 0f, 0f, 198f, 22f, HorizontalAlignment.Center);
        tierBar.AddChild(tierLabel);

        Label name = UiKit.MakeLabel(card.CardName, 18, UiKit.Paper);
        UiKit.Place(name, 8f, 30f, 184f, 28f, HorizontalAlignment.Center);
        name.ClipText = true;
        frame.AddChild(name);

        Label effect = UiKit.MakeLabel(EffectLine(card), 14, UiKit.Paper);
        UiKit.Place(effect, 10f, 62f, 180f, 44f);
        effect.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        effect.VerticalAlignment = VerticalAlignment.Top;
        frame.AddChild(effect);

        Label cd = UiKit.MakeLabel("蓄势 " + card.Countdown.ToString("0.0") + " 秒", 11, UiKit.Dim);
        UiKit.Place(cd, 10f, 108f, 180f, 18f);
        frame.AddChild(cd);

        string tagLine = KeywordLine(card);
        if (tagLine.Length > 0)
        {
            Label tag = UiKit.MakeLabel(tagLine, 11, UiKit.Gold);
            UiKit.Place(tag, 10f, 126f, 180f, 18f);
            frame.AddChild(tag);
        }

        bool affordable = state.CanAfford(offer.Price) && !offer.Sold;
        string buttonText;
        if (offer.Sold)
        {
            buttonText = "已 售 出";
        }
        else if (affordable)
        {
            buttonText = "买　" + offer.Price + " 银两";
        }
        else
        {
            buttonText = "银两不够（" + offer.Price + "）";
        }
        Button buy = UiKit.MakeButton(buttonText, affordable);
        buy.Position = new Vector2(16f, 158f);
        buy.Size = new Vector2(168f, 34f);
        buy.Disabled = !affordable;
        int buyIndex = index;
        buy.Pressed += () => OnBuy(buyIndex);
        frame.AddChild(buy);
    }

    /// <summary>
    /// 删牌：把整个牌组摊开，点哪张删哪张（60 银两，一局 5 次）。
    /// 牌组厚到 26 张以上时，这是唯一的「把废卡换回空间」的手段，也是最直接的硬 sink。
    /// </summary>
    private void BuildRemovalList(RunState state)
    {
        string titleText = "删牌　花 " + RunState.RemoveServiceCost + " 银两去掉一张　（已用 " +
            state.RemoveServiceUsed + "/" + RunState.RemoveServiceMax + "，现有 " + state.Gold + " 银两）";
        if (state.Gold < RunState.RemoveServiceCost)
        {
            titleText += "　·　银两不足";
        }
        else if (state.RemoveServiceUsed >= RunState.RemoveServiceMax)
        {
            titleText += "　·　次数已用完";
        }

        Label title = UiKit.MakeLabel(titleText, 13, UiKit.Gold);
        UiKit.Place(title, 24f, 2f, 650f, 24f);
        _body.AddChild(title);

        Button back = UiKit.MakeButton("返　回", false);
        back.Position = new Vector2(700f, 2f);
        back.Size = new Vector2(110f, 26f);
        back.Pressed += OnCloseRemoval;
        _body.AddChild(back);

        int columns = 4;
        float cellW = 216f;
        float cellH = 38f;
        float startX = 26f;
        float startY = 36f;
        float gapX = 8f;
        float gapY = 6f;

        for (int i = 0; i < state.Deck.Count; i++)
        {
            RunCard card = state.Deck[i];
            int column = i % columns;
            int row = i / columns;

            float x = startX + column * (cellW + gapX);
            float y = startY + row * (cellH + gapY);
            if (y + cellH > 400f)
            {
                break;
            }

            string label = card.CardName + "·" + CardTierRules.Name(card.Tier);
            Button button = UiKit.MakeButton(label, false);
            button.Position = new Vector2(x, y);
            button.Size = new Vector2(cellW, cellH);
            int deckIndex = i;
            button.Pressed += () => OnRemoveCard(deckIndex);
            _body.AddChild(button);
        }

        Label hint = UiKit.MakeLabel("点一张牌即付银两删除，本局内不再出现；牌组越薄，关键牌越容易抽到",
            11, UiKit.Dim);
        UiKit.Place(hint, 0f, 386f, W, 20f, HorizontalAlignment.Center);
        _body.AddChild(hint);
    }

    /// <summary>直接打开删牌列表。调试截图与「牌组整理」入口共用。</summary>
    public void OpenRemoval()
    {
        _shopMode = 1;
        Refresh();
    }

    private void OnOpenRemoval()
    {
        AudioManager.PlayUISFX("ui_click");
        _shopMode = 1;
        Refresh();
    }

    private void OnCloseRemoval()
    {
        AudioManager.PlayUISFX("ui_click");
        _shopMode = 0;
        Refresh();
    }

    private void OnRemoveCard(int index)
    {
        AudioManager.PlayUISFX("ui_click");
        RunState state = RunManager.Instance.Current;
        if (state == null)
        {
            return;
        }
        if (state.TryBuyRemoval(index))
        {
            Refresh();
        }
    }

    private void OnBuy(int index)
    {
        AudioManager.PlayUISFX("ui_click");
        RunState state = RunManager.Instance.Current;
        if (state == null)
        {
            return;
        }
        if (state.BuyOffer(index))
        {
            Refresh();
        }
    }

    private void OnRefreshShop()
    {
        AudioManager.PlayUISFX("ui_click");
        RunState state = RunManager.Instance.Current;
        if (state == null)
        {
            return;
        }
        if (state.TryRefreshShop())
        {
            Refresh();
        }
    }

    // —————————————————————— 遭遇 / 宿敌 ——————————————————————

    private void BuildCombat(RunState state, DayPhase phase)
    {
        if (phase.Type == PhaseType.Nemesis)
        {
            BuildNemesis(state);
            return;
        }
        BuildEncounterChoices(state, phase);
    }

    /// <summary>
    /// 遭遇三选一：低 / 中 / 高三档，各从对应品阶的池子里抽一只，品阶随天数上移。
    /// 气血读 EnemyHpConfig 的 Encounter[slot]，银两与阅历读品阶表 —— 所以「选贵的」是真的选贵的。
    /// </summary>
    private void BuildEncounterChoices(RunState state, DayPhase phase)
    {
        state.RollEncounterChoices(phase.Index);

        Label kind = UiKit.MakeLabel("遭　遇　·　三选一", 20, UiKit.Gold);
        UiKit.Place(kind, 0f, 26f, W, 30f, HorizontalAlignment.Center);
        _body.AddChild(kind);

        Label hint = UiKit.MakeLabel("越难打赏钱越多，但输了当天第 5 时辰空过", 12, UiKit.PaperDim);
        UiKit.Place(hint, 0f, 56f, W, 22f, HorizontalAlignment.Center);
        _body.AddChild(hint);

        for (int slot = 0; slot < state.EncounterChoices.Count; slot++)
        {
            int enemyId = state.EncounterChoices[slot];
            EnemyConfig enemy = ConfigManager.GetOne<EnemyConfig>(enemyId);
            if (enemy == null)
            {
                continue;
            }

            int hp = RunState.EncounterHp(state.Day, slot);
            EnemyTier tier = RunState.TierForSlot(state.Day, slot);
            int gold = RunState.RewardGoldOf(tier);
            int exp = RunState.RewardExpOf(tier);

            float x = 156f + slot * 224f;
            Panel card = new Panel();
            card.Position = new Vector2(x, 90f);
            card.Size = new Vector2(200f, 210f);
            card.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.105f, 0.092f, 0.074f), UiKit.Gold, 1, 3));
            _body.AddChild(card);

            PortraitFrame frame = new PortraitFrame();
            frame.Position = new Vector2(50f, 10f);
            frame.Size = new Vector2(100f, 64f);
            card.AddChild(frame);
            frame.SetShape(0.22f, 0.088f);
            frame.SetGem(new Color(0.620f, 0.300f, 0.250f), true);
            frame.SetAccent(new Color(0.520f, 0.352f, 0.228f));
            frame.SetAntiAlias(SettingManager.Data.AntiAlias);
            frame.SetPortrait(enemy.Portrait);

            Label name = UiKit.MakeLabel(enemy.Name, 17, UiKit.Paper);
            UiKit.Place(name, 0f, 80f, 200f, 24f, HorizontalAlignment.Center);
            card.AddChild(name);

            Label info = UiKit.MakeLabel(RunState.TierName(tier) + "　" + hp + " 气血", 12, UiKit.PaperDim);
            UiKit.Place(info, 0f, 104f, 200f, 20f, HorizontalAlignment.Center);
            card.AddChild(info);

            Label reward = UiKit.MakeLabel("+" + gold + " 银两 · 阅历 +" + exp, 12, UiKit.Gold);
            UiKit.Place(reward, 0f, 126f, 200f, 20f, HorizontalAlignment.Center);
            card.AddChild(reward);

            Button fight = UiKit.MakeButton("打　它", false);
            fight.Position = new Vector2(36f, 156f);
            fight.Size = new Vector2(128f, 36f);
            int chosenId = enemyId;
            int chosenHp = hp;
            EnemyTier chosenTier = tier;
            fight.Pressed += () => OnPickEncounter(chosenId, chosenHp, chosenTier);
            card.AddChild(fight);
        }

        Label note = UiKit.MakeLabel("每战满血。战斗用你当前的牌组（" + state.Deck.Count + " 张）。",
            11, UiKit.Dim);
        UiKit.Place(note, 0f, 318f, W, 22f, HorizontalAlignment.Center);
        _body.AddChild(note);
    }

    private void OnPickEncounter(int enemyId, int enemyHp, EnemyTier tier)
    {
        RunState state = RunManager.Instance.Current;
        state.EncounterTier = tier;
        GameFlow.PendingEnemyId = enemyId;
        GameFlow.PendingEnemyHp = enemyHp;
        OnFight();
    }

    /// <summary>宿敌只有一个，没有三选一。</summary>
    private void BuildNemesis(RunState state)
    {
        EnemyConfig enemy = PickEnemy(state, true);
        int enemyHp = RunState.NemesisHp(state.Day);
        if (enemy != null)
        {
            GameFlow.PendingEnemyId = enemy.Id;
            state.EncounterTier = enemy.Tier;
        }
        GameFlow.PendingEnemyHp = enemyHp;

        Label kind = UiKit.MakeLabel("宿　敌", 20, UiKit.Vermilion);
        UiKit.Place(kind, 0f, 30f, W, 32f, HorizontalAlignment.Center);
        _body.AddChild(kind);

        if (enemy != null)
        {
            PortraitFrame frame = new PortraitFrame();
            frame.Position = new Vector2((W - 150f) * 0.5f, 66f);
            frame.Size = new Vector2(150f, 96f);
            _body.AddChild(frame);
            frame.SetShape(0.22f, 0.088f);
            frame.SetGem(new Color(0.620f, 0.300f, 0.250f), true);
            frame.SetAccent(new Color(0.520f, 0.352f, 0.228f));
            frame.SetAntiAlias(SettingManager.Data.AntiAlias);
            frame.SetPortrait(enemy.Portrait);

            Label name = UiKit.MakeLabel(enemy.Name + "　" + RunState.TierName(state.EncounterTier) +
                "　" + enemyHp + " 气血", 18, UiKit.Paper);
            UiKit.Place(name, 0f, 172f, W, 30f, HorizontalAlignment.Center);
            _body.AddChild(name);

            Label title = UiKit.MakeLabel(enemy.Title, 12, UiKit.PaperDim);
            UiKit.Place(title, 0f, 202f, W, 22f, HorizontalAlignment.Center);
            _body.AddChild(title);
        }

        Label warnLabel = UiKit.MakeLabel(
            "胜：+" + state.NemesisReward + " 银两　负：扣 " + state.Day + " 点声望（现有 " + state.Renown + "）",
            13, UiKit.PaperDim);
        UiKit.Place(warnLabel, 0f, 232f, W, 24f, HorizontalAlignment.Center);
        _body.AddChild(warnLabel);

        Button fight = UiKit.MakeButton("开　战", true);
        fight.Position = new Vector2(400f, 272f);
        fight.Size = new Vector2(160f, 46f);
        fight.Pressed += OnFight;
        _body.AddChild(fight);

        Label note = UiKit.MakeLabel("每战满血。战斗用你当前的牌组（" + state.Deck.Count + " 张）。",
            11, UiKit.Dim);
        UiKit.Place(note, 0f, 328f, W, 22f, HorizontalAlignment.Center);
        _body.AddChild(note);
    }

    /// <summary>
    /// 暂时从全表随机一个敌人（现在 EnemyConfig 只有一条，等于固定打盗王查协）。
    /// 真正的「按地区池抽、按品阶加权」要等 EncounterConfig 落地。
    /// </summary>
    private EnemyConfig PickEnemy(RunState state, bool nemesis)
    {
        List<EnemyConfig> all = ConfigManager.GetAll<EnemyConfig>();
        if (all == null || all.Count == 0)
        {
            return null;
        }
        return all[state.Rng.Next(all.Count)];
    }

    private void OnFight()
    {
        AudioManager.PlayUISFX("ui_click");
        DayPhase phase = RunManager.Instance.CurrentPhase;
        if (phase == null)
        {
            return;
        }

        GameFlow.InRun = true;

        UIManager.ShowPanel<BattlePanel>();
        UIManager.HidePanel<RunPanel>();
    }

    // —————————————————————— 占位与结算 ——————————————————————

    /// <summary>百炼台：上方 5 张候选（点选），下方 3 个选项。</summary>
    private void BuildForgeEvent(RunState state, DayPhase phase)
    {
        state.OpenForgeEvent(phase.Index);
        ForgeEvent forge = state.PendingForge;
        if (forge == null)
        {
            BuildPlaceholder(phase);
            return;
        }

        Label title = UiKit.MakeLabel("百　炼　台", 22, UiKit.Gold);
        UiKit.Place(title, 0f, 18f, W, 30f, HorizontalAlignment.Center);
        _body.AddChild(title);

        Label hint = UiKit.MakeLabel(
            forge.Resolved
                ? "炉火已熄，这个时辰过去了"
                : "先点牌、再点下方选项（合成要选两张）",
            12, UiKit.PaperDim);
        UiKit.Place(hint, 0f, 48f, W, 22f, HorizontalAlignment.Center);
        _body.AddChild(hint);

        for (int i = 0; i < forge.Candidates.Count; i++)
        {
            RunCard card = forge.Candidates[i];
            bool picked = _forgePicked.Contains(card);
            float x = 135f + i * 140f;

            Panel frame = new Panel();
            frame.Position = new Vector2(x, 78f);
            frame.Size = new Vector2(130f, 150f);
            Color border = picked ? UiKit.Gold : new Color(0.300f, 0.250f, 0.180f);
            frame.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.105f, 0.092f, 0.074f), border, picked ? 3 : 1, 3));
            _body.AddChild(frame);

            Label name = UiKit.MakeLabel(card.CardName, 15, UiKit.Paper);
            UiKit.Place(name, 0f, 8f, 130f, 22f, HorizontalAlignment.Center);
            frame.AddChild(name);

            Label tier = UiKit.MakeLabel(CardTierRules.Name(card.Tier) + "阶", 11, TierColor(card.Tier));
            UiKit.Place(tier, 0f, 30f, 130f, 18f, HorizontalAlignment.Center);
            frame.AddChild(tier);

            Label stat = UiKit.MakeLabel(CardStatLine(card), 11, UiKit.PaperDim);
            UiKit.Place(stat, 0f, 52f, 130f, 18f, HorizontalAlignment.Center);
            frame.AddChild(stat);

            Label cd = UiKit.MakeLabel("蓄势 " + card.Countdown.ToString("0.0") + " 秒", 11, UiKit.Dim);
            UiKit.Place(cd, 0f, 72f, 130f, 18f, HorizontalAlignment.Center);
            frame.AddChild(cd);

            RunCard target = card;
            Button pickButton = UiKit.MakeButton(picked ? "已选" : "选它", false);
            pickButton.Position = new Vector2(15f, 100f);
            pickButton.Size = new Vector2(100f, 32f);
            pickButton.Disabled = forge.Resolved;
            pickButton.Pressed += () => OnForgePick(target);
            frame.AddChild(pickButton);
        }

        for (int i = 0; i < forge.Options.Count; i++)
        {
            ForgeOption option = forge.Options[i];
            float x = 156f + i * 224f;

            Panel box = new Panel();
            box.Position = new Vector2(x, 246f);
            box.Size = new Vector2(200f, 96f);
            box.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.100f, 0.088f, 0.070f), UiKit.Gold, 1, 3));
            _body.AddChild(box);

            // 权重 ≤5 的选项标「稀有」：合成 / 放逐 / 磨砺 / 换命 —— 都是本身净赚的那种
            bool rare = option.Weight <= 5;
            string optLabel = rare ? option.Name + "　稀" : option.Name;
            Label optName = UiKit.MakeLabel(optLabel, 17, rare ? UiKit.Vermilion : UiKit.Gold);
            UiKit.Place(optName, 0f, 6f, 200f, 24f, HorizontalAlignment.Center);
            box.AddChild(optName);

            Label optDesc = UiKit.MakeLabel(option.Desc, 10, UiKit.PaperDim);
            optDesc.AutowrapMode = TextServer.AutowrapMode.Word;
            UiKit.Place(optDesc, 10f, 30f, 180f, 34f, HorizontalAlignment.Center);
            box.AddChild(optDesc);

            ForgeOption picked = option;
            Button apply = UiKit.MakeButton("选它", false);
            apply.Position = new Vector2(50f, 64f);
            apply.Size = new Vector2(100f, 26f);
            apply.Disabled = forge.Resolved;
            apply.Pressed += () => OnForgeApply(picked);
            box.AddChild(apply);
        }

        Label note = UiKit.MakeLabel("牌库 " + state.Deck.Count + " 张　银两 " + state.Gold, 11, UiKit.Dim);
        UiKit.Place(note, 0f, 352f, W, 22f, HorizontalAlignment.Center);
        _body.AddChild(note);
    }

    private static string CardStatLine(RunCard card)
    {
        List<string> parts = new List<string>();
        if (card.Damage > 0)
        {
            parts.Add("攻 " + card.Damage);
        }
        if (card.Spell > 0)
        {
            parts.Add("法 " + card.Spell);
        }
        if (card.Shield > 0)
        {
            parts.Add("护 " + card.Shield);
        }
        if (card.Heal > 0)
        {
            parts.Add("疗 " + card.Heal);
        }
        if (card.Poison > 0)
        {
            parts.Add("毒 " + card.Poison);
        }
        return string.Join(" ", parts);
    }

    private void OnForgePick(RunCard card)
    {
        if (_forgePicked.Contains(card))
        {
            _forgePicked.Remove(card);
        }
        else
        {
            if (_forgePicked.Count >= 2)
            {
                _forgePicked.RemoveAt(0);
            }
            _forgePicked.Add(card);
        }
        Refresh();
    }

    private void OnForgeApply(ForgeOption option)
    {
        RunState state = RunManager.Instance.Current;
        bool ok = state.ApplyForgeOption(option, new List<RunCard>(_forgePicked));
        if (ok)
        {
            _forgePicked.Clear();
        }
        Refresh();
    }

    /// <summary>牌组抽屉：全屏列出牌组，可在这里 2 合 1。</summary>
    private void BuildDeckDrawer(RunState state)
    {
        Panel mask = new Panel();
        mask.Position = Vector2.Zero;
        mask.Size = new Vector2(W, H - 56f);
        mask.AddThemeStyleboxOverride("panel",
            UiKit.Box(new Color(0.055f, 0.048f, 0.040f, 0.98f), new Color(0.300f, 0.250f, 0.180f), 1, 0));
        _body.AddChild(mask);

        Label title = UiKit.MakeLabel("牌　组（" + state.Deck.Count + " 张）", 20, UiKit.Gold);
        UiKit.Place(title, 0f, 8f, W, 28f, HorizontalAlignment.Center);
        mask.AddChild(title);

        // 右上角关闭
        Button close = UiKit.MakeButton("关　闭", false);
        close.Position = new Vector2(W - 120f, 6f);
        close.Size = new Vector2(100f, 28f);
        close.Pressed += () =>
        {
            _deckOpen = false;
            _deckPicked.Clear();
            Refresh();
        };
        mask.AddChild(close);

        // 左下角合成
        bool canMerge = CanMergePicked();
        Button merge = UiKit.MakeButton(
            canMerge ? "合成（" + RunState.MergeCost + " 银两）" : "选 2 张同名同阶", true);
        merge.Position = new Vector2(20f, 6f);
        merge.Size = new Vector2(200f, 28f);
        merge.Disabled = !canMerge;
        merge.Pressed += OnDeckMerge;
        mask.AddChild(merge);

        for (int i = 0; i < state.Deck.Count && i < 30; i++)
        {
            RunCard card = state.Deck[i];
            bool picked = _deckPicked.Contains(card);
            int col = i % 6;
            int row = i / 6;
            float x = 40f + col * 148f;
            float y = 44f + row * 78f;

            Panel frame = new Panel();
            frame.Position = new Vector2(x, y);
            frame.Size = new Vector2(140f, 72f);
            Color border = picked ? UiKit.Gold : new Color(0.280f, 0.230f, 0.170f);
            frame.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.105f, 0.092f, 0.074f), border, picked ? 2 : 1, 2));
            mask.AddChild(frame);

            Label name = UiKit.MakeLabel(card.CardName, 13, UiKit.Paper);
            UiKit.Place(name, 4f, 4f, 132f, 18f, HorizontalAlignment.Left);
            frame.AddChild(name);

            Label info = UiKit.MakeLabel(CardTierRules.Name(card.Tier) + "阶　" + CardStatLine(card),
                11, TierColor(card.Tier));
            UiKit.Place(info, 4f, 24f, 132f, 16f, HorizontalAlignment.Left);
            frame.AddChild(info);

            // 角标：附魔 / 事件改过 / 锁死
            string marks = "";
            if (card.Enchant != EnchantType.None)
            {
                marks += RunState.EnchantName(card.Enchant) + " ";
            }
            if (card.BuffCount > 0)
            {
                marks += "改" + card.BuffCount + " ";
            }
            if (card.NoUpgrade)
            {
                marks += "锁 ";
            }
            if (card.PriorityDraw)
            {
                marks += "先 ";
            }
            if (card.BottomOfDeck)
            {
                marks += "后 ";
            }
            if (marks.Length > 0)
            {
                Label mark = UiKit.MakeLabel(marks.Trim(), 10, UiKit.Vermilion);
                UiKit.Place(mark, 4f, 42f, 132f, 14f, HorizontalAlignment.Left);
                frame.AddChild(mark);
            }

            Label cd = UiKit.MakeLabel("蓄势 " + card.Countdown.ToString("0.0"), 11, UiKit.Dim);
            UiKit.Place(cd, 4f, 54f, 132f, 14f, HorizontalAlignment.Left);
            frame.AddChild(cd);

            RunCard target = card;
            Button hit = new Button();
            hit.Flat = true;
            hit.Position = Vector2.Zero;
            hit.Size = new Vector2(140f, 72f);
            hit.Pressed += () => OnDeckPick(target);
            frame.AddChild(hit);
        }

        if (state.Deck.Count > 30)
        {
            Label more = UiKit.MakeLabel("（还有 " + (state.Deck.Count - 30) + " 张没显示）", 11, UiKit.Dim);
            UiKit.Place(more, 0f, H - 92f, W, 18f, HorizontalAlignment.Center);
            mask.AddChild(more);
        }
    }

    private void OnDeckPick(RunCard card)
    {
        if (_deckPicked.Contains(card))
        {
            _deckPicked.Remove(card);
        }
        else
        {
            if (_deckPicked.Count >= 2)
            {
                _deckPicked.RemoveAt(0);
            }
            _deckPicked.Add(card);
        }
        Refresh();
    }

    private bool CanMergePicked()
    {
        if (_deckPicked.Count != 2)
        {
            return false;
        }
        RunCard a = _deckPicked[0];
        RunCard b = _deckPicked[1];
        if (a == b || a.NoUpgrade || b.NoUpgrade)
        {
            return false;
        }
        return a.CardId == b.CardId && a.Tier == b.Tier && CardTierRules.CanMerge(a.Tier);
    }

    private void OnDeckMerge()
    {
        if (!CanMergePicked())
        {
            return;
        }
        RunState state = RunManager.Instance.Current;
        RunCard a = _deckPicked[0];
        state.TryMerge(a.CardId, a.Tier);
        _deckPicked.Clear();
        Refresh();
    }

    /// <summary>淬炉：每天免费给一张牌附魔（锋锐 / 疾风 / 厚土 / 蚀骨），一卡只能一个。</summary>
    private void BuildForgeShop(RunState state, DayPhase phase)
    {
        Label title = UiKit.MakeLabel("淬　炉", 22, UiKit.Gold);
        UiKit.Place(title, 0f, 14f, W, 30f, HorizontalAlignment.Center);
        _body.AddChild(title);

        string hint = state.FreeEnchantUsed
            ? "今日炉火已熄（每天免费一次），明天再来"
            : "先点一张牌，再点下方附魔　·　一卡只能附一个";
        Label hintLabel = UiKit.MakeLabel(hint, 12, UiKit.PaperDim);
        UiKit.Place(hintLabel, 0f, 44f, W, 22f, HorizontalAlignment.Center);
        _body.AddChild(hintLabel);

        // 只列「还能附魔」的牌（已附魔的不显示，选了也没用）
        List<RunCard> open = new List<RunCard>();
        for (int i = 0; i < state.Deck.Count; i++)
        {
            if (state.Deck[i].Enchant == EnchantType.None)
            {
                open.Add(state.Deck[i]);
            }
        }

        if (open.Count == 0)
        {
            Label none = UiKit.MakeLabel("牌组里已经没有能附魔的牌了", 14, UiKit.Dim);
            UiKit.Place(none, 0f, 120f, W, 30f, HorizontalAlignment.Center);
            _body.AddChild(none);
        }

        for (int i = 0; i < open.Count && i < 15; i++)
        {
            RunCard card = open[i];
            bool picked = _enchantPicked == card;
            int col = i % 5;
            int row = i / 5;
            float x = 108f + col * 152f;
            float y = 74f + row * 84f;

            Panel frame = new Panel();
            frame.Position = new Vector2(x, y);
            frame.Size = new Vector2(144f, 76f);
            frame.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.105f, 0.092f, 0.074f),
                    picked ? UiKit.Gold : new Color(0.300f, 0.250f, 0.180f), picked ? 3 : 1, 3));
            _body.AddChild(frame);

            Label name = UiKit.MakeLabel(card.CardName, 14, UiKit.Paper);
            UiKit.Place(name, 0f, 6f, 144f, 20f, HorizontalAlignment.Center);
            frame.AddChild(name);

            Label tier = UiKit.MakeLabel(CardTierRules.Name(card.Tier) + "阶　" + CardStatLine(card),
                11, TierColor(card.Tier));
            UiKit.Place(tier, 0f, 26f, 144f, 18f, HorizontalAlignment.Center);
            frame.AddChild(tier);

            RunCard target = card;
            Button pick = UiKit.MakeButton(picked ? "已选" : "选它", false);
            pick.Position = new Vector2(22f, 46f);
            pick.Size = new Vector2(100f, 24f);
            pick.Pressed += () =>
            {
                _enchantPicked = target;
                Refresh();
            };
            frame.AddChild(pick);
        }

        if (open.Count > 15)
        {
            Label more = UiKit.MakeLabel("（还有 " + (open.Count - 15) + " 张没显示）", 11, UiKit.Dim);
            UiKit.Place(more, 0f, 330f, W, 18f, HorizontalAlignment.Center);
            _body.AddChild(more);
        }

        EnchantType[] types =
        {
            EnchantType.Sharp, EnchantType.Swift, EnchantType.Sturdy, EnchantType.Venom,
        };

        for (int i = 0; i < types.Length; i++)
        {
            EnchantType type = types[i];
            float x = 108f + i * 190f;

            Panel box = new Panel();
            box.Position = new Vector2(x, 352f);
            box.Size = new Vector2(176f, 92f);
            box.AddThemeStyleboxOverride("panel",
                UiKit.Box(new Color(0.100f, 0.088f, 0.070f), UiKit.Gold, 1, 3));
            _body.AddChild(box);

            Label eName = UiKit.MakeLabel(RunState.EnchantName(type), 17, UiKit.Gold);
            UiKit.Place(eName, 0f, 6f, 176f, 24f, HorizontalAlignment.Center);
            box.AddChild(eName);

            Label eDesc = UiKit.MakeLabel(RunState.EnchantDesc(type), 11, UiKit.PaperDim);
            UiKit.Place(eDesc, 0f, 30f, 176f, 18f, HorizontalAlignment.Center);
            box.AddChild(eDesc);

            Button apply = UiKit.MakeButton("淬它", false);
            apply.Position = new Vector2(38f, 54f);
            apply.Size = new Vector2(100f, 28f);
            apply.Disabled = state.FreeEnchantUsed || _enchantPicked == null;
            apply.Pressed += () =>
            {
                if (state.ApplyEnchant(_enchantPicked, type))
                {
                    _enchantPicked = null;
                }
                Refresh();
            };
            box.AddChild(apply);
        }
    }

    private void BuildPlaceholder(DayPhase phase)
    {
        string name = PhaseTypeExtensions.Name(phase.Type);
        Label title = UiKit.MakeLabel(name + "　尚未实装", 20, UiKit.PaperDim);
        UiKit.Place(title, 0f, 100f, W, 32f, HorizontalAlignment.Center);
        _body.AddChild(title);

        Label note = UiKit.MakeLabel(
            phase.Type == PhaseType.Event
                ? "奇遇要配事件表（师门传授 / 魔功捷径 / 异宝现世），表还没有。"
                : "淬炉是免费附魔点（锋锐 / 疾风 / 厚土 / 蚀骨），界面还没有。",
            12, UiKit.Dim);
        UiKit.Place(note, 0f, 142f, W, 24f, HorizontalAlignment.Center);
        _body.AddChild(note);

        Button leave = UiKit.MakeButton("跳　过", true);
        leave.Position = new Vector2(400f, 200f);
        leave.Size = new Vector2(160f, 42f);
        leave.Pressed += OnLeavePhase;
        _body.AddChild(leave);
    }

    private void BuildPassed(DayPhase phase)
    {
        Label title = UiKit.MakeLabel("这一时辰已空过", 20, UiKit.PaperDim);
        UiKit.Place(title, 0f, 120f, W, 32f, HorizontalAlignment.Center);
        _body.AddChild(title);

        Button next = UiKit.MakeButton("继　续", true);
        next.Position = new Vector2(400f, 196f);
        next.Size = new Vector2(160f, 42f);
        next.Pressed += OnLeavePhase;
        _body.AddChild(next);
    }

    private void BuildOutcome(RunState state)
    {
        string titleText = state.IsWon ? "十 胜 达 成" : "一 局 结 束";
        Label title = UiKit.MakeLabel(titleText, 28, state.IsWon ? UiKit.Gold : UiKit.Vermilion);
        UiKit.Place(title, 0f, 90f, W, 40f, HorizontalAlignment.Center);
        _body.AddChild(title);

        string body = "第 " + state.Day + " 天　胜 " + state.Victories + "/" + RunState.TargetVictories +
            "　银两 " + state.Gold + "　牌组 " + state.Deck.Count + " 张";
        Label detail = UiKit.MakeLabel(body, 14, UiKit.Paper);
        UiKit.Place(detail, 0f, 142f, W, 26f, HorizontalAlignment.Center);
        _body.AddChild(detail);

        Button again = UiKit.MakeButton("再 走 一 趟", true);
        again.Position = new Vector2(300f, 210f);
        again.Size = new Vector2(160f, 44f);
        again.Pressed += OnRestart;
        _body.AddChild(again);

        Button menu = UiKit.MakeButton("回 主 菜 单", false);
        menu.Position = new Vector2(500f, 210f);
        menu.Size = new Vector2(160f, 44f);
        menu.Pressed += OnBackToMenu;
        _body.AddChild(menu);
    }

    // —————————————————————— 通用操作 ——————————————————————

    private void OnLeavePhase()
    {
        AudioManager.PlayUISFX("ui_click");
        _shopMode = 0;
        RunManager.Instance.ResolveCurrentPhase(true);
        Refresh();
    }

    private void OnRestart()
    {
        AudioManager.PlayUISFX("ui_click");
        RunManager.Instance.StartRun(GameFlow.SelectedCharacterId, 0);
        Refresh();
    }

    private void OnBackToMenu()
    {
        AudioManager.PlayUISFX("ui_close");
        RunManager.Instance.AbandonRun();
        UIManager.ShowPanel<MainMenuPanel>();
        UIManager.HidePanel<RunPanel>();
    }

    // —————————————————————— 文本与配色 ——————————————————————

    private static Color TierColor(CardTier tier)
    {
        if (tier == CardTier.Copper)
        {
            return new Color(0.700f, 0.440f, 0.280f);
        }
        if (tier == CardTier.Silver)
        {
            return new Color(0.720f, 0.740f, 0.780f);
        }
        if (tier == CardTier.Gold)
        {
            return new Color(0.840f, 0.690f, 0.340f);
        }
        return new Color(0.440f, 0.750f, 0.840f);
    }

    private static string EnchantName(EnchantType type)
    {
        if (type == EnchantType.Sharp)
        {
            return "锋锐";
        }
        if (type == EnchantType.Swift)
        {
            return "疾风";
        }
        if (type == EnchantType.Sturdy)
        {
            return "厚土";
        }
        if (type == EnchantType.Venom)
        {
            return "蚀骨";
        }
        return "—";
    }

    private static string ScopeWord(TimingScope scope)
    {
        if (scope == TimingScope.Foe)
        {
            return "敌";
        }
        if (scope == TimingScope.Both)
        {
            return "双";
        }
        return "我";
    }

    /// <summary>关键词行：消耗 / 移除 / 贯穿 / 多重。没有关键词就返回空串。</summary>
    private static string KeywordLine(RunCard card)
    {
        CardConfig cfg = card.Config;
        if (cfg == null)
        {
            return "";
        }

        List<string> parts = new List<string>();
        if (cfg.HasKeyword(CardKeyword.Exhaust))
        {
            parts.Add("消耗");
        }
        if (cfg.HasKeyword(CardKeyword.Remove))
        {
            parts.Add("移除");
        }
        if (cfg.HasKeyword(CardKeyword.Pierce))
        {
            parts.Add("贯穿");
        }
        if (parts.Count == 0)
        {
            return "";
        }

        string line = "";
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                line += " · ";
            }
            line += parts[i];
        }
        return line;
    }

    /// <summary>货架卡的效果行：用 RunCard 的按阶数值，不能读 CardConfig 的原始值。</summary>
    private static string EffectLine(RunCard card)
    {
        List<string> parts = new List<string>();

        if (card.Damage > 0)
        {
            parts.Add("攻 " + card.Damage);
        }
        if (card.Spell > 0)
        {
            parts.Add("法 " + card.Spell);
        }
        if (card.Shield > 0)
        {
            parts.Add("护 " + card.Shield);
        }
        if (card.Heal > 0)
        {
            parts.Add("疗 " + card.Heal);
        }
        if (card.Poison > 0)
        {
            parts.Add("毒 " + card.Poison);
        }

        CardConfig cfg = card.Config;
        if (cfg != null)
        {
            if (cfg.Expose > 0)
            {
                parts.Add("破绽 " + cfg.Expose);
            }
            if (cfg.MultiCount > 1)
            {
                parts.Add("×" + cfg.MultiCount + " 次");
            }
            if (cfg.HandSelf != 0)
            {
                parts.Add("上场 " + (cfg.HandSelf > 0 ? "+" : "") + cfg.HandSelf);
            }
            if (cfg.HandFoe != 0)
            {
                parts.Add("敌上场 " + (cfg.HandFoe > 0 ? "+" : "") + cfg.HandFoe);
            }
            string scope = ScopeWord(cfg.Scope);
            if (cfg.FreezeTime > 0f)
            {
                parts.Add(scope + "冰冻 " + cfg.FreezeTime.ToString("0.#") + " 秒");
            }
            if (cfg.HasteTime > 0f)
            {
                parts.Add(scope + "加速 " + cfg.HasteTime.ToString("0.#") + " 秒");
            }
            if (cfg.SlowTime > 0f)
            {
                parts.Add(scope + "减速 " + cfg.SlowTime.ToString("0.#") + " 秒");
            }
            if (cfg.ChargeCount > 0)
            {
                parts.Add("充能 " + cfg.ChargeCount + " 张");
            }
        }

        if (parts.Count == 0)
        {
            return "—";
        }

        string line = "";
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                line += "　";
            }
            line += parts[i];
        }
        return line;
    }
}
