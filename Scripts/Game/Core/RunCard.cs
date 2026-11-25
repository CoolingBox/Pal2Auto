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

    // —— 实例修正 ——
    // 事件（奇遇、奖励、诅咒）可以直接改「这一张牌」，不能去改 CardConfig
    // —— 那是全局配置，改一张等于改了所有同名卡，而且跟着存档走不了。
    // 最终数值 = 配置值 × 阶位缩放 + 附魔 + 这里的修正。
    public int BonusDamage;
    public int BonusShield;
    public int BonusHeal;
    public int BonusPoison;
    public int BonusExpose;

    /// <summary>蓄势修正（秒）。负数 = 更快，结果会被 Clamp 到 MinCountdown。</summary>
    public float BonusCountdown;

    /// <summary>这张卡被事件改过几次，界面用来标一个记号。</summary>
    public int BuffCount;

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
            int value = ApplySharp(CardTierRules.ScaledValue(cfg, cfg.Damage, Tier));
            return System.Math.Max(0, value + BonusDamage);
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
            int value = ApplySharp(CardTierRules.ScaledValue(cfg, cfg.Spell, Tier));
            return System.Math.Max(0, value + BonusDamage);
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
            return System.Math.Max(0, value + BonusShield);
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
            int value = CardTierRules.ScaledValue(cfg, cfg.Heal, Tier);
            return System.Math.Max(0, value + BonusHeal);
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
            return System.Math.Max(0, value + BonusPoison);
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
            value += BonusCountdown;
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
        copy.BonusDamage = BonusDamage;
        copy.BonusShield = BonusShield;
        copy.BonusHeal = BonusHeal;
        copy.BonusPoison = BonusPoison;
        copy.BonusExpose = BonusExpose;
        copy.BonusCountdown = BonusCountdown;
        copy.BuffCount = BuffCount;
        return copy;
    }


    /// <summary>
    /// 转成战斗用的 CardConfig 副本：数值已经按阶位与附魔算好。
    ///
    /// 为什么要有这一步：`DeckBattle` 只认 CardConfig，而 CardConfig 是「配置表里的原始值」。
    /// 直接把 CardId 丢进去，合成升阶（15 两一次）和淬炉附魔在战斗里就完全没效果 ——
    /// 玩家花了钱升到银卡，打出来还是铜卡的伤害。
    /// 这里做一份副本，把 RunCard 的缩放值写进去，DeckBattle 不用改一行。
    /// </summary>
    public CardConfig ToBattleConfig()
    {
        CardConfig cfg = Config;
        if (cfg == null)
        {
            return null;
        }

        CardConfig copy = new CardConfig();
        copy.Id = cfg.Id;
        copy.Name = cfg.Name;
        copy.Type = cfg.Type;
        copy.Tags = cfg.Tags;
        copy.Icon = cfg.Icon;
        copy.Desc = cfg.Desc;
        copy.Keywords = cfg.Keywords;
        copy.Element = cfg.Element;
        copy.Scope = cfg.Scope;
        copy.Master = cfg.Master;
        copy.MinTier = cfg.MinTier;
        copy.StarterOnly = cfg.StarterOnly;

        // 数值走 RunCard 的已缩放属性（阶位 → 附魔），不读 cfg 的原值
        copy.Countdown = Countdown;
        copy.Damage = Damage;
        copy.Spell = Spell;
        copy.Shield = Shield;
        copy.Heal = Heal;
        copy.Poison = Poison;

        // 机制类不随阶位涨（见 Docs/卡牌等阶与商店.md），原样抄
        copy.Expose = System.Math.Max(0, cfg.Expose + BonusExpose);
        copy.IgnoreShield = cfg.IgnoreShield;
        copy.HandSelf = cfg.HandSelf;
        copy.HandFoe = cfg.HandFoe;
        copy.HandTime = cfg.HandTime;
        copy.HasteCount = cfg.HasteCount;
        copy.HasteTime = cfg.HasteTime;
        copy.SlowCount = cfg.SlowCount;
        copy.SlowTime = cfg.SlowTime;
        copy.FreezeCount = cfg.FreezeCount;
        copy.FreezeTime = cfg.FreezeTime;
        copy.ChargeCount = cfg.ChargeCount;
        copy.MultiCount = cfg.MultiCount;

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
