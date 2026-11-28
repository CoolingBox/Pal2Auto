// 文件：RunSimulator.cs
// 描述：一局的自动推演。只在 `--runlog` 调试开关下运行，用来验证流程不会卡住、经济曲线是否合理。
//
// 这不是 AI，是一个「按最朴素策略也能把一局走完」的探针：
//   自由时辰 → 一律选集市 → 买一张买得起的最贵卡 → 用完免费刷新再买一次
//   遭遇/宿敌 → 按固定胜率掷骰
//   每个时辰结束后尝试合成
//
// 它回答三个问题：流程能不能跑完、银两最后剩多少、货架的阶位分布是否符合 Designated 概率。

using System.Collections.Generic;
using TinyFramework;

public static class RunSimulator
{
    /// <summary>固定的战斗胜率。要改平衡就先改这里再跑，别去猜。</summary>
    public const int DefaultWinPercent = 65;

    /// <summary>
    /// 牌组上限。超过就不再买 —— 不是为了模拟「理性玩家」，而是牌组太厚会让抽到关键牌的概率被稀释，
    /// 那已经不是构筑问题而是「什么都往兜里装」的问题。
    /// </summary>
    public const int DeckCap = 30;

    private sealed class Result
    {
        public int CharacterId;
        public int Days;
        public int Victories;
        public int Gold;
        public int DeckSize;
        public bool Won;
        public bool Stuck;
        public int Merges;
        public List<RunCard> FinalDeck = new List<RunCard>();
    }

    /// <summary>跑 count 局，把结果打到控制台。characterId 为 0 表示四个主角各跑一遍。</summary>
    public static void Run(int characterId, int count, int winPercent)
    {
        List<Result> results = new List<Result>();

        if (characterId <= 0)
        {
            for (int id = 1; id <= 4; id++)
            {
                results.Add(Simulate(id, 1000 + id, winPercent));
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                results.Add(Simulate(characterId, 2000 + i, winPercent));
            }
        }

        Print(results);
    }

    private static Result Simulate(int characterId, int seed, int winPercent)
    {
        Result result = new Result();
        result.CharacterId = characterId;

        RunManager manager = RunManager.Instance;
        RunState state = manager.StartRun(characterId, seed);

        // 每一步只处理一个时辰，最多 200 步（远超 15 天 ×6，卡住时能看出来）
        int guard = 0;
        while (!state.IsOver && guard < 200)
        {
            guard++;
            DayPhase phase = state.Today.Current;
            if (phase == null)
            {
                result.Stuck = true;
                break;
            }

            if (phase.Type == PhaseType.Free)
            {
                PlayFreePhase(state, manager);
            }
            else
            {
                // 遭遇奖励按品阶发，所以要先 roll 今天打的是哪一档（宿敌固定最高档）
                if (phase.Type == PhaseType.Encounter)
                {
                    state.EncounterTier = RollEncounterTier(state);
                }
                else
                {
                    state.EncounterTier = HighestTierOfDay(state.Day);
                }

                bool success = state.Rng.Next(100) < winPercent;
                manager.ResolveCurrentPhase(success);
            }

            result.Merges += MergeEverything(state);
        }

        result.Days = state.Day;
        result.Victories = state.Victories;
        result.Gold = state.Gold;
        result.DeckSize = state.Deck.Count;
        result.Won = state.IsWon;
        result.Stuck = result.Stuck || guard >= 200;
        result.FinalDeck = state.Deck;
        return result;
    }

    /// <summary>自由时辰：选集市，买一张，用完免费刷新再买一次。</summary>
    private static void PlayFreePhase(RunState state, RunManager manager)
    {
        manager.SelectFreePhase(PhaseType.Shop);

        BuyBest(state);
        int cost = state.Shop.NextRefreshCost;
        if (cost == 0)
        {
            state.TryRefreshShop();
            BuyBest(state);
        }

        // 牌组太厚就花钱删掉最弱的一张（删牌是最直接的硬 sink）
        TrimDeck(state);

        manager.ResolveCurrentPhase(true);
    }

