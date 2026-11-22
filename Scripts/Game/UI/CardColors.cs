// 文件：CardColors.cs
// 描述：卡面的颜色编码唯一出处 —— 大卡（玩家手牌）、小卡（敌方手牌）、伤害飘字都从这里取色，
//       避免同一个效果在三个地方三种颜色。

using Godot;

public static class CardColors
{
    /// <summary>主色：按卡牌定位（Type）取，法术按元素色。
    /// 不按"数值最大"判定——蛇涎蛊挂了 3 点伤害，但它的身份是毒，边框该是绿。</summary>
    public static Color PrimaryColor(CardConfig card)
    {
        if (card.MultiCount > 1)
        {
            return UiKit.MultiMagenta;
        }
        if (card.HasteCount > 0)
        {
            return UiKit.HasteGreen;
        }
        if (card.FreezeCount > 0)
        {
            return UiKit.FreezeIce;
        }
        if (card.SlowCount > 0)
        {
            return UiKit.SlowBlue;
        }
        if (card.ChargeCount > 0)
        {
            return UiKit.ChargeGold;
        }

        if (card.Type == CardType.Spell)
        {
            return ElementColor(card.Element);
        }
        if (card.Type == CardType.Guard)
        {
            return UiKit.ShieldGold;
        }
        if (card.Type == CardType.Heal)
        {
            return UiKit.HealGreen;
        }
        if (card.Type == CardType.Poison)
        {
            return UiKit.PoisonGreen;
        }
        if (card.Type == CardType.Support)
        {
            return UiKit.HandTeal;
        }

        return UiKit.DamageRed;
    }

    public static Color ElementColor(SpellElement element)
    {
        if (element == SpellElement.Fire)
        {
            return UiKit.FireOrange;
        }
        if (element == SpellElement.Ice)
        {
            return UiKit.IceCyan;
        }
        if (element == SpellElement.Thunder)
        {
            return UiKit.ThunderLilac;
        }
        return UiKit.FireOrange;
    }

    public static string ElementName(SpellElement element)
    {
        if (element == SpellElement.Fire)
        {
            return "火";
        }
        if (element == SpellElement.Ice)
        {
            return "冰";
        }
        if (element == SpellElement.Thunder)
        {
            return "雷";
        }
        return "术";
    }

    /// <summary>这张牌带的效果色，按"攻 → 术 → 守 → 疗 → 毒 → 破"排序。</summary>
    public static Color[] EffectColors(CardConfig card)
    {
        Color[] buffer = new Color[6];
        int count = 0;

        if (card.HandSelf != 0 || card.HandFoe != 0)
        {
            buffer[count] = UiKit.HandTeal;
            count++;
        }
        if (card.Damage > 0)
        {
            buffer[count] = UiKit.DamageRed;
            count++;
        }
        if (card.Spell > 0)
        {
            buffer[count] = ElementColor(card.Element);
            count++;
        }
        if (card.Shield > 0)
        {
            buffer[count] = UiKit.ShieldGold;
            count++;
        }
        if (card.Heal > 0)
        {
            buffer[count] = UiKit.HealGreen;
            count++;
        }
        if (card.Poison > 0)
        {
            buffer[count] = UiKit.PoisonGreen;
            count++;
        }
        if (card.Expose > 0)
        {
            buffer[count] = UiKit.ExposeViolet;
            count++;
        }

        Color[] result = new Color[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = buffer[i];
        }
        return result;
    }

    /// <summary>大卡上的彩色标签，复合牌会并排显示多个。</summary>
    public static string BuildEffectText(CardConfig card)
    {
        string text = "";

        if (card.MultiCount > 1)
        {
            text += Tag(UiKit.MultiMagenta, "重" + card.MultiCount);
        }
        if (card.HasteCount > 0)
        {
            text += Tag(UiKit.HasteGreen, "速" + card.HasteCount + "·" + Short(card.HasteTime) + "s");
        }
        if (card.SlowCount > 0)
        {
            text += Tag(UiKit.SlowBlue, "缓" + card.SlowCount + "·" + Short(card.SlowTime) + "s");
        }
        if (card.FreezeCount > 0)
        {
            text += Tag(UiKit.FreezeIce, "冻" + card.FreezeCount + "·" + Short(card.FreezeTime) + "s");
        }
        if (card.ChargeCount > 0)
        {
            text += Tag(UiKit.ChargeGold, "充" + card.ChargeCount);
        }

        if (card.HandSelf != 0 || card.HandFoe != 0)
        {
            text += Tag(UiKit.HandTeal, HandBody(card));
        }
        if (card.Damage > 0)
        {
            text += Tag(UiKit.DamageRed, "攻 " + card.Damage);
        }
        if (card.Spell > 0)
        {
            text += Tag(ElementColor(card.Element), ElementName(card.Element) + " " + card.Spell);
        }
        if (card.Shield > 0)
        {
            text += Tag(UiKit.ShieldGold, "守 " + card.Shield);
        }
        if (card.Heal > 0)
        {
            text += Tag(UiKit.HealGreen, "疗 " + card.Heal);
        }
        if (card.Poison > 0)
        {
            text += Tag(UiKit.PoisonGreen, "毒 " + card.Poison);
        }
        if (card.Expose > 0)
        {
            text += Tag(UiKit.ExposeViolet, "破 " + card.Expose);
        }

        return text.TrimEnd();
    }

