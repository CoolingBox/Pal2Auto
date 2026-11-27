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
    16: "Fan,Physical,Shield", 17: "Talisman,Shield", 18: "Armor,Shield", 19: "Armor,Shield",
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
    (83, "杯弓蛇影", 3, "Attack", 2.6, {"Damage": 6, "Poison": 1}, "",
     "Serpent,Poison", "item_poison2", "蛇系第二式。毒性更重。"),
    (84, "五蛟灭世", 3, "Attack", 5.4, {"Damage": 7, "Poison": 1, "MultiCount": 3}, "Remove",
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
    (89, "梦蛇", 3, "Support", 4.4, {"Poison": 1, "HasteCount": 1, "HasteTime": 6}, "Exhaust",
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
    (108, "御灵·蛊神", 4, "Support", 5.0, {"Poison": 1, "Damage": 4}, "Exhaust",
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
    (114, "毒蛊天下", 4, "Poison", 8.0, {"Poison": 3, "Damage": 3}, "",
     "Spirit,Poison", "item_amulet2", "蛊神的招式。六层毒，慢性死亡。"),
    # ================= 通用：装备与道具（owner 0） =================
    (115, "九节鞭", 0, "Attack", 2.6, {"Damage": 9}, "",
     "Whip,Physical", "item_whip2", "九节相连，抽出去收不住。"),
    (116, "断魂刺", 0, "Attack", 2.0, {"Damage": 6}, "Pierce",
     "Spear,Physical,Pierce", "item_dart3", "短刺贴身，专找护体的缝。"),
    (117, "菩提宝伞", 0, "Attack", 3.4, {"Damage": 14, "Shield": 8}, "",
     "Fan,Physical,Shield", "item_umbrella", "伞骨如竹节，撑开护身，收起来便是一棍。"),
    (118, "紫萝伞", 0, "Attack", 3.0, {"Damage": 9, "Shield": 5}, "",
     "Fan,Physical,Shield", "item_umbrella", "紫萝缠骨，撑开是盾，收起来能打人。"),
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
    (130, "九阴散", 0, "Poison", 6.0, {"Poison": 1}, "Exhaust",
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
    (139, "金蚕蛊", 0, "Poison", 7.0, {"Poison": 2, "Damage": 4}, "Exhaust",
     "Gu,Poison", "item_poison", "金蚕入体，边咬边毒。"),
    (140, "冰蚕蛊", 0, "Poison", 7.0, {"Poison": 2, "FreezeCount": 1, "FreezeTime": 3, "Scope": "Foe"}, "Exhaust",
     "Gu,Poison,Freeze", "item_poison2", "寒蚕附骨，冻住对方一张牌。"),
    (141, "三尸蛊", 0, "Poison", 8.0, {"Poison": 3, "Damage": 3}, "Exhaust",
     "Gu,Poison", "item_poison2", "三尸缠身，毒得慢但沉。"),
    (142, "幻蛊", 0, "Support", 3.6, {"SlowCount": 1, "SlowTime": 4, "Scope": "Foe"}, "Exhaust",
     "Gu,Control,Slow", "item_poison", "迷人心智，拖慢一张牌。"),
    # ================= 铜阶「入门本命卡」：起始牌组要用，所以都是铜起步 =================
    (145, "聚灵阵", 2, "Support", 4.0, {"Spell": 10, "Expose": 1}, "",
     "Formation,Expose", "item_talisman4", "以灵聚阵，阵中人皆受其伤。仙霞入门阵术。"),
    (146, "唤灵", 4, "Attack", 3.0, {"Damage": 6, "MultiCount": 2}, "",
     "Spirit,Multi", "item_amulet2", "轻唤一声，灵体应召而来。入门御灵。"),
    # ================= 147~177 原著器物补全（通用池，2026-11-24） =================
    # 原则：同名已于 1~146 出现的不再重复；数值按同阶 dps（铜 3~3.5 / 银 4~5 / 金 5~6）与
    # 护体（铜 4 / 银 4.3~5 / 金 5.3~6.2）对齐，机制卡（加速/减速/毒）压一档。
    (147, "青锋剑", 0, "Attack", 1.8, {"Damage": 6}, "",
     "Sword,Physical", "item_sword3", "剑身青亮，轻而快。"),
    (148, "龙渊剑", 0, "Attack", 2.6, {"Damage": 16}, "",
     "Sword,Physical", "item_sword", "古剑出渊，一剑沉如山。"),
    (149, "无双剑", 0, "Attack", 2.8, {"Damage": 13}, "",
     "Sword,Physical", "item_sword2", "剑名无双，招式却有破绽。"),
    (150, "血滴子", 0, "Attack", 2.0, {"Damage": 7}, "",
     "Dart,Physical", "item_dart2", "一顶飞罩，落下时只听风声。"),
    (151, "含沙射影", 0, "Attack", 2.6, {"Damage": 5, "Poison": 1}, "",
     "Dart,Poison", "item_dart3", "沙里藏针，中了不知道，知道时已晚。"),
    (152, "透骨钉", 0, "Attack", 1.6, {"Damage": 6, "IgnoreShield": True}, "",
     "Dart,Physical,Pierce", "item_dart4", "钉穿护体，专破硬功。"),
    (153, "金钱镖", 0, "Attack", 1.6, {"Damage": 5}, "",
     "Dart,Physical", "item_dart5", "铜钱磨边，飞出去比刀还快。"),
    (154, "天师道袍", 0, "Guard", 2.8, {"Shield": 12}, "",
     "Armor,Shield", "item_armor", "道袍宽大，符咒绣在衬里。"),
    (155, "金蚕宝甲", 0, "Guard", 3.2, {"Shield": 18}, "",
     "Armor,Shield", "item_armor2", "蛊中金蚕吐丝织成，刀枪难入。"),
    (156, "玄武战甲", 0, "Guard", 3.4, {"Shield": 20}, "",
     "Armor,Shield", "item_armor", "玄龟之背，沉得压弯腰。"),
    (157, "紫金冠", 0, "Guard", 2.2, {"Shield": 9}, "",
     "Armor,Shield", "item_armor2", "护住顶门，江湖人最怕的就是这一处。"),
    (158, "蛟鳞甲", 0, "Guard", 3.0, {"Shield": 15}, "",
     "Armor,Shield", "item_armor", "蛟皮所制，水浸不腐。"),
    (159, "凤凰羽衣", 0, "Guard", 3.0, {"Shield": 16, "HasteTime": 4, "Scope": "Self"}, "",
     "Armor,Shield,Haste", "item_armor2", "羽衣轻，穿者出手也快。"),
    (160, "土灵符", 0, "Guard", 2.6, {"Shield": 11}, "",
     "Talisman,Shield", "item_talisman_guard", "厚土为壁，挡得住三招。"),
    (161, "玉玲珑", 0, "Guard", 2.4, {"Shield": 10, "Heal": 6}, "",
     "Charm,Shield,Heal", "item_amulet", "玉能养人，贴身带着总有点用。"),
    (162, "圣灵珠", 0, "Spell", 3.6, {"Spell": 18}, "",
     "Spirit,Thunder", "item_amulet2", "珠中藏雷，落地时天都要应一声。"),
    (163, "水灵珠", 0, "Spell", 3.4, {"Spell": 11, "Shield": 6}, "",
     "Spirit,Ice", "item_amulet2", "水气凝珠，既伤人也护人。"),
    (164, "雷灵符", 0, "Spell", 3.0, {"Spell": 9, "Element": "Thunder"}, "",
     "Talisman,Thunder", "item_talisman_atk", "一道符引天雷，烧手也烧敌。"),
    (165, "火灵符", 0, "Spell", 2.6, {"Spell": 8, "Element": "Fire"}, "",
     "Talisman,Fire", "item_talisman_atk", "符火扑面，快得很。"),
    (166, "太虚镜", 0, "Spell", 3.8, {"Spell": 15, "Expose": 1}, "",
     "Charm,Expose", "item_amulet", "照见破绽，敌影皆虚。"),
    (167, "镇妖塔", 0, "Spell", 4.6, {"Spell": 24}, "",
     "Charm,Thunder", "item_amulet2", "塔影压下，妖邪俱伏。"),
    (168, "灵芝", 0, "Heal", 3.0, {"Heal": 10}, "Exhaust",
     "Pill,Heal", "item_pill2", "山中所采，吊命够用。"),
    (169, "天仙玉露", 0, "Heal", 3.2, {"Shield": 6, "Heal": 14}, "Exhaust",
     "Pill,Heal,Shield", "item_pill2", "玉露入喉，血就止住了。"),
    (170, "九转还魂丹", 0, "Heal", 4.4, {"Heal": 26}, "Exhaust",
     "Pill,Heal", "item_pill3", "九转九炼，死人也能拉回来。"),
    (171, "雪莲", 0, "Heal", 3.6, {"Heal": 18}, "Exhaust",
     "Pill,Heal", "item_pill4", "极寒之地长出来的，最补元气。"),
    (172, "龙涎草", 0, "Heal", 2.8, {"Heal": 8, "Shield": 4}, "Exhaust",
     "Pill,Heal,Shield", "item_pill", "草上带龙涎，疗伤也固本。"),
    (173, "蛇蛊", 0, "Poison", 6.0, {"Poison": 1}, "",
     "Gu,Poison", "item_poison", "小蛇入腹，慢慢咬。"),
    (174, "血蛊", 0, "Poison", 7.0, {"Damage": 3, "Poison": 2}, "",
     "Gu,Poison", "item_poison2", "蛊嗜血，越打越欢。"),
    (175, "蛊母", 0, "Poison", 8.0, {"Poison": 3}, "",
     "Gu,Poison", "item_poison", "养蛊之人不敢看它第二眼。"),
    (176, "风灵符", 0, "Support", 2.8, {"HasteTime": 5, "Scope": "Self"}, "Exhaust",
     "Talisman,Haste", "item_talisman3", "符借风势，出手快一分。"),
    (177, "忘忧蛊", 0, "Support", 3.4, {"SlowTime": 5, "Scope": "Foe"}, "Exhaust",
     "Gu,Control", "item_talisman5", "蛊入心神，敌人忘了自己该做什么。"),
    # ================= 178~183 本命器物（每主角 1~2 张招牌） =================
    (178, "魔刀", 1, "Attack", 3.0, {"Damage": 18}, "",
     "SaberArt,Physical", "item_saber", "刀上有魔，握久了会听它说话。"),
    (179, "醉仙望月步", 1, "Support", 3.2, {"HasteTime": 5, "Scope": "Self"}, "Exhaust",
     "Palm,Haste", "item_talisman3", "身法如醉，脚下自有明月。"),
    (180, "御剑术", 2, "Attack", 2.6, {"Damage": 12, "IgnoreShield": True}, "",
     "SwordArt,Pierce", "item_sword", "剑不沾手，破空而去。"),
    (181, "天蛇杖", 3, "Spell", 3.2, {"Spell": 14, "Poison": 1}, "",
     "Serpent,Poison", "item_staff", "杖头盘着蛇，吐信时敌人才知中毒。"),
    (182, "女娲血玉", 4, "Heal", 4.0, {"Heal": 24}, "Exhaust",
     "Incantation,Heal", "item_amulet", "血玉温润，女娲后人的最后一手。"),
    (183, "凤凰翎", 4, "Support", 3.0, {"HasteTime": 6, "Scope": "Self"}, "Exhaust",
     "Spirit,Haste", "item_amulet2", "凤羽一振，灵体先敌而动。"),

    # ================= 184~276 原著器物补全（2026-11-25） =================
    # 来源：《仙剑奇侠传二》物品一览（52PK / 逗游 / 巴哈 / Steam 社区四处交叉核对）。
    # 只做「有战斗效果」的：纯剧情道具（山神石 / 冥钱 / 茶叶蛋）与引擎表达不了的
    # （吉运、金钱加成、属性抗性、真气上限）另行说明，不硬塞成卡。
    (184, "鬼雄", 0, "Attack", 2.8, {"Damage": 14}, "",
     "Blade,Physical", "item_saber2", "古将遗刀，刀鸣如雷。"),
    (185, "凤鸣刀", 0, "Attack", 2.6, {"Damage": 13}, "",
     "Blade,Physical", "item_saber", "出鞘之声如凤鸣。"),
    (186, "威灵", 0, "Attack", 4.0, {"Damage": 22}, "",
     "Blade,Physical", "item_saber2", "古巨人元神所化，除天下之妖邪。"),
    (187, "云纹剑", 0, "Attack", 2.6, {"Damage": 12}, "",
     "Sword,Physical", "item_sword", "层层精钢压合，纹如流云。"),
    (188, "幽真剑", 0, "Attack", 2.2, {"Damage": 9}, "",
     "Sword,Physical", "item_sword2", "轻薄锋利，出手极快。"),
    (189, "流光古剑", 0, "Attack", 2.4, {"Damage": 11}, "",
     "Sword,Physical", "item_sword3", "五金之精，四千九百种灵药铸成。"),
    (190, "幻舞月隐剑", 0, "Attack", 2.2, {"Damage": 10}, "",
     "Sword,Physical", "item_sword2", "舞动时只见月光，不见剑影。"),
    (191, "太乙破阙剑", 0, "Attack", 3.2, {"Damage": 20}, "",
     "Sword,Physical", "item_sword", "剑身宽厚，一击可撼黄泉。"),
    (192, "天绝剑", 0, "Attack", 3.4, {"Damage": 24}, "",
     "Sword,Physical", "item_sword3", "上古剑仙所持，挥动时光华四射。"),
    (193, "金刚锥", 0, "Attack", 2.4, {"Damage": 10}, "",
     "Dart,Physical", "item_dart", "佛门法器，粗制却极沉。"),
    (194, "铁鳞甲", 0, "Guard", 3.4, {"Shield": 16}, "",
     "Armor,Shield", "item_armor", "鱼鳞甲片编缀，沉重但可靠。"),
    (195, "虎头冠", 0, "Guard", 2.4, {"Shield": 10}, "",
     "Armor,Shield", "item_armor2", "铁冠作虎头，威风镇邪。"),
    (196, "护心镜", 0, "Guard", 2.6, {"Shield": 12}, "",
     "Armor,Shield", "item_armor", "护住前胸要害，形如铜镜。"),
    (197, "羌皮靴", 0, "Guard", 2.8, {"Shield": 13}, "",
     "Armor,Shield", "item_armor2", "羌皮柔软，走得快也站得稳。"),
    (198, "武僧靴", 0, "Guard", 2.4, {"Shield": 11}, "",
     "Armor,Shield", "item_armor", "罗汉僧练武所穿的布靴。"),
    (199, "雪貂绒帽", 0, "Guard", 2.6, {"Shield": 12}, "",
     "Armor,Shield", "item_armor2", "雪貂幼子的绒毛，轻柔保暖。"),
    (200, "云霓彩带", 0, "Guard", 2.0, {"Shield": 9, "HasteTime": 3, "Scope": "Self"}, "",
     "Armor,Shield,Haste", "item_amulet", "天玄女的彩带，穿上身法都轻了。"),
    (201, "冲天冠", 0, "Guard", 3.6, {"Shield": 18}, "",
     "Armor,Shield", "item_armor2", "兵神将遗留的金盔，顶插双雉尾。"),
    (202, "背挂蛤蟆", 0, "Guard", 3.8, {"Shield": 17}, "",
     "Armor,Shield", "item_armor", "皮硬胜铁，挂在背后就是一面盾。"),
    (203, "九尺墨影", 0, "Guard", 2.8, {"Shield": 14, "HasteTime": 3, "Scope": "Self"}, "",
     "Armor,Shield,Haste", "item_armor2", "九尺黑蟒，灵动迅捷。"),
    (204, "伞蜘蛛", 0, "Guard", 3.2, {"Shield": 15}, "",
     "Armor,Shield", "item_armor", "遇风而长，持在手里便如伞具。"),
    (205, "兔绒披风", 0, "Guard", 2.4, {"Shield": 10}, "",
     "Armor,Shield", "item_armor2", "兔绒织的披风，轻飘飘挡一刀。"),
    (206, "柚皮帽", 0, "Guard", 2.2, {"Shield": 8}, "",
     "Armor,Shield", "item_armor", "吃完柚子剩下的皮，也算顶帽子。"),
    (207, "银钗", 0, "Guard", 2.0, {"Shield": 7}, "",
     "Armor,Shield", "item_amulet", "银打的发钗，插在发间。"),
    (208, "翠玉金钗", 0, "Guard", 2.2, {"Shield": 9}, "",
     "Armor,Shield", "item_amulet", "绿翡翠嵌黄金，好看也挡得住。"),
    (209, "珍珠冠", 0, "Guard", 2.6, {"Shield": 11}, "",
     "Armor,Shield", "item_amulet", "珍珠缝缀的红色锦冠。"),
    (210, "象牙发簪", 0, "Guard", 2.4, {"Shield": 10}, "",
     "Armor,Shield", "item_amulet", "象牙制成，仕女最爱。"),
    (211, "藏星伞", 0, "Attack", 3.4, {"Damage": 18, "Shield": 9}, "",
     "Fan,Physical,Shield", "item_umbrella", "天河星石为骨，伞内星光如天象。"),
    (212, "符纸伞", 0, "Attack", 3.0, {"Damage": 12, "Shield": 6}, "",
     "Fan,Physical,Shield", "item_talisman_guard", "伞面贴满灵符，可借鬼神之力。"),
    (213, "止血草", 0, "Heal", 2.6, {"Heal": 6}, "Exhaust",
     "Pill,Heal", "item_pill2", "嚼碎敷上，血就止住了。"),
    (214, "行军丹", 0, "Heal", 2.8, {"Heal": 9}, "Exhaust",
     "Pill,Heal", "item_pill2", "活血顺气的药丸。"),
    (215, "紫菁玉蓉膏", 0, "Heal", 4.6, {"Heal": 30}, "Exhaust",
     "Pill,Heal", "item_pill4", "宫廷秘方，疗伤药里的极品。"),
    (216, "鼠儿果", 0, "Heal", 2.8, {"Heal": 8}, "Exhaust",
     "Pill,Heal", "item_pill2", "山间野果，鼠类先尝过的。"),
    (217, "灵山仙芝", 0, "Heal", 3.2, {"Heal": 12}, "Exhaust",
     "Pill,Heal", "item_pill2", "生于枯木的瑞草，养气培元。"),
    (218, "雪莲子", 0, "Heal", 3.6, {"Heal": 16}, "Exhaust",
     "Pill,Heal", "item_pill3", "白玉雪莲的莲子，经脉通畅。"),
    (219, "灵葫仙丹", 0, "Heal", 4.0, {"Heal": 22}, "Exhaust",
     "Pill,Heal", "item_pill4", "修道隐士所炼，一颗顶半条命。"),
    (220, "神仙茶", 0, "Heal", 5.0, {"Heal": 34}, "Exhaust",
     "Pill,Heal", "item_pill3", "广成子养生延寿的秘方。"),
    (221, "陈皮守宫", 0, "Heal", 3.0, {"Heal": 11}, "Exhaust",
     "Pill,Heal", "item_pill2", "陈皮配守宫，气味难闻但管用。"),
    (222, "朱星", 0, "Heal", 4.2, {"Heal": 26}, "Exhaust",
     "Pill,Heal", "item_pill4", "朱红一颗，吊命最灵。"),
    (223, "人参蛆", 0, "Heal", 4.0, {"Heal": 20}, "Exhaust",
     "Pill,Heal", "item_pill3", "人参里长的蛆，药性极重。"),
    (224, "百草蜘蛛", 0, "Heal", 2.6, {"Heal": 7}, "Exhaust",
     "Pill,Heal", "item_pill2", "吃百草长大的蜘蛛，入药。"),
    (225, "冬草夏虫", 0, "Heal", 3.4, {"Heal": 14}, "Exhaust",
     "Pill,Heal", "item_pill2", "冬为草、夏为虫，一物两用。"),
    (226, "灵芝蛤蟆", 0, "Heal", 5.2, {"Heal": 36}, "Exhaust",
     "Pill,Heal", "item_pill4", "背长灵芝的蛤蟆，炼丹者最珍。"),
    (227, "银丝蛇", 0, "Heal", 4.8, {"Heal": 32}, "Exhaust",
     "Pill,Heal", "item_pill3", "银丝缠身的小蛇，能唤回魂。"),
    (228, "嗜血蜂", 0, "Heal", 4.4, {"Heal": 28}, "Exhaust",
     "Pill,Heal", "item_pill4", "好血腥味的蜂，入药奇效。"),
    (229, "鬼枯藤", 0, "Heal", 2.4, {"Heal": 5}, "Exhaust",
     "Pill,Heal", "item_pill2", "黑褐色的毒藤，却也能解毒。"),
    (230, "九节菖蒲", 0, "Heal", 2.6, {"Heal": 6}, "Exhaust",
     "Pill,Heal", "item_pill2", "叶狭长如剑，可解百毒。"),
    (231, "盐巴", 0, "Heal", 2.0, {"Heal": 4}, "Exhaust",
     "Pill,Heal", "item_pill2", "海水煎出来的，有时也解毒。"),
    (232, "雄黄酒", 0, "Heal", 2.4, {"Heal": 5}, "Exhaust",
     "Pill,Heal", "item_pill2", "端午的习俗，雄黄撒进酒里。"),
    (233, "大蒜", 0, "Guard", 2.2, {"Shield": 8}, "Exhaust",
     "Pill,Shield", "item_pill", "除秽袪病，嚼一瓣邪气就退了。"),
    (234, "毒龙胆", 0, "Poison", 8.0, {"Poison": 3}, "Exhaust",
     "Gu,Poison", "item_poison2", "以毒攻毒可解天下之毒，没中毒吃下去就毙命。"),
    (235, "孔雀胆", 0, "Poison", 7.0, {"Poison": 2}, "Exhaust",
     "Gu,Poison", "item_poison", "七大毒蛊之一，耗人真气至死。"),
    (236, "附蛇蛊", 0, "Poison", 7.0, {"Damage": 3, "Poison": 2}, "",
     "Gu,Poison", "item_poison2", "寄附蛇脏的蛊虫，瘴毒加倍。"),
    (237, "尸腐肉", 0, "Poison", 6.0, {"Poison": 1}, "",
     "Gu,Poison", "item_poison", "沾了尸毒的腐肉，臭得能杀人。"),
    (238, "尸蛆", 0, "Poison", 6.0, {"Poison": 1}, "",
     "Gu,Poison", "item_poison2", "从腐肉里长出来的白蛆。"),
    (239, "水晶蜘蛛", 0, "Poison", 7.0, {"Poison": 2}, "",
     "Gu,Poison", "item_poison", "肉眼难见的小蛛，咬了中丝毒。"),
    (240, "赤尾蝎", 0, "Poison", 6.0, {"Damage": 2, "Poison": 1}, "",
     "Gu,Poison", "item_poison2", "赤红毒蝎，尾钩带赤毒。"),
    (241, "青僵蚁", 0, "Poison", 7.0, {"Poison": 2}, "",
     "Gu,Poison", "item_poison", "青色的僵蚁，咬住就不松口。"),
    (242, "战蛊", 0, "Poison", 8.0, {"Poison": 3}, "",
     "Gu,Poison", "item_poison2", "专为厮杀养出来的蛊。"),
    (243, "金甲虫", 0, "Guard", 2.8, {"Shield": 12}, "",
     "Gu,Shield", "item_armor2", "金壳硬得刀都崩口。"),
    (244, "豹纹斗蛊", 0, "Poison", 6.0, {"Damage": 3, "Poison": 1}, "",
     "Gu,Poison", "item_poison", "天生善斗的豹纹蛊虫。"),
    (245, "十里香", 0, "Support", 3.0, {"SlowTime": 4, "Scope": "Foe"}, "Exhaust",
     "Control", "item_talisman5", "点燃后魔物自来，乱其心神。"),
    (246, "迷魂香", 0, "Support", 3.2, {"FreezeTime": 2, "Scope": "Foe"}, "Exhaust",
     "Control,Freeze", "item_talisman5", "香气一过，敌人站着发呆。"),
    (247, "土遁符", 0, "Support", 3.0, {"HasteTime": 4, "Scope": "Self"}, "Exhaust",
     "Talisman,Haste", "item_talisman3", "借土而行，出手快一截。"),
    (248, "腾云符", 0, "Support", 3.4, {"HasteTime": 6, "Scope": "Self"}, "Exhaust",
     "Talisman,Haste", "item_talisman3", "踏云而起，快得看不清。"),
    (249, "圣灵符", 0, "Support", 3.6, {"Expose": 2}, "Exhaust",
     "Talisman,Expose", "item_talisman_atk", "符光一照，敌人破绽尽现。"),
    (250, "观音符", 0, "Heal", 3.0, {"Shield": 5, "Heal": 8}, "Exhaust",
     "Talisman,Heal,Shield", "item_talisman_guard", "观音座前一符，护人也疗人。"),
    (251, "净衣符", 0, "Heal", 2.8, {"Heal": 7}, "Exhaust",
     "Talisman,Heal", "item_talisman4", "净衣净身，去秽祛病。"),
    (252, "金罡珠", 0, "Guard", 4.2, {"Shield": 20}, "",
     "Charm,Shield", "item_amulet", "大罗金仙千年内丹，防御无双。"),
    (253, "珠玉手环", 0, "Guard", 2.8, {"Shield": 12}, "",
     "Charm,Shield", "item_amulet2", "珠玉串成的手环，灵力自生。"),
    (254, "水之笛", 0, "Spell", 3.2, {"Spell": 10, "Shield": 4}, "",
     "Charm,Ice", "item_staff", "水神身边的七孔横笛。"),
    (255, "女娲石", 0, "Guard", 2.2, {"Shield": 8}, "",
     "Charm,Shield", "item_amulet2", "炼石补天所遗的五色石。"),
    (256, "八仙石", 0, "Guard", 2.4, {"Shield": 9}, "",
     "Charm,Shield", "item_amulet", "仙石洞采来的丹矿。"),
    (257, "试炼果", 0, "Spell", 2.6, {"Spell": 7}, "",
     "Charm,Physical", "item_pill4", "神农尝百草时最早发现的珍药。"),
    (258, "山灵琥珀", 0, "Spell", 3.4, {"Spell": 12}, "",
     "Charm,Thunder", "item_amulet2", "琥珀里封着一缕山灵之气。"),
    (259, "炎晶砂", 0, "Spell", 3.0, {"Spell": 9, "Expose": 1}, "",
     "Charm,Fire,Expose", "item_talisman8", "赤红的砂，一撒就是一片火。"),
    (260, "碧玉蜂", 0, "Poison", 7.0, {"Damage": 2, "Poison": 2}, "",
     "Gu,Poison", "item_poison2", "碧玉色的蜂，针上有毒。"),
    (261, "雷煌玉", 0, "Spell", 3.2, {"Spell": 11}, "",
     "Charm,Thunder", "item_amulet", "雷神佩玉，研磨服食可避雷击。"),
    (262, "风凌石", 0, "Spell", 2.8, {"Spell": 8}, "",
     "Charm,Physical", "item_amulet2", "大漠狂风磨了千年的石。"),
    (263, "冰晶石", 0, "Spell", 2.6, {"Spell": 7}, "",
     "Charm,Ice", "item_amulet", "极北之池里的冰糖状石。"),
    (264, "墨云石", 0, "Guard", 2.6, {"Shield": 10}, "",
     "Charm,Shield", "item_amulet2", "苗疆奇石，服食可御瘴毒。"),
    (265, "吞水石蟾", 0, "Guard", 3.6, {"Shield": 17}, "",
     "Charm,Shield,Formation", "item_amulet", "一饮可尽洞庭之水，护身最稳。"),
    (266, "定风蜘蛛", 0, "Guard", 3.0, {"Shield": 14}, "",
     "Charm,Shield", "item_armor", "脚踏八卦，身上纹如巽字。"),
    (267, "避雷蝎", 0, "Guard", 3.0, {"Shield": 14}, "",
     "Charm,Shield", "item_armor2", "紫青小蝎，尾刺色如黄金。"),
    (268, "绝火蚕", 0, "Guard", 3.2, {"Shield": 15}, "",
     "Charm,Shield", "item_armor", "全身铁黑如雪冰凉，不怕火。"),
    (269, "六坤玉锦", 0, "Guard", 3.4, {"Shield": 16}, "",
     "Charm,Shield", "item_armor2", "碧鳞锦蛇，身有六处坤纹。"),
    (270, "幼蛊", 0, "Guard", 2.4, {"Shield": 9}, "",
     "Gu,Shield", "item_poison", "蛊后的幼体，以毒液为食。"),
    (271, "香囊", 0, "Guard", 2.0, {"Shield": 6}, "",
     "Charm,Shield", "item_amulet", "茅香桂皮装进绢袋，佩之以辟邪。"),
    (272, "净气白玉瓶", 0, "Heal", 3.4, {"Heal": 13}, "Exhaust",
     "Charm,Heal", "item_amulet2", "玉瓶去浊还清，佩之真气自回。"),
    (273, "血玉扳指", 0, "Spell", 3.0, {"Spell": 9}, "",
     "Charm,Fire", "item_amulet", "血玉所制，施法省一分真气。"),
    (274, "瘟神镜", 0, "Poison", 7.0, {"Poison": 2}, "",
     "Charm,Poison", "item_amulet2", "被此镜照到者易生疾病。"),
    (275, "灵心珠", 0, "Guard", 3.8, {"Shield": 18}, "",
     "Charm,Shield", "item_amulet", "气定凝神，不为外物所动。"),
    (276, "七彩玉灵芝", 0, "Heal", 4.4, {"Heal": 22}, "Exhaust",
     "Charm,Heal", "item_pill4", "能把身上毒性化作气血。"),
    (277, "食妖虫", 0, "Poison", 6.0, {"Poison": 1}, "",
     "Gu,Poison", "item_poison", "专吃妖气的虫，越吃越毒。"),
    (278, "食妖蛊", 0, "Poison", 6.0, {"Poison": 1}, "",
     "Gu,Poison", "item_poison2", "食妖虫养成的蛊，毒里带妖气。"),
    (279, "傀儡虫", 0, "Support", 3.4, {"FreezeTime": 2, "Scope": "Foe"}, "Exhaust",
     "Gu,Control,Freeze", "item_poison", "钻进后颈，人就成了提线木偶。"),
    (280, "千尺蟾", 0, "Poison", 6.0, {"Poison": 1}, "",
     "Gu,Poison", "item_poison2", "藏在千尺深潭的蟾，毒得慢也毒得久。"),
    (281, "青铜花", 0, "Poison", 6.0, {"Poison": 1}, "",
     "Gu,Poison", "item_poison", "花是青铜色的，香气也是毒。"),
    (282, "雪蛤蟆", 0, "Guard", 2.8, {"Shield": 12}, "",
     "Gu,Shield", "item_armor2", "雪里蹲着的蛤蟆，皮冻得比铁硬。"),
    (283, "野球拳", 0, "Attack", 4.0, {"Damage": 6, "SpawnCardId": 283}, "",
     "Palm,Physical", "item_amulet2", "谁都会的一拳。打完两边都学会了这一招。"),
    (284, "催命符", 0, "Spell", 5.0, {"FatigueShift": 6.0, "Scope": "Foe"}, "",
     "Talisman,Control", "item_talisman3", "一张黄纸，写上名字。对方的风沙来得早一些。"),
    (285, "续命丹", 0, "Heal", 6.0, {"Heal": 8, "FatigueShift": -6.0, "Scope": "Self"}, "",
     "Pill,Heal", "item_pill2", "吞下去，能多撑一阵。自己的风沙晚一点到。"),
    (286, "同归帖", 0, "Attack", 6.0, {"Damage": 6, "FatigueShift": 5.0, "Scope": "Both"}, "",
     "Control,Physical", "item_talisman6", "既然要打，就早点打完。双方的风沙一起提前。"),
    # ================= 起始基础卡（只在开局牌组里出现，不进商店） =================
    (143, "劈砍", 0, "Attack", 1.4, {"Damage": 3}, "",
     "Blade,Physical", "item_saber", "最基础的刀招。砍得动，但砍不快。"),
    (144, "招架", 0, "Guard", 2.0, {"Shield": 6}, "",
     "Armor,Shield", "item_armor2", "最基础的守势。挡得住，但挡不久。"),
]

# 新卡的起步阶。1~146 的阶位写在 JSON 里、生成器不动；147 之后必须在这张表登记，
# 否则一律按 CardConfig.MinTier 的默认值（铜）落地 —— 一张金卡按铜价卖，直接超模。
NEW_TIERS = {
    147: "Copper", 148: "Gold", 149: "Silver", 150: "Copper", 151: "Silver",
    152: "Silver", 153: "Copper", 154: "Silver", 155: "Gold", 156: "Diamond",
    157: "Silver", 158: "Silver", 159: "Gold", 160: "Copper", 161: "Silver",
    162: "Gold", 163: "Silver", 164: "Copper", 165: "Copper", 166: "Gold",
    167: "Diamond", 168: "Copper", 169: "Silver", 170: "Gold", 171: "Silver",
    172: "Copper", 173: "Copper", 174: "Silver", 175: "Gold", 176: "Silver",
    177: "Silver", 178: "Gold", 179: "Silver", 180: "Gold", 181: "Gold",
    182: "Gold", 183: "Silver",
    184: "Silver",
    185: "Gold",
    186: "Diamond",
    187: "Silver",
    188: "Copper",
    189: "Silver",
    190: "Silver",
    191: "Gold",
    192: "Diamond",
    193: "Silver",
    194: "Silver",
    195: "Silver",
    196: "Copper",
    197: "Copper",
    198: "Copper",
    199: "Silver",
    200: "Gold",
    201: "Gold",
    202: "Gold",
    203: "Gold",
    204: "Silver",
    205: "Copper",
    206: "Copper",
    207: "Copper",
    208: "Silver",
    209: "Silver",
    210: "Copper",
    211: "Gold",
    212: "Silver",
    213: "Copper",
    214: "Copper",
    215: "Diamond",
    216: "Copper",
    217: "Silver",
    218: "Silver",
    219: "Gold",
    220: "Diamond",
    221: "Copper",
    222: "Gold",
    223: "Silver",
    224: "Copper",
    225: "Silver",
    226: "Diamond",
    227: "Diamond",
    228: "Gold",
    229: "Copper",
    230: "Copper",
    231: "Copper",
    232: "Copper",
    233: "Copper",
    234: "Gold",
    235: "Silver",
    236: "Silver",
    237: "Copper",
    238: "Copper",
    239: "Silver",
    240: "Copper",
    241: "Silver",
    242: "Gold",
    243: "Silver",
    244: "Copper",
    245: "Copper",
    246: "Silver",
    247: "Copper",
    248: "Silver",
    249: "Silver",
    250: "Copper",
    251: "Copper",
    252: "Gold",
    253: "Silver",
    254: "Gold",
    255: "Copper",
    256: "Copper",
    257: "Copper",
    258: "Silver",
    259: "Silver",
    260: "Silver",
    261: "Silver",
    262: "Copper",
    263: "Copper",
    264: "Copper",
    265: "Gold",
    266: "Silver",
    267: "Silver",
    268: "Silver",
    269: "Gold",
    270: "Copper",
    271: "Copper",
    272: "Silver",
    273: "Silver",
    274: "Silver",
    275: "Gold",
    276: "Gold",
    283: "Copper",
    284: "Copper",
    285: "Silver",
    286: "Gold"
}

# 起始牌组专用卡：不进任何商店、不出现在遭遇奖励里，只能靠删牌去掉。
STARTER_ONLY = {143, 144}

# 已合并掉的重复条目（元灵归心术/真元护体/赎魂 原本四主角各一张，现合并为通用卡）。
# 生成器如果重建它们，会和 61/62/81 撞名，所以显式丢弃。
DROP_IDS = {80, 100, 101, 102}


def tempo(cd):
    # 2026-11-25 冷却统一成整秒后重定的阈值：3 秒内算快、5 秒内算中、再慢算重
    if cd <= 3.0:
        return "Fast"
    if cd <= 5.0:
        return "Mid"
    return "Slow"


def merge_tags(base, cd, owner):
    # 节奏档必须由 Countdown 唯一决定：先清掉手写时残留的 Fast/Mid/Slow，
    # 否则会出现「Pill,Control,Slow,Mid」这种自相矛盾的标签。
    tags = [t for t in (base or "").split(",") if t and t not in ("Fast", "Mid", "Slow")]
    tags.append(tempo(cd))
    return ",".join(tags)


def build_card(cid, name, master, ctype, cd, values, keywords, tags, icon, desc):
    card = {"Id": cid, "Name": name, "Type": ctype, "Countdown": cd}
    for key in ("Damage", "Spell", "Element", "Expose", "Shield", "Heal", "Poison",
                "IgnoreShield", "HandSelf", "HandFoe", "HandTime", "Scope",
                "HasteCount", "HasteTime", "SlowCount", "SlowTime",
                "FreezeCount", "FreezeTime", "ChargeCount", "MultiCount", "SpawnCardId",
                "FatigueShift"):
        if key in values and values[key] not in (0, None, "None"):
            card[key] = values[key]
    if keywords:
        card["Keywords"] = keywords
    card["Tags"] = merge_tags(tags, cd, master)
    if master:
        card["Master"] = master
    card["Icon"] = icon
    card["Desc"] = desc
    if cid in STARTER_ONLY:
        card["StarterOnly"] = True
        card["MinTier"] = "Copper"
    return card


def main():
    # 卡表可能已经被 expand_card_tiers.py 展开成「每阶一条」——
    # 这里只取**每族的起步阶条目**作为原始定义，否则重跑会把展开结果当输入再展一次。
    # 不能用「Id < 1000」判断：银/金起步的卡（如龙渊剑 Gold）展开后只有 2148/3148，没有 < 1000 的条目，
    # 会被整族丢掉（2026-11-27 踩过一次，卡表从 279 掉到 260）。
    all_cards = json.load(open(PATH, encoding="utf-8"))
    tier_rank = {"Copper": 0, "Silver": 1, "Gold": 2, "Diamond": 3}
    by_base = {}
    for c in all_cards:
        base = c.get("BaseId", c["Id"] % 1000)
        rank = tier_rank.get(c.get("MinTier", "Copper"), 0)
        if base not in by_base or rank < by_base[base][0]:
            by_base[base] = (rank, c)
    old = []
    foreach_value = list(by_base.values())
    for i in range(len(foreach_value)):
        entry = dict(foreach_value[i][1])
        entry["Id"] = foreach_value[i][1].get("BaseId", foreach_value[i][1]["Id"] % 1000)
        old.append(entry)
    old_ids = {c["Id"] for c in old}

    # NEW 的第 3 列是 master（武学归属）。已存在的卡从这里恢复归属 ——
    # 2026-11-24 之前这里是一句无条件的 `c.pop("Owner")`，把 45~146 的本命归属全删掉了，
    # 商店偏好与武学馆的池子因此直接失效，而当时没人看得出来。
    master_of = {}
    for row in NEW:
        master_of[row[0]] = row[2]

    out = []
    for c in old:
        cid = c["Id"]
        # 前 44 张的标签表在 OLD_TAGS 里；之后的新卡用 JSON 里的现值当基准，
        # 只把节奏档重算一遍（手写时可能出现「Slow,Mid」并存）。
        base = OLD_TAGS.get(cid)
        if base is None:
            base = c.get("Tags", "")
        c["Tags"] = merge_tags(base, c.get("Countdown", 2.0), 0)
        if cid <= 44:
            # 通用卡不该有归属
            c.pop("Master", None)
        else:
            master = master_of.get(cid, 0)
            if master:
                c["Master"] = master
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
        cid, name, master, ctype, cd, values, kw, tags, icon, desc = row
        card = build_card(cid, name, master, ctype, cd, values, kw, tags, icon, desc)
        tier = NEW_TIERS.get(cid)
        if tier and not card.get("MinTier"):
            card["MinTier"] = tier
        out.append(card)
        added += 1

    # 图标存在性检查：缺图会在运行时刷一屏错误
    item_dir = os.path.join(ROOT, "Resources", "Image", "Items")
    have = {os.path.splitext(f)[0] for f in os.listdir(item_dir) if f.endswith(".png")}
    missing = sorted({c["Icon"] for c in out if c.get("Icon") and c["Icon"] not in have})
    if missing:
        print("缺图标：" + "、".join(missing))
        return 1

    out.sort(key=lambda c: c["Id"])
    # newline="\n"：Windows 下 text mode 默认写 CRLF，会让 .gitattributes 的 eol=lf 每次 add 都报警
    json.dump(out, open(PATH, "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=2)

    print("总卡数 %d（新增 %d 张）" % (len(out), added))
    return 0


if __name__ == "__main__":
    sys.exit(main())