    /// <summary>
    /// 按天 roll 一个遭遇品阶。概率与 Docs/局内流程设计.md 第五节那张表一致 ——
    /// 这里只服务经济模拟（银两/阅历按品阶发），不影响战斗强度（那是 EnemyHpConfig 的事）。
    /// </summary>
    private static EnemyTier RollEncounterTier(RunState state)
    {
        int day = state.Day;
        EnemyTier[] pool;

        if (day <= 2)
        {
            pool = new EnemyTier[] { EnemyTier.Bronze, EnemyTier.Bronze, EnemyTier.Bronze,
                EnemyTier.Bronze, EnemyTier.Bronze, EnemyTier.Bronze, EnemyTier.Bronze,
                EnemyTier.Silver, EnemyTier.Silver, EnemyTier.Silver };
        }
        else if (day <= 3)
        {
            pool = new EnemyTier[] { EnemyTier.Bronze, EnemyTier.Bronze, EnemyTier.Bronze, EnemyTier.Bronze,
                EnemyTier.Silver, EnemyTier.Silver, EnemyTier.Silver, EnemyTier.Gold, EnemyTier.Gold,
                EnemyTier.Silver };
        }
        else if (day <= 5)
        {
            pool = new EnemyTier[] { EnemyTier.Bronze, EnemyTier.Bronze, EnemyTier.Bronze,
                EnemyTier.Silver, EnemyTier.Silver, EnemyTier.Silver, EnemyTier.Silver,
                EnemyTier.Gold, EnemyTier.Gold, EnemyTier.Diamond };
        }
        else if (day <= 7)
        {
            pool = new EnemyTier[] { EnemyTier.Silver, EnemyTier.Silver, EnemyTier.Silver, EnemyTier.Silver,
                EnemyTier.Silver, EnemyTier.Gold, EnemyTier.Gold, EnemyTier.Gold, EnemyTier.Gold,
                EnemyTier.Diamond };
        }
        else if (day <= 10)
        {
            pool = new EnemyTier[] { EnemyTier.Silver, EnemyTier.Gold, EnemyTier.Gold, EnemyTier.Gold,
                EnemyTier.Gold, EnemyTier.Diamond, EnemyTier.Diamond, EnemyTier.Diamond,
                EnemyTier.Legend, EnemyTier.Legend };
        }
        else
        {
            pool = new EnemyTier[] { EnemyTier.Silver, EnemyTier.Gold, EnemyTier.Gold, EnemyTier.Gold,
                EnemyTier.Gold, EnemyTier.Gold, EnemyTier.Diamond, EnemyTier.Diamond,
                EnemyTier.Legend, EnemyTier.Legend };
        }

        return pool[state.Rng.Next(pool.Length)];
    }

    /// <summary>当天可能出现的最高品阶（宿敌用它，模拟里当近似）。</summary>
    private static EnemyTier HighestTierOfDay(int day)
    {
        if (day <= 2)
        {
            return EnemyTier.Silver;
        }
        if (day <= 3)
        {
            return EnemyTier.Gold;
        }
        if (day <= 7)
        {
            return EnemyTier.Diamond;
        }
        return EnemyTier.Legend;
    }

