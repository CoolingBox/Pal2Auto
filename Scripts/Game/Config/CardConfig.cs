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

/// <summary>时机效果的作用范围。张数为 0 时表示"范围内全体"。</summary>
public enum TimingScope
{
    Self,   // 自己
    Foe,    // 敌方
    Both,   // 双方各取
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

    /// <summary>时机效果的作用范围：自己 / 敌方 / 双方。卡面会把范围写在标签最前面。</summary>
    public TimingScope Scope = TimingScope.Self;

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

    /// <summary>
    /// 打出后往**双方抽牌堆**各塞一张这张卡的 Id（0 = 不塞）。
    /// 只在本场战斗内生效，不改玩家牌组 —— 野球拳用的就是这个：
    /// 一人一拳，两边牌组都变厚，谁抽到谁再打一拳。
    /// </summary>
    public int SpawnCardId;

    /// <summary>先发：战斗开始时优先进入起手手牌（百炼台给的）。</summary>
    public bool PriorityDraw;

    /// <summary>后发：洗牌后沉到牌库底（百炼台给的）。</summary>
    public bool BottomOfDeck;

    /// <summary>
    /// 疲劳位移（秒）。正数 = 让目标**提前**进入力歇，负数 = 延后。
    /// 作用于谁看 `Scope`：Self 自己、Foe 敌方、Both 双方。
    /// 「催命」类卡把它打在敌人身上（对方先挨风沙），「续命」类卡打在自己身上（拖到后期）。
    /// </summary>
    public float FatigueShift;

    /// <summary>关键字（可多个）。JSON 里写 "Exhaust" 或 "Exhaust, Remove"。</summary>
    public CardKeyword Keywords = CardKeyword.None;

    public string Icon = "";   // Resources/Image/Items/{Icon}.png
    public string Desc = "";

    /// <summary>
    /// 标签（可多个，逗号分隔）。照《大巴扎》的做法：一张卡同时属于若干类别，
    /// 商店、奇遇、偏好筛选都靠它，不靠卡名 —— 改文案不该影响概率。
    /// 词表见 Docs/装备与技能设计.md ：家族(Palm/SaberArt/SwordArt/Formation/Spirit/Serpent/Fox/Tiger/Incantation/Ultimate)、
    /// 类型(Blade/Armor/Charm/Pill/Talisman/Dart/Gu)、属性(Physical/Fire/Ice/Thunder/Poison/Heal/Shield/Pierce/Multi/Control/Expose)、
    /// 节奏(Fast/Mid/Slow，由 Countdown 自动补)。
    /// </summary>
    public string Tags = "";

    /// <summary>
    /// 武学归属：0 通用 / 1 王小虎 / 2 沈欺霜 / 3 苏媚 / 4 李忆如。非 0 的本命卡只在该主角池里高概率出现。
    ///
    /// 这个字段叫 Master 而不是 Owner，是为了跟战斗侧分开：`DeckBattle` 里的 `owner` 参数
    /// 指的是「此刻由哪一方出这张牌」（运行时持有方，另有 `BattleEvent.Side`），
    /// 而这里是「这门武学是谁的」（静态设计归属）。两者没有关系，同一张卡可以
    /// Master = 1（小虎的本命）却由敌人打出。
    /// </summary>
    public int Master;

    /// <summary>
    /// 同族卡的根源 id。展开后每一阶都是独立条目（大刀·铜 = 1、·银 = 1001、·金 = 2001…），
    /// 合成与卡表分组靠这个字段认「这是同一张卡的哪一阶」。
    /// </summary>
    public int BaseId;

    /// <summary>
    /// 起步阶（最低能出到几阶）。铜 1 / 银 2 / 金 3 / 钻 4。
    /// 铜阶卡可以一路合成到钻；起步金/钻的卡（绝学、顶级法宝）只能靠掉落或购买，合不出来。
    /// 数值缩放与蓄势缩短都以它为基准，见 CardTierRules。
    /// </summary>
    public CardTier MinTier = CardTier.Copper;

    /// <summary>
    /// 只在开局牌组里出现，不进商店、不进遭遇奖励。
    /// 用于「劈砍 / 招架」这类起点卡：它们的唯一作用是给出「该删什么」的动机。
    /// </summary>
    public bool StarterOnly;

    /// <summary>能不能从铜阶一路合上来。</summary>
    public bool CanGrowFromCopper()
    {
        return MinTier == CardTier.Copper;
    }

    /// <summary>
    /// 按标签判定（不改卡名）。
    /// 必须按逗号精确分词 —— 早期版本用 Tags.Contains(tag)，结果 "Sword" 会命中 "SwordArt"，
    /// 沈欺霜的整套仙霞剑法因此被当成兵器混进兵器铺。
    /// </summary>
    public bool HasTag(string tag)
    {
        if (string.IsNullOrEmpty(Tags))
        {
            return false;
        }

        string[] parts = Tags.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Trim() == tag)
            {
                return true;
            }
        }
        return false;
    }

    public bool HasKeyword(CardKeyword keyword)
    {
        return (Keywords & keyword) != 0;
    }
}
