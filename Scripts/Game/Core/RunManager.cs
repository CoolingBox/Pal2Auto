// 文件：RunManager.cs
// 描述：一局的推进器 —— 开局、选时辰、结算胜负、换天、落盘。
//
// 分工：RunState 只管数据与局部操作，这里管「流程位置」（现在第几天第几个时辰、这一步之后去哪）。
// 这样写的好处是 RunState 可以整份存/整份读，不依赖任何流程状态。
//
// 流程照 Docs/局内流程设计.md 第三节：
//   6 个时辰 → 第 3 遭遇、第 6 宿敌、其余自由三选一；
//   一天全结算完 → 换天（发日俸、重抽货架）；累计 10 场宿敌胜利即胜，第 15 天打完仍未 10 胜即败。

using System.Collections.Generic;
using TinyFramework;

public sealed class RunManager : Singleton<RunManager>
{
    /// <summary>当前这一局。没开局时为 null。</summary>
    public RunState Current { get; private set; }

    public bool HasActiveRun
    {
        get
        {
            return Current != null && !Current.IsOver;
        }
    }

    public DayPhase CurrentPhase
    {
        get
        {
            if (Current == null)
            {
                return null;
            }
            return Current.Today.Current;
        }
    }

    // —————————————————————— 开局 / 存档 ——————————————————————

    public RunState StartRun(int characterId, int seed)
    {
        Current = new RunState();
        Current.Begin(characterId, seed);
        TinyLog.Info("Run", "开局：" + Current.CharacterName + "（seed " + Current.Seed + "）");
        return Current;
    }

    public void AbandonRun()
    {
        Current = null;
    }

    public void SaveRun()
    {
        if (Current == null)
        {
            return;
        }

        // 百炼台的候选是「Deck 里的引用」，序列化会变成副本、读档后与牌组脱钩。
        // 所以存档时把它摘掉 —— 读档后重回奇遇时辰会重新 roll，玩家不会丢东西。
        RunEvent pending = Current.PendingEvent;
        Current.PendingEvent = null;

        RunSave save = new RunSave();
        save.Run = Current;
        SaveManager.Save(save);

        Current.PendingEvent = pending;
    }

    public bool LoadRun()
    {
        RunSave save = SaveManager.Load<RunSave>();
        if (save == null || save.Run == null)
        {
            return false;
        }

        Current = save.Run;
        if (Current.Today == null)
        {
            Current.Today = new DaySchedule();
        }
        if (Current.Shop == null)
        {
            Current.Shop = new ShopStock();
        }
        return true;
    }

    public bool HasSaveOnDisk()
    {
        RunSave save = SaveManager.Load<RunSave>();
        return save != null && save.Run != null && !save.Run.IsOver;
    }

    // —————————————————————— 时辰 ——————————————————————

    /// <summary>
    /// 自由时辰选一项。选了集市就当场抽货架。
    /// 遭遇/宿敌时辰不能调这个方法（要打，不是选）。
    /// </summary>
    public bool SelectFreePhase(PhaseType type)
    {
        if (Current == null || Current.IsOver)
        {
            return false;
        }
        if (!PhaseTypeExtensions.IsSelectable(type))
        {
            return false;
        }

        DayPhase phase = Current.Today.Current;
        if (phase == null)
        {
            return false;
        }
        if (phase.Type != PhaseType.Free)
        {
            return false;
        }

        phase.Type = type;
        Current.Notes.Add("第 " + phase.Index + " 时辰：" + PhaseTypeExtensions.Name(type));

        if (type == PhaseType.Shop)
        {
            // 每次进集市都开一家新店：同一天进两次，就是两家不同的铺子
            Current.Shop.Open(Current.Day, Current.CharacterId, Current.Rng, Current.Deck);
            Current.Notes.Add("进了" + Current.Shop.ShopName);
        }
        SaveRun();
        return true;
    }

    /// <summary>
    /// 结算当前时辰并推进。success 只对遭遇/宿敌有意义（自由时辰传 true）。
    /// 返回「这一局是否还活着」。
    /// </summary>
    public bool ResolveCurrentPhase(bool success)
    {
        if (Current == null || Current.IsOver)
        {
            return false;
        }

        DayPhase phase = Current.Today.Current;
        if (phase == null)
        {
            return false;
        }
        if (phase.Type == PhaseType.Free)
        {
            // 还没选就不能结算，避免 UI 漏一步把时辰空推过去
            return false;
        }

        switch (phase.Type)
        {
            case PhaseType.Encounter:
                // 每个时辰都有一点基础阅历（无论输赢），战斗胜利再加额外那份
                Current.GainExperience(1);
                ResolveEncounter(success);
                break;
            case PhaseType.Nemesis:
                Current.GainExperience(1);
                ResolveNemesis(success);
                break;
            default:
                // 集市/奇遇/淬炉/空过：只有基础阅历
                Current.GainExperience(1);
                break;
        }

        phase.Resolved = true;

        if (Current.IsOver)
        {
            SaveRun();
            return false;
        }

        if (Current.Today.IsComplete)
        {
            AdvanceDay();
        }

        // 每个时辰结算完落一次盘：退出游戏不丢进度
        SaveRun();
        return !Current.IsOver;
    }

