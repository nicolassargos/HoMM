// Le shader du décor de la carte (DecorLayer).
// Les pentes des montagnes prennent le sol qui est dessous (2026-11-17) : un pixel de pente (vert dans l'image d'une
// montagne d'herbe) prend la couleur du sol déjà dessiné à cet endroit (l'herbe, le sable, le marais, une route…), avec
// la lumière du pixel d'origine. Une montagne se fond ainsi dans n'importe quel sol, même au bord de deux sols.
// Seules les images dessinées avec la marque (le bleu de la couleur à 0,998) sont touchées : pas les arbres.
// Les arbres dans le vent (2026-11-25) : marqués par le rouge à 0,998, leur haut penche dans le sens du vent sous une
// rafale (Wind.cs), rangée de pixels par rangée de pixels, le pied fixe ; chaque arbre s'agite un peu à son rythme.
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
// la luminosité (0 à 1) d'un pixel de pente qui garde le sol tel quel ; plus sombre, le sol s'assombrit d'autant ;
// slope_relief : la part de ce relief gardée (1 : tout ; moins, les pentes se fondent mieux mais s'aplatissent)
uniform float slope_light = 0.47;
uniform float slope_relief = 0.6;

// le vent (réglé par Wind.cs) : son sens, l'écart et la vitesse des rafales (pixels de la carte), l'instant (secondes)
uniform vec2 wind_dir = vec2(0.958, 0.287);
uniform float gust_length = 448.0;
uniform float gust_speed = 96.0;
uniform float wind_time = 0.0;
// de combien le haut d'un arbre penche au plus fort d'une rafale (pixels d'art) ; la taille d'un pixel d'art
uniform float tree_sway = 1.5;
uniform float art_scale = 2.0;
// la marge ajoutée de chaque côté d'un arbre (pixels d'art), pour que le feuillage penché ne soit pas coupé
const float tree_pad = 3.0;

varying float slope;
varying float tree;
varying vec2 foot;
varying vec4 tint;

// la même rafale que Wind.Gust
float gust(vec2 p, float t) {
	float tau = 6.2831853;
	float along = dot(p, wind_dir), across = dot(p, vec2(-wind_dir.y, wind_dir.x));
	float band = 0.5 + 0.5 * sin(tau * (along - gust_speed * t) / gust_length);
	float vary = 0.5 + 0.5 * sin(tau * across / (gust_length * 1.7) + 0.8 * sin(tau * along / (gust_length * 2.9)) + 0.21 * t);
	return smoothstep(0.3, 1.0, band * (0.35 + 0.65 * vary));
}

void vertex() {
	slope = COLOR.b < 0.999 ? 1.0 : 0.0;
	tree = COLOR.r < 0.999 ? 1.0 : 0.0;
	tint = vec4(1.0, COLOR.g, 1.0, COLOR.a);
	tint.r = tree > 0.5 ? 1.0 : COLOR.r;
	// le pied de l'arbre (le milieu du bas de l'image), le même pour ses quatre coins
	foot = (MODEL_MATRIX * vec4(VERTEX.x - (UV.x - 0.5) / TEXTURE_PIXEL_SIZE.x * art_scale,
		VERTEX.y + (1.0 - UV.y) / TEXTURE_PIXEL_SIZE.y * art_scale, 0.0, 1.0)).xy;
	if (tree > 0.5) {
		float side = UV.x * 2.0 - 1.0;
		VERTEX.x += side * tree_pad * art_scale;
		UV.x += side * tree_pad * TEXTURE_PIXEL_SIZE.x;
	}
}

void fragment() {
	vec2 uv = UV;
	if (tree > 0.5) {
		float g = gust(foot, wind_time);
		// chaque arbre a son rythme (selon son pied) ; il s'agite plus fort dans une rafale
		float phase = fract(sin(dot(foot, vec2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
		float lean = tree_sway * (0.15 + 0.85 * g + 0.3 * (0.3 + g) * sin(wind_time * 2.6 + phase)) * sign(wind_dir.x);
		// le haut penche, le pied reste : un décalage en pixels d'art entiers, plus grand vers le haut
		float height = clamp(1.0 - UV.y, 0.0, 1.0);
		uv.x -= floor(lean * pow(height, 1.5) + 0.5) * TEXTURE_PIXEL_SIZE.x;
	}
	vec4 c = uv.x < 0.0 || uv.x > 1.0 ? vec4(0.0) : texture(TEXTURE, uv);
	// (nettement vert : un gris de roche à peine verdâtre reste de la roche)
	if (slope > 0.5 && c.g > c.r + 0.06 && c.g > c.b + 0.06) {
		float l = dot(c.rgb, vec3(0.3, 0.59, 0.11));
		vec3 ground = texture(screen_texture, SCREEN_UV).rgb;
		c.rgb = ground * mix(1.0, clamp(l / slope_light, 0.3, 1.4), slope_relief);
	}
	// (COLOR contient déjà l'image : on repart de la couleur de l'image, une seule fois)
	COLOR = c * tint;
}
