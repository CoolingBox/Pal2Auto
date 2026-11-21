// 文件：CardConfig.cs
// 描述：卡牌配置。字段与 Resources/Config/CardConfig.json 对应。
//
// 这一版没有费用/能量：唯一的节奏资源是 Countdown（蓄势秒数）。
// 快牌铺场、慢牌吃重击，构筑的差异先由倒计时的方差撑起来。

using TinyFramework;

public enum CardType
{
    Attack,   // 打伤害
    Guard,    // 加护体
    Heal,     // 回气血
    Poison,   // 叠中毒
}

public class CardConfig : BaseConfig
{
    public string Name = "";
    public CardType Type = CardType.Attack;

    /// <summary>蓄势时间（秒）。归零即自动打出。</summary>
    public float Countdown = 2f;

    public int Damage;
    public int Shield;
    public int Heal;
    public int Poison;
    public bool IgnoreShield;

    public string Icon = "";   // Resources/Image/Items/{Icon}.png
    public string Desc = "";
}
