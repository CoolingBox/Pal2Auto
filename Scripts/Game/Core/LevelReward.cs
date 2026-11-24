// 文件：LevelReward.cs
// 描述：升级三选一的奖励条目。
//
// 为什么单独一个类型：奖励是「数据 + 一句描述」，效果由 RunState.ApplyReward 统一执行。
// 把效果写进数据类会变成 switch 套 switch，后面加一种奖励要改三处。

public enum LevelRewardKind
{
    Bone,      // 铁骨：气血上限
    Coin,      // 盘缠：银两
    Wage,      // 月俸：日俸
    Scroll,    // 抄本：一张本命卡入组
    Refresh,   // 门路：每日免费刷新
}

public sealed class LevelReward
{
    public LevelRewardKind Kind;
    public string Name = "";
    public string Desc = "";
}
