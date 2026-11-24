// 文件：CharacterConfig.cs
// 描述：可选侠客配置。字段与 Resources/Config/CharacterConfig.json 一一对应。
//
// 说明：本版只做界面，侠客决定"开局携带哪件东西"；特长（Specialty）是规则预告，
// 战斗侧还没接，界面上必须标出来，不能让人以为已经生效。

using TinyFramework;

public class CharacterConfig : BaseConfig
{
    public string Name = "";
    public string Title = "";        // 名头：江湖游侠 / 冰心玉骨 ...
    public string Portrait = "";     // Resources/Image/Portrait/{Portrait}.png
    public string Style = "";        // 定位：兵器连击 / 蛊毒消耗 ...
    public string Specialty = "";    // 特长规则（预告）

    public string StarterName = "";  // 开局携带的物件
    public string StarterIcon = "";  // Resources/Image/Items/{StarterIcon}.png
    public string StarterDesc = "";

    public string Desc = "";

    /// <summary>
    /// 气血上限。写进配置而不是写死常量：小虎可以厚、忆如可以薄，
    /// 而且调平衡时不改代码（以前是 BattlePanel 里硬编码 120）。
    /// </summary>
    public int Hp = 200;

    /// <summary>开局牌组（CardConfig.Id），允许同一个 Id 出现多次。</summary>
    public int[] Deck = new int[0];
}
