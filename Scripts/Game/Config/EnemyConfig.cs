// 文件：EnemyConfig.cs
// 描述：敌方配置。敌方也是一个牌组（同样 5 张手牌、独立倒计时、打出补抽）。

using TinyFramework;

public class EnemyConfig : BaseConfig
{
    public string Name = "";
    public string Title = "";
    public string Portrait = "";
    public int Hp = 150;
    public int[] Deck = new int[0];
}
