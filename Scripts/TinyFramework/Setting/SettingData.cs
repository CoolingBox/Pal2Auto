// 文件：SettingData.cs
// 描述：玩家偏好（音量、倍速、画质）。与局内存档分开：这些是"怎么看"的设置，
//       换一局、重开都不该被重置。

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

    /// <summary>画面放大是否走 xBRZ 边缘感知滤镜。
    /// 开：960×540 离屏画布 → 放大到窗口，像素块的对角被磨圆；
    /// 关：直接按窗口分辨率渲染，字形最锐利。</summary>
    public bool XbrzEnabled = true;
}
