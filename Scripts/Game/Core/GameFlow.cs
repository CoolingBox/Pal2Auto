// 文件：GameFlow.cs
// 描述：跨面板的临时状态。目前只存"选中的侠客"，等做摆摊/战斗时再扩成整局状态。

public static class GameFlow
{
    /// <summary>选人界面确认后写入。0 表示还没选。</summary>
    public static int SelectedCharacterId;

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
    }
}
