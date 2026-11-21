// 文件：GameFlow.cs
// 描述：跨面板的临时状态。目前只存"选中的侠客"，等做摆摊/战斗时再扩成整局状态。

public static class GameFlow
{
    /// <summary>选人界面确认后写入。0 表示还没选。</summary>
    public static int SelectedCharacterId;

    public static void Reset()
    {
        SelectedCharacterId = 0;
    }
}
