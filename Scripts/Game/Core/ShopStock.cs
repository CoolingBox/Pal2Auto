// 文件：ShopStock.cs
// 描述：集市 —— 一家店，4 格货架，按阶定价，刷新费逐次翻倍。
//
// 2026-11-24 改成「分店制」：
//   每次进集市时辰，开一家随机店（品类 × 档次），4 格全部来自这家店的池子。
//   品类决定「卖什么」（兵器铺 / 防具铺 / 药铺 / 符咒铺 / 蛊铺 / 器物阁 / 暗器铺 / 武学馆 / 杂货摊），
//   档次决定「卖多好的」（寻常：铜银 ／ 上等：银金 ／ 珍稀：金钻）。
//   刷新 = 同店重新进货，不换店 —— 换店会让「店」这个概念失去意义。
//
// 已删除旧版的「补对位」（第三格优先给能凑成合成对的卡）：用户明确不要。
// 合成现在只能靠随机撞上同名同阶 —— 这是刻意的取舍，想要稳定合成就去武学馆买本命卡。
//
// 规则见 Docs/卡牌等阶与商店.md。

using System;
using System.Collections.Generic;
using TinyFramework;

/// <summary>店铺品类。决定这家店卖哪一类的卡。</summary>
public enum ShopType
{
    General,   // 杂货摊：不限品类，纯按当天阶位概率
    Blade,     // 兵器铺：刀、剑、鞭、枪
    Armor,     // 防具铺：甲、伞
    Pill,      // 药铺：丹
    Talisman,  // 符咒铺：符
    Gu,        // 蛊铺：蛊
    Charm,     // 器物阁：法宝
    Dart,      // 暗器铺：镖、针、刺
    Skill,     // 武学馆：当前主角的本命招式（别派绝学不卖）
}

/// <summary>店铺档次。决定货架的阶位偏向，也决定它多稀有、多久出现。</summary>
public enum ShopGrade
{
    Common,  // 寻常：铜银为主
    Fine,    // 上等：银金为主
    Rare,    // 珍稀：金钻为主
}

public sealed class ShopOffer
{
    public RunCard Card;
    public int Price;
    public bool Sold;
}

public sealed class ShopStock
{
    /// <summary>货架格数。2026-11-25 从 3 提到 4：卡池涨到 278 之后，3 格太难凑齐同名卡做 2 合 1。</summary>
    public const int SlotCount = 4;

    public ShopType Kind = ShopType.General;
    public ShopGrade Grade = ShopGrade.Common;

    public List<ShopOffer> Offers = new List<ShopOffer>();

    /// <summary>当天已经刷新过几次（含免费那次）。换天时归零。</summary>
    public int RefreshCount;

    /// <summary>当天有几次免费刷新。默认 1；阅历 8 级后升到 2，换天时由 RunManager 写入。</summary>
    public int FreeRefreshCount = 1;

    // —————————————————————— 开店 ——————————————————————

    /// <summary>开一家新店并进货。每次进集市时辰调一次。</summary>
    /// <summary>这家店是哪天开的（对齐牌组阶位时要用当天的品阶上限）。</summary>
    public int Day = 1;

    public void Open(int day, int characterId, Random rng, List<RunCard> deck = null)
    {
        Day = day;
        Kind = RollKind(rng);
        Grade = RollGrade(day, rng);
        RefreshCount = 0;
        Restock(day, characterId, rng);
        AlignToDeck(deck);
    }

    /// <summary>
    /// 货架对齐牌组：**牌组里已经有某个族，这家店就只卖该族的最高那一阶**（2026-11-27 用户定）。
    ///
    /// 为什么：同族再获得会自动升阶，而升的是「牌组里阶位最低那张」。
    /// 如果牌组已经是「大刀·银」，店里却摆着「大刀·铜」，买下来只是把银升成金 —— 花铜的钱办银的事，
    /// 玩家会觉得莫名其妙。对齐之后，买到的永远是「和你手上同阶」的那张，升一阶就够。
    ///
    /// 已经到钻的族直接撤柜：买了也升不上去，纯浪费 240 两。
    /// </summary>
    /// <summary>
    /// 当天货架能卖到的最高阶。取自 `CardTierRules.DayWeights` 里最后一个非零项 ——
    /// 权重表本来就是「某阶权重 0 = 当天不出」，这里只是把它翻译成一个上限值。
    /// </summary>
    public CardTier MaxTierToday()
    {
        int[] weights = CardTierRules.DayWeights(Day);
        int last = 0;
        for (int i = 0; i < 4; i++)
        {
            if (weights[i] > 0)
            {
                last = i;
            }
        }
        return (CardTier)(last + 1);
    }

