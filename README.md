# 仙剑2自走棋（工程名 Pal2Auto）

Godot 4.7 + C#（.NET 8）。核心玩法：**卡组构筑 + 手牌自动出手**——牌组是构筑，手牌是自动化产线。

---

## 跑起来

```bash
# 编辑器
D:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe --path D:\Godot\Projects\Pal2Auto

# 只编译
dotnet build
```

主场景 `Resources/Scene/Game.tscn`（空 Node），入口是 autoload `GameRoot`。

---

## 玩法（当前版本）

1. 主界面「开局」→ 选人（四位侠客：王小虎 / 沈欺霜 / 苏媚 / 李忆如）
2. 「就他了」直接进战斗：抽 5 张手牌，每张牌各自蓄势倒计时
3. 倒计时归零 → 自动打出 → 进弃牌堆 → **立刻补抽一张**进空位
4. 抽牌堆空了把弃牌堆洗回去；两边都空则该位留空
5. 敌方也是牌组（5 张手牌 + 独立倒计时），UI 上能看到它手上的牌与"下一手"
6. 关键字：**消耗**（打完进消耗堆，本场不再循环）、**移除**（永久离库）、**贯穿**（无视护体），卡面左上角有单字角标
7. 上场数基础 5、上限 8，器物牌可改自己/对方/双方，有本场持续与限时两种
8. 时机关键字：加速 / 充能 / 减速 / 冰冻 各带**作用范围**（我 / 敌 / 双方，张数写 0 = 全体），**多重**一次结算多下；加速与减速相乘相消
9. 每用一张牌，该槽位进入 **0.25 秒补抽冷却**（多重按次数累加），冷却走完才补牌

**界面**：上下对峙——敌在上、我在下，两排牌阵夹在中间；卡面上的效果数值放大到 18px、
倒计时 30px 并标"蓄势"。双方各有抽牌堆/弃牌堆（显示张数，弃牌堆露出最后一张做底纹，
悬停看牌名）。左上角「交战记录」是可选抽屉（默认关闭），
明细逐行写时间/牌名/真实掉血/被护体挡住多少，统计页给双方出手次数、均伤、直伤、承伤、毒伤等。

规则细节与数值取舍见 `Docs/卡组战斗设计.md`。

---

## 结构

```
Scripts/
  GameRoot.cs                autoload 入口：离屏画布 + 管理器启动 + 主界面
  Game/
    Config/                  CharacterConfig / CardConfig
    Core/GameFlow.cs         跨面板状态（当前选中的侠客）
    Battle/DeckBattle.cs     卡组战斗内核（纯逻辑，不碰节点）
    UI/                      UiKit / CardView / MainMenuPanel / CharacterSelectPanel
                             / BattlePanel / SettingsPanel
    DevShot.cs               --shot / --ui=battle|select|settings / --log[=stats] 等开发开关
  TinyFramework/             单例 / UIManager / 存档 / 配置表 / 音频（已清掉旧项目注释）
Resources/
  Config/                    CharacterConfig.json / CardConfig.json / PortraitManifest.json
  Image/{Portrait,Items,Env} 40 张立绘 / 10 张物品图标 / 屋顶瓦片
  UI/*.tscn                  面板空壳（版面全在 C# 里铺）
  Shaders/{xbrz,fade_bottom}.gdshader
  Audio/{BGM,SFX}
Docs/
  卡组战斗设计.md
  screenshots/
Tools/                       （素材导入脚本在旧工程里，这里按需手工导入）
```

---

## 画面

960×540 离屏画布（SubViewport）→ `xbrz.gdshader` 放大到窗口；设置里可切成原像素。
鼠标事件在 `GameRoot._Input` 里换算坐标后 `PushInput` 给离屏画布。

---

## 开发开关

```bash
--shot=2              启动 2 秒后截图到 user:// 并退出
--ui=battle           直接进战斗
--ui=select           直接进选人
--ui=settings         直接开设置
--no-xbrz             强制关闭放大滤镜（截图对比用）
```

---

## 素材

`D:\Res\仙剑2解包` 的原始导出帧（立绘 / 物品图标 / 屋顶瓦片 / 音效 / BGM）。
版权归原权利方，仅供个人学习研究，不对外分发。
