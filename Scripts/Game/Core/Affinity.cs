// 文件：Affinity.cs
// 描述：一张卡对某个主角的「偏好/中立/回避」判定。
//
// 判定只读标签与归属，不读卡名 —— 改文案不该影响商店概率（照《大巴扎》的做法）。
// 权重来自 Docs/装备与技能设计.md 第三节：集市 60/30/10、遭遇 65/25/10、宿敌 60/30/10。

using System.Collections.Generic;

public enum Affinity
{
    Preferred,  // 偏好
    Neutral,    // 中立
    Avoided,    // 非偏好
}

public static class AffinityRules
{
    // 偏好标签：卡类 / 家族 / 属性 / 节奏，四个维度混在一张表里，因为判定只看「命中几个」。
    private static readonly Dictionary<int, string[]> PreferredTags = new Dictionary<int, string[]>();
    private static readonly Dictionary<int, string[]> AvoidedTags = new Dictionary<int, string[]>();

    static AffinityRules()
    {
        // 王小虎：慢速高伤高防。兵器（刀）、护具、器物；慢卡；贯穿与多重。
        PreferredTags[1] = new string[]
        {
            "Palm", "SaberArt", "Tiger", "Blade", "Armor", "Charm",
            "Slow", "Pierce", "Multi",
        };
        // 回避符咒与法术、蛊，以及快卡（他拿快刀等于浪费慢速倍率）。
        AvoidedTags[1] = new string[]
        {
            "Talisman", "Gu", "Fire", "Ice", "Thunder", "Fast",
        };

        // 沈欺霜：中速均衡、剑与阵法、护体反伤。
        PreferredTags[2] = new string[]
        {
            "SwordArt", "Sword", "Formation", "Armor",
            "Mid", "Shield", "Pierce",
        };
        AvoidedTags[2] = new string[]
        {
            "Gu", "Dart", "Fast",
        };

        // 苏媚：快卡上毒、控制。蛊、暗器、变身。
        PreferredTags[3] = new string[]
        {
            "Serpent", "Fox", "Transform", "Gu", "Poison", "Dart",
            "Fast", "Control", "Freeze", "HandMod",
        };
        // 回避护具与慢速大伤（她血少，靠快与控）。
        AvoidedTags[3] = new string[]
        {
            "Armor", "Slow", "Shield",
        };

        // 李忆如：符法、御灵、丹。中慢速、穿透、治疗。
        PreferredTags[4] = new string[]
        {
            "Spirit", "Incantation", "Talisman", "Pill",
            "Heal", "Pierce", "Mid", "Slow",
        };
        // 回避近战兵器（她本身不上去打）。
        AvoidedTags[4] = new string[]
        {
            "Blade", "SaberArt", "Palm", "Whip", "Spear",
        };
    }

    public static Affinity Of(int characterId, CardConfig card)
    {
        if (card == null)
        {
            return Affinity.Neutral;
        }

        // 本命卡永远算偏好；别派本命算回避 —— 否则小虎会抽到一池子沈欺霜的剑法。
        if (card.Master == characterId && characterId != 0)
        {
            return Affinity.Preferred;
        }
        if (card.Master != 0 && card.Master != characterId)
        {
            return Affinity.Avoided;
        }

        int preferred = CountHits(card, PreferredTags, characterId);
        int avoided = CountHits(card, AvoidedTags, characterId);

        if (preferred == 0 && avoided == 0)
        {
            return Affinity.Neutral;
        }
        if (avoided == 0)
        {
            // 只命中一个偏好标签不算「偏好」，否则几乎每张卡都是偏好，权重就失去意义。
            if (preferred >= 2)
            {
                return Affinity.Preferred;
            }
            return Affinity.Neutral;
        }
        if (preferred == 0)
        {
            return Affinity.Avoided;
        }
        if (preferred > avoided)
        {
            return Affinity.Preferred;
        }
        if (preferred < avoided)
        {
            return Affinity.Avoided;
        }
        return Affinity.Neutral;
    }

    private static int CountHits(CardConfig card, Dictionary<int, string[]> table, int characterId)
    {
        if (!table.ContainsKey(characterId))
        {
            return 0;
        }

        string[] tags = table[characterId];
        int hits = 0;
        for (int i = 0; i < tags.Length; i++)
        {
            if (card.HasTag(tags[i]))
            {
                hits++;
            }
        }
        return hits;
    }

    public static string Name(Affinity affinity)
    {
        if (affinity == Affinity.Preferred)
        {
            return "偏好";
        }
        if (affinity == Affinity.Avoided)
        {
            return "非偏好";
        }
        return "中立";
    }
}
