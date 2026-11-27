// 文件：RunEvent.cs
// 描述：「百炼台」—— 每个自由时辰的奇遇里，最稀有的那一个。
//
// 结构：上方从牌组抽 5 张摆出来（都是 Deck 里的实例，点选作用对象），
// 下方给 3 个随机选项，每个选项要求选 1 张或 2 张。
//
// 为什么候选要「引用 Deck 里的实例」而不是复制：所有选项都是「改这一张牌」，
// 复制一份改完还得写回去，反而多一次出错的机会。

using System.Collections.Generic;

/// <summary>百炼台的一个选项。</summary>
public enum EventOptionType
{
    Merge,       // 合成：选 2 张，数值全部相加（含蓄势）
    Priority,    // 先发：战斗开始时优先上手
    Bottom,      // 后发：洗牌后置于抽牌堆底
    Sharp,       // 锋利：伤害 +2
    QuickDraw,   // 快枪：蓄势 -1 秒，数值减半
    Greed,       // 贪婪：塞一张基础攻/防卡进牌组 + 拿银两
    Multi,       // 多动：多重 +1，蓄势 +1 秒
    Exile,       // 放逐：移出牌组，但气血上限下降（删牌本身是奖励，必须带代价）
    Transmute,   // 变换：随机换成另一张同阶卡（可以抽到别派本命）
    Quench,      // 淬甲：护体 / 治疗 +2
    Whet,        // 磨砺：蓄势 -0.5 秒（不牺牲数值）
    Vitality,    // 换命：移除该卡，气血上限 +30
    Transcribe,  // 抄录：复制一张同阶同名卡进牌组
}

public sealed class EventOption
{
    public EventOptionType Kind;
    public string Name = "";
    public string Desc = "";

    /// <summary>要选几张（合并要 2，其余 1，贪婪 0）。</summary>
    public int TargetCount = 1;

    /// <summary>顺带给的银两。</summary>
    public int Gold;

    /// <summary>
    /// 出现权重。抽 3 个选项时按权重加权 —— 「移除 / 无代价提速 / 合成」这类
    /// 本身净赚的选项权重低，别每次都摆到脸上。
    /// </summary>
    public int Weight = 10;
}

/// <summary>一次「百炼台」的现场：5 张候选 + 3 个选项。</summary>
public sealed class RunEvent
{
    public List<RunCard> Candidates = new List<RunCard>();
    public List<EventOption> Options = new List<EventOption>();

    /// <summary>已经结算过了（每个时辰只能拿一次）。</summary>
    public bool Resolved;
}