    private void ResolveEncounter(bool success)
    {
        if (success)
        {
            int reward = Current.EncounterReward;
            // 额外阅历（基础那 1 点已经在 ResolveCurrentPhase 里给过了）
            int exp = RunState.RewardExpOf(Current.EncounterTier);
            Current.Earn(reward);
            Current.GainExperience(exp);
            Current.Notes.Add("遭遇胜利（" + RunState.TierName(Current.EncounterTier) + "）：+" +
                reward + " 银两，阅历 +" + exp);
            return;
        }

        // 失败代价：当天第 5 时辰作废，只给 10 银两保底。
        // 每战满血（B2）之后失败没有血量代价，必须补一个成本，否则三选一永远该选最难。
        // 2026-11-25：阅历不再给 —— 打输就是「这一天少长了经验」，等级会落后半级多。
        // 一天全胜 4+2+2 = 8 点刚好一级；输一场只剩 6 点。
        DayPhase fifth = Current.Today.Get(5);
        if (fifth != null && !fifth.Resolved)
        {
            fifth.Type = PhaseType.Passed;
            fifth.Resolved = true;
            Current.Earn(10);
            Current.Notes.Add("遭遇失败：第 5 时辰空过，保底 10 银两");
        }
        else
        {
            Current.Notes.Add("遭遇失败");
        }
    }

    private void ResolveNemesis(bool success)
    {
        if (success)
        {
            Current.Victories++;
            int reward = Current.NemesisReward;
            Current.Earn(reward);
            Current.GainExperience(1);
            Current.Notes.Add("宿敌胜利：第 " + Current.Victories + " 胜，+" + reward + " 银两");
        }
        else
        {
            // 输第 N 天扣 N 点声望；扣到 0 先背水，再用就终局
            int loss = Current.Day;
            RenownOutcome outcome = Current.TakeRenownLoss(loss);
            if (outcome == RenownOutcome.None)
            {
                Current.Notes.Add("宿敌失败：声望 -" + loss + " → " + Current.Renown);
            }
        }

        CheckOutcome();
    }

    /// <summary>换天：第 +1、发日俸、重抽货架；超过 15 天还没 10 胜就终局。</summary>
    private void AdvanceDay()
    {
        if (Current.IsOver)
        {
            return;
        }

        Current.Day++;

        if (Current.Day > RunState.MaxDay)
        {
            Current.IsOver = true;
            Current.IsWon = false;
            Current.Notes.Add("第 " + RunState.MaxDay + " 天已过，未达 10 胜，一局结束");
            return;
        }

        Current.Today.Build();

        int salary = Current.DaySalary;
        Current.Earn(salary);

        Current.FreeEnchantUsed = false;
        Current.Shop.ResetForNewDay();
        Current.Shop.FreeRefreshCount = Current.FreeRefreshPerDay;
        Current.Shop.Open(Current.Day, Current.CharacterId, Current.Rng, Current.Deck);

        Current.Notes.Add("第 " + Current.Day + " 天开始：日俸 +" + salary + " 银两，银两 " + Current.Gold);
    }

    /// <summary>胜负判定：10 胜即赢；第 15 天全结算完仍未达 10 胜即输。</summary>
    private void CheckOutcome()
    {
        if (Current.Victories >= RunState.TargetVictories)
        {
            Current.IsOver = true;
            Current.IsWon = true;
            Current.Notes.Add("累计 " + Current.Victories + " 场宿敌胜利，一局完成");
            return;
        }

        if (Current.Day >= RunState.MaxDay && Current.Today.IsComplete)
        {
            Current.IsOver = true;
            Current.IsWon = false;
            Current.Notes.Add("第 " + RunState.MaxDay + " 天打完，只有 " + Current.Victories + " 胜，一局结束");
        }
    }

    // —————————————————————— 查询 ——————————————————————

    /// <summary>当前时辰的候选（自由时辰才有三项）。</summary>
    public List<PhaseType> FreePhaseOptions()
    {
        List<PhaseType> options = new List<PhaseType>();
        options.Add(PhaseType.Shop);
        options.Add(PhaseType.Event);
        options.Add(PhaseType.Forge);
        return options;
    }

    /// <summary>一句话战报，给 DevShot / 结算面板用。</summary>
    public string Briefing
    {
        get
        {
            if (Current == null)
            {
                return "未开局";
            }

            string text = "第 " + Current.Day + " 天";
            DayPhase phase = Current.Today.Current;
            if (phase != null)
            {
                text += " 第 " + phase.Index + " 时辰（" + PhaseTypeExtensions.Name(phase.Type) + "）";
            }
            else
            {
                text += " 已结算";
            }
            text += " · 银两 " + Current.Gold;
            text += " · 声望 " + Current.Renown;
            text += " · 胜 " + Current.Victories + "/" + RunState.TargetVictories;
            return text;
        }
    }
}
