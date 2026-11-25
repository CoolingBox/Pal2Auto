# -*- coding: utf-8 -*-
"""把所有卡的冷却统一成整秒（3~8 为主，2 秒极快，10 秒绝学）。

规则：
1. 冷却映射到整数秒，**伤害 / 法术 / 护体 / 治疗 / 毒 按同比例缩放**，dps 不变 ——
   拉长冷却只是让每次出手更重，不是削弱。
2. 带 Ultimate 的绝学单独到 10 秒，并额外 +10%（绝学该比同 dps 的普通卡更值）。
3. 机制卡（加速/减速/冰冻/充能/多重/破绽/改上场数）的秒数与非数值字段不动。
4. `min 1` 兜底，避免 1 伤 × 1.5 取整后变 0。

用法：python Tools/unify_countdown.py [--dry]
"""

import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "Resources", "Config", "CardConfig.json")

# 旧冷却（0.2 的倍数）→ 新整数秒
BUCKETS = [
    (1.6, 2),   # 1.2 ~ 1.6 → 2
    (2.4, 3),   # 1.8 ~ 2.4 → 3
    (3.2, 4),   # 2.6 ~ 3.2 → 4
    (4.0, 5),   # 3.4 ~ 4.0 → 5
    (4.8, 6),   # 4.2 ~ 4.8 → 6
    (5.6, 7),   # 5.0 ~ 5.6 → 7
    (99.0, 8),  # 5.8 ~     → 8
]

ULTIMATE_CD = 10
ULTIMATE_BONUS = 1.1

SCALE_FIELDS = ("Damage", "Spell", "Shield", "Heal", "Poison")


def target_cd(old):
    for limit, cd in BUCKETS:
        if old <= limit + 1e-6:
            return cd
    return 8


def main():
    dry = "--dry" in sys.argv
    cards = json.load(open(PATH, encoding="utf-8"))

    changed = 0
    for c in cards:
        old = c.get("Countdown", 2.0)
        is_ult = "Ultimate" in c.get("Tags", "")
        new = ULTIMATE_CD if is_ult else target_cd(old)

        ratio = new / old if old > 0 else 1.0
        if is_ult:
            ratio *= ULTIMATE_BONUS

        for field in SCALE_FIELDS:
            if field in c and c[field]:
                scaled = int(round(c[field] * ratio))
                c[field] = max(1, scaled)

        c["Countdown"] = float(new)
        changed += 1

    if dry:
        for c in cards[:12]:
            print(c["Id"], c["Name"], c["Countdown"], {f: c.get(f) for f in SCALE_FIELDS if c.get(f)})
        print("（dry run，未写入）共 %d 张" % changed)
        return 0

    with open(PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cards, f, ensure_ascii=False, indent=2)
    print("已统一冷却：%d 张" % changed)
    return 0


if __name__ == "__main__":
    sys.exit(main())
