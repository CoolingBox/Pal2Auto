// 文件：RunState.cs
// 描述：一局的运行态。天数、银两、声望、阅历、牌组、当天日程、货架都在这里。
//
// 与 RunSave 的关系（照 Docs/代码命名约定.md 第五节）：RunState 是活对象，一局结束就丢；
// RunSave 是它落盘的快照。两者之间只走 RunManager.Capture() / Apply()。
//
// 不在这里做的事：胜负判定与跨天推进（RunManager）、单场战斗（DeckBattle）。
// 这里只放「一局的数据 + 局部的、不依赖流程位置的操作」（买卡、合成、升降级）。

using System;
using System.Collections.Generic;
using TinyFramework;

/// <summary>声望扣到 0 之后发生了什么。</summary>
public enum RenownOutcome
{
    None,
    LastStand,   // 触发背水：声望回到 1，一局继续
    GameOver,    // 背水已经用过，终局
}

public sealed class RunState
{
    /// <summary>开局银两。没有它第 1 天日俸 13 连一张铜卡都买不起。</summary>
    public const int StartGold = 40;

    /// <summary>江湖声望上限，也是起始值。</summary>
    public const int StartRenown = 20;

    /// <summary>拿够这么多场宿敌胜利就赢。</summary>
    public const int TargetVictories = 10;

    /// <summary>天数硬上限。超过还没 10 胜就终局。</summary>
    public const int MaxDay = 15;

    /// <summary>每级需要的阅历。</summary>
    public const int ExpPerLevel = 8;

    /// <summary>等级上限。15 天 × 每天约 1 级，留一级余量。</summary>
    public const int MaxLevel = 15;

    /// <summary>2 合 1 的手续费。合成是纯增益，收一点钱让它成为硬 sink。</summary>
    public const int MergeCost = 15;

    /// <summary>删牌服务单价与一局上限。</summary>
    public const int RemoveServiceCost = 60;
    public const int RemoveServiceMax = 5;

    /// <summary>上场数基数（DeckBattle.HandBase 也是 5）。</summary>
    public const int BaseHandCapacity = 5;

    public const int MaxHandCapacity = 8;

    // —————————————— 等级与气血 ——————————————

    /// <summary>
    /// 升级加血的起点与每级增幅（参考《The Bazaar》：它每级给的血量随等级递增，
    /// 2 级 +50 / 3 级 +100 / 4 级 +150，不是固定 +N）。
    /// 我们缩到本作的量级：升到 2 级 +8，之后每级比上一级多 +2（2 级 +8、3 级 +10、4 级 +12…）。
    /// 15 级累计 +294，基础 260 → 554。
    /// </summary>
    public const int HpGainFirst = 8;
    public const int HpGainStep = 2;

    /// <summary>这一级本身给多少气血（2 级起算）。</summary>
    public static int HpGainOfLevel(int level)
    {
        if (level < 2)
        {
            return 0;
        }
        return HpGainFirst + HpGainStep * (level - 2);
    }

    /// <summary>从 1 级升到 level 累计加了多少气血。</summary>
    public static int HpGainTotal(int level)
    {
        int total = 0;
        int top = level;
        if (top > MaxLevel)
        {
            top = MaxLevel;
        }
        for (int lv = 2; lv <= top; lv++)
        {
            total += HpGainOfLevel(lv);
        }
        return total;
    }

    /// <summary>等级上限处不再加血。</summary>
    public const int HpLevelCap = MaxLevel;

    // —————————————— 敌方气血（读配置表，不写公式）——————————————

    /// <summary>配置表缓存：天数 → 那一行。第一次用到时才读 JSON。</summary>
    private static Dictionary<int, EnemyHpConfig> _hpRows;

