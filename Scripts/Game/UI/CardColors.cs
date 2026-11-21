// 文件：CardColors.cs
// 描述：卡面的颜色编码唯一出处 —— 大卡（玩家手牌）、小卡（敌方手牌）、伤害飘字都从这里取色，
//       避免同一个效果在三个地方三种颜色。

using Godot;

public static class CardColors
{
    /// <summary>主色：按卡牌定位（Type）取，火焰牌一律橙黄。
    /// 不按"数值最大"判定——蛇涎蛊挂了 3 点伤害，但它的身份是毒，边框该是绿。</summary>
    public static Color PrimaryColor(CardConfig card)
    {
        if (card.Fire > 0)
        {
            return UiKit.FireOrange;
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

        return UiKit.DamageRed;
    }

    /// <summary>这张牌带的效果色，按"攻 → 火 → 守 → 疗 → 毒"排序。</summary>
    public static Color[] EffectColors(CardConfig card)
    {
        Color[] buffer = new Color[5];
        int count = 0;

        if (card.Damage > 0)
        {
            buffer[count] = UiKit.DamageRed;
            count++;
        }
        if (card.Fire > 0)
        {
            buffer[count] = UiKit.FireOrange;
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

        if (card.Damage > 0)
        {
            text += Tag(UiKit.DamageRed, "攻 " + card.Damage);
        }
        if (card.Fire > 0)
        {
            text += Tag(UiKit.FireOrange, "火 " + card.Fire);
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

        return text.TrimEnd();
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
        if (card.Damage > 0)
        {
            summary += "攻 " + card.Damage + "　";
        }
        if (card.Fire > 0)
        {
            summary += "火 " + card.Fire + "　";
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
        if (card.IgnoreShield)
        {
            summary += "无视护体　";
        }

        return summary.TrimEnd();
    }
}
