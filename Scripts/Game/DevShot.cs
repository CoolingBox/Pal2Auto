// 文件：DevShot.cs
// 描述：开发期截图开关 —— 带 --shot=秒数 启动，到点截一张图存到 user:// 然后退出。
//       用途：改完界面立刻自证"画面非空白、没有报错"，不靠猜。

using System;
using System.Collections.Generic;
using Godot;
using TinyFramework;

public static class DevShot
{
    /// <summary>命令行里有没有这个参数。调试开关散在各面板时，统一走这里查。</summary>
    public static bool HasArg(string key)
    {
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == key)
            {
                return true;
            }
        }
        return false;
    }

    public static void RunIfRequested(Node host)
    {
        string[] all = OS.GetCmdlineArgs();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == "--ui=select")
            {
                OpenSelectLater(host);
            }
            if (all[i] == "--ui=settings")
            {
                OpenSettingsLater(host);
            }
            if (all[i] == "--ui=battle")
            {
                OpenBattleLater(host);
            }
            if (all[i] == "--ui=run")
            {
                OpenRunLater(host, "free");
            }
            if (all[i] == "--ui=runshop")
            {
                OpenRunLater(host, "shop");
            }
            if (all[i] == "--ui=runevent")
            {
                OpenRunLater(host, "event");
            }
            if (all[i] == "--ui=runfight")
            {
                OpenRunLater(host, "fight");
            }
            if (all[i] == "--ui=runremove")
            {
                OpenRunLater(host, "remove");
            }
            if (all[i] == "--ui=runlevel")
            {
                OpenRunLater(host, "level");
            }
            if (all[i] == "--ui=runlevel8")
            {
                OpenRunLater(host, "level8");
            }
            if (all[i] == "--ui=runbattle")
            {
                OpenRunLater(host, "battle");
            }
            if (all[i] == "--clicktest")
            {
                RunClickTest(host);
            }
            if (all[i] == "--clicklog")
            {
                RunLogClickTest(host);
            }
            if (all[i] == "--resetdeck")
            {
                // 「移除」关键字会永久扣牌库，调试时用它复原
                DeckStore.ResetAll();
                GD.Print("RESETDECK 已清空永久移除记录");
            }
            if (all[i].StartsWith("--sim="))
            {
                RunSim(all[i].Substring(6), host);
            }
        }

        float seconds = 2f;
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--shot="))
            {
                float parsed;
                if (float.TryParse(args[i].Substring(7), out parsed))
                {
                    seconds = parsed;
                }
            }
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--shot" || args[i].StartsWith("--shot="))
            {
                Capture(host, seconds);
                return;
            }
        }
    }

    /// <summary>用真实输入事件走一遍：开局 → 选王小虎 → 就他了。位置按 2 倍放大换算。</summary>
    private static async void RunClickTest(Node host)
    {
        await host.ToSignal(host.GetTree().CreateTimer(0.6f), SceneTreeTimer.SignalName.Timeout);
        await ClickAt(host, new Vector2(328f, 472f));
        await host.ToSignal(host.GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        await ClickAt(host, new Vector2(198f, 356f));
        await host.ToSignal(host.GetTree().CreateTimer(0.4f), SceneTreeTimer.SignalName.Timeout);
        await ClickAt(host, new Vector2(1654f, 904f));
        GD.Print("CLICKTEST 已点击：开局 / 第一张卡 / 就他了");
    }

    private static async System.Threading.Tasks.Task ClickAt(Node host, Vector2 position)
    {
        InputEventMouseMotion motion = new InputEventMouseMotion();
        motion.Position = position;
        Input.ParseInputEvent(motion);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        InputEventMouseButton down = new InputEventMouseButton();
        down.ButtonIndex = MouseButton.Left;
        down.Pressed = true;
        down.Position = position;
        Input.ParseInputEvent(down);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        InputEventMouseButton up = new InputEventMouseButton();
        up.ButtonIndex = MouseButton.Left;
        up.Pressed = false;
        up.Position = position;
        Input.ParseInputEvent(up);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>真实鼠标点一遍：进战斗 → 开交战记录 → 切统计页。</summary>
    private static async void RunLogClickTest(Node host)
    {
        await host.ToSignal(host.GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);

        GameFlow.SelectedCharacterId = ReadCharId();
        TinyFramework.UIManager.ShowPanel<BattlePanel>();
        TinyFramework.UIManager.HidePanel<MainMenuPanel>();

        await host.ToSignal(host.GetTree().CreateTimer(1.2f), SceneTreeTimer.SignalName.Timeout);

        Vector2 scale = host.GetViewport().GetVisibleRect().Size / new Vector2(960f, 540f);

        await ClickAt(host, new Vector2(872f, 21f) * scale);
        await host.ToSignal(host.GetTree().CreateTimer(0.4f), SceneTreeTimer.SignalName.Timeout);

        await ClickAt(host, new Vector2(164f, 103f) * scale);
        GD.Print("CLICKLOG 已点击：交战记录开关 / 统计页");
    }

    /// <summary>
    /// 无头批量对局：--sim=20 打 20 场，只打印胜负与用时。
    /// 用来在没人陪玩时量一下改动对胜率的影响，别靠感觉调数值。
    /// </summary>
    private static void RunSim(string raw, Node host)
    {
        int rounds;
        if (!int.TryParse(raw, out rounds) || rounds <= 0)
        {
            rounds = 10;
        }

        int charId = ReadCharId();
        CharacterConfig character = ConfigManager.GetOne<CharacterConfig>(charId);
        // --simenemy=N 指定打哪只怪（默认 1 号），用来比较同一天三档候选的难度差
        int enemyId = ReadIntArg("--simenemy=", 1);
        EnemyConfig enemy = ConfigManager.GetOne<EnemyConfig>(enemyId);
        if (character == null || enemy == null)
        {
            GD.Print("SIM 配置缺失 charId=" + charId);
            System.Console.Out.Flush();
            host.GetTree().Quit(1);
            return;
        }

        // --simhp=N 临时覆盖敌方气血：扫平衡曲线时不用反复改 EnemyConfig.json
        int enemyHp = ReadIntArg("--simhp=", enemy.Hp);
        // --simmyhp=N 覆盖我方气血（默认取 CharacterConfig.Hp）
        int playerHp = ReadIntArg("--simmyhp=", character.Hp > 0 ? character.Hp : 200);
        bool logEach = ReadIntArg("--simlog=", 0) > 0;
        // --simhand=N 覆盖上场数（模拟阅历升到 2/4/6 级之后的 6/7/8 槽）
        int playerSlots = ReadIntArg("--simhand=", 0);

        // --simdeck=143,143,... 用指定牌组顶掉角色自带牌组（测「买了几张之后能不能过」）
        List<int> playerDeck = new List<int>(character.Deck);
        string deckArg = ReadStringArg("--simdeck=", "");
        if (deckArg.Length > 0)
        {
            playerDeck.Clear();
            string[] ids = deckArg.Split(',');
            for (int i = 0; i < ids.Length; i++)
            {
                int cardId;
                if (int.TryParse(ids[i], out cardId))
                {
                    playerDeck.Add(cardId);
                }
            }
        }

        // --simtier=N 统一给牌组定一个阶位（0 铜 / 1 银 / 2 金 / 3 钻）。
        // 这是模拟「后期构筑起来」的唯一办法：不指定就是铜，跟以前的模拟等价。
        int deckTierArg = ReadIntArg("--simtier=", 0);
        CardTier deckTier = CardTier.Copper;
        if (deckTierArg >= 1 && deckTierArg <= 3)
        {
            deckTier = (CardTier)deckTierArg;
        }
        List<CardConfig> battleCards = new List<CardConfig>();
        for (int i = 0; i < playerDeck.Count; i++)
        {
            battleCards.Add(new RunCard(playerDeck[i], deckTier).ToBattleConfig());
        }

        // --simpriority=1 给牌组第一张打「先发」，统计它进起手的比例（对照默认随机）
        bool testPriority = ReadIntArg("--simpriority=", 0) > 0;
        if (testPriority && battleCards.Count > 0)
        {
            battleCards[0].PriorityDraw = true;
        }
        int priorityHits = 0;

        int wins = 0;
        float totalTime = 0f;
        int maxHand = DeckBattle.HandBase;
        int minHand = DeckBattle.HandBase;

        for (int round = 0; round < rounds; round++)
        {
            DeckBattle battle = DeckBattle.CreateWithCards(character.Name, character.Portrait, playerHp,
                new List<CardConfig>(battleCards), enemy.Name, enemy.Portrait, enemyHp, enemy.Deck, playerSlots);

            // --simpoison=N 开局给玩家叠 N 层毒（验「治疗带解毒」）
            int startPoison = ReadIntArg("--simpoison=", 0);
            if (startPoison > 0)
            {
                battle.Player.PoisonStacks = startPoison;
            }

            // --simstorm=1 打印沙尘暴的头几跳，验证「起始 1 伤、每跳 +1」
            bool traceStorm = ReadIntArg("--simstorm=", 0) > 0 || ReadIntArg("--simpoison=", 0) > 0;
            int stormPrinted = 0;
            if (testPriority && battleCards.Count > 0)
            {
                for (int h = 0; h < battle.Player.Hand.Length; h++)
                {
                    if (battle.Player.Hand[h].Card == battleCards[0])
                    {
                        priorityHits++;
                        break;
                    }
                }
            }

            while (!battle.Finished)
            {
                List<BattleEvent> stepEvents = battle.Step(0.05f);
                if (traceStorm && stormPrinted < 5)
                {
                    for (int e = 0; e < stepEvents.Count; e++)
                    {
                        if (stepEvents[e].Text != null && stepEvents[e].Text.StartsWith("化毒"))
                        {
                            GD.Print("CURE " + stepEvents[e].Text);
                            stormPrinted++;
                        }
                    }
                }
            }

            if (battle.PlayerWon)
            {
                wins++;
            }
            totalTime += battle.Elapsed;

            int peak = Mathf.Max(battle.Player.HandCapacity, battle.Enemy.HandCapacity);
            if (peak > maxHand)
            {
                maxHand = peak;
            }
            if (peak < minHand)
            {
                minHand = peak;
            }

            // --simlog=1：逐场打印残局，用来判断「输是因为打不动还是挨不住」
            if (logEach)
            {
                GD.Print("  R" + round + " 敌余 " + Mathf.RoundToInt(battle.Enemy.Hp) + " / 我余 " +
                         Mathf.RoundToInt(battle.Player.Hp) + " / " + battle.Elapsed.ToString("0.0") + " 秒");
            }
        }

        GD.Print("SIM " + character.Name + " vs " + enemy.Name + "(敌 " + enemyHp + " / 我 " + playerHp +
                 " 血)：" + rounds + " 场 " + wins + " 胜（" + Mathf.RoundToInt(100f * wins / rounds) +
                 "%），均时 " + (totalTime / rounds).ToString("0.0") + " 秒，上场数区间 " + minHand + "~" + maxHand);
        if (testPriority)
        {
            GD.Print("PRIORITY 起手含先发卡 " + priorityHits + "/" + rounds +
                "（" + Mathf.RoundToInt(100f * priorityHits / rounds) + "%）");
        }

        // headless 下 Quit 紧跟 print 会丢掉标准输出缓冲，重定向到文件时看不到结果
        System.Console.Out.Flush();
        host.GetTree().Quit(0);
    }

    /// <summary>局内面板的三个状态：自由时辰三选一 / 集市货架 / 遭遇时辰。</summary>
    private static async void OpenRunLater(Node host, string mode)
    {
        GameFlow.SelectedCharacterId = ReadCharId();
        GameFlow.InRun = true;

        await host.ToSignal(host.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);

        RunManager run = RunManager.Instance;
        run.StartRun(GameFlow.SelectedCharacterId, 0);

        // --buff=N 给牌组每张牌永久 +N 伤害（验「事件改单卡」是否真的进了战斗）
        int buffDamage = ReadIntArg("--buff=", 0);
        if (buffDamage != 0)
        {
            for (int i = 0; i < run.Current.Deck.Count; i++)
            {
                RunCard card = run.Current.Deck[i];
                int before = card.Damage;
                run.Current.BuffCard(card, CardStat.Damage, buffDamage);
                GD.Print("BUFF " + card.CardName + " 伤害 " + before + " → " + card.Damage);
            }
        }

        // --day=N 直接跳到第 N 天（验后期敌血曲线 / 界面对不对）
        int dayOverride = ReadIntArg("--day=", 0);
        if (dayOverride > 0)
        {
            run.Current.Day = dayOverride;
        }

        // --forgestat=N 连开 N 次百炼台，统计每个选项的出现频率（验权重）
        int forgeStat = ReadIntArg("--forgestat=", 0);
        if (forgeStat > 0)
        {
            RunState st = run.Current;
            Dictionary<ForgeOptionKind, int> counter = new Dictionary<ForgeOptionKind, int>();
            for (int i = 0; i < forgeStat; i++)
            {
                st.PendingForge = null;
                st.ForgePhaseIndex = -1;
                st.OpenForgeEvent(0);
                if (st.PendingForge == null)
                {
                    continue;
                }
                for (int k = 0; k < st.PendingForge.Options.Count; k++)
                {
                    ForgeOptionKind kind = st.PendingForge.Options[k].Kind;
                    if (!counter.ContainsKey(kind))
                    {
                        counter[kind] = 0;
                    }
                    counter[kind]++;
                }
            }

            GD.Print("FORGESTAT 共 " + forgeStat + " 次，每次 3 个选项，合计 " + (forgeStat * 3));
            foreach (KeyValuePair<ForgeOptionKind, int> pair in counter)
            {
                GD.Print("FORGESTAT " + pair.Key + " " + pair.Value + " 次（" +
                    (100f * pair.Value / (forgeStat * 3f)).ToString("0.0") + "%）");
            }
            System.Console.Out.Flush();
            host.GetTree().Quit(0);
            return;
        }

        // --forgeact=merge|sharp|quick|multi 直接跑一次百炼台结算（不依赖 UI），验数值改动
        string forgeAct = ReadStringArg("--forgeact=", "");
        if (forgeAct.Length > 0)
        {
            RunState st = run.Current;
            st.OpenForgeEvent(0);
            ForgeEvent forge = st.PendingForge;
            GD.Print("FORGE 牌库 " + st.Deck.Count + " 张，候选 " + (forge == null ? -1 : forge.Candidates.Count));

            if (forge != null && forge.Candidates.Count >= 2)
            {
                RunCard a = forge.Candidates[0];
                RunCard b = forge.Candidates[1];
                GD.Print("FORGE 前 " + a.CardName + " 攻 " + a.Damage + " 蓄 " + a.Countdown.ToString("0.0"));
                GD.Print("FORGE 前 " + b.CardName + " 攻 " + b.Damage + " 蓄 " + b.Countdown.ToString("0.0"));

                ForgeOption opt = new ForgeOption();
                opt.Kind = ForgeOptionKind.Merge;
                opt.TargetCount = 2;
                if (forgeAct == "sharp")
                {
                    opt.Kind = ForgeOptionKind.Sharp;
                    opt.TargetCount = 1;
                }
                else if (forgeAct == "quick")
                {
                    opt.Kind = ForgeOptionKind.QuickDraw;
                    opt.TargetCount = 1;
                }
                else if (forgeAct == "multi")
                {
                    opt.Kind = ForgeOptionKind.Multi;
                    opt.TargetCount = 1;
                }
                else if (forgeAct == "transmute")
                {
                    opt.Kind = ForgeOptionKind.Transmute;
                    opt.TargetCount = 1;
                }
                else if (forgeAct == "greed")
                {
                    opt.Kind = ForgeOptionKind.Greed;
                    opt.TargetCount = 0;
                }
                else if (forgeAct == "exile")
                {
                    opt.Kind = ForgeOptionKind.Exile;
                    opt.TargetCount = 1;
                }

                List<RunCard> targets = new List<RunCard>();
                targets.Add(a);
                if (opt.TargetCount == 2)
                {
                    targets.Add(b);
                }

                bool ok = st.ApplyForgeOption(opt, targets);
                string lastNote = st.Notes.Count > 0 ? st.Notes[st.Notes.Count - 1] : "（无记录）";
                GD.Print("FORGE " + forgeAct + " 成功=" + ok + "，牌库 " + st.Deck.Count + " 张");
                GD.Print("FORGE 记录：" + lastNote);
                if (ok && st.Deck.Count > 0)
                {
                    RunCard shown = st.Deck[0];
                    GD.Print("FORGE 牌组首张：" + shown.CardName + " 攻 " + shown.Damage +
                        " 蓄 " + shown.Countdown.ToString("0.0"));
                }
            }
        }

        if (mode == "shop")
        {
            run.SelectFreePhase(PhaseType.Shop);
        }
        else if (mode == "event")
        {
            run.SelectFreePhase(PhaseType.Event);
        }
        else if (mode == "remove")
        {
            // 删牌列表是集市里的子视图，得先开店
            run.SelectFreePhase(PhaseType.Shop);
        }
        else if (mode == "fight")
        {
            // 推到第 3 时辰（固定遭遇）：前两个自由时辰各选一样并结算
            run.SelectFreePhase(PhaseType.Shop);
            run.ResolveCurrentPhase(true);
            run.SelectFreePhase(PhaseType.Forge);
            run.ResolveCurrentPhase(true);
        }
        else if (mode == "level")
        {
            // 升一级，停在「三选一奖励」界面
            run.Current.GainExperience(RunState.ExpPerLevel);
        }
        else if (mode == "level8")
        {
            // 连升到 8 级：看满级槽位 / 气血，也顺手把三选一选掉
            while (run.Current.Level < 8)
            {
                run.Current.GainExperience(RunState.ExpPerLevel);
                if (run.Current.PendingRewards.Count > 0)
                {
                    run.Current.ApplyReward(run.Current.PendingRewards[0]);
                }
            }
        }

        if (mode == "remove")
        {
            GameFlow.PendingOpenRemoval = true;
        }

        TinyFramework.UIManager.ShowPanel<RunPanel>();
        TinyFramework.UIManager.HidePanel<MainMenuPanel>();

        if (mode == "remove")
        {
            GameFlow.PendingOpenRemoval = true;
        }

        // 局内直接开战：验证「战斗吃的是 RunState 的等级气血 + 当天敌血」
        if (mode == "battle")
        {
            DayPhase phase = run.CurrentPhase;
            int enemyHp = RunState.EncounterHp(run.Current.Day, 1);
            if (phase != null && phase.Type == PhaseType.Nemesis)
            {
                enemyHp = RunState.NemesisHp(run.Current.Day);
            }
            GameFlow.PendingEnemyHp = enemyHp;
            TinyFramework.UIManager.ShowPanel<BattlePanel>();
            TinyFramework.UIManager.HidePanel<RunPanel>();
        }
    }

    private static async void OpenBattleLater(Node host)
    {
        GameFlow.SelectedCharacterId = ReadCharId();
        await host.ToSignal(host.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
        TinyFramework.UIManager.ShowPanel<BattlePanel>();
        TinyFramework.UIManager.HidePanel<MainMenuPanel>();
    }

    /// <summary>--char=N 指定进战斗时用哪位侠客（截不同牌组用）。</summary>
    private static int ReadCharId()
    {
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--char="))
            {
                int parsed;
                if (int.TryParse(args[i].Substring(7), out parsed))
                {
                    return parsed;
                }
            }
        }
        return 1;
    }

    /// <summary>读 `--xxx=123` 形式的整数参数，缺省时返回 fallback。</summary>
    private static int ReadIntArg(string prefix, int fallback)
    {
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(prefix))
            {
                int parsed;
                if (int.TryParse(args[i].Substring(prefix.Length), out parsed) && parsed > 0)
                {
                    return parsed;
                }
            }
        }
        return fallback;
    }

    /// <summary>读 `--xxx=abc` 形式的字符串参数，缺省时返回 fallback。</summary>
    private static string ReadStringArg(string prefix, string fallback)
    {
        string[] args = OS.GetCmdlineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(prefix))
            {
                return args[i].Substring(prefix.Length);
            }
        }
        return fallback;
    }

    private static async void OpenSettingsLater(Node host)
    {
        await host.ToSignal(host.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
        TinyFramework.UIManager.ShowPanel<SettingsPanel>();
    }

    private static async void OpenSelectLater(Node host)
    {
        await host.ToSignal(host.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
        TinyFramework.UIManager.ShowPanel<CharacterSelectPanel>();
        TinyFramework.UIManager.HidePanel<MainMenuPanel>();
    }

    private static async void Capture(Node host, float seconds)
    {
        await host.ToSignal(host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        Image image = host.GetViewport().GetTexture().GetImage();
        string path = "user://shot_" + DateTime.Now.ToString("HHmmss") + ".png";
        Error error = image.SavePng(path);
        GD.Print("SHOT " + (error == Error.Ok ? "已保存 " : "保存失败 ") + ProjectSettings.GlobalizePath(path) +
                 "（FPS " + Engine.GetFramesPerSecond() + "）");

        host.GetTree().Quit(0);
    }
}
