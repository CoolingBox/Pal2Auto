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

    /// <summary>当前时辰的「百炼台」现场（奇遇时辰打开）。</summary>
    public ForgeEvent PendingForge;

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

    /// <summary>
    /// 获得一张牌。**同族已经在牌组里就直接升阶，不再堆第二张**（2026-11-26 用户设定）：
    /// 买 / 捡到一张你已经有的牌，等于把它往上推一阶，而不是让牌组里躺两张同名卡。
    ///
    /// 升的是「同族里阶位最低的那张」，所以牌组永远是「每族一张、只往上涨」；
    /// 改的是同一张 RunCard 的 CardId，`BonusDamage` 之类的实例修正与附魔自然继承。
    /// 已经钻阶的不再升，按新卡加入。
    /// </summary>
    public void AddCard(RunCard card)
    {
        if (card == null)
        {
            return;
        }

        int baseId = CardTierRules.BaseIdOf(card.CardId);
        int lowestIndex = -1;
        CardTier lowest = CardTier.Diamond;

        for (int i = 0; i < Deck.Count; i++)
        {
            if (CardTierRules.BaseIdOf(Deck[i].CardId) != baseId)
            {
                continue;
            }
            if (Deck[i].NoUpgrade)
            {
                // 贪婪塞的卡不能被「再获得即升阶」吃掉
                continue;
            }
            if (lowestIndex < 0 || (int)Deck[i].Tier < (int)lowest)
            {
                lowest = Deck[i].Tier;
                lowestIndex = i;
            }
        }

        if (lowestIndex >= 0 && CardTierRules.CanMerge(lowest))
        {
            RunCard target = Deck[lowestIndex];
            CardTier next = CardTierRules.Next(lowest);
            string before = target.DisplayName;
            target.CardId = CardTierRules.TierIdOf(baseId, next);
            Notes.Add("再次获得「" + target.CardName + "」：" + before + " → " + target.DisplayName);
            return;
        }

        Deck.Add(card);
    }

    public int CountOf(int cardId, CardTier tier)
    {
        int count = 0;
        for (int i = 0; i < Deck.Count; i++)
        {
            if (Deck[i].NoUpgrade)
            {
                continue;
            }
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

    /// <summary>2 合 1：消耗两张同名同阶 + 15 银两，产出一张高一阶（换的是 CardId）。</summary>
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
            if (Deck[i].NoUpgrade)
            {
                continue;
            }
            if (Deck[i].CardId == cardId && Deck[i].Tier == tier)
            {
                Deck.RemoveAt(i);
                removed++;
            }
        }

        int nextId = CardTierRules.TierIdOf(CardTierRules.BaseIdOf(cardId), CardTierRules.Next(tier));
        RunCard merged = new RunCard(nextId, CardTierRules.Next(tier));
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
        string bought = offer.Card.DisplayName;
        // 走 AddCard：同族已在牌组里就会直接升阶，而不是再堆一张（2026-11-26 设定）
        AddCard(offer.Card);
        Notes.Add("买入 " + bought + "，花 " + offer.Price + " 银两");
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

    // —————————————————————— 百炼台（奇遇）——————————————————————

    /// <summary>开一场百炼台：抽 5 张候选 + 3 个随机选项。同一时辰不重复 roll。</summary>
    public void OpenForgeEvent(int phaseIndex)
    {
        if (PendingForge != null && !PendingForge.Resolved && ForgePhaseIndex == phaseIndex)
        {
            return;
        }

        ForgeEvent ev = new ForgeEvent();
        List<RunCard> pool = new List<RunCard>(Deck);
        for (int i = 0; i < 5 && pool.Count > 0; i++)
        {
            int pick = Rng.Next(pool.Count);
            ev.Candidates.Add(pool[pick]);
            pool.RemoveAt(pick);
        }

        List<ForgeOption> options = AllForgeOptions();
        for (int i = 0; i < 3 && options.Count > 0; i++)
        {
            int pick = WeightedPick(options);
            ev.Options.Add(options[pick]);
            options.RemoveAt(pick);
        }

        PendingForge = ev;
        ForgePhaseIndex = phaseIndex;
    }

    /// <summary>百炼台是在哪个时辰开的（同一时辰不重 roll）。</summary>
    public int ForgePhaseIndex = -1;

    private static List<ForgeOption> AllForgeOptions()
    {
        List<ForgeOption> list = new List<ForgeOption>();

        // 权重口径（2026-11-26）：「移除牌库 / 无代价提速 / 合成」本身是净赚，
        // 这类选项权重压到 4~5，否则每次开炉都摆到脸上，百炼台就退化成「白嫖台」。
        list.Add(MakeOption(ForgeOptionKind.Merge, "合成", "选 2 张：数值与蓄势全部相加，合成一张", 2, 0, 4));
        list.Add(MakeOption(ForgeOptionKind.Exile, "放逐", "选 1 张：移出牌库，但气血上限 -10", 1, 0, 4));
        list.Add(MakeOption(ForgeOptionKind.Whet, "磨砺", "选 1 张：蓄势 -0.5 秒，不牺牲数值", 1, 0, 5));
        list.Add(MakeOption(ForgeOptionKind.Vitality, "换命", "选 1 张：移出牌库，气血上限 +15", 1, 0, 5));

        list.Add(MakeOption(ForgeOptionKind.Transmute, "变换", "选 1 张：随机变成另一张同阶卡（可能抽到别派绝学）", 1, 0, 8));
        list.Add(MakeOption(ForgeOptionKind.Sharp, "锋利", "选 1 张：伤害 +2", 1, 0));
        list.Add(MakeOption(ForgeOptionKind.Quench, "淬甲", "选 1 张：护体 / 治疗 +2", 1, 0));
        list.Add(MakeOption(ForgeOptionKind.Priority, "先发", "选 1 张：战斗开始时优先上手", 1, 0));
        list.Add(MakeOption(ForgeOptionKind.Bottom, "后发", "选 1 张：洗牌后沉到牌库底", 1, 0));
        list.Add(MakeOption(ForgeOptionKind.Multi, "多动", "选 1 张：多重 +1，蓄势 +1 秒", 1, 0));
        list.Add(MakeOption(ForgeOptionKind.QuickDraw, "快枪", "选 1 张：蓄势 -1 秒，但数值减半", 1, 0));
        list.Add(MakeOption(ForgeOptionKind.Greed, "贪婪", "牌库塞一张铜阶基础卡（永不升级），另得 40 银两", 0, 40));
        list.Add(MakeOption(ForgeOptionKind.Transcribe, "抄录", "选 1 张：复制一张同阶同名卡进牌库", 1, 0));
        return list;
    }

    private static ForgeOption MakeOption(ForgeOptionKind kind, string name, string desc,
        int targets, int gold, int weight = 10)
    {
        ForgeOption option = new ForgeOption();
        option.Kind = kind;
        option.Name = name;
        option.Desc = desc;
        option.TargetCount = targets;
        option.Gold = gold;
        option.Weight = weight;
        return option;
    }

    /// <summary>结算一个百炼台选项。targets 是玩家点选的牌（可以来自候选，也可以来自牌组）。</summary>
    public bool ApplyForgeOption(ForgeOption option, List<RunCard> targets)
    {
        if (option == null || PendingForge == null || PendingForge.Resolved)
        {
            return false;
        }
        if (targets == null)
        {
            targets = new List<RunCard>();
        }
        if (targets.Count < option.TargetCount)
        {
            return false;
        }

        if (option.Kind == ForgeOptionKind.Merge)
        {
            RunCard first = targets[0];
            RunCard second = targets[1];
            if (first == second)
            {
                return false;
            }

            // 数值与蓄势全部相加，落在第一张上（第二张消失）
            first.BonusDamage += second.Damage;
            first.BonusShield += second.Shield;
            first.BonusHeal += second.Heal;
            first.BonusPoison += second.Poison;
            first.BonusCountdown += second.Countdown;
            first.BuffCount++;
            Deck.Remove(second);
            Notes.Add("百炼：合成「" + first.CardName + "」+" +
                second.CardName + "（蓄势 +" + second.Countdown.ToString("0.0") + " 秒）");
        }
        else if (option.Kind == ForgeOptionKind.Priority)
        {
            targets[0].PriorityDraw = true;
            targets[0].BuffCount++;
            Notes.Add("百炼：先发「" + targets[0].CardName + "」");
        }
        else if (option.Kind == ForgeOptionKind.Bottom)
        {
            targets[0].BottomOfDeck = true;
            targets[0].BuffCount++;
            Notes.Add("百炼：后发「" + targets[0].CardName + "」");
        }
        else if (option.Kind == ForgeOptionKind.Sharp)
        {
            targets[0].BonusDamage += 2;
            targets[0].BuffCount++;
            Notes.Add("百炼：锋利「" + targets[0].CardName + "」伤害 +2");
        }
        else if (option.Kind == ForgeOptionKind.QuickDraw)
        {
            targets[0].ValueScale *= 0.5f;
            targets[0].BonusCountdown -= 1f;
            targets[0].BuffCount++;
            Notes.Add("百炼：快枪「" + targets[0].CardName + "」蓄势 -1 秒，数值减半");
        }
        else if (option.Kind == ForgeOptionKind.Multi)
        {
            targets[0].BonusMulti += 1;
            targets[0].BonusCountdown += 1f;
            targets[0].BuffCount++;
            Notes.Add("百炼：多动「" + targets[0].CardName + "」多重 +1，蓄势 +1 秒");
        }
        else if (option.Kind == ForgeOptionKind.Exile)
        {
            // 删牌本身是奖励（集市删一张要 60 两），所以这里必须挂代价
            Deck.Remove(targets[0]);
            BonusHp -= 10;
            if (BonusHp < 0)
            {
                BonusHp = 0;
            }
            Notes.Add("百炼：放逐「" + targets[0].CardName + "」，气血上限 -10");
        }
        else if (option.Kind == ForgeOptionKind.Quench)
        {
            targets[0].BonusShield += 2;
            targets[0].BonusHeal += 2;
            targets[0].BuffCount++;
            Notes.Add("百炼：淬甲「" + targets[0].CardName + "」护体/治疗 +2");
        }
        else if (option.Kind == ForgeOptionKind.Whet)
        {
            targets[0].BonusCountdown -= 0.5f;
            targets[0].BuffCount++;
            Notes.Add("百炼：磨砺「" + targets[0].CardName + "」蓄势 -0.5 秒");
        }
        else if (option.Kind == ForgeOptionKind.Vitality)
        {
            Deck.Remove(targets[0]);
            BonusHp += 15;
            Notes.Add("百炼：换命「" + targets[0].CardName + "」气血上限 +15");
        }
        else if (option.Kind == ForgeOptionKind.Transmute)
        {
            int newId = RandomSameTierCard(targets[0]);
            if (newId <= 0)
            {
                return false;
            }
            string oldName = targets[0].CardName;
            int index = Deck.IndexOf(targets[0]);
            RunCard fresh = new RunCard(newId, targets[0].Tier);
            if (index >= 0)
            {
                Deck[index] = fresh;
            }
            else
            {
                Deck.Add(fresh);
            }
            if (PendingForge != null)
            {
                int slot = PendingForge.Candidates.IndexOf(targets[0]);
                if (slot >= 0)
                {
                    PendingForge.Candidates[slot] = fresh;
                }
            }
            Notes.Add("百炼：变换「" + oldName + "」→「" + fresh.CardName + "」");
        }
        else if (option.Kind == ForgeOptionKind.Transcribe)
        {
            Deck.Add(targets[0].Clone());
            Notes.Add("百炼：抄录「" + targets[0].CardName + "」");
        }
        else if (option.Kind == ForgeOptionKind.Greed)
        {
            int fillerId = Rng.Next(2) == 0 ? StarterSlashId : StarterGuardId;
            RunCard filler = new RunCard(fillerId, CardTier.Copper);
            filler.NoUpgrade = true;   // 锁死：这张卡永远升不了阶，也合不了
            Deck.Add(filler);
            Notes.Add("百炼：贪婪，牌库多了一张「" + CardTierRules.Name(CardTier.Copper) + "阶基础卡」");
        }

        if (option.Gold > 0)
        {
            Earn(option.Gold);
        }

        PendingForge.Resolved = true;
        return true;
    }

    /// <summary>按权重抽一个下标。</summary>
    private int WeightedPick(List<ForgeOption> options)
    {
        int total = 0;
        for (int i = 0; i < options.Count; i++)
        {
            total += options[i].Weight;
        }
        if (total <= 0)
        {
            return Rng.Next(options.Count);
        }

        int roll = Rng.Next(total);
        for (int i = 0; i < options.Count; i++)
        {
            roll -= options[i].Weight;
            if (roll < 0)
            {
                return i;
            }
        }
        return options.Count - 1;
    }

    /// <summary>
    /// 随机换一张同阶的卡（BaseId 不同即可）。**不限本命** —— 这正是「变换」的价值：
    /// 有概率抽到别派绝学，等于用一张废卡赌一次。
    /// </summary>
    private int RandomSameTierCard(RunCard source)
    {
        List<CardConfig> all = ConfigManager.GetAll<CardConfig>();
        if (all == null)
        {
            return 0;
        }

        int baseId = CardTierRules.BaseIdOf(source.CardId);
        CardTier tier = source.Tier;
        List<int> pool = new List<int>();
        for (int i = 0; i < all.Count; i++)
        {
            CardConfig card = all[i];
            if (card.MinTier != tier)
            {
                continue;
            }
            if (CardTierRules.BaseIdOf(card.Id) == baseId)
            {
                continue;
            }
            pool.Add(card.Id);
        }

        if (pool.Count == 0)
        {
            return 0;
        }
        return pool[Rng.Next(pool.Count)];
    }

    /// <summary>贪婪塞的基础卡：劈砍 / 招架（都是不能合成的起始卡）。</summary>
    private const int StarterSlashId = 143;
    private const int StarterGuardId = 144;

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
