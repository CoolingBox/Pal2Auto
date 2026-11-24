// 文件：ShopStock.cs
// 描述：集市 —— 一家店，3 格货架，按阶定价，刷新费逐次翻倍。
//
// 2026-11-24 改成「分店制」：
//   每次进集市时辰，开一家随机店（品类 × 档次），3 格全部来自这家店的池子。
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
public enum ShopKind
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
    public const int SlotCount = 3;

    public ShopKind Kind = ShopKind.General;
    public ShopGrade Grade = ShopGrade.Common;

    public List<ShopOffer> Offers = new List<ShopOffer>();

    /// <summary>当天已经刷新过几次（含免费那次）。换天时归零。</summary>
    public int RefreshCount;

    /// <summary>当天有几次免费刷新。默认 1；阅历 8 级后升到 2，换天时由 RunManager 写入。</summary>
    public int FreeRefreshCount = 1;

    // —————————————————————— 开店 ——————————————————————

    /// <summary>开一家新店并进货。每次进集市时辰调一次。</summary>
    public void Open(int day, int characterId, Random rng)
    {
        Kind = RollKind(rng);
        Grade = RollGrade(day, rng);
        RefreshCount = 0;
        Restock(day, characterId, rng);
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
            CardConfig config = CardPool.PickForShop(tier, Kind, characterId, rng, usedIds, out actualTier);
            if (config == null)
            {
                continue;
            }

            usedIds.Add(config.Id);
            ShopOffer offer = new ShopOffer();
            offer.Card = new RunCard(config.Id, actualTier);
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
    private static ShopKind RollKind(Random rng)
    {
        int roll = rng.Next(1000);

        if (roll < 60)
        {
            return ShopKind.General;
        }
        if (roll < 180)
        {
            return ShopKind.Blade;
        }
        if (roll < 300)
        {
            return ShopKind.Armor;
        }
        if (roll < 420)
        {
            return ShopKind.Pill;
        }
        if (roll < 540)
        {
            return ShopKind.Talisman;
        }
        if (roll < 640)
        {
            return ShopKind.Gu;
        }
        if (roll < 760)
        {
            return ShopKind.Charm;
        }
        if (roll < 860)
        {
            return ShopKind.Dart;
        }
        return ShopKind.Skill;
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

    public static string KindName(ShopKind kind)
    {
        switch (kind)
        {
            case ShopKind.Blade:
                return "兵器铺";
            case ShopKind.Armor:
                return "防具铺";
            case ShopKind.Pill:
                return "药铺";
            case ShopKind.Talisman:
                return "符咒铺";
            case ShopKind.Gu:
                return "蛊铺";
            case ShopKind.Charm:
                return "器物阁";
            case ShopKind.Dart:
                return "暗器铺";
            case ShopKind.Skill:
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

    public static string KindMotto(ShopKind kind)
    {
        switch (kind)
        {
            case ShopKind.Blade:
                return "刀剑鞭枪，一应俱全";
            case ShopKind.Armor:
                return "甲胄伞盖，挡得住刀";
            case ShopKind.Pill:
                return "丹药济世，也救急";
            case ShopKind.Talisman:
                return "符纸朱砂，专克邪祟";
            case ShopKind.Gu:
                return "苗疆蛊虫，勿轻触";
            case ShopKind.Charm:
                return "法宝奇珍，看缘法";
            case ShopKind.Dart:
                return "镖针袖箭，见血封喉";
            case ShopKind.Skill:
                return "本门武学，只传有缘人";
            default:
                return "什么都有一点";
        }
    }
}
