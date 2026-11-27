// 文件：CardTierRules.cs
// 描述：卡牌等阶（铜/银/金/钻）的数值规则、价格与刷新费。
//
// 设计前提：
//   1. 等阶是**卡牌实例**的属性，不是配置的属性。配置里只有 MinTier（这张卡最低能是几阶）。
//   2. 同名牌同阶两张 → 合一张高一阶（2 合 1）。
//   3. 数值类升阶放大效果，机制类升阶缩短蓄势。两者混在一起时都取保守系数。
//
// 规则原文见 Docs/卡牌等阶与商店.md，改这里等于改全局平衡。

using System;

/// <summary>卡牌等阶。与敌人的「青铜/白银/黄金/钻石/传奇」五阶不是一套，卡牌只有四阶。</summary>
public enum CardTier
{
    Copper = 1,   // 铜
    Silver = 2,   // 银
    Gold = 3,     // 金
    Diamond = 4,  // 钻
}

public static class CardTierRules
{
    /// <summary>最高阶。到顶后不能再合。</summary>
    public const int MaxTier = 4;

    /// <summary>合成一张高一阶需要的同名牌数量。</summary>
    public const int MergeCount = 2;

    // —— 升阶系数 ——
    // 主规则：每阶 ×1.6（即「加 0.6 倍」，落在用户要求的 0.5~1 倍区间中段）。
    public const float NumericGrowth = 1.6f;
    // 快卡的例外：CD ≤3 秒的卡，同样的乘数会让它的 dps 涨得比慢卡猛三倍
    // （铜大刀 4.4 dps → 钻大刀 18.3 dps，而同阶绝学只有 6.7 dps）。
    // 所以快卡压到 ×1.35，中速 ×1.45，慢卡才是 ×1.60。
    public const float FastGrowth = 1.35f;
    public const float MidGrowth = 1.45f;
    // 带机制的卡在上面的基础上再打这个折扣（机制本身已经是收益，数值不该再全额涨）。
    public const float MechanicDiscount = 0.85f;
    // 纯机制卡：不放大数值，只缩蓄势时间 ×0.90（越快越早触发）。
    public const float MechanicCooling = 0.90f;
    // 混合卡的蓄势缩短。
    public const float MixedCooling = 0.95f;
    // 蓄势时间的下限：再快也不能低于 0.8 秒，否则读条看不见。
    public const float MinCountdown = 0.8f;

    // —— 商店 ——
    // 价格阶梯刻意满足「2 × 本阶 < 上一阶」：铜 20 → 银 45（省 5），银 45 → 金 105（省 15），
    // 金 105 → 钻 240（省 30）。合成永远比直接买高一阶便宜一点，否则合成没人用。
    private static readonly int[] TierPrices = { 0, 20, 45, 105, 240 };

    /// <summary>商店第一次刷新免费，之后这个基数逐次翻倍。</summary>
    public const int RefreshBaseCost = 10;

    /// <summary>一天内最多花钱刷新几次（免费那次不算）。</summary>
    public const int RefreshMaxPaid = 5;

    /// <summary>展开后的 id 规则：铜 = 原 id、银 +1000、金 +2000、钻 +3000。</summary>
    public const int TierIdStep = 1000;

    public static int TierIdOf(int baseId, CardTier tier)
    {
        // 注意：CardTier 枚举是 Copper=1 / Silver=2 / Gold=3 / Diamond=4（不是 0~3），
        // 而 id 区间是 0/1000/2000/3000 —— 换算时必须减 1，否则铜卡升阶会直接跳到金。
        return baseId + TierIdStep * ((int)tier - 1);
    }

    public static int BaseIdOf(int cardId)
    {
        return cardId % TierIdStep;
    }

    public static CardTier TierOfId(int cardId)
    {
        int rank = cardId / TierIdStep;
        if (rank < 0)
        {
            rank = 0;
        }
        if (rank > 3)
        {
            rank = 3;
        }
        // rank 是 0~3，CardTier 是 1~4
        return (CardTier)(rank + 1);
    }

    public static string Name(CardTier tier)
    {
        if (tier == CardTier.Copper)
        {
            return "铜";
        }
        if (tier == CardTier.Silver)
        {
            return "银";
        }
        if (tier == CardTier.Gold)
        {
            return "金";
        }
        if (tier == CardTier.Diamond)
        {
            return "钻";
        }
        return "—";
    }

    /// <summary>高一阶。已到钻则原样返回。</summary>
    public static CardTier Next(CardTier tier)
    {
        int next = (int)tier + 1;
        if (next > MaxTier)
        {
            return CardTier.Diamond;
        }
        return (CardTier)next;
    }

    public static bool CanMerge(CardTier tier)
    {
        return (int)tier < MaxTier;
    }

    /// <summary>商店售价。None 返回 0。</summary>
    public static int PriceOf(CardTier tier)
    {
        int index = (int)tier;
        if (index < 1 || index > MaxTier)
        {
            return 0;
        }
        return TierPrices[index];
    }

    /// <summary>一天内第 paidIndex 次付费刷新的价格（paidIndex 从 0 起）：10 / 20 / 40 / 80 / 160。</summary>
    public static int RefreshCost(int paidIndex)
    {
        if (paidIndex < 0 || paidIndex >= RefreshMaxPaid)
        {
            return 0;
        }
        int cost = RefreshBaseCost;
        for (int i = 0; i < paidIndex; i++)
        {
            cost *= 2;
        }
        return cost;
    }

