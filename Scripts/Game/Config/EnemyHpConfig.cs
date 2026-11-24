// 文件：EnemyHpConfig.cs
// 描述：每天的敌方气血，写死在 Resources/Config/EnemyHpConfig.json 里，代码不参与计算。
//
// 为什么不用公式：曲线是策划要反复手调的东西（某一天太难就单独改那一条），
// 公式一改就是全表位移，而调一天只影响一天。表缺某天时退回「表里最大天数」那一行，让 15 天之后也不会崩。
//
// 三档 Encounter 对应遭遇三选一（低 / 中 / 高），Nemesis 是当天宿敌。

using TinyFramework;

public class EnemyHpConfig : BaseConfig
{
    /// <summary>三选一的三档气血，顺序就是界面上的上/中/下。</summary>
    public int[] Encounter = new int[0];

    /// <summary>当天宿敌的气血。</summary>
    public int Nemesis;
}
