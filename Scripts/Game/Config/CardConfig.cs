// 文件：CardConfig.cs
// 描述：卡牌配置。字段与 Resources/Config/CardConfig.json 对应。
//
// 这一版没有费用/能量：唯一的节奏资源是 Countdown（蓄势秒数）。
// 快牌铺场、慢牌吃重击，构筑的差异先由倒计时的方差撑起来。

using TinyFramework;

public enum CardType
{
    Attack,   // 兵器：物理伤害
    Spell,    // 法术：五灵伤害（结算同伤害，颜色按元素）
    Guard,    // 护具：护体
    Heal,     // 丹药：回复
    Poison,   // 蛊毒：中毒
    Support,  // 器物：改上场数（布袋、符咒、金石之类）
}

/// <summary>卡牌关键字。一张牌可以同时带多个（位标记）。</summary>
[System.Flags]
public enum CardKeyword
{
    None = 0,

    /// <summary>消耗：打完进「消耗堆」，本场不再回到牌库。</summary>
    Exhaust = 1,

    /// <summary>移除：打完从牌库永久去掉（存进 DeckProgress，下一场也不会出现）。</summary>
    Remove = 2,

    /// <summary>贯穿：无视护体，直接扣气血。</summary>
    Pierce = 4,

    /// <summary>加速：我方随机 X 张牌，蓄势速度翻倍，持续 Y 秒。</summary>
    Haste = 8,

    /// <summary>减速：我方随机 X 张牌，蓄势速度减半，持续 Y 秒。与加速可抵消。</summary>
    Slow = 16,

    /// <summary>冰冻：我方随机 X 张牌，Y 秒内不能自然蓄势（可被充能），化冻后才能打出。</summary>
    Freeze = 32,

    /// <summary>充能：我方随机 X 张牌，蓄势立刻前进 1 秒。</summary>
    Charge = 64,

    /// <summary>多重：这张牌每次使用结算 X 次（补抽冷却也按次数拉长）。</summary>
    Multi = 128,
}

public enum SpellElement
{
    None,
    Fire,     // 火
    Ice,      // 冰
    Thunder,  // 雷
}

public class CardConfig : BaseConfig
{
    public string Name = "";
    public CardType Type = CardType.Attack;

    /// <summary>蓄势时间（秒）。归零即自动打出。</summary>
    public float Countdown = 2f;

    public int Damage;
    /// <summary>火焰伤害：结算方式和普通伤害一致（吃护体），只是卡面/飘字用橙黄区分。</summary>
    public int Spell;
    public SpellElement Element = SpellElement.None;

    /// <summary>破绽：给目标叠层，每层使其受到的所有伤害 +15%（最多 3 层，4 秒）。</summary>
    public int Expose;
    public int Shield;
    public int Heal;
    public int Poison;
    public bool IgnoreShield;

    /// <summary>本方上场数 +N（可为负）。</summary>
    public int HandSelf;

    /// <summary>对方上场数 +N（可为负）。</summary>
    public int HandFoe;

    /// <summary>上场数改动的持续时间（秒）。0 = 本场一直生效；>0 = 到点自动收回。</summary>
    public float HandTime;

    // —— 时机类（影响蓄势速度）——
    /// <summary>加速：影响几张牌。</summary>
    public int HasteCount;
    /// <summary>加速：双倍蓄势持续几秒。</summary>
    public float HasteTime;

    /// <summary>减速：影响几张牌。</summary>
    public int SlowCount;
    /// <summary>减速：半速蓄势持续几秒。</summary>
    public float SlowTime;

    /// <summary>冰冻：影响几张牌。</summary>
    public int FreezeCount;
    /// <summary>冰冻：几秒内不能自然蓄势。</summary>
    public float FreezeTime;

    /// <summary>充能：影响几张牌（每张蓄势前进 1 秒）。</summary>
    public int ChargeCount;

    /// <summary>多重：这张牌每次使用结算几次。</summary>
    public int MultiCount;

    /// <summary>关键字（可多个）。JSON 里写 "Exhaust" 或 "Exhaust, Remove"。</summary>
    public CardKeyword Keywords = CardKeyword.None;

    public string Icon = "";   // Resources/Image/Items/{Icon}.png
    public string Desc = "";

    public bool HasKeyword(CardKeyword keyword)
    {
        return (Keywords & keyword) != 0;
    }
}
