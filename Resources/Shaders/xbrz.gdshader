// xBRZ 风格放大（边缘感知 + 对角混合）
//
// 用途：SubViewport 以 960x540 渲染，本着色器把画面放大到窗口尺寸。
// 与 libretro xBRZ 的关系：同源思路（先按颜色距离判定边线，再让像素块的对角
// 按权重混合），但**不是逐位复刻**的 xBRZ —— 真正的 xBRZ 是 4 级多趟算法。
// 这里是单趟实现：每个源像素取 3x3 邻域，四个象限各算一次"该往哪个对角靠"，
// 再按小数位置双线性合成。整数倍（2x）时结果就是干净的 2x 像素风；
// 非整数倍时小数位置会把象限结果平滑接起来，不会出现锯齿跳变。
//
// 关闭（enabled=0）时退化为直接取样，即"原始解包资源"的硬像素样子。

shader_type canvas_item;
render_mode unshaded;

uniform vec2 source_size = vec2(960.0, 540.0);
uniform float enabled = 1.0;
// 颜色差阈值：低于它视为同一块颜色（同一条边内）
uniform float edge_threshold : hint_range(0.01, 0.5) = 0.10;
// 对角混合强度：0 = 只做像素块判断（Scale2x/EPX），1 = 完全取对角平均色
uniform float blend_amount : hint_range(0.0, 1.0) = 0.55;

// Godot 的着色语言不允许把 TEXTURE 当参数传给函数，只能就地取样，故用宏
#define TEX(off) texture(TEXTURE, (base + (off) + vec2(0.5)) / source_size)

float color_dist(vec4 a, vec4 b) {
	vec3 d = abs(a.rgb - b.rgb);
	return d.r * 0.299 + d.g * 0.587 + d.b * 0.114;
}

bool same_color(vec4 a, vec4 b) {
	return color_dist(a, b) < edge_threshold;
}

// 一个象限的结果：两条正交邻边同色、且与斜对角不同色 → 有斜线穿过，把角磨圆
vec4 quadrant(vec4 p, vec4 ortho_a, vec4 ortho_b, vec4 diag) {
	if (same_color(ortho_a, ortho_b) && !same_color(ortho_a, diag) && !same_color(p, ortho_a)) {
		vec4 corner = mix(ortho_a, ortho_b, 0.5);
		return mix(p, corner, blend_amount);
	}
	return p;
}

void fragment() {
	if (enabled < 0.5) {
		COLOR = texture(TEXTURE, UV);
	} else {
		// 换到"以像素中心为整数"的坐标
		vec2 sp = UV * source_size - vec2(0.5);
		vec2 base = floor(sp + vec2(0.5));

		vec4 p = TEX(vec2(0.0, 0.0));
		vec4 n = TEX(vec2(0.0, -1.0));
		vec4 s = TEX(vec2(0.0, 1.0));
		vec4 w = TEX(vec2(-1.0, 0.0));
		vec4 e = TEX(vec2(1.0, 0.0));
		vec4 nw = TEX(vec2(-1.0, -1.0));
		vec4 ne = TEX(vec2(1.0, -1.0));
		vec4 sw = TEX(vec2(-1.0, 1.0));
		vec4 se = TEX(vec2(1.0, 1.0));

		vec4 q_tl = quadrant(p, n, w, nw);
		vec4 q_tr = quadrant(p, n, e, ne);
		vec4 q_bl = quadrant(p, w, s, sw);
		vec4 q_br = quadrant(p, e, s, se);

		// 象限中心位于源像素的 0.25 / 0.75 处，据此做双线性权重
		vec2 f = clamp(sp + vec2(0.5) - base, vec2(0.0), vec2(1.0));
		vec2 t = clamp((f - vec2(0.25)) / vec2(0.5), vec2(0.0), vec2(1.0));

		vec4 top = mix(q_tl, q_tr, t.x);
		vec4 bottom = mix(q_bl, q_br, t.x);
		COLOR = mix(top, bottom, t.y);
	}
}
