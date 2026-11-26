# -*- coding: utf-8 -*-
"""生成 62 只敌人的 EnemyConfig.json。

名字 / 品阶 / 机制倾向全部来自 Docs/遭遇池与BGM.md 第六节那张表（原著怪物，不新造名字）。
牌组不手写：按「品阶定张数 + 机制倾向定构成」从 278 张卡池里抽，
同品阶的怪强度相近，机制不同 —— 这样三选一里「打谁」是一次真选择，而不是必然选最弱的。

用法：python Tools/gen_enemies.py
"""

import json
import os
import random

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CARDS = os.path.join(ROOT, "Resources", "Config", "CardConfig.json")
OUT = os.path.join(ROOT, "Resources", "Config", "EnemyConfig.json")

# 品阶 → 牌组张数。张数就是难度的第一层：牌多 = 轮转快 = 更难被打断。
DECK_SIZE = {"Bronze": 8, "Silver": 10, "Gold": 12, "Diamond": 14, "Legend": 16}

# 护体卡张数：多了就打不动。实测青铜怪带 2 张护体（布靴 11 + 幼蛊 9）时，
# 玩家 6 伤一刀砍上去全被吃，101 血的怪 25 秒打不完，胜率 0%。
# 护体在敌人手里是「持续减伤」，必须少给，靠血量与攻击卡体现强度。
DECK_GUARD = {"Bronze": 1, "Silver": 2, "Gold": 2, "Diamond": 3, "Legend": 3}

# 品阶 → 图鉴用的基准气血（实际战斗读 EnemyHpConfig，这里只给界面/文档一个数量级）
BASE_HP = {"Bronze": 90, "Silver": 130, "Gold": 180, "Diamond": 240, "Legend": 320}

TITLE = {
    "Bronze": "寻常妖物",
    "Silver": "江湖好手",
    "Gold": "一方凶徒",
    "Diamond": "绝顶高手",
    "Legend": "天下独步",
}

