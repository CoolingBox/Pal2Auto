shader_type canvas_item;

// 不规则头像框：切角八边形遮罩 + 金边 + 四角菱形钉。
// uv_scale 由 C# 按"铺满裁切"算好传进来 —— 立绘比例有 1.28 / 1.0 / 0.8 三种，直接拉伸会变形。

uniform vec2 uv_scale = vec2(1.0, 1.0);
uniform float cut : hint_range(0.02, 0.45) = 0.13;
uniform float band : hint_range(0.02, 0.30) = 0.055;
uniform vec4 frame_color : source_color = vec4(0.800, 0.650, 0.360, 1.0);
uniform vec4 line_color : source_color = vec4(0.100, 0.090, 0.070, 1.0);
uniform float ornament : hint_range(0.0, 1.0) = 1.0;

// 切角八边形的距离场：<0 在内部，>0 在外部
float oct_d(vec2 uv) {
	vec2 q = abs(uv - vec2(0.5)) * 2.0;
	float box = max(q.x, q.y) - 1.0;
	float corner = (q.x + q.y) - (2.0 - cut);
	return max(box, corner);
}

void fragment() {
	vec2 uv = clamp((UV - vec2(0.5)) * uv_scale + vec2(0.5), vec2(0.0), vec2(1.0));
	vec4 art = texture(TEXTURE, uv);

	float d = oct_d(UV);

	if (d > 0.0) {
		COLOR = vec4(0.0);
	} else if (d > -band) {
		// 外沿亮、内沿暗：一块有厚度的包边
		float t = (d + band) / band;
		vec3 col = mix(frame_color.rgb * 0.35, frame_color.rgb, t);
		COLOR = vec4(col, 1.0);
	} else if (d > -band - 0.022) {
		// 包边与画面之间压一条暗线，把两者分开
		COLOR = vec4(line_color.rgb, 1.0);
	} else {
		COLOR = art;
	}

	// 四角菱形钉：落在四条切角边的中点
	if (ornament > 0.5) {
		vec2 q = abs(UV - vec2(0.5)) * 2.0;
		vec2 c = vec2(1.0 - cut * 0.55, 1.0 - cut * 0.55);
		vec2 dd = abs(q - c);
		if (dd.x + dd.y < cut * 0.42) {
			COLOR = vec4(frame_color.rgb * 1.3, 1.0);
		}
	}
}
