// 文件：CardStatType.cs
// 描述：可以被事件修改的卡牌数值。
//
// 为什么不用字符串：事件配置里写 "Damage" 拼错一个字母是运行时报错，
// 枚举拼错是编译报错。

public enum CardStatType
{
    Damage,
    Shield,
    Heal,
    Poison,
    Expose,
}
