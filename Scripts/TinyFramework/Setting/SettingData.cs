// 文件：SettingData.cs
// 描述：玩家偏好（音量、倍速、画质）。与局内存档分开：这些是"怎么看"的设置，
//       换一局、重开都不该被重置。

using System.Text.Json.Serialization;

namespace TinyFramework;

public class SettingData : SaveData
{
    public float Volume = 100;
    public float BGMVolume = 100;
    public float SFXVolume = 100;

    /// <summary>数字显示模式：科学表示法（K/M/B）或中文计数法（万/亿/兆）。
    /// 默认中文——《仙剑2自走棋》是武侠语境，UI 上蹦出 "15.0K" 会出戏。</summary>
    public NumberFormat NumberFormat = NumberFormat.Chinese;

    /// <summary>自动保存。默认开：玩家不会记得手动存档。</summary>
    public bool AutoSave = true;

    /// <summary>战斗倍速（1 = 正常，2 = 快进）。只影响战斗推进，不影响商店与结算。</summary>
    public float BattleSpeed = 1f;

    /// <summary>可选倍速档位。既是设置面板的选项，也是白名单：
    /// 存档里出现表外的值一律退回第一档。</summary>
    public static readonly float[] SpeedSteps = { 1f, 2f };

    /// <summary>
    /// 内部渲染倍率：1 = 960×540（像素风，硬块感），2 = 1920×1080（高清）。
    /// 版面坐标不变，只是每个坐标能画到 N 倍多的像素上 —— 立绘/图标被缩小的比例也随之减小。
    /// 默认 2：1080p 窗口下 1:1 输出，文字与立绘最清楚；想要老游戏颗粒感就选 1。
    /// </summary>
    public int RenderScale = 2;

    /// <summary>
    /// 着色器画的边是否做解析抗锯齿（头像框的八角切角、宝石、铆片）。
    /// 默认开：边缘按 1 像素过渡；关掉是要 2002 年那种硬台阶。位图立绘与文字不吃这个开关。
    /// </summary>
    public bool AntiAlias = true;

    /// <summary>
    /// 屏幕空间后期抗锯齿，对应 Unity 里 Camera 的 Anti-aliasing 下拉：
    /// 0 = 关，1 = FXAA（最省，字会糊），2 = SMAA（比 FXAA 锐，本项目是简化版：无 LUT）。
    /// 与 AntiAlias 正交 —— Unity 也允许 MSAA 和后处理 AA 同时开。
    /// 注意它作用在**已经画好的整屏**上，所以文字和 1px 内框线也会被一起处理。
    /// </summary>
    public int PostAa = 0;

    /// <summary>
    /// 设置面板里「抗锯齿」一格的取值（把上面两个字段组合成人能看懂的档位）：
    /// 0 = 关，1 = 解析（着色器内），2 = 解析 + FXAA，3 = 解析 + SMAA。
    /// </summary>
    [JsonIgnore]
    public int AaMode
    {
        get
        {
            if (!AntiAlias)
            {
                return 0;
            }
            if (PostAa == 1)
            {
                return 2;
            }
            if (PostAa == 2)
            {
                return 3;
            }
            return 1;
        }
    }

    /// <summary>战斗里是否展开"交手记录"抽屉。
    /// 默认关：记录是查账用的，常驻会把牌阵挡住。开了就记住，下次进战斗仍然开着。</summary>
    public bool BattleLogEnabled = false;
}
