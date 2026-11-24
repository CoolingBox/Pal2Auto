shader_type canvas_item;

// 后期抗锯齿（屏幕空间）—— 照 Unity URP 的两档后处理 AA 搬过来
//
// 对应关系：
//   Unity Camera → Rendering → Anti-aliasing → FXAA   → mode 1
//   Unity Camera → Rendering → Anti-aliasing → SMAA   → mode 2
//   Unity URP Asset → Quality → Anti Aliasing (MSAA)  → 不在本文件，走 Viewport.Msaa2D
//
// 【FXAA（mode 1）】
//   亮度边缘检测 → 二阶差分定边缘走向 → 沿法线做 5 点加权平均。
//   这是 FXAA 的核心结构（Lottes 原版还多一段长距离步进搜索，对 2D 直边收益很小，省掉）。
//   代价：11px 的字和 1px 内框线会被抹开，文字最吃亏。
//
// 【SMAA（mode 2）】
//   边缘检测用「亮度 + 彩度」，混合只取 2 个点、按亚像素覆盖率给权重 —— 比 FXAA 锐。
//   注意：**这是简化版**，没有 area/search 两张 LUT（256x256 + 160x560），
//   也没有多步模式搜索，所以斜边修得不如原生 SMAA 彻底。要真 SMAA 得先生成那两张 LUT。
//
// 【为什么屏幕空间 AA 在这套 UI 上先天弱势】
//   我们的边是着色器里用距离场画出来的（切角、宝石、铆片），本来就已经做了 1 像素解析过渡；
//   再走一遍屏幕空间 AA 等于对"已经平滑过的边"二次模糊。真正需要它的是位图立绘的软边。
//
// 【实现约束】
//   TEXTURE / TEXTURE_PIXEL_SIZE 只在 fragment() 里可见，带不进普通函数，
//   所以取样全部写在 fragment 里，只有纯数学（亮度换算）抽成函数。

uniform int mode = 0;
uniform float edge_threshold_min : hint_range(0.0, 0.5) = 0.030;
uniform float edge_threshold : hint_range(0.0, 1.0) = 0.160;
uniform float smaa_threshold : hint_range(0.0, 1.0) = 0.080;

float luma_of(vec3 c)
{
	return dot(c, vec3(0.299, 0.587, 0.114));
}

void fragment()
{
	vec2 uv = UV;
	vec2 px = TEXTURE_PIXEL_SIZE;

	// —— 关：原样输出 ——
	if (mode == 0)
	{
		COLOR = texture(TEXTURE, uv);
	}
	// —— FXAA ——
	else if (mode == 1)
	{
		vec3 m = texture(TEXTURE, uv).rgb;
		float l_m = luma_of(m);

		vec3 c_n = texture(TEXTURE, uv + vec2(0.0, -px.y)).rgb;
		vec3 c_s = texture(TEXTURE, uv + vec2(0.0, px.y)).rgb;
		vec3 c_w = texture(TEXTURE, uv + vec2(-px.x, 0.0)).rgb;
		vec3 c_e = texture(TEXTURE, uv + vec2(px.x, 0.0)).rgb;
		vec3 c_nw = texture(TEXTURE, uv + vec2(-px.x, -px.y)).rgb;
		vec3 c_ne = texture(TEXTURE, uv + vec2(px.x, -px.y)).rgb;
		vec3 c_sw = texture(TEXTURE, uv + vec2(-px.x, px.y)).rgb;
		vec3 c_se = texture(TEXTURE, uv + vec2(px.x, px.y)).rgb;

		float l_n = luma_of(c_n);
		float l_s = luma_of(c_s);
		float l_w = luma_of(c_w);
		float l_e = luma_of(c_e);
		float l_nw = luma_of(c_nw);
		float l_ne = luma_of(c_ne);
		float l_sw = luma_of(c_sw);
		float l_se = luma_of(c_se);

		float l_min = min(l_m, min(min(l_n, l_s), min(l_w, l_e)));
		float l_max = max(l_m, max(max(l_n, l_s), max(l_w, l_e)));

		// 对比度太低就当作平坦区原样返回 —— 否则整片暗背景都会被动一遍，画面发灰
		if (l_max - l_min < max(edge_threshold_min, l_max * edge_threshold))
		{
			COLOR = vec4(m, 1.0);
		}
		else
		{
			// 二阶差分：竖边在上下方向有响应，横边在左右方向有响应
			float edge_v = abs(0.25 * l_nw - 0.5 * l_n + 0.25 * l_ne) +
			               abs(0.25 * l_sw - 0.5 * l_s + 0.25 * l_se);
			float edge_h = abs(0.25 * l_nw - 0.5 * l_w + 0.25 * l_sw) +
			               abs(0.25 * l_ne - 0.5 * l_e + 0.25 * l_se);

			vec2 off = (edge_v < edge_h) ? vec2(0.0, px.y) : vec2(px.x, 0.0);

			// 沿法线 5 点加权：中心 0.50，±0.5 各 0.15，±1.5 各 0.10
			vec3 n1 = texture(TEXTURE, uv + off * 0.5).rgb;
			vec3 n2 = texture(TEXTURE, uv - off * 0.5).rgb;
			vec3 n3 = texture(TEXTURE, uv + off * 1.5).rgb;
			vec3 n4 = texture(TEXTURE, uv - off * 1.5).rgb;

			COLOR = vec4(m * 0.50 + (n1 + n2) * 0.15 + (n3 + n4) * 0.10, 1.0);
		}
	}
	// —— SMAA（简化：无 LUT）——
	else
	{
		vec3 m = texture(TEXTURE, uv).rgb;
		float l_m = luma_of(m);

		vec3 c_n = texture(TEXTURE, uv + vec2(0.0, -px.y)).rgb;
		vec3 c_s = texture(TEXTURE, uv + vec2(0.0, px.y)).rgb;
		vec3 c_w = texture(TEXTURE, uv + vec2(-px.x, 0.0)).rgb;
		vec3 c_e = texture(TEXTURE, uv + vec2(px.x, 0.0)).rgb;

		// SMAA 的边缘判定是逐通道取差再取最大，彩度变化也算边（FXAA 只看亮度）
		float dv = max(abs(m.r - c_n.r) + abs(m.g - c_n.g) + abs(m.b - c_n.b),
		               abs(m.r - c_s.r) + abs(m.g - c_s.g) + abs(m.b - c_s.b));
		float dh = max(abs(m.r - c_w.r) + abs(m.g - c_w.g) + abs(m.b - c_w.b),
		               abs(m.r - c_e.r) + abs(m.g - c_e.g) + abs(m.b - c_e.b));

		if (max(dv, dh) < smaa_threshold)
		{
			COLOR = vec4(m, 1.0);
		}
		else
		{
			bool horiz = dh >= dv;
			vec3 c1 = horiz ? c_s : c_e;
			vec3 c2 = horiz ? c_n : c_w;
			float l1 = luma_of(c1);
			float l2 = luma_of(c2);

			float lo = min(l1, l2);
			float hi = max(l1, l2);

			if (hi - lo < 1e-4)
			{
				COLOR = vec4(m, 1.0);
			}
			else
			{
				// 亚像素覆盖率：中心亮度落在两侧之间的位置，决定往哪边偏、偏多少
				float cover = clamp((l_m - lo) / (hi - lo), 0.0, 1.0);
				vec3 target = (cover > 0.5) ? c1 : c2;
				float amount = 0.5 * (1.0 - abs(cover * 2.0 - 1.0)) * 0.85;
				COLOR = vec4(mix(m, target, amount), 1.0);
			}
		}
	}
}