# (编号, 名字, 品阶, 机制偏好标签, 一句话描述) —— 编号即 mXX 的头像键
MONSTERS = [
    (1, "武僧", "Bronze", ["Armor", "Shield"], "护体 + 反击，先站稳再说话。"),
    (2, "蓝刀手", "Bronze", ["Multi"], "刀快，出手就是两下。"),
    (3, "铜鞭手", "Bronze", ["Pierce"], "鞭梢专抽护体的缝。"),
    (4, "江湖棍", "Bronze", ["Pierce", "Multi"], "一根棍扫一片。"),
    (5, "洪衣门弟子", "Bronze", ["Haste"], "身法轻，越打越快。"),
    (6, "白石版精", "Silver", ["Armor", "Shield"], "石头缝里长出来的，硬。"),
    (7, "青石版精", "Silver", ["Control"], "压得人喘不过气。"),
    (8, "冻狸", "Bronze", ["Freeze", "Ice"], "尾巴一扫，手脚就僵了。"),
    (9, "盗贼查协", "Silver", ["Control"], "偷牌、拖时间，占山为王。"),
    (10, "锦八爷", "Silver", ["Heal", "Shield"], "越打越有精神的老鼠精。"),
    (11, "扬枭", "Silver", ["Haste", "Multi"], "天上打转，抓不住。"),
    (12, "虞蛇", "Gold", ["Poison", "Serpent"], "月凉山雾里的蛇，毒得慢。"),
    (13, "百眼蜈蚣", "Silver", ["Poison"], "每一节都带毒。"),
    (14, "绿石版精", "Gold", ["Heal", "Shield"], "会自己修的石像。"),
    (15, "赤石版精", "Gold", ["Fire"], "石身烧红，碰一下就烫。"),
    (16, "唐志达", "Silver", ["Multi"], "一把刀分成三道影。"),
    (17, "江都王", "Gold", ["Multi", "Armor"], "身边总有东西替他挡。"),
    (18, "密姬", "Gold", ["Poison", "Control"], "毒里掺了迷香。"),
    (19, "天鹿", "Gold", ["Pierce"], "角尖，一击很沉。"),
    (20, "魔岩怪", "Legend", ["Armor", "Shield"], "矿洞深处的石魔，护体厚得离谱。"),
    (21, "鬼弟", "Diamond", ["Control"], "专烧人手里的牌。"),
    (22, "鬼姐", "Diamond", ["Heal", "Poison"], "吸你的血补自己的。"),
    (23, "鬼书生", "Gold", ["Control"], "翻书的声音让人心散。"),
    (24, "雷精", "Gold", ["Thunder", "Pierce"], "雷落下来不带犹豫。"),
    (25, "幻画妖精甲", "Diamond", ["Multi"], "分身比本尊还多。"),
    (26, "幻画妖精乙", "Diamond", ["Armor"], "打它像打在画布上。"),
    (27, "幻媚画妖", "Diamond", ["Control", "Freeze"], "看久了就忘了自己在打什么。"),
    (28, "孔麟", "Diamond", ["Fire", "Thunder"], "火与雷一起压下来。"),
    (29, "山猪婆", "Diamond", ["Armor", "Heal"], "胖，慢，打不死。"),
    (30, "食人宴主", "Diamond", ["Poison", "Multi"], "吃掉你的牌，再吃掉你。"),
    (31, "右罗汉", "Diamond", ["Armor", "Shield"], "罗汉阵的一半。"),
    (32, "左罗汉", "Diamond", ["Armor", "Shield"], "罗汉阵的另一半。"),
    (33, "沈齐", "Diamond", ["Multi", "SwordArt"], "剑法连成一条线。"),
    (34, "三面谋神", "Legend", ["Control", "Multi"], "三张脸轮流说话。"),
    (35, "千叶", "Legend", ["Talisman", "Shield"], "符法一层叠一层。"),
    (36, "究极千叶", "Legend", ["Talisman", "Thunder"], "符纸漫天，避无可避。"),
    (37, "侯神君", "Legend", ["Haste", "Multi"], "快得只剩残影。"),
    (38, "啸狼", "Legend", ["Multi", "Haste"], "一声长啸，群狼扑上来。"),
    (39, "枯叶小妖", "Bronze", ["Multi"], "叶子一片片地割。"),
    (40, "蜜蜂", "Bronze", ["Multi", "Poison"], "嗡嗡一片，扎得到处都是。"),
    (41, "老鼠", "Bronze", ["Control"], "偷了东西就跑。"),
    (42, "青蛇", "Bronze", ["Poison", "Serpent"], "咬一口，慢慢烂。"),
    (43, "蝙蝠", "Bronze", ["Heal"], "吸一口血就精神。"),
    (44, "僵尸兵", "Bronze", ["Armor"], "慢，但很难打倒。"),
    (45, "风鬼球", "Silver", ["Haste"], "滚起来抓不住。"),
    (46, "云雾罗汉", "Silver", ["Armor", "Shield"], "云雾罩身，打上去发闷。"),
    (47, "赤蛇", "Silver", ["Poison", "Fire"], "毒里带火。"),
    (48, "狼", "Silver", ["Multi"], "扑上来就是连咬。"),
    (49, "赤食火蟾", "Silver", ["Fire"], "吐出来的气是烫的。"),
    (50, "铁叉牛", "Silver", ["Pierce"], "叉子直直捅过来。"),
    (51, "宫女僵尸", "Gold", ["Poison"], "身上的香是毒。"),
    (52, "河鬼", "Gold", ["Control"], "水一漫，什么都慢了。"),
    (53, "雷鬼球", "Gold", ["Thunder", "Haste"], "带电的球，追着人跑。"),
    (54, "火鬼球", "Gold", ["Fire", "Haste"], "滚过的地方都是焦的。"),
    (55, "五毒蜈蚣", "Gold", ["Poison"], "五种毒混在一起。"),
    (56, "吹雪熊", "Gold", ["Freeze", "Armor"], "一掌下来，寒气先到。"),
    (57, "石贔屭", "Gold", ["Armor", "Shield"], "陵前的石龟，护体极高。"),
    (58, "石狻狔", "Gold", ["Armor", "Pierce"], "石狮扑人，反手就是一爪。"),
    (59, "五毒巨蝎", "Diamond", ["Poison", "Pierce"], "尾钩带毒，壳也硬。"),
    (60, "冰蛟龙", "Diamond", ["Ice", "Pierce"], "一口寒气能把人钉住。"),
    (61, "火蛟龙", "Diamond", ["Fire", "Multi"], "龙息扫过，寸草不留。"),
    (62, "霜婆", "Diamond", ["Freeze", "Control"], "她走过的地方都结霜。"),
]


