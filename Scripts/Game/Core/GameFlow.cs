// 文件：GameFlow.cs
// 描述：跨面板的临时状态。目前只存"选中的侠客"，等做摆摊/战斗时再扩成整局状态。

public static class GameFlow
{
    /// <summary>选人界面确认后写入。0 表示还没选。</summary>
    public static int SelectedCharacterId;

    /// <summary>是否在局内。战斗结算时靠它决定「回日程」还是「回主菜单」。</summary>
    public static bool InRun;

    /// <summary>进战斗前由 RunPanel 写入。战斗面板据此取敌人，避免两边各抽一次抽到不同的人。</summary>
    public static int PendingEnemyId = 1;

    /// <summary>
    /// 进战斗前写入的敌方气血。0 表示用 EnemyConfig.Hp。
    /// 局内按天数算血（RunState.EncounterHp / NemesisHp），敌人表里的 Hp 只当图鉴默认值。
    /// </summary>
    public static int PendingEnemyHp;

    /// <summary>局内面板打开后是否直接跳到删牌列表（调试截图用，一次有效）。</summary>
    public static bool PendingOpenRemoval;

    /// <summary>进战斗前调一次：没选过人就退回 1 号，避免空引用。</summary>
    public static void EnsureRunStarted()
    {
        if (SelectedCharacterId <= 0)
        {
            SelectedCharacterId = 1;
        }
    }

    public static void Reset()
    {
        SelectedCharacterId = 0;
        InRun = false;
        PendingEnemyId = 1;
        PendingEnemyHp = 0;
        PendingOpenRemoval = false;
    }
}
