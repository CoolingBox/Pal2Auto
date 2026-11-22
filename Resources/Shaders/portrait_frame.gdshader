shader_type canvas_item;

// 角色头像框（参考炉石 / 大巴扎的牌框做法）：
//   1. 以"高度"为单位做距离场，边框宽度在横竖两个方向一样粗（按 UV 算的话宽框左右边会变粗）
//   2. 外框用距离场的梯度当法线做斜上方打光 —— 上边亮、下边暗，金属才有厚度
//   3. 外缘压暗 + 内圈一道亮金细线，把"框"和"画"分开
//   4. 画面靠近框的位置压一层内阴影，画就不会像贴纸一样浮在框上
//   5. 顶边正中一颗宝石扣，四角一颗铆钉（带暗环，不像贴片）
//   6. 框外一层柔和投影，让头像从背景里浮起来

uniform vec2 uv_scale = vec2(1.0, 1.0);
uniform float aspect = 1.6;

uniform float cut : hint_range(0.05, 0.45) = 0.30;   // 切角大小（占高度比例）
uniform float rim : hint_range(0.02, 0.25) = 0.075;   // 外框厚度
uniform float lip : hint_range(0.0, 0.06) = 0.016;    // 内圈亮线宽度
uniform float inner : hint_range(0.0, 0.20) = 0.060;  // 内阴影深度
uniform float soft : hint_range(0.0, 0.20) = 0.070;   // 外投影范围

uniform vec4 frame_color : source_color = vec4(0.780, 0.620, 0.330, 1.0);
uniform vec4 gem_color : source_color = vec4(0.560, 0.760, 0.880, 1.0);
uniform float gem : hint_range(0.0, 1.0) = 1.0;

// p: 以高度的一半为 1 的坐标；返回切角矩形的距离场（<0 在内部）
float plate_d(vec2 p, float half_w, float c) {
	float box = max(abs(p.x) - half_w, abs(p.y) - 1.0);
	float chamfer = (abs(p.x) + abs(p.y)) - (half_w + 1.0 - c);
	return max(box, chamfer);
}

void fragment() {
	vec2 uv = clamp((UV - vec2(0.5)) * uv_scale + vec2(0.5), vec2(0.0), vec2(1.0));
	vec4 art = texture(TEXTURE, uv);

	// 归一化坐标：y 的上下边是 ±1，x 按比例展开
	float half_w = aspect;
	vec2 p = vec2((UV.x - 0.5) * aspect, UV.y - 0.5) * 2.0;

	// 参数都以"高度"为单位，×2 换成归一化单位
	float c = cut * 2.0;
	float r = rim * 2.0;
	float l = lip * 2.0;
	float sh = inner * 2.0;
	float sf = soft * 2.0;

	float d = plate_d(p, half_w, c);
	vec4 col;

	if (d > 0.0) {
		// 外投影：越远越淡
		float a = clamp(1.0 - d / max(sf, 1e-4), 0.0, 1.0);
		col = vec4(0.0, 0.0, 0.0, a * a * 0.55);
	} else if (d > -r) {
		// 金属外框：用距离场梯度当法线，光从左上来
		vec2 g = vec2(dFdx(d), dFdy(d));
		vec2 n = normalize(g + vec2(1e-5));
		float lambert = dot(n, normalize(vec2(-0.75, -1.0)));
		float lit = clamp(0.5 + 0.5 * lambert, 0.0, 1.0);

		vec3 metal = mix(frame_color.rgb * 0.55, frame_color.rgb * 1.18, lit);

		// 外缘压暗收轮廓，内缘再亮一点做倒角反光
		float edge = smoothstep(0.0, 0.10, -d / max(r, 1e-4));
		metal *= mix(0.45, 1.0, edge);
		float bevel = smoothstep(0.55, 0.95, -d / max(r, 1e-4));
		metal += vec3(0.10) * bevel;

		col = vec4(metal, 1.0);
	} else if (d > -r - l) {
		// 内圈亮金细线：把框和画切开
		col = vec4(mix(vec3(1.0, 0.97, 0.88), frame_color.rgb, 0.30), 1.0);
	} else {
		// 画面 + 内阴影
		float depth = clamp((-d - r - l) / max(sh, 1e-4), 0.0, 1.0);
		col = vec4(art.rgb * mix(0.42, 1.0, depth), art.a);
	}

	// 四角铆钉：小菱形 + 暗环
	vec2 cq = abs(p) - vec2(half_w - c * 0.5, 1.0 - c * 0.5);
	float diamond = abs(cq.x) + abs(cq.y);
	if (diamond < 0.058) {
		col = vec4(mix(frame_color.rgb, vec3(1.0, 0.95, 0.80), 0.55), 1.0);
	} else if (diamond < 0.084) {
		col = vec4(frame_color.rgb * 0.30, 1.0);
	}

	// 顶边正中的宝石扣
	if (gem > 0.5) {
		vec2 gp = p - vec2(0.0, -1.0 + r * 0.5);
		float gd = abs(gp.x) + abs(gp.y) * 1.15;
		if (gd < 0.088) {
			float core = clamp(1.0 - gd / 0.088, 0.0, 1.0);
			col = vec4(mix(gem_color.rgb * 0.55, gem_color.rgb * 1.35, core), 1.0);
		} else if (gd < 0.125) {
			col = vec4(frame_color.rgb * 0.25, 1.0);
		}
	}

	COLOR = col;
}
