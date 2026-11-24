# -*- coding: utf-8 -*-
"""
卡表生成器：给现有 44 张补齐标签，再追加四主角本命卡与通用装备，输出 CardConfig.json。

为什么要生成而不是手写：
    140 多张卡手写 JSON 必然出错（漏字段、ID 重复、图标名拼错）。
    标签与节奏档由这里统一补，数值改动只改这张表。

标签词表（详见 Docs/装备与技能设计.md）：
    家族  Palm 穿云掌 / SaberArt 魔刀 / SwordArt 仙霞剑 / Formation 阵术 /
          Serpent 蛇系 / Fox 狐系 / Transform 变身 / Spirit 御灵 / Tiger 虎煞 / Incantation 咒术 / Ultimate 绝学
    类型  Blade 兵器 / Armor 护具 / Charm 法宝 / Pill 丹 / Talisman 符 / Dart 暗器 / Gu 蛊
    属性  Physical / Fire / Ice / Thunder / Poison / Heal / Shield / Pierce / Multi / Control / Expose
    节奏  Fast(<=2.2) / Mid(2.3-2.8) / Slow(>=2.9) —— 自动补

用法：python Tools/gen_cards.py

【重要】这个脚本只做两件事：①重算所有卡的 Tags（节奏档统一由 Countdown 决定）
②补 NEW 里还没出现过的 id。**已存在的 id 一律跳过重建** ——
早期版本会整条重建，把手工改过的 53 个卡名还原成旧名、把四主角的 Owner 全删掉。
所以：改已有卡的名字/数值请直接改 CardConfig.json（或用 gen_card_list.py 反向核对），
只有当它是新卡时才加进 NEW。
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "Resources", "Config", "CardConfig.json")

# 现有 44 张的标签（按 Id）。类型/属性是人工判断，节奏由 Countdown 自动补。
OLD_TAGS = {
    1: "Blade,Physical", 2: "Blade,Physical", 3: "Blade,Physical", 4: "Whip,Physical",
    5: "Dart,Physical", 6: "Dart,Physical,Pierce", 7: "Sword,Physical", 8: "Sword,Physical",
    9: "Blade,Physical", 10: "Blade,Shield",
    11: "Talisman,Fire", 12: "Talisman,Fire", 13: "Talisman,Thunder", 14: "Talisman,Ice,Shield",
    15: "Charm,Charge",
    16: "Fan,Shield", 17: "Talisman,Shield", 18: "Armor,Shield", 19: "Armor,Shield",
    20: "Armor,Shield",
    21: "Pill,Heal", 22: "Pill,Heal", 23: "Pill,Heal", 24: "Pill,Heal,Physical",
    25: "Gu,Poison", 26: "Gu,Poison", 27: "Gu,Poison",
    28: "Ultimate,Physical",
    29: "Charm,Shield,Heal", 30: "Gu,Poison,Shield",
    31: "Charm,HandMod", 32: "Charm,HandMod", 33: "Charm,HandMod",
    34: "Pill,Control", 35: "Charm,Control", 36: "Gu,Charge",
    37: "Charm,Control,Freeze", 38: "Pill,Heal", 39: "Talisman,Control,Slow", 40: "Talisman,Ice,Freeze",
    41: "Dart,Physical,Multi", 42: "Talisman,Physical", 43: "Charm,Haste", 44: "Talisman,Ice,Freeze,Control",
}

# 新卡：(id, 名称, owner, 类型, 冷却, 数值dict, 关键词, 标签, 图标, 描述)
# 数值字段：Damage/Spell/Element/Shield/Heal/Poison/Expose/IgnoreShield/MultiCount/
#           HandSelf/HandFoe/HandTime/HasteCount/HasteTime/SlowCount/SlowTime/FreezeCount/FreezeTime/ChargeCount
NEW = [
    # ================= 王小虎（owner 1）穿云掌六式 =================
    (45, "穿云裂石", 1, "Attack", 2.6, {"Damage": 9}, "Pierce",
     "Palm,Physical,Pierce", "item_amulet2", "穿云掌第一式。一掌透甲，护体挡不住。"),
    (46, "覆雨翻云", 1, "Attack", 3.0, {"Damage": 6, "MultiCount": 2}, "",
     "Palm,Physical,Multi", "item_amulet2", "穿云掌第二式。掌影连成一片，结算两次。"),
    (47, "云起龙骧", 1, "Attack", 2.8, {"Damage": 11}, "",
     "Palm,Physical", "item_amulet2", "穿云掌第三式。掌势拾级而上。"),
    (48, "烘云托月", 1, "Attack", 3.6, {"Damage": 6, "MultiCount": 3}, "",
     "Palm,Physical,Multi", "item_amulet2", "穿云掌第四式。三掌托一记重手，慢但密。"),
    (49, "拨雨撩云", 1, "Attack", 3.2, {"Damage": 5, "Expose": 1}, "",
     "Palm,Physical,Expose", "item_amulet2", "穿云掌第五式。拨开架势，给目标留个破绽。"),
    (50, "云出无心", 1, "Attack", 3.8, {"Damage": 18}, "",
     "Palm,Physical", "item_amulet2", "穿云掌第六式。收势最重的一掌。"),
    # ================= 王小虎 魔刀刀法五式 =================
    (51, "断月波", 1, "Attack", 2.4, {"Damage": 8}, "",
     "SaberArt,Physical", "item_saber", "魔刀第一式。横斩如水。"),
    (52, "裂地击", 1, "Attack", 2.8, {"Damage": 10}, "",
     "SaberArt,Physical", "item_saber", "魔刀第二式。劈山之势。"),
    (53, "真炎斩", 1, "Attack", 3.2, {"Damage": 7, "MultiCount": 2, "Element": "Fire"}, "",
     "SaberArt,Fire,Multi", "item_saber", "魔刀第三式。刀上起火，两段灼烧。"),
    (54, "炙天煞", 1, "Attack", 3.6, {"Damage": 20, "Element": "Fire"}, "",
     "SaberArt,Fire", "item_saber", "魔刀第四式。一刀烧穿。"),
    (55, "群魔恸", 1, "Attack", 4.2, {"Damage": 8, "MultiCount": 3}, "",
     "SaberArt,Physical,Multi", "item_saber", "魔刀第五式。三刀连落，魔气四散。"),
    # ================= 王小虎 虎煞 =================
    (56, "虎噬", 1, "Attack", 3.0, {"Damage": 6}, "Exhaust",
     "Tiger,Physical", "item_saber2", "虎煞之力：吞噬一口，伤不高，但这是攒煞的入口。"),
    (57, "虎啸风生", 1, "Attack", 4.6, {"Damage": 12, "MultiCount": 2}, "",
     "Tiger,Physical,Multi", "item_saber2", "虎煞絕招。一声长啸，两段风刃。"),
    (58, "猛虎出闸", 1, "Attack", 4.0, {"Damage": 22}, "",
     "Tiger,Physical", "item_saber2", "虎煞絕招。一击必是重手。"),
    (59, "虎踞鲸吞", 1, "Attack", 6.0, {"Damage": 40}, "Remove",
     "Tiger,Physical,Ultimate", "item_talisman8", "虎煞終式。一局只有一次，吞掉眼前的一切。"),
    # ================= 王小虎 辅助 =================
    (60, "气疗术", 1, "Heal", 3.4, {"Heal": 10}, "Exhaust",
     "Incantation,Heal", "item_pill", "调一口真气，回十点气血。"),
    (61, "元灵归心术", 1, "Heal", 4.4, {"Heal": 18}, "Exhaust",
     "Incantation,Heal", "item_pill2", "心持玄咒，元灵归一。回得多，但慢。"),
    (62, "真元护体", 1, "Guard", 3.2, {"Shield": 16}, "",
     "Incantation,Shield", "item_talisman_guard", "以真元护身，护体久驻。"),
    (63, "天罡战气", 1, "Support", 3.8, {"Expose": 2}, "Exhaust",
     "Incantation,Expose", "item_talisman_atk", "战气外放，压得对方手忙脚乱（两层破绽）。"),
    (64, "神眼", 1, "Support", 2.4, {"Expose": 1}, "Exhaust",
     "Incantation,Expose", "item_amulet", "看清对手的路数，留一处破绽。"),
    # ================= 沈欺霜（owner 2）仙霞剑法六式 =================
    (65, "余霞成绮", 2, "Attack", 2.2, {"Damage": 7}, "Pierce",
     "SwordArt,Pierce", "item_sword2", "仙霞剑第一式。剑走偏锋，直取要害。"),
    (66, "白虹彤霞", 2, "Attack", 3.0, {"Damage": 6, "MultiCount": 2}, "",
     "SwordArt,Multi", "item_sword2", "仙霞剑第二式。两道剑虹先后到。"),
    (67, "霞光艳艳", 2, "Attack", 2.6, {"Damage": 9}, "",
     "SwordArt", "item_sword3", "仙霞剑第三式。剑光一亮，朴实但准。"),
    (68, "云霞满天", 2, "Attack", 3.4, {"Damage": 6, "MultiCount": 3}, "",
     "SwordArt,Multi", "item_sword3", "仙霞剑第四式。三剑织成霞网。"),
    (69, "云兴霞蔚", 2, "Attack", 3.0, {"Damage": 13}, "",
     "SwordArt", "item_sword2", "仙霞剑第五式。剑势最盛的一式。"),
    (70, "晚霞烂然", 2, "Spell", 5.0, {"Spell": 26}, "Remove",
     "SwordArt,Ultimate", "item_talisman8", "仙霞剑終式。一局一次，剑光烧尽暮色。"),
    # ================= 沈欺霜 阵术七种 =================
    (71, "火之阵术", 2, "Support", 4.2, {"Expose": 2, "Scope": "Foe"}, "Exhaust",
     "Formation,Fire,Expose", "item_talisman5", "布火阵。己方打得开，对方站不稳（两层破绽）。"),
    (72, "冰之阵术", 2, "Support", 4.2, {"FreezeCount": 0, "FreezeTime": 2, "Scope": "Foe"}, "Exhaust",
     "Formation,Ice,Freeze", "item_talisman6", "布冰阵。敌方全体牌迟滞两秒。"),
    (73, "风之阵术", 2, "Support", 4.0, {"HasteCount": 0, "HasteTime": 5, "Scope": "Self"}, "Exhaust",
     "Formation,Haste", "item_talisman3", "布风阵。我方全体牌双倍蓄势五秒。"),
    (74, "雷之阵术", 2, "Support", 4.4, {"Spell": 10, "Element": "Thunder"}, "Exhaust",
     "Formation,Thunder", "item_talisman7", "布雷阵。落雷一击，十点法术伤害。"),
    (75, "土之阵术", 2, "Support", 4.2, {"Shield": 14}, "Exhaust",
     "Formation,Shield", "item_talisman_guard", "布土阵。厚土护身十四点。"),
    (76, "金刚阵术", 2, "Support", 4.0, {"Shield": 10, "SlowCount": 1, "SlowTime": 4, "Scope": "Foe"}, "Exhaust",
     "Formation,Shield,Control", "item_talisman_guard", "布金刚阵。护体十点，顺手拖慢对方一张牌。"),
    (77, "天师阵术", 2, "Support", 4.6, {"Expose": 2, "HasteCount": 1, "HasteTime": 5}, "Exhaust",
     "Formation,Expose,Haste", "item_talisman8", "布天师阵。己方加速，对方露破绽——阵是双刃。"),
    # ================= 沈欺霜 辅助 =================
    (78, "冰心诀", 2, "Guard", 3.6, {"Shield": 8, "Heal": 6}, "",
     "Incantation,Shield,Heal", "item_talisman4", "心中持诀，心神不乱，护体兼回血。"),
    (79, "仙风云体术", 2, "Support", 4.0, {"HasteCount": 0, "HasteTime": 6, "Scope": "Self"}, "Exhaust",
     "Incantation,Haste", "item_talisman3", "身如风云。我方全体牌双倍蓄势六秒。"),
    (80, "元灵归心术", 2, "Heal", 4.4, {"Heal": 18}, "Exhaust",
     "Incantation,Heal", "item_pill2", "元灵归一，回十八点气血。"),
    (81, "赎魂", 2, "Heal", 5.2, {"Heal": 26}, "Remove",
     "Incantation,Heal,Ultimate", "item_talisman8", "与鬼差交涉。一局一次，把命赎回来。"),
    # ================= 苏媚（owner 3）蛇系 =================
    (82, "灵蛇出洞", 3, "Attack", 2.6, {"Damage": 3, "Poison": 1}, "",
     "Serpent,Poison", "item_poison", "蛇系第一式。快，且带毒。"),
    (83, "杯弓蛇影", 3, "Attack", 2.6, {"Damage": 6, "Poison": 3}, "",
     "Serpent,Poison", "item_poison2", "蛇系第二式。毒性更重。"),
    (84, "五蛟灭世", 3, "Attack", 5.4, {"Damage": 7, "Poison": 4, "MultiCount": 3}, "Remove",
     "Serpent,Poison,Multi,Ultimate", "item_talisman8", "蛇系終式。五蛟齐出，毒三波。"),
    # ================= 苏媚 狐系 =================
    (85, "狐御天雷", 3, "Spell", 2.4, {"Spell": 7, "Element": "Thunder"}, "",
     "Fox,Thunder", "item_talisman3", "狐系第一式。引雷而下。"),
    (86, "狐爪碎岩", 3, "Attack", 3.0, {"Damage": 8}, "Pierce",
     "Fox,Physical,Pierce", "item_dart2", "狐系第二式。爪子比兵器快。"),
    (87, "狐舞动天", 3, "Attack", 3.6, {"Damage": 6, "MultiCount": 3}, "",
     "Fox,Physical,Multi", "item_dart2", "狐系第三式。三段连舞。"),
    # ================= 苏媚 变身 =================
    (88, "化狐", 3, "Support", 4.4, {"HasteCount": 0, "HasteTime": 6, "Scope": "Self"}, "Exhaust",
     "Transform,Fox,Haste", "item_talisman3", "化身狐形。我方全体牌双倍蓄势六秒（近似『攻防上升』）。"),
    (89, "梦蛇", 3, "Support", 4.4, {"Poison": 4, "HasteCount": 1, "HasteTime": 6}, "Exhaust",
     "Transform,Serpent,Poison,Haste", "item_poison", "化为蛇灵。毒加深，一张牌提速（近似『灵速上升』）。"),
    # ================= 苏媚 控制与辅助 =================
    (90, "回梦", 3, "Support", 4.0, {"FreezeCount": 0, "FreezeTime": 3, "Scope": "Foe"}, "Exhaust",
     "Control,Freeze", "item_talisman6", "让人入梦。敌方全体牌三秒不能蓄势（近似『昏睡』）。"),
    (91, "鬼降", 3, "Support", 4.2, {"SlowCount": 0, "SlowTime": 5, "Scope": "Foe"}, "Exhaust",
     "Control,Slow", "item_talisman5", "降下巫蛊，扰乱心智。敌方全体牌半速五秒（近似『疯魔』）。"),
    (92, "夺魂", 3, "Attack", 5.0, {"Damage": 40}, "Remove",
     "Control,Ultimate", "item_talisman8", "吸取魂魄。一局一次，直接抹掉一条命。"),
    (93, "净衣咒", 3, "Heal", 3.2, {"Heal": 8, "Shield": 4}, "Exhaust",
     "Incantation,Heal", "item_pill3", "口念玄咒，解异毒、回气血。"),
    (94, "凝神归元", 3, "Heal", 3.8, {"Heal": 14}, "Exhaust",
     "Incantation,Heal", "item_pill4", "屏气凝神，抱元守一。"),
    # ================= 李忆如（owner 4）咒术 =================
    (95, "观音咒", 4, "Heal", 3.0, {"Heal": 12}, "Exhaust",
     "Incantation,Heal", "item_pill", "水月宫传下的治疗之术。"),
    (96, "金蝉脱壳", 4, "Support", 3.2, {"Shield": 10, "HasteCount": 1, "HasteTime": 4}, "Exhaust",
     "Incantation,Shield,Haste", "item_talisman3", "脱壳遁走。护体十点，顺带一张牌提速。"),
    (97, "还魂咒", 4, "Heal", 4.2, {"Heal": 16}, "Exhaust",
     "Incantation,Heal", "item_pill2", "把魂叫回来。回十六点。"),
    (98, "灵血咒", 4, "Heal", 3.4, {"Heal": 8, "Shield": 6}, "Exhaust",
     "Incantation,Heal,Shield", "item_pill3", "灵血为引，解异常并护体。"),
    (99, "五气朝元", 4, "Heal", 4.8, {"Heal": 14, "Shield": 10}, "Exhaust",
     "Incantation,Heal,Shield", "item_pill4", "五气归元。回血又加护体，是全场最稳的一张。"),
    (100, "真元护体", 4, "Guard", 3.2, {"Shield": 16}, "",
     "Incantation,Shield", "item_talisman_guard", "真元护身，护体常驻。"),
    (101, "元灵归心术", 4, "Heal", 4.4, {"Heal": 18}, "Exhaust",
     "Incantation,Heal", "item_pill2", "元灵归一，回十八点。"),
    (102, "赎魂", 4, "Heal", 5.2, {"Heal": 26}, "Remove",
     "Incantation,Heal,Ultimate", "item_talisman8", "与鬼差交涉。一局一次。"),
    # ================= 李忆如 御灵六只（契约：近似为一次性召唤伤害） =================
    (103, "御灵·锦八爷", 4, "Support", 4.6, {"Shield": 12, "Damage": 6}, "Exhaust",
     "Spirit,Shield", "item_amulet", "土属御灵。落地先护主，再撞一下（近似『五岳擎天』）。"),
    (104, "御灵·蕴儿", 4, "Support", 4.8, {"Spell": 12, "Element": "Ice"}, "Exhaust",
     "Spirit,Ice", "item_amulet", "水属御灵。万里飞霜，冻结般的法术伤害。"),
    (105, "御灵·扬枭", 4, "Support", 4.4, {"Damage": 5, "MultiCount": 3}, "Exhaust",
     "Spirit,Multi", "item_amulet", "风属御灵。风卷残云，三段快攻。"),
    (106, "御灵·火猴", 4, "Support", 4.8, {"Spell": 10, "Element": "Fire", "MultiCount": 2}, "Exhaust",
     "Spirit,Fire,Multi", "item_amulet", "火属御灵。烈焰焚天，两段火。"),
    (107, "御灵·雷兽", 4, "Support", 4.6, {"Spell": 14, "Element": "Thunder"}, "Exhaust",
     "Spirit,Thunder", "item_amulet", "雷属御灵。雷动九天，单点法术。"),
    (108, "御灵·蛊神", 4, "Support", 5.0, {"Poison": 5, "Damage": 4}, "Exhaust",
     "Spirit,Poison", "item_amulet", "蛊属御灵。毒蛊天下，五层毒压上去。"),
    # ================= 李忆如 御灵绝招 =================
    (109, "五岳擎天", 4, "Attack", 3.4, {"Damage": 12, "Shield": 8}, "",
     "Spirit,Shield", "item_amulet2", "锦八爷的招式。土石齐举，攻守一并。"),
    (110, "万里飞霜", 4, "Spell", 3.6, {"Spell": 14, "Element": "Ice"}, "",
     "Spirit,Ice", "item_amulet2", "蕴儿的招式。霜落千里。"),
    (111, "风卷残云", 4, "Attack", 3.0, {"Damage": 5, "MultiCount": 3}, "",
     "Spirit,Multi", "item_amulet2", "扬枭的招式。三记风刃。"),
    (112, "烈焰焚天", 4, "Spell", 3.8, {"Spell": 12, "Element": "Fire"}, "",
     "Spirit,Fire", "item_amulet2", "火猴的招式。一片火海。"),
    (113, "雷动九天", 4, "Spell", 3.6, {"Spell": 16, "Element": "Thunder"}, "",
     "Spirit,Thunder", "item_amulet2", "雷兽的招式。一雷贯顶。"),
    (114, "毒蛊天下", 4, "Poison", 4.0, {"Poison": 6, "Damage": 3}, "",
     "Spirit,Poison", "item_amulet2", "蛊神的招式。六层毒，慢性死亡。"),
    # ================= 通用：装备与道具（owner 0） =================
    (115, "九节鞭", 0, "Attack", 2.6, {"Damage": 9}, "",
     "Whip,Physical", "item_whip2", "九节相连，抽出去收不住。"),
    (116, "断魂刺", 0, "Attack", 2.0, {"Damage": 6}, "Pierce",
     "Spear,Physical,Pierce", "item_dart3", "短刺贴身，专找护体的缝。"),
    (117, "青竹杖", 0, "Guard", 3.4, {"Shield": 12}, "",
     "Staff,Shield", "item_staff", "竹杖挡招，比铁甲轻，比肉身硬。"),
    (118, "油纸伞", 0, "Guard", 3.0, {"Shield": 9}, "",
     "Fan,Shield", "item_umbrella", "撑开就是一面小盾，收起来还能打人。"),
    (119, "飞蝗石", 0, "Attack", 1.8, {"Damage": 5}, "",
     "Dart,Physical", "item_dart", "随手一撒，便宜量大。"),
    (120, "铁蒺藜", 0, "Attack", 2.2, {"Damage": 6, "Expose": 1}, "",
     "Dart,Physical,Expose", "item_dart5", "撒在地上，踩上去就露破绽。"),
    (121, "头巾", 0, "Guard", 3.2, {"Shield": 7, "HasteCount": 1, "HasteTime": 3}, "",
     "Armor,Shield,Haste", "item_armor", "束发戴巾，出手也快些。"),
    (122, "披风", 0, "Guard", 3.6, {"Shield": 11}, "",
     "Armor,Shield", "item_armor2", "厚披风挡刀，也挡风。"),
    (123, "云靴", 0, "Guard", 3.0, {"Shield": 6, "HasteCount": 1, "HasteTime": 3}, "",
     "Armor,Shield,Haste", "item_armor", "走山路的人都知道，鞋比甲重要。"),
    (124, "玉坠", 0, "Guard", 3.8, {"Shield": 8, "Heal": 6}, "",
     "Charm,Shield,Heal", "item_amulet", "佩玉养气，回一点血。"),
    (125, "巽天旗", 0, "Support", 4.4, {"HasteCount": 0, "HasteTime": 5, "Scope": "Self"}, "Exhaust",
     "Charm,Haste", "item_talisman3", "法宝。展开即起风，我方全体提速五秒。"),
    (126, "彻地钟", 0, "Support", 4.6, {"Damage": 14, "Shield": 6}, "Exhaust",
     "Charm,Shield", "item_amulet2", "法宝。钟声一动，地动山摇。"),
    (127, "北冥玄珠", 0, "Support", 4.6, {"Spell": 16, "Element": "Ice"}, "Exhaust",
     "Charm,Ice", "item_amulet2", "法宝。珠中寒气，一击成冰。"),
    (128, "孟婆汤", 0, "Heal", 4.0, {"Heal": 20}, "Exhaust",
     "Pill,Heal", "item_pill4", "一碗忘川。回二十点，忘了疼。"),
    (129, "天香续命露", 0, "Heal", 5.2, {"Heal": 30}, "Remove",
     "Pill,Heal,Ultimate", "item_pill3", "续命之物。一局一次，从鬼门关拉回来。"),
    (130, "九阴散", 0, "Poison", 3.0, {"Poison": 5}, "Exhaust",
     "Pill,Poison", "item_poison2", "阴寒之药。五层毒，慎用。"),
    (131, "赎魂灯", 0, "Heal", 4.8, {"Heal": 16, "Shield": 8}, "Exhaust",
     "Charm,Heal,Shield", "item_pill", "点灯引魂，回血加护体。"),
    (132, "灵心符", 0, "Support", 3.4, {"Shield": 10, "Heal": 6}, "Exhaust",
     "Talisman,Shield,Heal", "item_talisman4", "清心定神。护体十点，顺手回血。"),
    (133, "天师符法", 0, "Spell", 4.0, {"Spell": 18, "Element": "Thunder"}, "Exhaust",
     "Talisman,Thunder", "item_talisman_atk", "天师道的正统符法。十八点雷伤。"),
    (134, "五雷正法", 0, "Spell", 4.6, {"Spell": 12, "Element": "Thunder", "MultiCount": 2}, "Exhaust",
     "Talisman,Thunder,Multi", "item_talisman7", "五雷连落，两段。"),
    (135, "银针", 0, "Attack", 1.8, {"Damage": 4}, "Exhaust",
     "Dart,Physical", "item_dart2", "一针一线，用完就没了。"),
    (136, "无影神针", 0, "Attack", 2.6, {"Damage": 9}, "Exhaust,Pierce",
     "Dart,Physical,Pierce", "item_dart2", "看不见的针，专走空档。"),
    (137, "袖里剑", 0, "Attack", 2.4, {"Damage": 8}, "Exhaust",
     "Dart,Physical", "item_dart4", "袖中藏剑，一击即收。"),
    (138, "血玲珑", 0, "Attack", 3.0, {"Damage": 12, "Heal": 6}, "Remove",
     "Dart,Physical,Heal,Ultimate", "item_dart5", "饮血之物。伤敌亦养己，一局一次。"),
    (139, "金蚕蛊", 0, "Poison", 3.2, {"Poison": 4, "Damage": 4}, "Exhaust",
     "Gu,Poison", "item_poison", "金蚕入体，边咬边毒。"),
    (140, "冰蚕蛊", 0, "Poison", 3.4, {"Poison": 3, "FreezeCount": 1, "FreezeTime": 3, "Scope": "Foe"}, "Exhaust",
     "Gu,Poison,Freeze", "item_poison2", "寒蚕附骨，冻住对方一张牌。"),
    (141, "三尸蛊", 0, "Poison", 3.8, {"Poison": 5, "Damage": 3}, "Exhaust",
     "Gu,Poison", "item_poison2", "三尸缠身，毒得慢但沉。"),
    (142, "幻蛊", 0, "Support", 3.6, {"SlowCount": 1, "SlowTime": 4, "Scope": "Foe"}, "Exhaust",
     "Gu,Control,Slow", "item_poison", "迷人心智，拖慢一张牌。"),
    # ================= 铜阶「入门本命卡」：起始牌组要用，所以都是铜起步 =================
    (145, "聚灵阵", 2, "Support", 4.0, {"Spell": 10, "Expose": 1}, "",
     "Formation,Expose", "item_talisman4", "以灵聚阵，阵中人皆受其伤。仙霞入门阵术。"),
    (146, "唤灵", 4, "Attack", 3.0, {"Damage": 6, "MultiCount": 2}, "",
     "Spirit,Multi", "item_amulet2", "轻唤一声，灵体应召而来。入门御灵。"),
    # ================= 起始基础卡（只在开局牌组里出现，不进商店） =================
    (143, "劈砍", 0, "Attack", 1.4, {"Damage": 3}, "",
     "Blade,Physical", "item_saber", "最基础的刀招。砍得动，但砍不快。"),
    (144, "招架", 0, "Guard", 2.0, {"Shield": 6}, "",
     "Armor,Shield", "item_armor2", "最基础的守势。挡得住，但挡不久。"),
]

# 起始牌组专用卡：不进任何商店、不出现在遭遇奖励里，只能靠删牌去掉。
STARTER_ONLY = {143, 144}

# 已合并掉的重复条目（元灵归心术/真元护体/赎魂 原本四主角各一张，现合并为通用卡）。
# 生成器如果重建它们，会和 61/62/81 撞名，所以显式丢弃。
DROP_IDS = {80, 100, 101, 102}


def tempo(cd):
    if cd <= 2.2:
        return "Fast"
    if cd <= 2.8:
        return "Mid"
    return "Slow"


def merge_tags(base, cd, owner):
    # 节奏档必须由 Countdown 唯一决定：先清掉手写时残留的 Fast/Mid/Slow，
    # 否则会出现「Pill,Control,Slow,Mid」这种自相矛盾的标签。
    tags = [t for t in (base or "").split(",") if t and t not in ("Fast", "Mid", "Slow")]
    tags.append(tempo(cd))
    return ",".join(tags)


def build_card(cid, name, owner, ctype, cd, values, keywords, tags, icon, desc):
    card = {"Id": cid, "Name": name, "Type": ctype, "Countdown": cd}
    for key in ("Damage", "Spell", "Element", "Expose", "Shield", "Heal", "Poison",
                "IgnoreShield", "HandSelf", "HandFoe", "HandTime", "Scope",
                "HasteCount", "HasteTime", "SlowCount", "SlowTime",
                "FreezeCount", "FreezeTime", "ChargeCount", "MultiCount"):
        if key in values and values[key] not in (0, None, "None"):
            card[key] = values[key]
    if keywords:
        card["Keywords"] = keywords
    card["Tags"] = merge_tags(tags, cd, owner)
    if owner:
        card["Owner"] = owner
    card["Icon"] = icon
    card["Desc"] = desc
    if cid in STARTER_ONLY:
        card["StarterOnly"] = True
        card["MinTier"] = "Copper"
    return card


def main():
    old = json.load(open(PATH, encoding="utf-8"))
    old_ids = {c["Id"] for c in old}

    out = []
    for c in old:
        cid = c["Id"]
        # 前 44 张的标签表在 OLD_TAGS 里；之后的新卡用 JSON 里的现值当基准，
        # 只把节奏档重算一遍（手写时可能出现「Slow,Mid」并存）。
        base = OLD_TAGS.get(cid)
        if base is None:
            base = c.get("Tags", "")
        c["Tags"] = merge_tags(base, c.get("Countdown", 2.0), 0)
        c.pop("Owner", None)
        out.append(c)

    # 只补新 id：已存在的卡一律不重建 —— 名字、数值、描述都可能被手工改过，
    # 早期版本直接重建，把 138 条卡名全部还原成改名前的旧名。
    added = 0
    for row in NEW:
        cid = row[0]
        if cid in DROP_IDS:
            continue
        if cid in old_ids:
            continue
        cid, name, owner, ctype, cd, values, kw, tags, icon, desc = row
        out.append(build_card(cid, name, owner, ctype, cd, values, kw, tags, icon, desc))
        added += 1

    # 图标存在性检查：缺图会在运行时刷一屏错误
    item_dir = os.path.join(ROOT, "Resources", "Image", "Items")
    have = {os.path.splitext(f)[0] for f in os.listdir(item_dir) if f.endswith(".png")}
    missing = sorted({c["Icon"] for c in out if c.get("Icon") and c["Icon"] not in have})
    if missing:
        print("缺图标：" + "、".join(missing))
        return 1

    out.sort(key=lambda c: c["Id"])
    json.dump(out, open(PATH, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    print("总卡数 %d（新增 %d 张）" % (len(out), added))
    return 0


if __name__ == "__main__":
    sys.exit(main())