    private void AlignToDeck(List<RunCard> deck)
    {
        if (deck == null || deck.Count == 0)
        {
            return;
        }

        for (int i = Offers.Count - 1; i >= 0; i--)
        {
            ShopOffer offer = Offers[i];
            if (offer.Card == null || offer.Sold)
            {
                continue;
            }

            int baseId = CardTierRules.BaseIdOf(offer.Card.CardId);
            CardTier best = CardTier.Copper;
            bool found = false;

            for (int k = 0; k < deck.Count; k++)
            {
                if (CardTierRules.BaseIdOf(deck[k].CardId) != baseId)
                {
                    continue;
                }
                if (!found || (int)deck[k].Tier > (int)best)
                {
                    best = deck[k].Tier;
                    found = true;
                }
            }

            if (!found)
            {
                continue;
            }

            // 天数上限：第 2 天就算牌组里有金大刀，货架也不能摆金（DayWeights 里金的权重是 0）
            CardTier cap = MaxTierToday();
            CardTier target = best;
            if ((int)target > (int)cap)
            {
                target = cap;
            }

            if (best == CardTier.Diamond && (int)cap >= (int)CardTier.Diamond)
            {
                Offers.RemoveAt(i);
                continue;
            }

            if ((int)offer.Card.Tier >= (int)target)
            {
                continue;
            }

            int alignedId = CardTierRules.TierIdOf(baseId, target);
            CardConfig aligned = ConfigManager.GetOne<CardConfig>(alignedId);
            if (aligned == null)
            {
                continue;
            }

            offer.Card = new RunCard(alignedId, target);
            offer.Price = PriceOf(target, Grade);
        }
    }

    /// <summary>同店重新进货。刷新按钮调这个，不换店也不换档次。</summary>
    public void Restock(int day, int characterId, Random rng)
    {
        Offers.Clear();
        List<int> usedIds = new List<int>();

        for (int i = 0; i < SlotCount; i++)
        {
            CardTier tier = RollTier(day, Grade, rng);
            CardTier actualTier = tier;
            CardConfig config = CardPool.PickForShop(tier, Kind, characterId, rng, usedIds,
                out actualTier, MaxTierToday());
            if (config == null)
            {
                continue;
            }

            usedIds.Add(CardTierRules.BaseIdOf(config.Id));
            ShopOffer offer = new ShopOffer();
            offer.Card = new RunCard(config.Id, config.MinTier);
            offer.Price = PriceOf(actualTier, Grade);
            offer.Sold = false;
            Offers.Add(offer);
        }
    }

    public void ResetForNewDay()
    {
        RefreshCount = 0;
        Offers.Clear();
    }

    // —————————————————————— 品类与档次 ——————————————————————

    /// <summary>品类概率。武学馆只占一成多 —— 本命卡池小，天天开就腻了。</summary>
    private static ShopType RollKind(Random rng)
    {
        int roll = rng.Next(1000);

        if (roll < 60)
        {
            return ShopType.General;
        }
        if (roll < 180)
        {
            return ShopType.Blade;
        }
        if (roll < 300)
        {
            return ShopType.Armor;
        }
        if (roll < 420)
        {
            return ShopType.Pill;
        }
        if (roll < 540)
        {
            return ShopType.Talisman;
        }
        if (roll < 640)
        {
            return ShopType.Gu;
        }
        if (roll < 760)
        {
            return ShopType.Charm;
        }
        if (roll < 860)
        {
            return ShopType.Dart;
        }
        return ShopType.Skill;
    }

    /// <summary>
    /// 档次按天解锁：摊天天有；第 3 天起可能碰上「上等」；第 8 天起可能碰上「珍稀」。
    /// 与阶位概率表呼应 —— 店再好，也不能卖当天抽不出来的阶。
    /// </summary>
    private static ShopGrade RollGrade(int day, Random rng)
    {
        if (day >= 8 && rng.Next(100) < 20)
        {
            return ShopGrade.Rare;
        }
        if (day >= 3)
        {
            int fineChance = day >= 6 ? 50 : 35;
            if (rng.Next(100) < fineChance)
            {
                return ShopGrade.Fine;
            }
        }
        return ShopGrade.Common;
    }

