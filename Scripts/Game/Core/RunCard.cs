// 文件：RunCard.cs
// 描述：一局里的「一张牌」——配置（CardConfig）+ 当前阶位（CardTier）+ 附魔。
//
// 为什么不能只用 CardConfig.Id：2 合 1 升阶改的是「这一张牌」的阶，
// 同名的两张铜卡与一张银卡在牌组里必须能区分开。所以牌组是 List<RunCard>，不是 List<int>。
//
// 数值一律从这里取（Damage/Countdown 等），不要在 UI 或内核里直接读 CardConfig.Damage，
// 否则升阶对战斗完全没影响。

using TinyFramework;

/// <summary>淬炉附魔。每天免费一次、每张牌只能有一个。</summary>
public enum EnchantType
{
    None,     // 未附魔
    Sharp,    // 锋锐：伤害 +25%
    Swift,    // 疾风：蓄势时间 -15%
    Sturdy,   // 厚土：护体 +2
    Venom,    // 蚀骨：中毒 +1
}

public sealed class RunCard
{
    public int CardId;
    public CardTier Tier = CardTier.Copper;
    public EnchantType Enchant = EnchantType.None;

    public RunCard()
    {
    }

    public RunCard(int cardId, CardTier tier)
    {
        CardId = cardId;
        Tier = tier;
    }

    public CardConfig Config
    {
        get
        {
            return ConfigManager.GetOne<CardConfig>(CardId);
        }
    }

    public bool IsValid
    {
        get
        {
            return Config != null;
        }
    }

    public string CardName
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return "未知卡牌";
            }
            return cfg.Name;
        }
    }

    /// <summary>带阶位的显示名，例如「大刀·银」。</summary>
    public string DisplayName
    {
        get
        {
            return CardName + "·" + CardTierRules.Name(Tier);
        }
    }

    public string Icon
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return "";
            }
            return cfg.Icon;
        }
    }

    // —— 按阶缩放后的数值 ——
    // 附魔在缩放之后再算，顺序不能反：先升阶（乘 1.35~1.6），再附魔（乘 1.25）。

    public int Damage
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return 0;
            }
            return ApplySharp(CardTierRules.ScaledValue(cfg, cfg.Damage, Tier));
        }
    }

    public int Spell
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return 0;
            }
            return ApplySharp(CardTierRules.ScaledValue(cfg, cfg.Spell, Tier));
        }
    }

    public int Shield
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return 0;
            }
            int value = CardTierRules.ScaledValue(cfg, cfg.Shield, Tier);
            if (Enchant == EnchantType.Sturdy && value > 0)
            {
                value += 2;
            }
            return value;
        }
    }

    public int Heal
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return 0;
            }
            return CardTierRules.ScaledValue(cfg, cfg.Heal, Tier);
        }
    }

    public int Poison
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return 0;
            }
            int value = CardTierRules.ScaledValue(cfg, cfg.Poison, Tier);
            if (Enchant == EnchantType.Venom && value > 0)
            {
                value += 1;
            }
            return value;
        }
    }

    /// <summary>蓄势时间（秒）。机制类升阶会缩短。</summary>
    public float Countdown
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return 2f;
            }
            float value = CardTierRules.ScaledCountdown(cfg, cfg.Countdown, Tier);
            if (Enchant == EnchantType.Swift)
            {
                value *= 0.85f;
            }
            if (value < CardTierRules.MinCountdown)
            {
                value = CardTierRules.MinCountdown;
            }
            return value;
        }
    }

    private int ApplySharp(int value)
    {
        if (value <= 0)
        {
            return value;
        }
        if (Enchant != EnchantType.Sharp)
        {
            return value;
        }
        float scaled = value * 1.25f;
        int rounded = (int)System.Math.Round(scaled, System.MidpointRounding.AwayFromZero);
        if (rounded <= value)
        {
            rounded = value + 1;
        }
        return rounded;
    }

    public RunCard Clone()
    {
        RunCard copy = new RunCard();
        copy.CardId = CardId;
        copy.Tier = Tier;
        copy.Enchant = Enchant;
        return copy;
    }

    /// <summary>能不能和另一张合成（同名同阶、且未到钻）。</summary>
    public bool CanMergeWith(RunCard other)
    {
        if (other == null)
        {
            return false;
        }
        if (CardId != other.CardId)
        {
            return false;
        }
        if (Tier != other.Tier)
        {
            return false;
        }
        return CardTierRules.CanMerge(Tier);
    }
}
