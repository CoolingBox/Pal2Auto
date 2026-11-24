shader_type canvas_item;

// 卡面 v3 —— 老游戏质感
//
// v2 用了柔光外投影 + 渐变斜面 + 细噪 + 内框线，路子是"现代卡牌游戏"。
// v3 反过来：**硬边、硬倒角、平色**，层与层之间只有 1 像素过渡 —— 像 2002 年的界面贴图。
//   外 1px 暗线 → 1px 斜面（受光侧亮 / 背光侧暗）→ 平色牌面 → 内 1px 主色细线
//   → 底部凹槽（槽沿压暗，不压亮）
// 进度条也画在这里，省掉两个 ColorRect。
//
//   ⚠ 颜色空间：Godot 4 的 2D 是线性空间出图的，着色器里写死的 vec3(0.09) 是**线性值**，
//     屏幕上会变成 0.33 的浅灰。所以底色一律走 source_color 的 uniform（按 sRGB 填）。

uniform vec2 rect = vec2(132.0, 160.0);              // 卡面像素尺寸
uniform vec4 accent : source_color = vec4(0.720, 0.270, 0.230, 1.0);  // 主色（类型/元素色）
uniform vec4 bar_color : source_color = vec4(0.620, 0.470, 0.240, 1.0);
uniform vec3 face_color : source_color = vec3(0.086, 0.076, 0.064);   // 暗纸
uniform vec3 edge_dark : source_color = vec3(0.040, 0.036, 0.030);    // 最外 1px 暗线
uniform vec3 groove_dark : source_color = vec3(0.036, 0.032, 0.027);  // 凹槽底
uniform vec3 groove_rim : source_color = vec3(0.130, 0.112, 0.086);   // 凹槽上沿（压暗，不发光）

uniform float corner = 2.0;      // 圆角半径（px）：老界面基本是直角
uniform float bar = 0.0;         // 蓄势进度 0~1
uniform float bar_h = 7.0;       // 凹槽高度（px）
uniform float bar_inset = 12.0;  // 凹槽左右内缩（px）
uniform float bar_bottom = 3.0;  // 凹槽距底边（px）
uniform float slot_empty = 0.0;  // 1 = 空槽（更暗、内框虚线）

float rrect(vec2 p, vec2 b, float r) {
	vec2 q = abs(p) - b + vec2(r);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - r;
}

float hash21(vec2 p) {
	vec2 q = fract(p * vec2(123.34, 456.21));
	q += dot(q, q + 45.32);
	return fract(q.x * q.y);
}

void fragment() {
	vec2 px = UV * rect;
	vec2 half_size = rect * 0.5;
	float d = rrect(px - half_size, half_size, corner);
	float t = -d;   // 距边框内缘的像素距离，<0 在外面

	vec3 c;
	float a = 1.0;

	if (t < 0.0) {
		// 外投影：1px 硬边，不做柔光晕
		float k = clamp(1.0 + t / 2.5, 0.0, 1.0);
		c = vec3(0.0);
		a = k * 0.55;
	} else {
		vec2 g = vec2(dFdx(d), dFdy(d));
		vec2 n = normalize(g + vec2(1e-5));
		float lit = clamp(0.5 + 0.5 * dot(n, normalize(vec2(-0.70, -1.0))), 0.0, 1.0);

		// —— 面：平色，只混 6% 主色；一点极淡的噪当纸纹 ——
		vec3 face = face_color;
		if (slot_empty < 0.5) {
			face = mix(face_color, accent.rgb, 0.06);
		}
		face *= 0.98 + 0.025 * hash21(floor(px * 0.55));
		if (slot_empty > 0.5) {
			face *= 0.78;   // 空槽压暗一档，但别压成黑洞
		}
		c = face;

		// —— 1px 主色细线：暗底上只要一条线，不要 2px 亮框（那就是霓虹灯）——
		if (t < 2.2) {
			if (lit > 0.5) {
				c = accent.rgb * 0.58;
			} else {
				c = accent.rgb * 0.30;
			}
			if (slot_empty > 0.5) {
				c = edge_dark * 1.6;
			}
		}
		// —— 最外 1px 暗线：收轮廓 ——
		if (t < 1.0) {
			c = edge_dark;
		}
		// —— 内 1px 线：实心牌是主色细线，空槽是虚线 ——
		if (abs(t - 4.2) < 0.5) {
			if (slot_empty > 0.5) {
				float dash = step(0.45, fract((px.x + px.y) * 0.14));
				if (dash > 0.5) {
					c = mix(face, vec3(0.21, 0.19, 0.16), 0.8);
				}
			} else if (lit > 0.5) {
				c = mix(face, accent.rgb * 0.72, 0.5);
			} else {
				c = mix(face, accent.rgb * 0.36, 0.5);
			}
		}

		// —— 底部凹槽 + 进度条 ——
		float groove_top = rect.y - bar_bottom - bar_h;
		float left = bar_inset;
		float right = rect.x - bar_inset;
		if (px.y >= groove_top - 1.0 && px.y <= rect.y - bar_bottom + 0.5 && px.x >= left && px.x <= right) {
			// 槽：上沿再压暗一档，做出"挖进去"的感觉
			c = groove_dark;
			if (px.y < groove_top + 1.0) {
				c = groove_rim;
			}
			if (px.x - left <= bar * (right - left)) {
				// 填充：上亮下暗只有两档，不做渐变
				if (px.y < groove_top + bar_h * 0.5) {
					c = bar_color.rgb * 0.86;
				} else {
					c = bar_color.rgb * 0.60;
				}
			}
		}
	}

	COLOR = vec4(c, a);
}
