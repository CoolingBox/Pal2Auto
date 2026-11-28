// 文件：CardPowerUtility.cs
// 描述：卡牌分值 —— 把一张卡折算成「每秒等价伤害」。
//
// 与 Tools/card_power.py 同一套公式（那边用配置值做离线评估，这边用 RunCard 的运行时值，
// 所以能算进附魔、百炼台改过的数值、阶位）。
//
// 公式：
//    每秒价值 = (伤害 × 次数 + 护体 × 0.6 + 治疗 × 0.5) / 蓄势
//             + 毒层数 × 1.0
//             + 破绽 × 0.3
//             + 机制分（冻结 1.5 / 减速加速 1.2 / 充能 0.8 / 上场数 0.8 / 塞牌 0.5 / 疲劳 0.4 每秒）
//
// 系数 rationale 见 Docs/卡牌分值.md。

using System;

public static class CardPowerUtility
{
    public const float ShieldWeight = 0.6f;
    public const float HealWeight = 0.5f;
    public const float PoisonWeight = 1.0f;
    public const float ExposeWeight = 0.3f;

    public const float FreezeBonus = 1.5f;
    public const float SlowBonus = 1.2f;
    public const float HasteBonus = 1.2f;
    public const float ChargeBonus = 0.8f;
    public const float HandBonus = 0.8f;
    public const float SpawnBonus = 0.5f;
    public const float FatigueBonusPerSecond = 0.4f;

    /// <summary>一张牌的分值（每秒等价伤害）。</summary>
    public static float Of(RunCard card)
    {
        if (card == null)
        {
            return 0f;
        }
        CardConfig cfg = card.Config;
        if (cfg == null)
        {
            return 0f;
        }

        float cooldown = card.Countdown;
        if (cooldown <= 0f)
        {
            cooldown = 1f;
        }

        // 数值：RunCard 上的属性已经吃掉了阶位 / 附魔 / 百炼台修正
        int damage = card.Damage;
        if (damage <= 0)
        {
            damage = card.Spell;
        }
        int multi = cfg.MultiCount <= 1 ? 1 : cfg.MultiCount;
        multi += card.BonusMulti;
        if (multi < 1)
        {
            multi = 1;
        }

        float attackPart = damage * multi / cooldown;
        float shieldPart = card.Shield * ShieldWeight / cooldown;
        float healPart = card.Heal * HealWeight / cooldown;
        float poisonPart = card.Poison * PoisonWeight;
        float exposePart = Math.Max(0, cfg.Expose + card.BonusExpose) * ExposeWeight;

        float mech = 0f;
        if (cfg.FreezeTime > 0f)
        {
            mech += FreezeBonus;
        }
        if (cfg.SlowTime > 0f)
        {
            mech += SlowBonus;
        }
        if (cfg.HasteTime > 0f)
        {
            mech += HasteBonus;
        }
        if (cfg.ChargeCount > 0)
        {
            mech += ChargeBonus;
        }
        if (cfg.HandSelf != 0 || cfg.HandFoe != 0)
        {
            mech += HandBonus;
        }
        if (cfg.SpawnCardId > 0)
        {
            mech += SpawnBonus;
        }
        if (cfg.FatigueShift != 0f)
        {
            mech += Math.Abs(cfg.FatigueShift) * FatigueBonusPerSecond;
        }

        return attackPart + shieldPart + healPart + poisonPart + exposePart + mech;
    }

    /// <summary>牌组的单卡均值（**不看总分**：总分会被张数带偏，张数单独看）。</summary>
    public static float Average(System.Collections.Generic.List<RunCard> deck)
    {
        if (deck == null || deck.Count == 0)
        {
            return 0f;
        }
        float sum = 0f;
        for (int i = 0; i < deck.Count; i++)
        {
            sum += Of(deck[i]);
        }
        return sum / deck.Count;
    }

    /// <summary>牌组里最贵 / 最便宜那张的分值，用来看内部是否均匀。</summary>
    public static void Range(System.Collections.Generic.List<RunCard> deck, out float top, out float low)
    {
        top = 0f;
        low = 0f;
        if (deck == null || deck.Count == 0)
        {
            return;
        }
        top = float.MinValue;
        low = float.MaxValue;
        for (int i = 0; i < deck.Count; i++)
        {
            float p = Of(deck[i]);
            if (p > top)
            {
                top = p;
            }
            if (p < low)
            {
                low = p;
            }
        }
    }
}