def load_cards():
    with open(CARDS, encoding="utf-8") as f:
        cards = json.load(f)
    # 配表已按阶展开（1~283 铜 / 1001+ 银 / 2001+ 金 / 3001+ 钻）。
    # 这里只用「铜起步」的条目组牌，最后按怪的品阶整体映射到对应 id 区间 ——
    # 这样同一套骨架在青铜怪手里是铜卡、在钻石怪手里是钻卡，强度跟着品阶走。
    return [c for c in cards if c["Id"] < 1000 and c.get("MinTier") == "Copper"]


MIN_TIER_RANK = {"Copper": 0, "Silver": 1, "Gold": 2, "Diamond": 3}

# 怪的品阶 → 用几阶的卡（Legend 放开到钻）
MONSTER_TIER_RANK = {"Bronze": 0, "Silver": 1, "Gold": 2, "Diamond": 3, "Legend": 3}

# 怪的品阶 ≠ 卡的阶位名：青铜怪用铜卡，传奇怪放开到钻卡
TIER_CAP = {"Bronze": 0, "Silver": 1, "Gold": 2, "Diamond": 3, "Legend": 3}

# 每只怪能用的卡，效率（伤害/蓄势）上限。铜卡池里红瓔刀 5.4 dps、大刀 4.4 dps，
# 和玩家的劈砍 2.1 dps 差两倍多 —— 不设上限的话「低品阶怪」比玩家起手牌强。
TIER_DPS_CAP = {"Bronze": 4.4, "Silver": 5.0, "Gold": 5.2, "Diamond": 6.5, "Legend": 8.0}


def allowed(card, tier):
    """敌人只能用通用卡（别人家的本命招式不能出现在它手里），且起步阶不高于自身品阶。
    Legend 放开到钻起步 —— 传奇怪拿最强的卡才配得上「天下独步」。"""
    if card.get("StarterOnly"):
        return False
    if card.get("Master", 0) != 0:
        return False
    rank = MIN_TIER_RANK.get(card.get("MinTier", "Copper"), 0)
    cap = TIER_CAP[tier]
    if rank > cap:
        return False

    cd = card.get("Countdown", 2.0) or 2.0
    dps = (card.get("Damage", 0) + card.get("Spell", 0)) / cd
    return dps <= TIER_DPS_CAP[tier]


def candidate_pools(cards, tier, allow_heal):
    """按用途分三桶：攻击 / 护体 / 机制（毒、法术、控制）。

    治疗卡默认不给敌人：实测一个白银怪带 2 张治疗，110 血能拖到玩家先死
    （0% 胜率，均时 40 秒）—— 敌人回血 + 护体的组合过于能磨。只有机制倾向就是
    「治疗」的那几只（锦八爷 / 鬼姐 / 山猪婆）才放一张。
    """
    attack, guard, mech = [], [], []
    for c in cards:
        if not allowed(c, tier):
            continue
        tags = c.get("Tags", "")
        if c["Type"] == "Attack":
            attack.append(c)
        elif c["Type"] == "Guard":
            guard.append(c)
        elif c["Type"] == "Heal":
            # 敌人不带任何治疗卡：单挑里治疗等于无限续航，玩家没有对应手段去破
            # （实测白银怪带 1 张孟婆汤就能把 110 血拖成 0% 胜率）。
            continue
        else:
            mech.append(c)
        # 伞这类「攻 + 护」的卡同时进两个桶
        if c["Type"] == "Attack" and "Shield" in tags:
            guard.append(c)
    return attack, guard, mech


