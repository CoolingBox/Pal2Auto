// 文件：RunSave.cs
// 描述：一局的存档快照。继承 SaveData，由 SaveManager 落盘到 user://RunSave.json。
//
// 只放一个 Run 字段：整局的数据形状已经在 RunState 里定好了，这里再拆一遍只会让两处不同步。
// 版本号留着给 SaveMigrationRegistry 用。

using TinyFramework;

public sealed class RunSave : SaveData
{
    /// <summary>结构版本。改 RunState 的字段含义时加 1，并在 SaveMigration 里补迁移。</summary>
    public int Version = 1;

    public RunState Run;
}
