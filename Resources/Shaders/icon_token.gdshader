shader_type canvas_item;

// 物品图标统一裁成"圆牌"。
// 解包素材本身是一枚圆形玉牌，但画布是方的、四角还留着底纸，直接贴到卡面上
// 就是一个米色硬边方块，像贴纸。这里裁圆 + 边缘柔化 + 外圈压暗，才像一枚嵌在卡上的牌。
//
// v3：整体压暗一档。米色玉牌原画很亮，贴着深色卡面看，亮的那几块就是"闪"的来源；
//     老游戏的图标是"印"在牌子上的，不是打光的贴纸。

uniform float feather = 0.05;  // 边缘柔化宽度（占半径）
uniform float ring = 0.16;     // 外圈压暗宽度（占半径）
uniform float shade = 0.40;    // 外圈压暗强度
uniform float dim = 0.74;      // 整体压暗（米色玉牌原画太亮，是卡面上最闪的一块）

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	float r = length(UV - vec2(0.5)) * 2.0;
	float a = 1.0 - smoothstep(1.0 - feather, 1.0, r);
	float v = smoothstep(1.0 - ring, 1.0, r);
	COLOR = vec4(t.rgb * dim * (1.0 - shade * v), t.a * a);
}
