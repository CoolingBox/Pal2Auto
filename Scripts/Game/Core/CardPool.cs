// 文件：CardPool.cs
// 描述：按「当天阶位概率 + 主角偏好」从 138 张卡里抽货架/奖励。
//
// 两道筛选，顺序不能反：
//   1. 先按当天的阶位权重抽出「这一格是几阶」（CardTierRules.DayWeights）
//   2. 再在该阶的候选池里按主角偏好加权（60/30/10）
//
// 「该阶的候选池」= 起步阶 ≤ 目标阶的卡。起步银的卡不会出现在铜阶货架上，
// 这是起步阶唯一的实际作用 —— 它决定一张卡「最早能什么时候见到」。

using System;
using System.Collections.Generic;
using TinyFramework;

public static class CardPool
{
    // 集市/宿敌的偏好权重 60/30/10（Docs/装备与技能设计.md 第三节）。
    public const int ShopPreferredWeight = 60;
    public const int ShopNeutralWeight = 30;
    public const int ShopAvoidedWeight = 10;

    // 遭遇奖励是 65/25/10。
    public const int RewardPreferredWeight = 65;
    public const int RewardNeutralWeight = 25;
    public const int RewardAvoidedWeight = 10;

    public static List<CardConfig> All()
    {
        return ConfigManager.GetAll<CardConfig>();
    }

    /// <summary>按当天权重抽一个阶。第 1 天只可能出铜或银。</summary>
    public static CardTier RollTier(int day, Random rng)
    {
        int[] weights = CardTierRules.DayWeights(day);
        int total = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            total += weights[i];
        }
        if (total <= 0)
        {
            return CardTier.Copper;
        }

        int roll = rng.Next(total);
        int acc = 0;
        for (int i = 0; i < weights.Length; i++)
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

    /// <summary>集市/宿敌的偏好抽样：60 偏好 / 30 中立 / 10 非偏好。</summary>
    public static Affinity RollShopAffinity(Random rng)
    {
        return RollAffinity(rng, ShopPreferredWeight, ShopNeutralWeight, ShopAvoidedWeight);
    }

    /// <summary>遭遇奖励的偏好抽样：65 / 25 / 10。</summary>
    public static Affinity RollRewardAffinity(Random rng)
    {
        return RollAffinity(rng, RewardPreferredWeight, RewardNeutralWeight, RewardAvoidedWeight);
    }

    private static Affinity RollAffinity(Random rng, int preferred, int neutral, int avoided)
    {
        int total = preferred + neutral + avoided;
        if (total <= 0)
        {
            return Affinity.Neutral;
        }

        int roll = rng.Next(total);
        if (roll < preferred)
        {
            return Affinity.Preferred;
        }
        if (roll < preferred + neutral)
        {
            return Affinity.Neutral;
        }
        return Affinity.Avoided;
    }

    /// <summary>能不能进商店/奖励池。起始牌组专用卡（劈砍、招架）永远不进。</summary>
    public static bool ShopEligible(CardConfig card)
    {
        if (card == null)
        {
            return false;
        }
        return !card.StarterOnly;
    }

    /// <summary>该阶能吃到的卡：起步阶不高于目标阶。</summary>
    public static bool TierAvailable(CardConfig card, CardTier tier)
    {
        if (card == null)
        {
            return false;
        }
        return (int)card.MinTier <= (int)tier;
    }

    /// <summary>该阶 + 该倾向的候选池。</summary>
    public static List<CardConfig> Candidates(CardTier tier, int characterId, Affinity affinity)
    {
        List<CardConfig> result = new List<CardConfig>();
        List<CardConfig> all = All();
        if (all == null)
        {
            return result;
        }

        for (int i = 0; i < all.Count; i++)
        {
            CardConfig card = all[i];
            if (!ShopEligible(card))
            {
                continue;
            }
            if (!TierAvailable(card, tier))
            {
                continue;
            }
            if (AffinityRules.Of(characterId, card) != affinity)
            {
                continue;
            }
            result.Add(card);
        }
        return result;
    }

    /// <summary>该阶的全部卡（不分倾向）。</summary>
    public static List<CardConfig> CandidatesAnyAffinity(CardTier tier)
    {
        List<CardConfig> result = new List<CardConfig>();
        List<CardConfig> all = All();
        if (all == null)
        {
            return result;
        }

        for (int i = 0; i < all.Count; i++)
        {
            if (!ShopEligible(all[i]))
            {
                continue;
            }
            if (TierAvailable(all[i], tier))
            {
                result.Add(all[i]);
            }
        }
        return result;
    }

    /// <summary>抽一张。excludeIds 是本次货架已经用过的卡 id，避免三格出同一张。
    /// 抽不到时逐级放宽：本倾向 → 全倾向 → 该阶的全部卡，最后才返回 null。
    /// </summary>
    public static CardConfig Pick(CardTier tier, int characterId, Affinity affinity, Random rng, List<int> excludeIds)
    {
        List<CardConfig> pool = Candidates(tier, characterId, affinity);
        List<CardConfig> usable = WithoutExcluded(pool, excludeIds);
        if (usable.Count > 0)
        {
            return usable[rng.Next(usable.Count)];
        }

        List<CardConfig> anyAffinity = CandidatesAnyAffinity(tier);
        usable = WithoutExcluded(anyAffinity, excludeIds);
        if (usable.Count > 0)
        {
            return usable[rng.Next(usable.Count)];
        }

        // 该阶的卡全部已经在货架上了（早期阶位池子浅时会遇到），允许重复。
        if (anyAffinity.Count > 0)
        {
            return anyAffinity[rng.Next(anyAffinity.Count)];
        }
        return null;
    }

