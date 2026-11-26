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


def effect_line(card, mt, numeric=True):
    parts = []
    if numeric:
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


def stat_line(card):
    """该阶条目的数值摘要（攻击 / 法术 / 护体 / 治疗 / 毒）。"""
    parts = []
    if card.get("Damage"):
        parts.append("攻 %d" % card["Damage"])
    if card.get("Spell"):
        parts.append("法 %d" % card["Spell"])
    if card.get("Shield"):
        parts.append("护 %d" % card["Shield"])
    if card.get("Heal"):
        parts.append("疗 %d" % card["Heal"])
    if card.get("Poison"):
        parts.append("毒 %d" % card["Poison"])
    return "、".join(parts) or "—"


def main():
    cards = json.load(open(SRC, encoding="utf-8"))

    # 展开后每阶一条，按 BaseId 聚回「一张卡」
    families = {}
    for c in cards:
        base = c.get("BaseId", c["Id"] % 1000)
        families.setdefault(base, {})[c["MinTier"]] = c

    owners = {0: [], 1: [], 2: [], 3: [], 4: []}
    for base, tiers in families.items():
        first = tiers[sorted(tiers, key=lambda t: TIER_ORDER[t])[0]]
        owners.setdefault(first.get("Master", 0), []).append((base, tiers))

    starters = [(b, t) for o in owners.values() for (b, t) in o
                if any(c.get("StarterOnly") for c in t.values())]
    counts = {name: 0 for name in TIER_ORDER}
    for base, tiers in [(b, t) for o in owners.values() for (b, t) in o]:
        counts[min(tiers, key=lambda x: TIER_ORDER[x])] += 1

    lines = [
        "# 卡表（自动生成）",
        "",
        "> 来源：`Resources/Config/CardConfig.json`，由 `Tools/gen_card_list.py` 导出。",
        "> 改卡请改 `Tools/gen_cards.py`（原始定义）→ `Tools/expand_card_tiers.py`（按阶展开）。",
        "",
        "**配表已按阶展开**：每张卡在它的起步阶及以上各配一条独立条目，数值已经算好写死。",
        "",
        "| 阶 | Id 区间 | 例 |",
        "| --- | --- | --- |",
        "| 铜 | 1 ~ 283 | 大刀 = 1 |",
        "| 银 | 1001 ~ 1283 | 大刀·银 = 1001 |",
        "| 金 | 2001 ~ 2283 | 大刀·金 = 2001 |",
        "| 钻 | 3001 ~ 3283 | 大刀·钻 = 3001 |",
        "",
        "> 表里 `Id` 列是**基础 id**（同族共用）。要改某一阶的数值，去 JSON 里找对应区间的那条。",
        "> 数值列格式：铜 / 银 / 金 / 钻，缺哪阶显示 `—`。",
        "",
        "共 %d 张卡（配表 %d 条）。铜起步 %d / 银起步 %d / 金起步 %d / 钻起步 %d。"
        % (len(families), len(cards), counts["Copper"], counts["Silver"],
           counts["Gold"], counts["Diamond"]),
        "",
    ]

    if starters:
        lines += ["## 起始牌组专用（%d 张，不进商店）" % len(starters), ""]
        lines += ["| Id | 名称 | 类型 | 冷却 | 效果 | 关键词 | 标签 |",
                  "| --- | --- | --- | --- | --- | --- | --- |"]
        for base, tiers in starters:
            c = tiers["Copper"]
            lines.append("| %d | %s | %s | %.1f | %s | %s | %s |" % (
                base, c["Name"], c["Type"], c["Countdown"],
                effect_line(c, 1), c.get("Keywords", "—"), c.get("Tags", "—")))
        lines += [
            "",
            "起始牌组 = **3 劈砍 + 3 招架 + 2 张本命卡**（共 8 张），劈砍 / 招架 不进任何商店与奖励池。",
            "",
        ]

    for owner in (1, 2, 3, 4, 0):
        group = owners.get(owner, [])
        if not group:
            continue
        lines += [
            "## %s —— %d 张" % (OWNER_NAME.get(owner, str(owner)), len(group)),
            "",
            "| Id | 名称 | 类型 | 冷却 | 铜 | 银 | 金 | 钻 | 效果 / 关键词 | 标签 |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
        ]
        group.sort(key=lambda x: x[0])
        for base, tiers in group:
            low = tiers[sorted(tiers, key=lambda t: TIER_ORDER[t])[0]]
            cells = []
            for name in ("Copper", "Silver", "Gold", "Diamond"):
                c = tiers.get(name)
                cells.append(stat_line(c) if c else "—")
            cd = low["Countdown"]
            lines.append("| %d | %s | %s | %.1f | %s | %s | %s | %s | %s | %s |" % (
                base, low["Name"], low["Type"], cd,
                cells[0], cells[1], cells[2], cells[3],
                effect_line(low, TIER_ORDER[low["MinTier"]], False), low.get("Tags", "—")))
        lines.append("")

    open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    print("已导出 %s，共 %d 张卡（配表 %d 条，含起始卡 %d 张）"
          % (OUT, len(families), len(cards), len(starters)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