    /// <summary>这张卡带「机制」词条吗（改上场数、时机、多重）。</summary>
    public static bool HasMechanic(CardConfig card)
    {
        if (card == null)
        {
            return false;
        }
        if (card.HandSelf != 0 || card.HandFoe != 0)
        {
            return true;
        }
        if (card.HasteTime > 0f || card.SlowTime > 0f || card.FreezeTime > 0f)
        {
            return true;
        }
        if (card.ChargeCount > 0 || card.MultiCount > 1)
        {
            return true;
        }
        return false;
    }

    /// <summary>这张卡带「数值」词条吗（伤害/法术/护体/回复/毒）。</summary>
    public static bool HasNumeric(CardConfig card)
    {
        if (card == null)
        {
            return false;
        }
        if (card.Damage > 0 || card.Spell > 0)
        {
            return true;
        }
        if (card.Shield > 0 || card.Heal > 0 || card.Poison > 0)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 这张卡每升一阶的数值倍率。按蓄势时间分档 —— 快卡收益被压住，否则升满阶的快卡会把绝学挤成废牌。
    /// </summary>
    public static float ValueGrowth(CardConfig card)
    {
        if (card == null)
        {
            return NumericGrowth;
        }

        float growth = NumericGrowth;
        if (card.Countdown <= 3.0f)
        {
            growth = FastGrowth;
        }
        else if (card.Countdown <= 5.0f)
        {
            growth = MidGrowth;
        }

        if (HasMechanic(card))
        {
            growth *= MechanicDiscount;
        }
        return growth;
    }

    /// <summary>
    /// 升阶后的效果数值。以卡牌的 MinTier 为基准，每高一阶乘 ValueGrowth。
    /// 只放大正数字段；破绽层数、时机秒数、上场数这些不跟着涨（涨了会失控）。
    /// </summary>
    public static int ScaledValue(CardConfig card, int baseValue, CardTier tier)
    {
        if (card == null || baseValue <= 0)
        {
            return baseValue;
        }

        int steps = (int)tier - (int)card.MinTier;
        if (steps <= 0)
        {
            return baseValue;
        }

        float growth = ValueGrowth(card);
        float factor = 1f;
        for (int i = 0; i < steps; i++)
        {
            factor *= growth;
        }

        int scaled = (int)Math.Round(baseValue * factor, MidpointRounding.AwayFromZero);
        if (scaled <= baseValue)
        {
            scaled = baseValue + 1;
        }
        return scaled;
    }

    /// <summary>
    /// 升阶后的蓄势时间。纯机制卡每阶 ×0.90；混合卡 ×0.95；纯数值卡不动。
    /// </summary>
    public static float ScaledCountdown(CardConfig card, float baseCountdown, CardTier tier)
    {
        if (card == null || baseCountdown <= 0f)
        {
            return baseCountdown;
        }

        int steps = (int)tier - (int)card.MinTier;
        if (steps <= 0)
        {
            return baseCountdown;
        }

        bool mechanic = HasMechanic(card);
        bool numeric = HasNumeric(card);
        if (!mechanic)
        {
            return baseCountdown;
        }

        float cooling = MechanicCooling;
        if (numeric)
        {
            cooling = MixedCooling;
        }

        float factor = 1f;
        for (int i = 0; i < steps; i++)
        {
            factor *= cooling;
        }

        float scaled = baseCountdown * factor;
        if (scaled < MinCountdown)
        {
            scaled = MinCountdown;
        }
        return (float)Math.Round(scaled, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 当天各阶出现权重的近似读法：返回铜/银/金/钻四档权重之和。
    /// 真实概率表在 Docs/卡牌等阶与商店.md，这里的数字供内核抽卡时校对。
    /// </summary>
    public static int[] DayWeights(int day)
    {
        if (day <= 1)
        {
            return new[] { 80, 20, 0, 0 };
        }
        if (day == 2)
        {
            return new[] { 65, 35, 0, 0 };
        }
        if (day == 3)
        {
            return new[] { 55, 36, 9, 0 };
        }
        if (day == 4)
        {
            return new[] { 45, 40, 15, 0 };
        }
        if (day == 5)
        {
            return new[] { 38, 40, 22, 0 };
        }
        if (day == 6)
        {
            return new[] { 30, 40, 25, 5 };
        }
        if (day == 7)
        {
            return new[] { 24, 38, 30, 8 };
        }
        if (day == 8)
        {
            return new[] { 18, 36, 34, 12 };
        }
        if (day == 9)
        {
            return new[] { 12, 33, 38, 17 };
        }
        if (day == 10)
        {
            return new[] { 8, 30, 40, 22 };
        }
        if (day == 11)
        {
            return new[] { 4, 26, 42, 28 };
        }
        if (day == 12)
        {
            return new[] { 0, 22, 44, 34 };
        }
        if (day == 13)
        {
            return new[] { 0, 18, 44, 38 };
        }
        if (day == 14)
        {
            return new[] { 0, 12, 44, 44 };
        }
        return new[] { 0, 8, 42, 50 };
    }
}
