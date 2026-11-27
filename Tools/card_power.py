# -*- coding: utf-8 -*-
"""卡牌分值：把一张卡折算成「每秒等价伤害」，用于衡量构筑与牌组的强度。

为什么需要它（2026-11-27）：
  敌血表按天标定之后发现，同一品阶的怪强度差一倍（6 号白石版精 258 血 0% vs 8 号冻狸 278 血 62%）。
  根因是 `gen_enemies` 随机拼牌 —— 抽到三张红瓔刀和三张尸蛆是两种怪。
  要分层就得先有个「这张卡值多少」的标尺。

公式（v1）：
    每秒价值 = (伤害 × 次数 + 护体 × 0.6 + 治疗 × 0.5) / 蓄势 + 毒层数 × 1.0

  系数 rationale：
  - 护体 ×0.6：只在挨打时兑现，而且每战满血（护体不会跨场累积），打不满的场合等于白给。
  - 治疗 ×0.5：受血量上限约束，溢出的部分是浪费。
  - 毒 ×1.0：1 层 = 每秒 1 伤，**穿护体、不衰减、不封顶**，所以它不打折也不除蓄势
    （蓄势只决定「多久叠一次」，毒的强度上限由层数决定）。
  - 破绽 +0.3/层：让后续伤害 +25%，按「一张卡触发一次」估。
  - 多重已含在「伤害 × 次数」里。

用法：
    python Tools/card_power.py            # 打印全卡池分值分布
    python Tools/card_power.py --family   # 按族看四阶成长
"""

import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CARDS = os.path.join(ROOT, "Resources", "Config", "CardConfig.json")

SHIELD_WEIGHT = 0.6
HEAL_WEIGHT = 0.5
POISON_WEIGHT = 1.0
EXPOSE_BONUS = 0.3

# 机制附加分（绝对值，不再除蓄势）：
#   纯机制卡（风灵符、土遁符）的数值是 0，只按数值算会被判成「0 分废卡」，
#   但它们实际改变的是「出手节奏」，对强度的影响是真实的 —— 按「一次效果值多少 dps」给固定分。
#   冻结 > 减速/加速：冻结是硬控（对面整段时间不出手），减速只是拖延。
MECHANIC_BONUS = {
    "freeze": 1.5,
    "slow": 1.2,
    "haste": 1.2,
    "charge": 0.8,
    "hand": 0.8,
    "spawn": 0.5,
    "fatigue_per_second": 0.4,
}


def card_power(card):
    """返回 (每秒价值, 明细 dict)。"""
    cd = card.get("Countdown", 1.0)
    if cd <= 0:
        cd = 1.0

    damage = card.get("Damage", 0) or card.get("Spell", 0) or 0
    multi = card.get("MultiCount", 1) or 1
    shield = card.get("Shield", 0) or 0
    heal = card.get("Heal", 0) or 0
    poison = card.get("Poison", 0) or 0
    expose = card.get("Expose", 0) or 0

    attack_part = damage * multi / cd
    shield_part = shield * SHIELD_WEIGHT / cd
    heal_part = heal * HEAL_WEIGHT / cd
    poison_part = poison * POISON_WEIGHT
    expose_part = expose * EXPOSE_BONUS

    # 机制分
    mech = 0.0
    if card.get("FreezeTime", 0) > 0:
        mech += MECHANIC_BONUS["freeze"]
    if card.get("SlowTime", 0) > 0:
        mech += MECHANIC_BONUS["slow"]
    if card.get("HasteTime", 0) > 0:
        mech += MECHANIC_BONUS["haste"]
    if card.get("ChargeCount", 0) > 0:
        mech += MECHANIC_BONUS["charge"]
    if card.get("HandSelf", 0) != 0 or card.get("HandFoe", 0) != 0:
        mech += MECHANIC_BONUS["hand"]
    if card.get("SpawnCardId", 0) > 0:
        mech += MECHANIC_BONUS["spawn"]
    fatigue = card.get("FatigueShift", 0) or 0
    if fatigue != 0:
        mech += abs(fatigue) * MECHANIC_BONUS["fatigue_per_second"]

    total = attack_part + shield_part + heal_part + poison_part + expose_part + mech
    detail = {
        "atk": round(attack_part, 2),
        "shield": round(shield_part, 2),
        "heal": round(heal_part, 2),
        "poison": round(poison_part, 2),
        "expose": round(expose_part, 2),
        "mech": round(mech, 2),
    }
    return round(total, 2), detail


def main():
    cards = json.load(open(CARDS, encoding="utf-8"))
    rows = []
    for c in cards:
        if c.get("StarterOnly"):
            continue
        power, detail = card_power(c)
        rows.append((power, c, detail))

    rows.sort(key=lambda r: -r[0])

    print("卡池 %d 张（不含起始卡）" % len(rows))
    print("分值分布：最高 %.2f / 中位 %.2f / 最低 %.2f"
          % (rows[0][0], rows[len(rows) // 2][0], rows[-1][0]))
    print()

    if "--family" in sys.argv:
        fam = {}
        for power, c, _ in rows:
            fam.setdefault(c.get("BaseId", c["Id"] % 1000), []).append((c["MinTier"], power))
        for base in sorted(fam)[:12]:
            seq = sorted(fam[base], key=lambda x: ["Copper", "Silver", "Gold", "Diamond"].index(x[0]))
            print(base, " → ".join("%s %.2f" % (t[:2], p) for t, p in seq))
        return 0

    print("%-5s %-14s %-8s %-6s %s" % ("Id", "名称", "阶", "分值", "构成"))
    for power, c, d in rows[:25]:
        parts = " ".join("%s%.2f" % (k, v) for k, v in d.items() if v > 0)
        print("%-5d %-14s %-8s %-6.2f %s"
              % (c["Id"], c["Name"], c.get("MinTier", "")[:6], power, parts))

    print()
    print("最低的 10 张：")
    for power, c, d in rows[-10:]:
        print("  %-5d %-14s %-8s %.2f" % (c["Id"], c["Name"], c.get("MinTier", "")[:6], power))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
