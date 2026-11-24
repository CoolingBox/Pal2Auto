shader_type canvas_item;

// 角色头像框 v4 —— 在 v3（老游戏硬倒角）之上补**解析抗锯齿**
//
// v3 用 if/else 在距离场上切档，切出来的八角边永远是台阶 —— 一个像素不是"在档内"就是"在档外"。
// v4 不换美术，只换取值方式：所有边界都改成 ±半个像素的 smoothstep（宽度由 fwidth(d) 给出），
// 于是切角、斜面、宝石、铆片的外轮廓在**任何分辨率**下都是平滑的，而且不花额外采样、不吃分辨率。
//
// 这就是"解析抗锯齿"（analytic AA）：能算出几何距离的地方，就不该靠 MSAA / FXAA 去猜。
// 位图立绘做不到这一点（它没有距离场），只能靠提高渲染倍率或多重采样。

// —— 立绘摆放（由 PortraitFrame.ApplyFit 按内容包围盒算好）——
uniform vec2 art_size = vec2(1.0, 1.0);
uniform vec2 art_pos = vec2(0.5, 0.5);
uniform vec3 paper : source_color = vec3(0.062, 0.053, 0.042);
uniform float aspect = 1.6;

// —— 框形（都以框高为 1）——
uniform float cut : hint_range(0.05, 0.45) = 0.22;    // 切角大小
uniform float rim : hint_range(0.02, 0.25) = 0.088;   // 金属框总厚
uniform float lip : hint_range(0.0, 0.06) = 0.018;    // 内圈暗缝宽度
uniform float inner : hint_range(0.0, 0.20) = 0.055;  // 画面内阴影深度

// 抗锯齿开关：1 = 按 1 像素过渡；0 = 硬台阶（老游戏感）。
// 注意不能直接置 0 后拿 0 当 smoothstep 的宽度 —— edge0 == edge1 在 GLSL 里结果未定义，
// 所以取一个极小正数当兜底。
uniform float aa_scale : hint_range(0.0, 1.0) = 1.0;

uniform vec4 frame_color : source_color = vec4(0.780, 0.620, 0.330, 1.0);
uniform vec4 gem_color : source_color = vec4(0.560, 0.760, 0.880, 1.0);
uniform float gem : hint_range(0.0, 1.0) = 1.0;

// p：以高度的一半为 1 的坐标；返回切角矩形的距离场（<0 在内部）
float plate_d(vec2 p, float half_w, float c) {
	float box = max(abs(p.x) - half_w, abs(p.y) - 1.0);
	float chamfer = (abs(p.x) + abs(p.y)) - (half_w + 1.0 - c);
	return max(box, chamfer);
}

