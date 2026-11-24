# -*- coding: utf-8 -*-
"""
宿敌 / 遭遇敌人头像导入：从《仙剑2》战斗序列帧生成头像。

来源：`D:\\Res\\仙剑2解包\\Godot素材\\Images\\角色动画` 下 `23xx_` 编号的条目
（2301 盗贼查协、2310 虞蛇、2311 百眼蜈蚣…这些是**敌方战斗精灵**，
带 `_光`/`_影` 后缀的多为法术特效与影子，不能当头像）。

规则（与 `Docs/遭遇池与BGM.md` 一致）：
  1. 取 `-备`/`备战`/`攻` 这类动作帧（没有则取第 2 帧）；
  2. 按不透明内容包围盒裁切，去掉四周空白；
  3. 等比缩放，高度占 92%，水平居中，底部留 6%；
  4. 输出 320×250 RGBA，键名 `m01..`，**按品阶顺序编号**：
     青铜 m01–m08 / 白银 m09–m16 / 黄金 m17–m23 / 钻石 m24–m33 / 传奇 m34–m37。

用法：
    python Tools/import_monster_portrait.py
"""
import glob
import os
import sys

from PIL import Image

SRC = r"D:\Res\仙剑2解包\Godot素材\Images\角色动画"
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "Resources", "Image", "Portrait")
W, H = 320, 250
CONTENT_H = 0.86
BOTTOM_PAD = 0.06

# (素材目录, 显示名, 品阶)
ROSTER = [
    # —— 青铜：杭州一带的杂兵 ——
    ("0776_2335_武僧", "武僧", "青铜"),
    ("0781_2337_蓝刀手_攻击", "蓝刀手", "青铜"),
    ("0778_2336_铜鞭手_攻击", "铜鞭手", "青铜"),
    ("0782_2338_江湖棍", "江湖棍", "青铜"),
    ("0998_2345_洪衣门弟子", "洪衣门弟子", "青铜"),
    ("0669_2304_白石版精", "白石版精", "青铜"),
    ("0667_2303_青石版精", "青石版精", "青铜"),
    ("0999_2344_冻狸", "冻狸", "青铜"),
    # —— 白银：天师陵到江宁府 ——
    ("0384_2301_盗贼查协", "盗贼查协", "白银"),
    ("0811_2306_锦八爷", "锦八爷", "白银"),
    ("0812_2307_扬枭", "扬枭", "白银"),
    ("0816_2310_虞蛇", "虞蛇", "白银", 1),
    ("1598_2311_百眼蜈蚣", "百眼蜈蚣", "白银"),
    ("0668_2302_绿石版精", "绿石版精", "白银"),
    ("0670_2305_赤石版精", "赤石版精", "白银"),
    ("1324_2343_唐志达_新", "唐志达", "白银"),
    # —— 黄金：月凉山到江都 ——
    ("0814_2308_江都王", "江都王", "黄金"),
    ("0815_2309_密姬", "密姬", "黄金"),
    ("1560_2312_天鹿_备", "天鹿", "黄金"),
    ("0817_2313_魔岩怪", "魔岩怪", "黄金"),
    ("1569_2315_鬼弟", "鬼弟", "黄金"),
    ("1570_2316_鬼姐_备", "鬼姐", "黄金"),
    ("1576_2317_鬼书生_攻1", "鬼书生", "黄金"),
    # —— 钻石：云梦到开封 ——
    ("1579_2318_雷精_攻", "雷精", "钻石"),
    ("0741_2319_幻画妖精A", "幻画妖精甲", "钻石"),
    ("0742_2320_幻画妖精B", "幻画妖精乙", "钻石"),
    ("0831_2322_幻媚画妖_备", "幻媚画妖", "钻石"),
    ("1592_2323_孔麟_备", "孔麟", "钻石"),
    ("0827_2324_山猪婆", "山猪婆", "钻石"),
    ("0819_2326_食人宴主", "食人宴主", "钻石"),
    ("0820_2328_右罗汉", "右罗汉", "钻石"),
    ("0822_2329_左罗汉", "左罗汉", "钻石"),
    ("0983_2334_沈齐", "沈齐", "钻石"),
    # —— 传奇：摩诃禅寺之后 ——
    ("0824_2331_三面谋神", "三面谋神", "传奇"),
    ("1585_2332_千叶_攻", "千叶", "传奇", 1),
    ("0002_2333_究极千叶", "究极千叶", "传奇"),
    ("0829_2325_侯神君", "侯神君", "传奇"),
    ("1341_2330_啸狼_新备战", "啸狼", "传奇"),
    # —— 扩充批次：21xx 小怪补齐前中期，22xx 补后期 ——
    ("0361_2102_枯叶小妖", "枯叶小妖", "青铜"),
    ("0373_2111_蜜蜂", "蜜蜂", "青铜"),
    ("0663_2112_老鼠", "老鼠", "青铜"),
    ("0374_2116_青蛇(备)", "青蛇", "青铜"),
    ("0539_2120_蝙蝠", "蝙蝠", "青铜"),
    ("0544_2128_僵屍兵a", "僵尸兵", "青铜"),
    ("0363_2104_风鬼球", "风鬼球", "白银"),
    ("0366_2108_云雾罗汉(备)", "云雾罗汉", "白银"),
    ("0377_2117_赤蛇(备)", "赤蛇", "白银"),
    ("0381_2119_狼", "狼", "白银"),
    ("0541_2124_赤食火蟾", "赤食火蟾", "白银"),
    ("0722_2155_铁叉牛", "铁叉牛", "白银"),
    ("0547_2132_宫女僵屍", "宫女僵尸", "黄金"),
    ("0712_2133_河鬼", "河鬼", "黄金"),
    ("0555_2136_雷鬼球", "雷鬼球", "黄金"),
    ("0558_2144_火鬼球", "火鬼球", "黄金"),
    ("0560_2150_五毒蜈蚣", "五毒蜈蚣", "黄金"),
    ("0562_2152_吹雪熊", "吹雪熊", "黄金"),
    ("0729_2172_石贔屭", "石贔屭", "黄金"),
    ("0731_2175_石狻狔", "石狻狔", "黄金"),
    ("0578_2174_五毒巨蝎", "五毒巨蝎", "钻石"),
    ("0585_2195_冰蛟龙", "冰蛟龙", "钻石"),
    ("0586_2196_火蛟龙", "火蛟龙", "钻石"),
    ("0796_2203_霜婆", "霜婆", "钻石"),
]