    /// <summary>上场数标签的正文：我+1 / 敌-1 / 双+1，限时的再加 ·6s。</summary>
    public static string HandBody(CardConfig card)
    {
        string body;
        if (card.HandSelf != 0 && card.HandFoe != 0 && card.HandSelf == card.HandFoe)
        {
            body = "双" + Sign(card.HandSelf);
        }
        else if (card.HandSelf != 0 && card.HandFoe == 0)
        {
            body = "我" + Sign(card.HandSelf);
        }
        else if (card.HandFoe != 0 && card.HandSelf == 0)
        {
            body = "敌" + Sign(card.HandFoe);
        }
        else
        {
            body = "我" + Sign(card.HandSelf) + " 敌" + Sign(card.HandFoe);
        }

        if (card.HandTime > 0f)
        {
            body += "·" + card.HandTime.ToString("0") + "s";
        }

        return body;
    }

    /// <summary>秒数去掉多余的 .0：6 秒写 "6"，2.5 秒写 "2.5"。</summary>
    private static string Short(float seconds)
    {
        return seconds.ToString("0.#");
    }

    private static string Sign(int value)
    {
        if (value > 0)
        {
            return "+" + value;
        }
        return value.ToString();
    }

    private static string Tag(Color color, string content)
    {
        return "[color=#" + color.ToHtml(false) + "]" + content + "[/color]  ";
    }

    /// <summary>把底色朝主色偏一点：卡面能一眼看出是攻牌还是守牌，又不至于像色卡。</summary>
    public static Color Tint(Color baseColor, Color accent, float amount)
    {
        return new Color(
            baseColor.R + (accent.R - baseColor.R) * amount,
            baseColor.G + (accent.G - baseColor.G) * amount,
            baseColor.B + (accent.B - baseColor.B) * amount,
            baseColor.A);
    }

    /// <summary>纯文本摘要，给悬浮提示用。</summary>
    public static string EffectSummary(CardConfig card)
    {
        string summary = "";
        if (card.MultiCount > 1)
        {
            summary += "多重 " + card.MultiCount + "　";
        }
        if (card.HasteCount > 0)
        {
            summary += "加速 " + card.HasteCount + " 张 " + Short(card.HasteTime) + " 秒　";
        }
        if (card.SlowCount > 0)
        {
            summary += "减速 " + card.SlowCount + " 张 " + Short(card.SlowTime) + " 秒　";
        }
        if (card.FreezeCount > 0)
        {
            summary += "冰冻 " + card.FreezeCount + " 张 " + Short(card.FreezeTime) + " 秒　";
        }
        if (card.ChargeCount > 0)
        {
            summary += "充能 " + card.ChargeCount + " 张　";
        }
        if (card.HandSelf != 0 || card.HandFoe != 0)
        {
            summary += "上场 " + HandBody(card) + "　";
        }
        if (card.Damage > 0)
        {
            summary += "攻 " + card.Damage + "　";
        }
        if (card.Spell > 0)
        {
            summary += ElementName(card.Element) + " " + card.Spell + "　";
        }
        if (card.Shield > 0)
        {
            summary += "守 " + card.Shield + "　";
        }
        if (card.Heal > 0)
        {
            summary += "疗 " + card.Heal + "　";
        }
        if (card.Poison > 0)
        {
            summary += "毒 " + card.Poison + "　";
        }
        if (card.Expose > 0)
        {
            summary += "破绽 " + card.Expose + "　";
        }
        if (card.IgnoreShield)
        {
            summary += "无视护体　";
        }

        return summary.TrimEnd();
    }
}