void fragment() {
	// —————————————————————— 画面 ——————————————————————
	vec2 uv = (UV - art_pos) / art_size + vec2(0.5);
	vec3 paint = paper;
	if (uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0) {
		vec4 t = texture(TEXTURE, uv);
		paint = mix(paint, t.rgb, t.a);
	}
	// 底部略压暗，让半身像"坐"在框里；比 v2 轻 —— 老游戏不讲究景深
	paint *= mix(0.66, 1.0, smoothstep(0.0, 0.20, 1.0 - UV.y));

	// —————————————————————— 框 ——————————————————————
	float half_w = aspect;
	vec2 p = vec2((UV.x - 0.5) * aspect, UV.y - 0.5) * 2.0;

	float c = cut * 2.0;
	float r = rim * 2.0;
	float l = lip * 2.0;
	float sh = inner * 2.0;

	float d = plate_d(p, half_w, c);

	// 一个像素有多宽（以距离场为单位）。所有过渡都以它为宽度 —— 抗锯齿的全部秘密就在这一行。
	float aa = max(fwidth(d) * aa_scale, 1e-5);
	float hw = aa * 0.5;

	vec2 g = vec2(dFdx(d), dFdy(d));
	vec2 n = normalize(g + vec2(1e-5));
	float lit = clamp(0.5 + 0.5 * dot(n, normalize(vec2(-0.62, -1.0))), 0.0, 1.0);

	// —— 画面（含内阴影）——
	float depth = clamp((-d - r - l) / max(sh, 1e-4), 0.0, 1.0);
	vec3 colArt = paint * mix(0.66, 1.0, depth);

	// —— 框面五档：外沿暗线 / 外倒角 / 平色面 / 内倒角 / 内沿暗线 ——
	// 受光侧亮、背光侧暗仍然只取两档；只是档与档之间用半个像素过渡，不再硬切。
	float t = clamp(-d / max(r, 1e-4), 0.0, 1.0);
	float bw = aa / max(r, 1e-4);

	vec3 cBevel = (lit > 0.5) ? frame_color.rgb * 1.12 : frame_color.rgb * 0.50;
	vec3 cFace  = (lit > 0.5) ? frame_color.rgb * 0.94 : frame_color.rgb * 0.76;
	vec3 cInner = (lit > 0.5) ? frame_color.rgb * 1.04 : frame_color.rgb * 0.58;

	vec3 colFrame = frame_color.rgb * 0.30;
	colFrame = mix(colFrame, cBevel, smoothstep(0.15 - bw * 0.5, 0.15 + bw * 0.5, t));
	colFrame = mix(colFrame, cFace,  smoothstep(0.33 - bw * 0.5, 0.33 + bw * 0.5, t));
	colFrame = mix(colFrame, cInner, smoothstep(0.74 - bw * 0.5, 0.74 + bw * 0.5, t));
	colFrame = mix(colFrame, frame_color.rgb * 0.34, smoothstep(0.90 - bw * 0.5, 0.90 + bw * 0.5, t));

	// —— 由内到外拼：画面 → 内圈暗缝 → 框 ——
	vec3 col = colArt;
	float lipEdge = -r - l;
	col = mix(col, frame_color.rgb * 0.38, smoothstep(lipEdge - hw, lipEdge + hw, d));
	col = mix(col, colFrame, smoothstep(-r - hw, -r + hw, d));

	// —— 外形轮廓：板内 vs 板外投影。这一层决定八角边有没有台阶 ——
	float cover = 1.0 - smoothstep(-hw, hw, d);
	float shadowAlpha = clamp(1.0 - d / 0.075, 0.0, 1.0) * 0.55;
	vec4 outColor = mix(vec4(0.0, 0.0, 0.0, shadowAlpha), vec4(col, 1.0), cover);

	// —————————————————————— 四角铆片 ——————————————————————
	vec2 cq = abs(p) - vec2(half_w - c * 0.5 - 0.050, 1.0 - c * 0.5 - 0.050);
	float sq = max(abs(cq.x), abs(cq.y));
	if (sq < 0.072 + aa) {
		float inCore = 1.0 - smoothstep(0.052 - hw, 0.052 + hw, sq);
		float inRing = 1.0 - smoothstep(0.072 - hw, 0.072 + hw, sq);
		vec3 cqCol = mix(frame_color.rgb * 0.28, frame_color.rgb * 1.05, inCore);
		outColor = mix(outColor, vec4(cqCol, 1.0), inRing * cover);
	}

	// —————————————————————— 顶冠宝石 ——————————————————————
	if (gem > 0.5) {
		vec2 gp = p - vec2(0.0, -1.0 + r * 0.55);
		float gd = abs(gp.x) * 1.15 + abs(gp.y);
		if (gd < 0.078 + aa) {
			float core = 1.0 - smoothstep(0.050 - hw, 0.050 + hw, gd);
			float seat = 1.0 - smoothstep(0.078 - hw, 0.078 + hw, gd);
			vec3 gemCol = mix(gem_color.rgb * 0.42, gem_color.rgb * 0.78,
				clamp(1.0 - gd / 0.050, 0.0, 1.0));
			vec3 g3 = mix(frame_color.rgb * 0.24, gemCol, core);
			outColor = mix(outColor, vec4(g3, 1.0), seat);
		}
	}

	COLOR = outColor;
}