    private static Dictionary<int, EnemyHpConfig> HpRows
    {
        get
        {
            if (_hpRows == null)
            {
                _hpRows = new Dictionary<int, EnemyHpConfig>();
                List<EnemyHpConfig> all = ConfigManager.GetAll<EnemyHpConfig>();
                if (all == null)
                {
                    return _hpRows;
                }
                for (int i = 0; i < all.Count; i++)
                {
                    EnemyHpConfig row = all[i];
                    _hpRows[row.Id] = row;
                }
            }
            return _hpRows;
        }
    }

    /// <summary>
    /// 取第 day 天的配置行。表里没有这一天时：小于表头用最小天数，大于表尾用最大天数。
    /// 不写公式就是不想让「改一天」变成「全表位移」，缺行也必须能跑。
    /// </summary>
    public static EnemyHpConfig HpRowFor(int day)
    {
        Dictionary<int, EnemyHpConfig> rows = HpRows;
        if (rows.Count == 0)
        {
            return null;
        }

        EnemyHpConfig row;
        if (rows.TryGetValue(day, out row))
        {
            return row;
        }

        int minDay = int.MaxValue;
        int maxDay = int.MinValue;
        foreach (KeyValuePair<int, EnemyHpConfig> pair in rows)
        {
            if (pair.Key < minDay)
            {
                minDay = pair.Key;
            }
            if (pair.Key > maxDay)
            {
                maxDay = pair.Key;
            }
        }

        int fallback = maxDay;
        if (day <= minDay)
        {
            fallback = minDay;
        }
        rows.TryGetValue(fallback, out row);
        return row;
    }

    /// <summary>遭遇三选一里第 tier 档（0/1/2）的敌血。tier 越界时贴边。</summary>
    public static int EncounterHp(int day, int tier)
    {
        EnemyHpConfig row = HpRowFor(day);
        if (row == null || row.Encounter == null || row.Encounter.Length == 0)
        {
            return 110;
        }

        int index = tier;
        if (index < 0)
        {
            index = 0;
        }
        if (index >= row.Encounter.Length)
        {
            index = row.Encounter.Length - 1;
        }
        return row.Encounter[index];
    }

    /// <summary>当天宿敌的气血。</summary>
    public static int NemesisHp(int day)
    {
        EnemyHpConfig row = HpRowFor(day);
        if (row == null || row.Nemesis <= 0)
        {
            return 121;
        }
        return row.Nemesis;
    }

    /// <summary>
    /// 遭遇三选一的「第 slot 档该是什么品阶」。品阶随天数上移：
    /// 第 1~3 天 青铜/白银/黄金，第 4~7 天 白银/黄金/钻石，第 8 天起 黄金/钻石/传奇。
    /// 这样三选一的三个选项天然是「便宜好打 / 标准 / 贵且难」。
    /// </summary>
    public static EnemyTier TierForSlot(int day, int slot)
    {
        int baseIndex;
        if (day <= 3)
        {
            baseIndex = 0;
        }
        else if (day <= 7)
        {
            baseIndex = 1;
        }
        else
        {
            baseIndex = 2;
        }

        int index = baseIndex + slot;
        if (index > 4)
        {
            index = 4;
        }
        return (EnemyTier)index;
    }

    /// <summary>roll 本次遭遇的三个候选（同一时辰内只 roll 一次）。</summary>
    public void RollEncounterChoices(int phaseIndex)
    {
        if (EncounterChoices.Count == 3 && EncounterChoicesPhase == phaseIndex)
        {
            return;
        }

        EncounterChoices.Clear();
        EncounterChoicesPhase = phaseIndex;

        List<EnemyConfig> all = ConfigManager.GetAll<EnemyConfig>();
        if (all == null || all.Count == 0)
        {
            return;
        }

        for (int slot = 0; slot < EncounterScaleLength; slot++)
        {
            EnemyTier want = TierForSlot(Day, slot);
            List<EnemyConfig> pool = new List<EnemyConfig>();
            for (int i = 0; i < all.Count; i++)
            {
                EnemyConfig enemy = all[i];
                if (enemy.Tier != want)
                {
                    continue;
                }
                if (EncounterChoices.Contains(enemy.Id))
                {
                    continue;
                }
                pool.Add(enemy);
            }

            // 该品阶一只都没有就退回全池（图鉴没铺满时也不崩）
            if (pool.Count == 0)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (!EncounterChoices.Contains(all[i].Id))
                    {
                        pool.Add(all[i]);
                    }
                }
            }

