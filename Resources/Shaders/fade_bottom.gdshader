// 立绘下沿淡出：只做 alpha 衰减，不叠颜色。
// 用纯色渐变去盖木纹地面会露出一块矩形，只有把 alpha 淡掉才是"融进背景"。

shader_type canvas_item;

uniform float fade_start : hint_range(0.0, 1.0) = 0.62;
uniform float fade_end : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float cut = smoothstep(fade_start, fade_end, UV.y);
	tex.a *= 1.0 - cut;
	COLOR = tex;
}