    /// <summary>按档次偏移当天的阶位权重。当天概率为 0 的阶永远是 0。</summary>
    private static CardTier RollTier(int day, ShopGrade grade, Random rng)
    {
        int[] baseWeights = CardTierRules.DayWeights(day);
        int[] weights = new int[4];

        for (int i = 0; i < 4; i++)
        {
            weights[i] = baseWeights[i];
        }

        if (grade == ShopGrade.Common)
        {
            // 寻常：铜更常见
            weights[0] += 25;
        }
        else if (grade == ShopGrade.Fine)
        {
            // 上等：砍一半铜，翻倍金
            weights[0] /= 2;
            weights[1] += 10;
            weights[2] *= 2;
        }
        else if (grade == ShopGrade.Rare)
        {
            // 珍稀：没有铜，银减半，金翻倍，钻三倍
            weights[0] = 0;
            weights[1] /= 2;
            weights[2] *= 2;
            weights[3] *= 3;
        }

        for (int i = 0; i < 4; i++)
        {
            if (baseWeights[i] == 0)
            {
                weights[i] = 0;
            }
        }

        int total = 0;
        for (int i = 0; i < 4; i++)
        {
            total += weights[i];
        }
        if (total <= 0)
        {
            return CardTier.Copper;
        }

        int roll = rng.Next(total);
        int acc = 0;
        for (int i = 0; i < 4; i++)
        {
            if (weights[i] <= 0)
            {
                continue;
            }
            acc += weights[i];
            if (roll < acc)
            {
                return (CardTier)(i + 1);
            }
        }
        return CardTier.Copper;
    }

    /// <summary>档次溢价：上等 +10%，珍稀 +25%。档次高不只是货好，也贵一点。</summary>
    private static int PriceOf(CardTier tier, ShopGrade grade)
    {
        int price = CardTierRules.PriceOf(tier);
        if (grade == ShopGrade.Fine)
        {
            price = (int)Math.Round(price * 1.1f);
        }
        else if (grade == ShopGrade.Rare)
        {
            price = (int)Math.Round(price * 1.25f);
        }
        return price;
    }

    // —————————————————————— 刷新 ——————————————————————

    /// <summary>下一次刷新要花多少。0 = 免费（当天还有免费次数）。-1 = 不能再刷。</summary>
    public int NextRefreshCost
    {
        get
        {
            if (RefreshCount < FreeRefreshCount)
            {
                return 0;
            }
            int paid = RefreshCount - FreeRefreshCount;
            if (paid >= CardTierRules.RefreshMaxPaid)
            {
                return -1;
            }
            return CardTierRules.RefreshCost(paid);
        }
    }

    public bool CanRefresh
    {
        get
        {
            return NextRefreshCost >= 0;
        }
    }

    public void RegisterRefresh()
    {
        RefreshCount++;
    }

    public ShopOffer Get(int index)
    {
        if (index < 0 || index >= Offers.Count)
        {
            return null;
        }
        return Offers[index];
    }

    public int UnsoldCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Offers.Count; i++)
            {
                if (!Offers[i].Sold)
                {
                    count++;
                }
            }
            return count;
        }
    }

    // —————————————————————— 显示名 ——————————————————————

    public string ShopName
    {
        get
        {
            return GradeName(Grade) + KindName(Kind);
        }
    }

    public string ShopMotto
    {
        get
        {
            return KindMotto(Kind);
        }
    }

    public static string KindName(ShopType kind)
    {
        switch (kind)
        {
            case ShopType.Blade:
                return "兵器铺";
            case ShopType.Armor:
                return "防具铺";
            case ShopType.Pill:
                return "药铺";
            case ShopType.Talisman:
                return "符咒铺";
            case ShopType.Gu:
                return "蛊铺";
            case ShopType.Charm:
                return "器物阁";
            case ShopType.Dart:
                return "暗器铺";
            case ShopType.Skill:
                return "武学馆";
            default:
                return "杂货摊";
        }
    }

    public static string GradeName(ShopGrade grade)
    {
        switch (grade)
        {
            case ShopGrade.Fine:
                return "上等";
            case ShopGrade.Rare:
                return "珍稀";
            default:
                return "寻常";
        }
    }

    public static string KindMotto(ShopType kind)
    {
        switch (kind)
        {
            case ShopType.Blade:
                return "刀剑鞭枪，一应俱全";
            case ShopType.Armor:
                return "甲胄伞盖，挡得住刀";
            case ShopType.Pill:
                return "丹药济世，也救急";
            case ShopType.Talisman:
                return "符纸朱砂，专克邪祟";
            case ShopType.Gu:
                return "苗疆蛊虫，勿轻触";
            case ShopType.Charm:
                return "法宝奇珍，看缘法";
            case ShopType.Dart:
                return "镖针袖箭，见血封喉";
            case ShopType.Skill:
                return "本门武学，只传有缘人";
            default:
                return "什么都有一点";
        }
    }
}
