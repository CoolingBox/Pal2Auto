// 文件：DaySchedule.cs
// 描述：一天的六个时辰。结构照 Docs/局内流程设计.md 第三节。
//
// 一天 6 个时辰（DayPhase）：
//   第 1、2、4、5 → 自由三选一（集市 / 奇遇 / 淬炉）
//   第 3         → 遭遇（三选一难度）
//   第 6         → 宿敌（当天唯一一战）
//
// 命名照 Docs/代码命名约定.md：时辰叫 DayPhase，不叫 Slot（HandSlot 已占用）。

using System.Collections.Generic;

/// <summary>一个时辰实际发生的事。Free 是「还没选」的初始状态。</summary>
public enum PhaseType
{
    Free,       // 自由时辰，等玩家在集市/奇遇/淬炉里选一个
    Shop,       // 集市
    Event,      // 奇遇
    Forge,      // 淬炉（免费附魔）
    Encounter,  // 遭遇
    Nemesis,    // 宿敌
    Passed,     // 空过：遭遇失败后，当天第 5 时辰作废
}

public static class PhaseTypeExtensions
{
    public static string Name(PhaseType type)
    {
        if (type == PhaseType.Free)
        {
            return "未定";
        }
        if (type == PhaseType.Shop)
        {
            return "集市";
        }
        if (type == PhaseType.Event)
        {
            return "奇遇";
        }
        if (type == PhaseType.Forge)
        {
            return "淬炉";
        }
        if (type == PhaseType.Encounter)
        {
            return "遭遇";
        }
        if (type == PhaseType.Nemesis)
        {
            return "宿敌";
        }
        if (type == PhaseType.Passed)
        {
            return "空过";
        }
        return "—";
    }

    /// <summary>自由时辰的候选项，供 UI 摆三个按钮。</summary>
    public static bool IsFreeChoice(PhaseType type)
    {
        return type == PhaseType.Free;
    }

    /// <summary>要打一场的时辰。</summary>
    public static bool IsCombat(PhaseType type)
    {
        return type == PhaseType.Encounter || type == PhaseType.Nemesis;
    }

    /// <summary>玩家能主动选的三个自由项。</summary>
    public static bool IsSelectable(PhaseType type)
    {
        return type == PhaseType.Shop || type == PhaseType.Event || type == PhaseType.Forge;
    }
}

/// <summary>一个时辰。Index 从 1 起，与玩家看到的「第几个时辰」一致。</summary>
public sealed class DayPhase
{
    public int Index = 1;
    public PhaseType Type = PhaseType.Free;
    public bool Resolved;

    /// <summary>第 3 时辰固定遭遇、第 6 时辰固定宿敌，其余留空等玩家选。</summary>
    public static PhaseType DefaultFor(int index)
    {
        if (index == DaySchedule.EncounterPhase)
        {
            return PhaseType.Encounter;
        }
        if (index == DaySchedule.NemesisPhase)
        {
            return PhaseType.Nemesis;
        }
        return PhaseType.Free;
    }
}

/// <summary>一天的日程：6 个时辰的容器。</summary>
public sealed class DaySchedule
{
    public const int PhaseCount = 6;
    public const int EncounterPhase = 3;
    public const int NemesisPhase = 6;

    public List<DayPhase> Phases = new List<DayPhase>();

    public void Build()
    {
        Phases.Clear();
        for (int i = 1; i <= PhaseCount; i++)
        {
            DayPhase phase = new DayPhase();
            phase.Index = i;
            phase.Type = DayPhase.DefaultFor(i);
            phase.Resolved = false;
            Phases.Add(phase);
        }
    }

    /// <summary>按 1 起的时辰序号取。越界返回 null。</summary>
    public DayPhase Get(int index)
    {
        if (index < 1 || index > Phases.Count)
        {
            return null;
        }
        return Phases[index - 1];
    }

    /// <summary>第一个没结算的时辰。全部结算完返回 null。</summary>
    public DayPhase Current
    {
        get
        {
            for (int i = 0; i < Phases.Count; i++)
            {
                if (!Phases[i].Resolved)
                {
                    return Phases[i];
                }
            }
            return null;
        }
    }

    public bool IsComplete
    {
        get
        {
            return Current == null;
        }
    }

    public int ResolvedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Phases.Count; i++)
            {
                if (Phases[i].Resolved)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
