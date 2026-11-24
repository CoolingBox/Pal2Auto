# -*- coding: utf-8 -*-
"""
卡表导出：把 CardConfig.json 导成 Docs/卡表.md。

为什么要脚本：140 张卡手写表格必然和 JSON 脱节。
改卡请改 Tools/gen_cards.py，再跑本脚本刷新文档。

用法：python Tools/gen_card_list.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Resources", "Config", "CardConfig.json")
OUT = os.path.join(ROOT, "Docs", "卡表.md")

TIER_NAME = {"Copper": "铜", "Silver": "银", "Gold": "金", "Diamond": "钻"}
TIER_ORDER = {"Copper": 1, "Silver": 2, "Gold": 3, "Diamond": 4}
OWNER_NAME = {
    0: "通用（四主角共用）",
    1: "王小虎（虎煞 · 穿云掌 / 魔刀）",
    2: "沈欺霜（仙霞剑法 · 阵术）",
    3: "苏媚（蛇系 · 狐系 · 变身）",
    4: "李忆如（咒术 · 御灵）",
}


def growth(card):
    g = 1.6
    if card["Countdown"] <= 2.2:
        g = 1.35
    elif card["Countdown"] <= 2.8:
        g = 1.45
    mech = bool(card.get("HandSelf") or card.get("HandFoe") or card.get("HasteTime")
                or card.get("SlowTime") or card.get("FreezeTime") or card.get("ChargeCount")
                or (card.get("MultiCount") or 0) > 1)
    if mech:
        g *= 0.85
    return g


def seq(base, card, mt):
    if base <= 0:
        return str(base)
    g = growth(card)
    vals = []
    for t in range(mt, 5):
        v = int(round(base * (g ** (t - mt))))
        if not vals or v != vals[-1]:
            vals.append(v)
    return "/".join(str(x) for x in vals)


def effect_line(card, mt):
    parts = []
    for key, label in (("Damage", "攻 %d"), ("Spell", "法 %d"), ("Shield", "护 %d"),
                       ("Heal", "疗 %d"), ("Poison", "毒 %d")):
        if card.get(key):
            parts.append(label % card[key] + "（" + seq(card[key], card, mt) + "）")
    if card.get("Expose"):
        parts.append("破绽 %d（不涨）" % card["Expose"])
    if card.get("MultiCount"):
        parts.append("×%d 次" % card["MultiCount"])
    if card.get("HandSelf"):
        parts.append("上场 %+d" % card["HandSelf"])
    if card.get("HandFoe"):
        parts.append("敌上场 %+d" % card["HandFoe"])
    scope = card.get("Scope", "Self")
    prefix = {"Foe": "敌", "Both": "双"}.get(scope, "我")
    if card.get("FreezeTime"):
        parts.append(prefix + "冰冻 %g 秒" % card["FreezeTime"])
    if card.get("HasteTime"):
        parts.append(prefix + "加速 %g 秒" % card["HasteTime"])
    if card.get("SlowTime"):
        parts.append(prefix + "减速 %g 秒" % card["SlowTime"])
    if card.get("ChargeCount"):
        parts.append("充能 %d 张" % card["ChargeCount"])
    return "、".join(parts) or "—"


def main():
    cards = json.load(open(SRC, encoding="utf-8"))

    groups = {0: [], 1: [], 2: [], 3: [], 4: []}
    for c in cards:
        groups.setdefault(c.get("Owner", 0), []).append(c)

    starters = [c for c in cards if c.get("StarterOnly")]
    counts = {t: 0 for t in TIER_NAME}
    for c in cards:
        counts[c.get("MinTier", "Copper")] = counts.get(c.get("MinTier", "Copper"), 0) + 1

    lines = [
        "# 卡表（自动生成）",
        "",
        "> 来源：`Resources/Config/CardConfig.json`，由 `Tools/gen_card_list.py` 导出。",
        "> 改卡请改 `Tools/gen_cards.py` 再跑一遍，不要手改这份文档。",
        "> 阶位规则见 `Docs/卡牌等阶与商店.md`：`起步` 是该卡最低能出到的阶。",
        "> 效果列括号内是「起步阶 → 钻阶」的数值序列；破绽、多重次数、时机秒数、上场数不随阶涨。",
        "",
        "共 %d 张。铜起步 %d / 银起步 %d / 金起步 %d / 钻起步 %d。"
        % (len(cards), counts["Copper"], counts["Silver"], counts["Gold"], counts["Diamond"]),
        "",
    ]

    if starters:
        lines += [
            "## 起始牌组专用（%d 张，不进商店）" % len(starters),
            "",
            "| Id | 名称 | 类型 | 冷却 | 效果 | 关键词 | 标签 |",
            "| --- | --- | --- | --- | --- | --- | --- |",
        ]
        for c in starters:
            lines.append("| %d | %s | %s | %.1f | %s | %s | %s |" % (
                c["Id"], c["Name"], c["Type"], c["Countdown"],
                effect_line(c, 1), c.get("Keywords", "—"), c.get("Tags", "—")))
        lines += [
            "",
            "起始牌组 = **3 劈砍 + 3 招架 + 2 张本命卡**（共 8 张），劈砍 / 招架 不进任何商店与奖励池。",
            "两张基础卡的数值刻意压到同阶通用件的一半左右（劈砍 2.14 dps 是大刀的 48%，招架 3.00 护/秒是麻布衣的 80%），",
            "**不带 `Exhaust`**：打出后照常进弃牌堆，每洗一次牌都会转回来 —— 详见 `Docs/起始牌组设计.md`。",
            "",
        ]

    for owner in (1, 2, 3, 4, 0):
        group = groups.get(owner, [])
        if not group:
            continue
        lines += [
            "## %s —— %d 张" % (OWNER_NAME.get(owner, str(owner)), len(group)),
            "",
            "| Id | 名称 | 类型 | 起步 | 冷却 | 效果（起步 → 钻） | 关键词 | 标签 |",
            "| --- | --- | --- | --- | --- | --- | --- | --- |",
        ]
        for c in group:
            mt = TIER_ORDER.get(c.get("MinTier", "Copper"), 1)
            lines.append("| %d | %s | %s | %s | %.1f | %s | %s | %s |" % (
                c["Id"], c["Name"], c["Type"], TIER_NAME.get(c.get("MinTier", "Copper"), "铜"),
                c["Countdown"], effect_line(c, mt), c.get("Keywords", "—"), c.get("Tags", "—")))
        lines.append("")

    open(OUT, "w", encoding="utf-8").write("\n".join(lines))
    print("已导出 %s，共 %d 张（含起始卡 %d 张）" % (OUT, len(cards), len(starters)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
