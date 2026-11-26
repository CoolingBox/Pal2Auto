# -*- coding: utf-8 -*-
"""把卡表按阶位展开：每张卡在它的起步阶及以上的每一阶，各配一条独立条目。

为什么这么做（2026-11-26 用户要求）：
  1. 商店按阶抽卡时，「初始为铜」的卡在后期不会再出现 —— 因为运行时的阶位是乘数，
     铜卡的铜版本永远不会被当成「金卡」抽出来；
  2. 数值藏在乘数里（快 1.35 / 中 1.45 / 慢 1.60，机制再 ×0.85），人工改一个金卡的数值得先心算乘数；
  3. 展开之后，每一阶都是配表里的一行数字，改哪阶改哪行。

Id 规则（人工读 id 就知道阶位）：
  铜 = 原 id（1~283）   银 = 1000 + 原 id   金 = 2000 + 原 id   钻 = 3000 + 原 id
新增字段：
  BaseId —— 同族卡的根源 id（铜版就是自己），合成与卡表分组用它
  MinTier —— 语义改成「这就是第几阶」，于是 CardTierRules 的 steps 恒为 0，不再二次缩放

用法：python Tools/expand_card_tiers.py
"""

import json
import math
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "Resources", "Config", "CardConfig.json")

TIER_RANK = {"Copper": 0, "Silver": 1, "Gold": 2, "Diamond": 3}
TIER_NAME = ["Copper", "Silver", "Gold", "Diamond"]
TIER_OFFSET = [0, 1000, 2000, 3000]

# 与 CardTierRules 保持一致
FAST_GROWTH = 1.35
MID_GROWTH = 1.45
SLOW_GROWTH = 1.60
MECHANIC_DISCOUNT = 0.85
MECHANIC_COOLING = 0.90
MIXED_COOLING = 0.95
MIN_COUNTDOWN = 0.8

NUMERIC_FIELDS = ("Damage", "Spell", "Shield", "Heal", "Poison")


def has_mechanic(card):
    if card.get("HandSelf", 0) or card.get("HandFoe", 0):
        return True
    if card.get("HasteTime", 0) > 0 or card.get("SlowTime", 0) > 0 or card.get("FreezeTime", 0) > 0:
        return True
    if card.get("ChargeCount", 0) > 0 or card.get("MultiCount", 0) > 1:
        return True
    if card.get("Expose", 0) > 0:
        return True
    return False


def has_numeric(card):
    return any(card.get(f, 0) > 0 for f in NUMERIC_FIELDS)


def growth_of(card):
    cd = card.get("Countdown", 2.0)
    if cd <= 3.0:
        g = FAST_GROWTH
    elif cd <= 5.0:
        g = MID_GROWTH
    else:
        g = SLOW_GROWTH
    if has_mechanic(card):
        g *= MECHANIC_DISCOUNT
    return g


def scaled(base, factor):
    value = int(round(base * factor + 1e-9))
    if value <= base:
        value = base + 1
    return value


def build_tier(card, tier_rank):
    """按目标阶位造一条新条目（从铜版算起，累乘到该阶）。"""
    copy = dict(card)
    cd = card.get("Countdown", 2.0)
    factor = 1.0
    cd_factor = 1.0
    g = growth_of(card)
    mechanic = has_mechanic(card)
    numeric = has_numeric(card)

    for _ in range(tier_rank):
        factor *= g

    if mechanic and numeric:
        cd_factor = MIXED_COOLING ** tier_rank
    elif mechanic and not numeric:
        cd_factor = MECHANIC_COOLING ** tier_rank

    for field in NUMERIC_FIELDS:
        if card.get(field):
            copy[field] = scaled(card[field], factor)

    new_cd = cd * cd_factor
    if new_cd < MIN_COUNTDOWN:
        new_cd = MIN_COUNTDOWN
    copy["Countdown"] = float(new_cd)

    return copy


def main():
    cards = json.load(open(PATH, encoding="utf-8"))
    out = []

    for card in cards:
        base_id = card["Id"] % 1000
        start = TIER_RANK.get(card.get("MinTier", "Copper"), 0)

        # 原始条目当铜版：清掉可能存在的旧展开痕迹
        card["Id"] = base_id
        card["BaseId"] = base_id
        if start > 0:
            card["MinTier"] = TIER_NAME[start]

        for rank in range(start, 4):
            if rank == start:
                entry = dict(card)
                if rank > 0:
                    entry = build_tier(card, 0)   # 起步阶本身：数值不动
            else:
                entry = build_tier(card, rank - start)

            entry["Id"] = base_id + TIER_OFFSET[rank]
            entry["BaseId"] = base_id
            entry["MinTier"] = TIER_NAME[rank]
            out.append(entry)

    out.sort(key=lambda c: (c["MinTier"], c["Id"]))
    out.sort(key=lambda c: c["Id"])

    max_id = max(c["Id"] for c in out)
    with open(PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)

    by_tier = {}
    for c in out:
        by_tier[c["MinTier"]] = by_tier.get(c["MinTier"], 0) + 1
    print("展开后 %d 条（最大 Id %d）：%s" % (len(out), max_id, by_tier))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