    // —————————————————————— 分店制：按品类抽卡 ——————————————————————

    /// <summary>
    /// 店铺品类对应的标签。General 返回 null（不限品类）。
    /// 一张卡只要命中其中一个标签就算这家店的货。
    /// </summary>
    public static string[] KindTags(ShopKind kind)
    {
        switch (kind)
        {
            case ShopKind.Blade:
                return new string[] { "Blade", "Sword", "Whip", "Spear" };
            case ShopKind.Armor:
                return new string[] { "Armor", "Fan" };
            case ShopKind.Pill:
                return new string[] { "Pill" };
            case ShopKind.Talisman:
                return new string[] { "Talisman" };
            case ShopKind.Gu:
                return new string[] { "Gu" };
            case ShopKind.Charm:
                return new string[] { "Charm" };
            case ShopKind.Dart:
                return new string[] { "Dart" };
            case ShopKind.Skill:
                // 武学馆卖的是「家族招式」，不含通用绝学（Ultimate 单独存在于通用卡上）
                return new string[]
                {
                    "Palm", "SaberArt", "Tiger", "SwordArt", "Formation",
                    "Serpent", "Fox", "Transform", "Spirit", "Incantation",
                };
            default:
                return null;
        }
    }

    /// <summary>这张卡是不是这家店的货。</summary>
    public static bool MatchesKind(CardConfig card, ShopKind kind, int characterId)
    {
        if (card == null)
        {
            return false;
        }
        if (kind == ShopKind.General)
        {
            // 杂货摊只排除别派本命卡：那类卡买来对当前主角没用
            return card.Owner == 0 || card.Owner == characterId;
        }

        string[] tags = KindTags(kind);
        if (tags == null)
        {
            return true;
        }

        bool hit = false;
        for (int i = 0; i < tags.Length; i++)
        {
            if (card.HasTag(tags[i]))
            {
                hit = true;
                break;
            }
        }
        if (!hit)
        {
            return false;
        }

        // 武学馆之外也要挡住别派本命卡（比如别派的剑法不该出现在杂货摊）
        if (card.Owner != 0 && card.Owner != characterId)
        {
            return false;
        }
        return true;
    }

    /// <summary>该店该阶的全部货。</summary>
    public static List<CardConfig> CandidatesForShop(CardTier tier, ShopKind kind, int characterId)
    {
        List<CardConfig> result = new List<CardConfig>();
        List<CardConfig> all = All();
        if (all == null)
        {
            return result;
        }

        for (int i = 0; i < all.Count; i++)
        {
            CardConfig card = all[i];
            if (!ShopEligible(card))
            {
                continue;
            }
            if (!TierAvailable(card, tier))
            {
                continue;
            }
            if (!MatchesKind(card, kind, characterId))
            {
                continue;
            }
            result.Add(card);
        }
        return result;
    }

    /// <summary>
    /// 按店铺抽一张。actualTier 是这张卡实际落在的阶 ——
    /// 有些品类在某阶本来就没货（比如器物阁最便宜的法宝是银阶的玉菩提），
    /// 这时往上找最近的、有货的阶，价格也按那个阶算，而不是硬塞一张别品类的卡。
    /// </summary>
    public static CardConfig PickForShop(CardTier tier, ShopKind kind, int characterId,
        Random rng, List<int> excludeIds, out CardTier actualTier)
    {
        int start = (int)tier;
        for (int t = start; t <= CardTierRules.MaxTier; t++)
        {
            List<CardConfig> pool = CandidatesForShop((CardTier)t, kind, characterId);
            List<CardConfig> usable = WithoutExcluded(pool, excludeIds);
            if (usable.Count == 0)
            {
                continue;
            }
            actualTier = (CardTier)t;
            return usable[rng.Next(usable.Count)];
        }

        // 这家店在这个阶以上彻底没货（不该发生），退回该阶任意卡，别让货架空着
        actualTier = tier;
        List<CardConfig> any = CandidatesAnyAffinity(tier);
        List<CardConfig> fallback = WithoutExcluded(any, excludeIds);
        if (fallback.Count > 0)
        {
            return fallback[rng.Next(fallback.Count)];
        }
        if (any.Count > 0)
        {
            return any[rng.Next(any.Count)];
        }
        return null;
    }

    private static List<CardConfig> WithoutExcluded(List<CardConfig> pool, List<int> excludeIds)
    {
        List<CardConfig> result = new List<CardConfig>();
        for (int i = 0; i < pool.Count; i++)
        {
            bool used = false;
            if (excludeIds != null)
            {
                for (int k = 0; k < excludeIds.Count; k++)
                {
                    if (excludeIds[k] == pool[i].Id)
                    {
                        used = true;
                        break;
                    }
                }
            }
            if (!used)
            {
                result.Add(pool[i]);
            }
        }
        return result;
    }
}