            if (pool.Count == 0)
            {
                EncounterChoices.Add(all[Rng.Next(all.Count)].Id);
                continue;
            }

            EncounterChoices.Add(pool[Rng.Next(pool.Count)].Id);
        }
    }

    /// <summary>三选一的档数（低/中/高），与 EnemyHpConfig.Encounter 的长度一致。</summary>
    public const int EncounterScaleLength = 3;

    /// <summary>当天的基础敌血（遭遇中间档），给界面上的「本日强度」这类文案用。</summary>
    public static int DayBaseHp(int day)
    {
        return EncounterHp(day, 1);
    }

    // —————————————— 升级奖励 ——————————————

    /// <summary>每次升级给几个候选。</summary>
    public const int RewardChoices = 3;

    /// <summary>「铁骨」每次加多少气血上限。</summary>
    public const int BoneBonusHp = 30;

    /// <summary>「盘缠」给多少银两。</summary>
    public const int CoinBonusGold = 50;

    /// <summary>「月俸」每次加多少日俸。</summary>
    public const int WageBonusPerPick = 4;

    // —— 存档字段（public 字段才能被 SaveManager 的 IncludeFields 序列化）——

    public int CharacterId = 1;
    public int Day = 1;
    public int Gold = StartGold;
    public int Renown = StartRenown;
    public int Experience;
    public int Level = 1;
    public int Victories;
    public int HandCapacity = BaseHandCapacity;

    /// <summary>基础气血上限（开局从 CharacterConfig.Hp 抄一份，跟着存档走）。</summary>
    public int BaseHp = 260;

    /// <summary>「铁骨」奖励累计的气血加成。</summary>
    public int BonusHp;

    /// <summary>「月俸」奖励累计的日俸加成。</summary>
    public int WageBonus;

    /// <summary>「门路」奖励累计的每日免费刷新次数。</summary>
    public int FreeRefreshBonus;

    /// <summary>本场遭遇的敌人品阶。进战斗前由面板写入，打赢后按它发银两与阅历。</summary>
    public EnemyTier EncounterTier = EnemyTier.Bronze;

    /// <summary>升级时还没选的三张奖励。非空时面板要挡住其它操作。</summary>
    public List<LevelReward> PendingRewards = new List<LevelReward>();

    /// <summary>还没选的升级轮数。一次阅历涨两级时会有两轮，选完一轮自动补下一轮。</summary>
    public int PendingRewardRounds;

    public bool LastStandUsed;
    public bool IsOver;
    public bool IsWon;

    /// <summary>一局里已经买过几次删牌服务。</summary>
    public int RemoveServiceUsed;

    /// <summary>随机种子。存种子而不是存 Random，读档后能复现同一串随机。</summary>
    public int Seed;

    public DaySchedule Today = new DaySchedule();
    public ShopStock Shop = new ShopStock();
    public List<RunCard> Deck = new List<RunCard>();

    /// <summary>本局被「移除」的卡（永久离开这套牌组，但不影响 DeckProgress 的跨局记录）。</summary>
    public List<int> RemovedCardIds = new List<int>();

    /// <summary>事件流水，供 DevShot / 战报查看。</summary>
    public List<string> Notes = new List<string>();

    /// <summary>
    /// 本次遭遇时辰的三个候选敌人（低 / 中 / 高三档，各一个品阶）。
    /// 存下来是为了「刷新界面不重 roll」—— 同一个时辰反复进出面板不能换人。
    /// </summary>
    public List<int> EncounterChoices = new List<int>();

    /// <summary>EncounterChoices 是在哪个时辰 roll 的。换时辰或换天就失效。</summary>
    public int EncounterChoicesPhase;

    private Random _rng;

    /// <summary>随机源。读档后按 Seed 重建，不序列化。</summary>
    public Random Rng
    {
        get
        {
            if (_rng == null)
            {
                _rng = new Random(Seed);
            }
            return _rng;
        }
    }

    // —————————————————————— 开局 ——————————————————————

    /// <summary>按主角配置建初始牌组。初始阶取每张卡的起步阶。</summary>
    public void Begin(int characterId, int seed)
    {
        CharacterId = characterId;
        Seed = seed == 0 ? Environment.TickCount : seed;
        _rng = new Random(Seed);

        Day = 1;
        Gold = StartGold;
        Renown = StartRenown;
        Experience = 0;
        Level = 1;
        Victories = 0;
        HandCapacity = BaseHandCapacity;
        BonusHp = 0;
        WageBonus = 0;
        FreeRefreshBonus = 0;
        PendingRewards.Clear();
        PendingRewardRounds = 0;
        LastStandUsed = false;
        IsOver = false;
        IsWon = false;
        RemoveServiceUsed = 0;

        Deck.Clear();
        RemovedCardIds.Clear();
        Notes.Clear();

        CharacterConfig character = ConfigManager.GetOne<CharacterConfig>(characterId);
        BaseHp = 260;
        if (character != null)
        {
            if (character.Hp > 0)
            {
                BaseHp = character.Hp;
            }
            for (int i = 0; i < character.Deck.Length; i++)
            {
                int cardId = character.Deck[i];
                CardConfig card = ConfigManager.GetOne<CardConfig>(cardId);
                CardTier tier = CardTier.Copper;
                if (card != null)
                {
                    tier = card.MinTier;
                }
                Deck.Add(new RunCard(cardId, tier));
            }
        }

        Today.Build();
        Shop.ResetForNewDay();
        Shop.FreeRefreshCount = FreeRefreshPerDay;
        Shop.Open(Day, CharacterId, Rng);

        Notes.Add("开局：" + CharacterName + "，牌组 " + Deck.Count + " 张，银两 " + Gold);
    }

    public string CharacterName
    {
        get
        {
            CharacterConfig character = ConfigManager.GetOne<CharacterConfig>(CharacterId);
            if (character == null)
            {
                return "无名侠客";
            }
            return character.Name;
        }
    }

    // —————————————————————— 气血与敌方难度 ——————————————————————

    /// <summary>本局的气血上限：基础 + 等级加成 + 「铁骨」奖励。</summary>
    public int MaxHp
    {
        get
        {
            int level = Level;
            if (level > HpLevelCap)
            {
                level = HpLevelCap;
            }
            int hp = BaseHp + HpGainTotal(level) + BonusHp;
            if (hp < 1)
            {
                hp = 1;
            }
            return hp;
        }
    }

    // —————————————————————— 钱 ——————————————————————

    /// <summary>当天日俸：10 + 3×等级 + 月俸奖励。</summary>
    public int DaySalary
    {
        get
        {
            return 10 + 3 * Level + WageBonus;
        }
    }

    /// <summary>
    /// 一场遭遇的银两：15 + 2×天。
    /// 2026-11-24 从「20 + 3×天」下调 —— 实测一局余银 277~455 花不掉，先砍 source 再补 sink。
    /// </summary>
    public int EncounterReward
    {
        get
        {
            return RewardGoldOf(EncounterTier);
        }
    }

    /// <summary>
    /// 一场遭遇的银两：只看品阶，不看天数（2026-11-24 从「15 + 2×天」改的）。
    /// 天数本来就会把品阶概率往上推（青铜退场、钻石传奇进池），收入跟着品阶走，等于跟着进度走。
    /// </summary>
    public static int RewardGoldOf(EnemyTier tier)
    {
        if (tier == EnemyTier.Bronze)
        {
            return 18;
        }
        if (tier == EnemyTier.Silver)
        {
            return 26;
        }
        if (tier == EnemyTier.Gold)
        {
            return 36;
        }
        if (tier == EnemyTier.Diamond)
        {
            return 50;
        }
        return 68;
    }

    /// <summary>
    /// 一场遭遇胜利的**额外**阅历（基础 1 点每时辰都有，不在这里算）。
    /// 黄金及以下 +1（该时辰共 2 点），钻石 +2（共 3 点），传奇 +3（共 4 点）。
    /// 一天全胜 = 4 自由 + 2 遭遇 + 2 宿敌 = 8 点 = 正好一级；输掉战斗只剩 6 点。
    /// </summary>
    public static int RewardExpOf(EnemyTier tier)
    {
        if (tier == EnemyTier.Diamond)
        {
            return 2;
        }
        if (tier == EnemyTier.Legend)
        {
            return 3;
        }
        return 1;
    }

    /// <summary>品阶的中文名，界面上显示用。</summary>
    public static string TierName(EnemyTier tier)
    {
        if (tier == EnemyTier.Bronze)
        {
            return "青铜";
        }
        if (tier == EnemyTier.Silver)
        {
            return "白银";
        }
        if (tier == EnemyTier.Gold)
        {
            return "黄金";
        }
        if (tier == EnemyTier.Diamond)
        {
            return "钻石";
        }
        return "传奇";
    }

    /// <summary>一场宿敌的银两：40 + 已胜场。</summary>
    public int NemesisReward
    {
        get
        {
            return 40 + Victories;
        }
    }

    public bool CanAfford(int cost)
    {
        return Gold >= cost;
    }

    public bool Spend(int cost)
    {
        if (cost <= 0)
        {
            return true;
        }
        if (Gold < cost)
        {
            return false;
        }
        Gold -= cost;
        return true;
    }

    public void Earn(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        Gold += amount;
    }

    // —————————————————————— 牌组 ——————————————————————

    public void AddCard(RunCard card)
    {
        if (card == null)
        {
            return;
        }
        Deck.Add(card);
    }

    public int CountOf(int cardId, CardTier tier)
    {
        int count = 0;
        for (int i = 0; i < Deck.Count; i++)
        {
            if (Deck[i].CardId == cardId && Deck[i].Tier == tier)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>牌组里所有「够 2 张同阶、且没到钻」的卡，供合成界面列出来。</summary>
    public List<RunCard> MergeableCards()
    {
        List<RunCard> result = new List<RunCard>();
        List<int> seen = new List<int>();

        for (int i = 0; i < Deck.Count; i++)
        {
            RunCard card = Deck[i];
            if (!CardTierRules.CanMerge(card.Tier))
            {
                continue;
            }
            if (CountOf(card.CardId, card.Tier) < CardTierRules.MergeCount)
            {
                continue;
            }

            // 同 id 同阶只列出一张当代表
            bool listed = false;
            for (int k = 0; k < seen.Count; k++)
            {
                if (seen[k] == card.CardId * 10 + (int)card.Tier)
                {
                    listed = true;
                    break;
                }
            }
            if (listed)
            {
                continue;
            }

            seen.Add(card.CardId * 10 + (int)card.Tier);
            result.Add(card);
        }
        return result;
    }

    /// <summary>2 合 1：消耗两张同名同阶 + 15 银两，产出一张高一阶。</summary>
    public bool TryMerge(int cardId, CardTier tier)
    {
        if (!CardTierRules.CanMerge(tier))
        {
            return false;
        }
        if (CountOf(cardId, tier) < CardTierRules.MergeCount)
        {
            return false;
        }
        if (!Spend(MergeCost))
        {
            return false;
        }

        int removed = 0;
        for (int i = Deck.Count - 1; i >= 0; i--)
        {
            if (removed >= CardTierRules.MergeCount)
            {
                break;
            }
            if (Deck[i].CardId == cardId && Deck[i].Tier == tier)
            {
                Deck.RemoveAt(i);
                removed++;
            }
        }

        RunCard merged = new RunCard(cardId, CardTierRules.Next(tier));
        Deck.Add(merged);
        Notes.Add("合成 " + merged.DisplayName + "（消耗 2 张" + CardTierRules.Name(tier) + "阶同名卡，" +
            MergeCost + " 银两）");
        return true;
    }

    /// <summary>删牌服务还能不能买。</summary>
    public bool CanBuyRemoval
    {
        get
        {
            return RemoveServiceUsed < RemoveServiceMax && Gold >= RemoveServiceCost;
        }
    }

    /// <summary>付 60 银两删掉牌组里第 index 张。一局限 5 次。</summary>
    public bool TryBuyRemoval(int index)
    {
        if (RemoveServiceUsed >= RemoveServiceMax)
        {
            return false;
        }
        if (index < 0 || index >= Deck.Count)
        {
            return false;
        }
        if (!Spend(RemoveServiceCost))
        {
            return false;
        }

        RemoveServiceUsed++;
        RemoveCard(index);
        Notes.Add("删牌服务：花 " + RemoveServiceCost + " 银两，本次一局已用 " +
            RemoveServiceUsed + "/" + RemoveServiceMax);
        return true;
    }

    /// <summary>移除一张卡（本局）。用于「移除」关键词与删牌服务。</summary>
    public bool RemoveCard(int index)
    {
        if (index < 0 || index >= Deck.Count)
        {
            return false;
        }
        RunCard card = Deck[index];
        Deck.RemoveAt(index);
        RemovedCardIds.Add(card.CardId);
        Notes.Add("移除 " + card.DisplayName);
        return true;
    }

    // —————————————————————— 集市 ——————————————————————

    public bool BuyOffer(int index)
    {
        ShopOffer offer = Shop.Get(index);
        if (offer == null)
        {
            return false;
        }
        if (offer.Sold)
        {
            return false;
        }
        if (!Spend(offer.Price))
        {
            return false;
        }

        offer.Sold = true;
        Deck.Add(offer.Card);
        Notes.Add("买入 " + offer.Card.DisplayName + "，花 " + offer.Price + " 银两");
        return true;
    }

    public bool TryRefreshShop()
    {
        int cost = Shop.NextRefreshCost;
        if (cost < 0)
        {
            return false;
        }
        if (cost > 0)
        {
            if (!Spend(cost))
            {
                return false;
            }
        }

        Shop.RegisterRefresh();
        Shop.Restock(Day, CharacterId, Rng);
        Notes.Add("在" + Shop.ShopName + "刷新货架，花 " + cost + " 银两");
        return true;
    }

    // —————————————————————— 阅历 ——————————————————————

    /// <summary>每天几次免费刷新：默认 1，第 8 级起 2，再加「门路」奖励。</summary>
    public int FreeRefreshPerDay
    {
        get
        {
            int baseCount = 1;
            if (Level >= 8)
            {
                baseCount = 2;
            }
            return baseCount + FreeRefreshBonus;
        }
    }

    public void GainExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Experience += amount;
        while (Level < MaxLevel && Experience >= ExpPerLevel)
        {
            Experience -= ExpPerLevel;
            Level++;
            ApplyLevelUp();
        }
    }

    private void ApplyLevelUp()
    {
        Notes.Add("阅历升到 " + Level + " 级");

        // 2026-11-24：等级不再给上场数。卡槽是「稀有资源」，
        // 只能靠器物牌（捆仙索 / 七星灯 / 如意玄武遗 的 HandSelf +1）在局内争取。
        // 所以这里只给气血与三选一。

        // 气血是每次升级的固定收益，三选一是额外的。
        Notes.Add("气血上限 → " + MaxHp);

        PendingRewardRounds++;
        if (PendingRewards.Count == 0)
        {
            PendingRewards.AddRange(PickRewards(RewardChoices));
        }
    }

    // —————————————————————— 改单张牌（事件 / 奇遇用）——————————————————————

    /// <summary>
    /// 给牌组里的某一张牌加永久修正。**不动 CardConfig** ——
    /// 那是全局配置，改一张等于改所有同名卡，而且跟着存档走不了。
    /// </summary>
    public void BuffCard(RunCard card, CardStat stat, int amount)
    {
        if (card == null || amount == 0)
        {
            return;
        }

        if (stat == CardStat.Damage)
        {
            card.BonusDamage += amount;
        }
        else if (stat == CardStat.Shield)
        {
            card.BonusShield += amount;
        }
        else if (stat == CardStat.Heal)
        {
            card.BonusHeal += amount;
        }
        else if (stat == CardStat.Poison)
        {
            card.BonusPoison += amount;
        }
        else if (stat == CardStat.Expose)
        {
            card.BonusExpose += amount;
        }

        card.BuffCount++;
        Notes.Add("「" + card.CardName + "」" + StatName(stat) + " " + amount.ToString("+0;-0"));
    }

    /// <summary>改蓄势（秒，负数更快）。</summary>
    public void BuffCardCountdown(RunCard card, float seconds)
    {
        if (card == null || seconds == 0f)
        {
            return;
        }
        card.BonusCountdown += seconds;
        card.BuffCount++;
        Notes.Add("「" + card.CardName + "」蓄势 " + seconds.ToString("+0.0;-0.0") + " 秒");
    }

    private static string StatName(CardStat stat)
    {
        if (stat == CardStat.Damage)
        {
            return "伤害";
        }
        if (stat == CardStat.Shield)
        {
            return "护体";
        }
        if (stat == CardStat.Heal)
        {
            return "治疗";
        }
        if (stat == CardStat.Poison)
        {
            return "中毒";
        }
        return "破绽";
    }

    // —————————————————————— 升级奖励 ——————————————————————

    /// <summary>抽 count 个互不重复的奖励候选。</summary>
    public List<LevelReward> PickRewards(int count)
    {
        List<LevelReward> result = new List<LevelReward>();
        List<LevelRewardKind> pool = new List<LevelRewardKind>();
        pool.Add(LevelRewardKind.Bone);
        pool.Add(LevelRewardKind.Bone);
        pool.Add(LevelRewardKind.Bone);
        pool.Add(LevelRewardKind.Coin);
        pool.Add(LevelRewardKind.Coin);
        pool.Add(LevelRewardKind.Wage);
        pool.Add(LevelRewardKind.Scroll);
        pool.Add(LevelRewardKind.Refresh);

        while (result.Count < count && pool.Count > 0)
        {
            int index = Rng.Next(pool.Count);
            LevelRewardKind kind = pool[index];

            // 同一个三选一里不出现两张一样的
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (pool[i] == kind)
                {
                    pool.RemoveAt(i);
                }
            }

            result.Add(MakeReward(kind));
        }

        return result;
    }

    private LevelReward MakeReward(LevelRewardKind kind)
    {
        LevelReward reward = new LevelReward();
        reward.Kind = kind;
        if (kind == LevelRewardKind.Bone)
        {
            reward.Name = "铁骨";
            reward.Desc = "气血上限 +" + BoneBonusHp;
        }
        else if (kind == LevelRewardKind.Coin)
        {
            reward.Name = "盘缠";
            reward.Desc = "银两 +" + CoinBonusGold;
        }
        else if (kind == LevelRewardKind.Wage)
        {
            reward.Name = "月俸";
            reward.Desc = "每日俸禄 +" + WageBonusPerPick;
        }
        else if (kind == LevelRewardKind.Scroll)
        {
            reward.Name = "抄本";
            reward.Desc = "一张本命铜卡入组";
        }
        else
        {
            reward.Name = "门路";
            reward.Desc = "每日免费刷新 +1";
        }
        return reward;
    }

    /// <summary>执行选中的奖励，并清空待选列表。</summary>
    public void ApplyReward(LevelReward reward)
    {
        if (reward == null)
        {
            return;
        }

        if (reward.Kind == LevelRewardKind.Bone)
        {
            BonusHp += BoneBonusHp;
            Notes.Add("选「铁骨」：气血上限 → " + MaxHp);
        }
        else if (reward.Kind == LevelRewardKind.Coin)
        {
            Gold += CoinBonusGold;
            Notes.Add("选「盘缠」：银两 +" + CoinBonusGold);
        }
        else if (reward.Kind == LevelRewardKind.Wage)
        {
            WageBonus += WageBonusPerPick;
            Notes.Add("选「月俸」：日俸 → " + DaySalary);
        }
        else if (reward.Kind == LevelRewardKind.Refresh)
        {
            FreeRefreshBonus++;
            Notes.Add("选「门路」：每日免费刷新 → " + FreeRefreshPerDay);
        }
        else if (reward.Kind == LevelRewardKind.Scroll)
        {
            int cardId = PickOwnCopperCard();
            if (cardId > 0)
            {
                CardConfig card = ConfigManager.GetOne<CardConfig>(cardId);
                CardTier tier = CardTier.Copper;
                if (card != null)
                {
                    tier = card.MinTier;
                }
                AddCard(new RunCard(cardId, tier));
                string name = card != null ? card.Name : "无名招式";
                Notes.Add("选「抄本」：" + name + " 入组");
            }
        }

        PendingRewards.Clear();

        // 连升两级：补上下一轮
        if (PendingRewardRounds > 0)
        {
            PendingRewardRounds--;
        }
        if (PendingRewardRounds > 0)
        {
            PendingRewards.AddRange(PickRewards(RewardChoices));
        }
    }

    /// <summary>从本命池里随机一张铜起步的卡。抽不到就退回全池铜卡。</summary>
    private int PickOwnCopperCard()
    {
        List<CardConfig> all = ConfigManager.GetAll<CardConfig>();
        List<int> own = new List<int>();
        List<int> any = new List<int>();
        for (int i = 0; i < all.Count; i++)
        {
            CardConfig card = all[i];
            if (card.MinTier != CardTier.Copper)
            {
                continue;
            }
            if (card.StarterOnly)
            {
                continue;
            }
            any.Add(card.Id);
            if (card.Master == CharacterId)
            {
                own.Add(card.Id);
            }
        }

        if (own.Count > 0)
        {
            return own[Rng.Next(own.Count)];
        }
        if (any.Count > 0)
        {
            return any[Rng.Next(any.Count)];
        }
        return 0;
    }

    // —————————————————————— 声望 ——————————————————————

    /// <summary>输掉第 N 天宿敌扣 N 点声望。扣到 0 时先用一次「背水」，再用就终局。</summary>
    public RenownOutcome TakeRenownLoss(int amount)
    {
        if (amount <= 0)
        {
            return RenownOutcome.None;
        }

        Renown -= amount;
        if (Renown > 0)
        {
            return RenownOutcome.None;
        }

        if (!LastStandUsed)
        {
            LastStandUsed = true;
            Renown = 1;
            Notes.Add("背水：声望见底，回到 1 点，再输一次就终局");
            return RenownOutcome.LastStand;
        }

        Renown = 0;
        IsOver = true;
        IsWon = false;
        Notes.Add("声望耗尽，一局结束");
        return RenownOutcome.GameOver;
    }

    public bool IsDefeated
    {
        get
        {
            return IsOver && !IsWon;
        }
    }
}
