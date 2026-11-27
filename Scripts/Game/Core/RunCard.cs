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
    public EnchantType Enchant = EnchantType.None;

    /// <summary>
    /// 这张牌是几阶 —— **由 CardId 决定**（展开后每阶是独立条目），不再单独存。
    /// 2026-11-26 之前这里是字段，阶位是运行时的乘数；现在配表已经按阶展开，
    /// 再存一个 Tier 迟早会和 CardId 对不上（合成、商店、存档三处都要同步，漏一处就是「银卡当铜卡卖」）。
    /// </summary>
    public CardTier Tier
    {
        get
        {
            CardConfig cfg = Config;
            if (cfg == null)
            {
                return CardTier.Copper;
            }
            return cfg.MinTier;
        }
    }

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

    // —— 百炼台给的标记 ——

    /// <summary>先发：战斗开始时优先进入起手手牌。</summary>
    public bool PriorityDraw;

    /// <summary>后发：洗牌后沉到抽牌堆底。</summary>
    public bool BottomOfDeck;

    /// <summary>多动：多重次数修正（正数 = 多打几次）。</summary>
    public int BonusMulti;

    /// <summary>数值倍率（快枪 = 0.5）。乘在所有数值上，蓄势不乘。</summary>
    public float ValueScale = 1f;

    /// <summary>
    /// 锁死不能升级（贪婪塞进来的基础卡）。它既不能参与「再获得即升阶」，
    /// 也不能手动 2 合 1 —— 否则这张「负面塞卡」会被后续的自动升阶吃掉，贪婪就没有代价了。
    /// </summary>
    public bool NoUpgrade;

    public RunCard()
    {
    }

    /// <summary>tier 参数只为兼容旧调用（DevShot / BattlePanel），实际阶位由 CardId 决定。</summary>
    public RunCard(int cardId, CardTier tier)
    {
        CardId = cardId;
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
            value = (int)System.Math.Round(value * ValueScale, System.MidpointRounding.AwayFromZero);
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
            value = (int)System.Math.Round(value * ValueScale, System.MidpointRounding.AwayFromZero);
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
            value = (int)System.Math.Round(value * ValueScale, System.MidpointRounding.AwayFromZero);
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
            value = (int)System.Math.Round(value * ValueScale, System.MidpointRounding.AwayFromZero);
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
        copy.Enchant = Enchant;
        copy.BonusDamage = BonusDamage;
        copy.BonusShield = BonusShield;
        copy.BonusHeal = BonusHeal;
        copy.BonusPoison = BonusPoison;
        copy.BonusExpose = BonusExpose;
        copy.BonusCountdown = BonusCountdown;
        copy.BuffCount = BuffCount;
        copy.PriorityDraw = PriorityDraw;
        copy.BottomOfDeck = BottomOfDeck;
        copy.BonusMulti = BonusMulti;
        copy.ValueScale = ValueScale;
        copy.NoUpgrade = NoUpgrade;
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
        copy.PriorityDraw = PriorityDraw;
        copy.BottomOfDeck = BottomOfDeck;

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
        copy.BaseId = cfg.BaseId;
        // 下面两个是「后加的机制字段」，曾经漏在这里 ——
        // 漏了的表现是：面板上写得清清楚楚，进战斗一点效果没有（野球拳不塞牌、催命符不催命）。
        // 以后往 CardConfig 加机制字段，先来这里补一行。
        copy.SpawnCardId = cfg.SpawnCardId;
        copy.FatigueShift = cfg.FatigueShift;
        // MultiCount 的语义是「打几次」：配置里 0 和 1 都表示单次，
        // 所以「多动 +1」要先把它归一到 1 再加，否则对单次卡 +1 还是 1（等于没加）。
        int baseMulti = cfg.MultiCount <= 1 ? 1 : cfg.MultiCount;
        copy.MultiCount = baseMulti + BonusMulti;
        if (copy.MultiCount < 1)
        {
            copy.MultiCount = 1;
        }

        return copy;
    }

    /// <summary>能不能和另一张合成（同名同阶、且未到钻、且没被锁死）。</summary>
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