def frame_score(im):
    """给一帧打分：不透明面积 × 色彩丰富度。用来剔除阴影帧、白光帧、空帧。"""
    bbox = im.getbbox()
    if bbox is None:
        return 0, None

    alpha = im.split()[3]
    opaque = alpha.point(lambda v: 255 if v > 32 else 0)
    count = sum(1 for v in opaque.getdata() if v)
    if count < 40:
        return 0, bbox

    small = im.resize((64, 64))
    rgb = small.convert("RGB")
    vals = [(r + g + b) / 3 for r, g, b in rgb.getdata()]
    lum = sum(vals) / len(vals)
    var = sum((v - lum) ** 2 for v in vals) / len(vals)
    std = var ** 0.5

    # 阴影（整体很暗且没层次）、白光（整体很亮且没层次）都丢掉
    if lum < 32 and std < 14:
        return 0, bbox
    if lum > 224 and std < 14:
        return 0, bbox

    area = (bbox[2] - bbox[0]) * (bbox[3] - bbox[1])
    return area * (1.0 + std / 64.0), bbox


def build(folder, fixed=None):
    files = sorted(glob.glob(os.path.join(SRC, folder, "*.png")))
    if not files:
        return None, 0, 0.0

    if fixed is not None and 0 <= fixed < len(files):
        im = Image.open(files[fixed]).convert("RGBA")
        bbox = im.getbbox()
        if bbox is not None:
            return _fit(im, bbox), len(files), 0.0
        return None, len(files), 0.0

    best = None
    best_score = 0.0
    for f in files:
        im = Image.open(f).convert("RGBA")
        score, _ = frame_score(im)
        if score > best_score:
            best_score = score
            best = im
    if best is None:
        # 全被规则筛掉时退回第 1 帧
        best = Image.open(files[0]).convert("RGBA")

    bbox = best.getbbox()
    if bbox is None:
        return None, len(files), 0.0

    return _fit(best, bbox), len(files), 0.0


def _fit(frame, bbox):
    crop = frame.crop(bbox)
    cw, ch = crop.size
    scale = min(W * 0.96 / cw, H * CONTENT_H / ch)
    nw, nh = max(1, int(cw * scale)), max(1, int(ch * scale))

    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    x = (W - nw) // 2
    y = H - int(H * BOTTOM_PAD) - nh
    canvas.alpha_composite(crop.resize((nw, nh), Image.LANCZOS), (x, max(0, y)))
    return canvas


def main():
    if not os.path.isdir(SRC):
        print("素材目录不存在：" + SRC)
        return 1
    os.makedirs(OUT, exist_ok=True)

    # 旧文件清掉，避免改编号后留下孤儿
    for old in glob.glob(os.path.join(OUT, "m*.png")):
        os.remove(old)

    tier_now = ""
    index = 0
    skipped = []
    for entry in ROSTER:
        folder, name, tier = entry[0], entry[1], entry[2]
        fixed = entry[3] if len(entry) > 3 else None
        canvas, count, scale = build(folder, fixed)
        if canvas is None:
            skipped.append(name)
            continue
        index += 1
        key = "m%02d" % index
        canvas.save(os.path.join(OUT, key + ".png"))
        if tier != tier_now:
            print("  —— %s ——" % tier)
            tier_now = tier
        print("  %-4s %-8s 帧数 %2d  放大 %.2f×" % (key, name, count, scale))

    print("完成：%d 张；跳过 %d 个（无可用帧：%s）" %
          (index, len(skipped), "、".join(skipped) if skipped else "无"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
