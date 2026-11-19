// Les pentes des montagnes prennent le sol qui est dessous (2026-11-17) : un pixel de pente (vert dans l'image d'une
// montagne d'herbe) prend la couleur du sol déjà dessiné à cet endroit (l'herbe, le sable, le marais, une route…), avec
// la lumière du pixel d'origine. Une montagne se fond ainsi dans n'importe quel sol, même au bord de deux sols.
// Seules les images dessinées avec la marque (le bleu de la couleur à 0,998) sont touchées : pas les arbres.
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
// la luminosité (0 à 1) d'un pixel de pente qui garde le sol tel quel ; plus sombre, le sol s'assombrit d'autant ;
// slope_relief : la part de ce relief gardée (1 : tout ; moins, les pentes se fondent mieux mais s'aplatissent)
uniform float slope_light = 0.47;
uniform float slope_relief = 0.6;

varying float slope;
varying vec4 tint;

void vertex() {
	slope = COLOR.b < 0.999 ? 1.0 : 0.0;
	tint = vec4(COLOR.rg, 1.0, COLOR.a);
}

void fragment() {
	vec4 c = texture(TEXTURE, UV);
	// (nettement vert : un gris de roche à peine verdâtre reste de la roche)
	if (slope > 0.5 && c.g > c.r + 0.06 && c.g > c.b + 0.06) {
		float l = dot(c.rgb, vec3(0.3, 0.59, 0.11));
		vec3 ground = texture(screen_texture, SCREEN_UV).rgb;
		c.rgb = ground * mix(1.0, clamp(l / slope_light, 0.3, 1.4), slope_relief);
	}
	// (COLOR contient déjà l'image : on repart de la couleur de l'image, une seule fois)
	COLOR = c * tint;
}
