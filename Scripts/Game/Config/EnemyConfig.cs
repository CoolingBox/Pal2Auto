// 文件：EnemyConfig.cs
// 描述：敌方配置。敌方也是一个牌组（同样 5 张手牌、独立倒计时、打出补抽）。

using TinyFramework;

/// <summary>
/// 敌人品阶。只决定「什么时候能出现」「牌组多厚」「打赢给多少银两与阅历」，
/// 不乘气血系数 —— 气血全部写在 Resources/Config/EnemyHpConfig.json。
/// </summary>
public enum EnemyTier
{
    Bronze = 0,    // 早期杂兵
    Silver = 1,    // 主力
    Gold = 2,      // 带机制
    Diamond = 3,   // 带绝学
    Legend = 4,    // 宿敌级
}

public class EnemyConfig : BaseConfig
{
    public string Name = "";
    public string Title = "";
    public string Portrait = "";
    public int Hp = 150;
    public int[] Deck = new int[0];

    /// <summary>品阶：决定奖励与出现天数，不参与气血计算。</summary>
    public EnemyTier Tier = EnemyTier.Bronze;
}