def pick(pool, prefer, count, rng, used):
    """优先抽带偏好标签的，抽不满就退回全池。不重复同一张卡。"""
    chosen = []
    preferred = [c for c in pool if any(t in c.get("Tags", "") for t in prefer)]
    for source in (preferred, pool):
        if len(chosen) >= count:
            break
        rng.shuffle(source)
        for c in source:
            if len(chosen) >= count:
                break
            # 同一张卡最多带 2 张：低品阶的池子小，不给重复就凑不满张数
            if used.count(c["Id"]) >= 2:
                continue
            used.append(c["Id"])
            chosen.append(c)
    return chosen


# —— 牌组模板 ——
# 用固定骨架而不是纯随机抽：62 只怪如果各抽各的，强度会飘到天上或地上
# （实测白银怪抽到 6 张「攻+护」双效伞能打出 0% 胜率，换个种子又变 100%）。
# 骨架定强度，机制卡定个性 —— 同品阶的怪打起来快慢差不多，但手感不同。
BASE_DECK = {
    "Bronze":  [143, 143, 143, 144, 144, 5, 5],        # 7 张 + 1 张机制
    "Silver":  [143, 143, 144, 144, 5, 9, 9, 2],          # 8 张 + 2 张机制
    "Gold":    [143, 143, 144, 144, 5, 5, 9, 9, 1, 150],  # 10 张 + 2 张机制
    "Diamond": [143, 143, 144, 144, 5, 5, 2, 2, 9, 1, 150, 147],  # 12 + 2
    "Legend":  [143, 143, 144, 144, 5, 5, 2, 2, 9, 1, 150, 147, 149, 148],  # 14 + 2
}


def build_deck(tier, prefer, cards, rng):
    size = DECK_SIZE[tier]
    base = list(BASE_DECK[tier])
    mech_slots = size - len(base)

    attack_pool, guard_pool, mech_pool = candidate_pools(cards, tier, False)
    # 机制位只放毒 / 法术 / 控制，且毒最多 2 张
    poison_pool = [c for c in mech_pool if c["Type"] == "Poison"]
    other_mech = [c for c in mech_pool if c["Type"] != "Poison"]
    max_poison = 2 if "Poison" in prefer else 1

    used = list(base)
    extra = [c["Id"] for c in pick(poison_pool, prefer, min(max_poison, mech_slots), rng, used)]
    rest = mech_slots - len(extra)
    if rest > 0:
        extra += [c["Id"] for c in pick(other_mech, prefer, rest, rng, used)]

    # 敌人暂时**统一用铜卡**（rank 恒 0）：展开后试过「怪按品阶用对应阶的卡」，
    # 实测金怪（24 伤的大刀·金）在 D7 把玩家 20 秒打死 —— 玩家的构筑速度（买 105 两一张金卡）
    # 追不上敌人品阶的爬升。等玩家侧的经济或成长曲线调好，再把 MONSTER_TIER_RANK 接回来。
    rank = 0
    return [cid + 1000 * rank for cid in base + extra]


def main():
    cards = load_cards()

    rows = []
    for mid, name, tier, prefer, desc in MONSTERS:
        rng = random.Random(20261125 + mid)   # 固定种子：同编号每次生成的牌组一致
        deck = build_deck(tier, prefer, cards, rng)
        rows.append({
            "Id": mid,
            "Name": name,
            "Title": TITLE[tier] + " · " + desc,
            "Portrait": "m%02d" % mid,
            "Hp": BASE_HP[tier],
            "Deck": deck,
            "Tier": tier,
        })

    rows.sort(key=lambda r: r["Id"])
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(rows, f, ensure_ascii=False, indent=2)

    by_tier = {}
    for r in rows:
        by_tier.setdefault(r["Tier"], 0)
        by_tier[r["Tier"]] += 1
    print("已生成 %d 只：%s" % (len(rows), "、".join("%s %d" % (k, v) for k, v in by_tier.items())))


if __name__ == "__main__":
    main()