    /// <summary>
    /// 买卡：牌组到上限就停。只买买得起的最贵一张（阶位最高）。
    ///
    /// 不再优先「凑合成对」—— 分店制下商店不给配对了，硬凑只会让 AI 一直买铜卡，
    /// 测不出「高阶卡到底能不能到手」这个更关键的问题。
    /// </summary>
    private static void BuyBest(RunState state)
    {
        if (state.Deck.Count >= DeckCap)
        {
            return;
        }

        int bestIndex = -1;
        int bestTier = 0;

        for (int i = 0; i < state.Shop.Offers.Count; i++)
        {
            ShopOffer offer = state.Shop.Offers[i];
            if (offer.Sold)
            {
                continue;
            }
            if (!state.CanAfford(offer.Price))
            {
                continue;
            }

            int tier = (int)offer.Card.Tier;
            if (tier > bestTier)
            {
                bestTier = tier;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            state.BuyOffer(bestIndex);
        }
    }

    /// <summary>
    /// 牌组超过 26 张就付 60 两删掉最弱的一张，最多用满一局 5 次。
    /// 这是 AI 唯一会主动花钱的「非买卡」项目 —— 没有它，删牌这个 sink 在推演里根本不生效。
    /// </summary>
    private static void TrimDeck(RunState state)
    {
        int guard = 0;
        while (state.Deck.Count > 26 && state.CanBuyRemoval && guard < 5)
        {
            guard++;
            int worst = FindWeakest(state);
            if (worst < 0)
            {
                break;
            }
            if (!state.TryBuyRemoval(worst))
            {
                break;
            }
        }
    }

    /// <summary>牌组里阶位最低的那张（同阶取靠前的）。</summary>
    private static int FindWeakest(RunState state)
    {
        int worstIndex = -1;
        int worstTier = 99;

        for (int i = 0; i < state.Deck.Count; i++)
        {
            int tier = (int)state.Deck[i].Tier;
            if (tier < worstTier)
            {
                worstTier = tier;
                worstIndex = i;
            }
        }
        return worstIndex;
    }

    /// <summary>
    /// 花钱的 2 合 1 已取消（2026-11-27）：升阶只靠「再获得同名卡」与百炼台的合成选项，
    /// 模拟器不再执行这个动作，返回 0 只为不破坏调用处。
    /// </summary>
    private static int MergeEverything(RunState state)
    {
        return 0;
    }
    private static void Print(List<Result> results)
    {
        TinyLog.Info("RunLog", "=== 自动推演 " + results.Count + " 局，胜率参数 " + DefaultWinPercent + "% ===");

        int won = 0;
        int totalDays = 0;
        int totalGold = 0;
        int totalVictories = 0;
        int stuck = 0;
        int merges = 0;

        for (int i = 0; i < results.Count; i++)
        {
            Result r = results[i];
            TinyLog.Info("RunLog",
                "主角 " + r.CharacterId +
                " · 第 " + r.Days + " 天" +
                " · 胜 " + r.Victories +
                " · 余银 " + r.Gold +
                " · 牌组 " + r.DeckSize +
                (r.Won ? " · 通关" : " · 未完") +
                (r.Stuck ? " · 卡住" : ""));

            if (r.Won)
            {
                won++;
            }
            if (r.Stuck)
            {
                stuck++;
            }
            merges += r.Merges;
            totalDays += r.Days;
            totalGold += r.Gold;
            totalVictories += r.Victories;
        }

        if (results.Count > 0)
        {
            TinyLog.Info("RunLog",
                "平均：" + (totalDays / results.Count) + " 天 · " +
                (totalVictories / results.Count) + " 胜 · 余银 " + (totalGold / results.Count) +
                " · 通关率 " + (won * 100 / results.Count) + "%" +
                " · 卡住 " + stuck + " 局" +
                " · 平均合成 " + (merges / results.Count) + " 次/局");
        }

        PrintTierSample(results[0]);
    }

    /// <summary>把第一局的最终牌组按阶位分布打出来，便于确认升阶真的发生了。</summary>
    private static void PrintTierSample(Result result)
    {
        int[] counts = new int[5];
        for (int i = 0; i < result.FinalDeck.Count; i++)
        {
            counts[(int)result.FinalDeck[i].Tier]++;
        }

        TinyLog.Info("RunLog",
            "最终牌组阶位：铜 " + counts[1] + " / 银 " + counts[2] +
            " / 金 " + counts[3] + " / 钻 " + counts[4]);

        // 单卡均值（不看总分：总分会被张数带偏）。起始卡单独算一份，别让它把均值拖下去。
        float average = CardPowerUtility.Average(result.FinalDeck);
        float top;
        float low;
        CardPowerUtility.Range(result.FinalDeck, out top, out low);

        List<RunCard> grown = new List<RunCard>();
        for (int i = 0; i < result.FinalDeck.Count; i++)
        {
            CardConfig cfg = result.FinalDeck[i].Config;
            if (cfg != null && cfg.StarterOnly)
            {
                continue;
            }
            grown.Add(result.FinalDeck[i]);
        }
        float grownAverage = CardPowerUtility.Average(grown);

        TinyLog.Info("RunLog",
            "最终牌组质量：" + result.FinalDeck.Count + " 张，单卡均值 " + average.ToString("0.00") +
            "（去掉起始卡 " + grownAverage.ToString("0.00") + "），最高 " + top.ToString("0.00") +
            " / 最低 " + low.ToString("0.00"));
    }
}
