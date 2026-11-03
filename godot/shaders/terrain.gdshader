// Le sol de la carte, avec des transitions naturelles entre les terrains (étape 5).
//
// Chaque pixel d'art (24 par case, affiché ×2 ou ×3) part du terrain de sa case. Près d'une case voisine
// d'un autre terrain, les deux se mélangent progressivement le long d'une ligne ondulée : les terrains
// « au-dessus » (rang plus élevé) débordent un peu plus. Entre deux terres, le mélange est doux ; contre l'eau,
// la rive reste une ligne nette, avec un contour, une berge un peu sombre et de l'écume.
// Les images des terrains ne servent que de motifs : on peut les changer, les transitions suivent.
// Les routes sont une couche posée par-dessus : une bande qui relie le centre de chaque case de route à ses
// voisines, avec des virages en quart de cercle, quel que soit le terrain dessous.
shader_type canvas_item;
render_mode unshaded;

// Le terrain de chaque case (canal rouge = numéro du terrain, vert = route), un pixel par case
uniform sampler2D terrain_map : filter_nearest;
// L'image de la route (24 × 24)
uniform sampler2D road_tile : filter_nearest, repeat_enable;
// Les images de chaque terrain (24 × 24) : « variants » couches par terrain, à la suite
// (couche = numéro du terrain × variants + variante)
uniform sampler2DArray tiles : filter_nearest;
uniform int variants = 1;
uniform ivec2 map_cells = ivec2(1, 1);
// Rang de chaque terrain : un terrain de rang plus élevé déborde davantage sur l'autre
uniform int priority[8];
// Numéro du terrain « eau » (rives nettes et écume)
uniform int water = 5;

// La ligne entre deux terrains : décalage vers le terrain du dessous (pixels d'art), ondulations
uniform float edge_bias = 2.0;
uniform float edge_wave = 5.5;
uniform float wave_scale = 13.0;
uniform float wave_detail = 0.4;
// Mélange entre deux terres : demi-largeur (pixels d'art), grain qui rend le mélange irrégulier,
// et nombre de paliers (0 = mélange continu ; 4 = 0, 25, 50, 75, 100 %, plus « pixel art »)
uniform float blend = 3.5;
uniform float blend_grain = 0.35;
uniform float grain_scale = 3.0;
uniform float blend_steps = 4.0;
// Rives : contour et berge (assombrissement de la terre), écume (éclaircissement de l'eau)
uniform float outline = 0.18;
uniform float bank = 0.08;
uniform float foam = 0.4;
uniform vec3 foam_color = vec3(0.9, 0.95, 1.0);
// Variantes : taille des zones de chaque variante (pixels d'art) et largeur de la frontière tramée entre elles
uniform float variant_scale = 30.0;
uniform float variant_dither = 0.25;
// Routes : demi-largeur (pixels d'art), ondulation des bords, largeur du fondu et assombrissement du bord
uniform float road_width = 4.5;
uniform float road_wave = 1.5;
uniform float road_wave_scale = 7.0;
uniform float road_blend = 1.0;
uniform float road_edge = 0.15;
// Grandes zones un peu plus claires ou plus sombres : force et taille (pixels d'art)
uniform float shade = 0.08;
uniform float shade_scale = 70.0;

const float ART = 24.0;

int terrain_at(ivec2 c) {
	c = clamp(c, ivec2(0), map_cells - ivec2(1));
	return int(texelFetch(terrain_map, c, 0).r * 255.0 + 0.5);
}

bool road_at(ivec2 c) {
	if (c.x < 0 || c.y < 0 || c.x >= map_cells.x || c.y >= map_cells.y) return false;
	return texelFetch(terrain_map, c, 0).g > 0.5;
}

// Distance du point q à un segment [a, b]
float segment(vec2 q, vec2 a, vec2 b) {
	vec2 qa = q - a;
	vec2 ba = b - a;
	float h = clamp(dot(qa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(qa - ba * h);
}

// Distance de q à la ligne médiane de la route dans la case n (qui a une route). Chaque case dessine sa moitié de
// chaque liaison : jusqu'au milieu du bord vers une voisine en ligne droite, ou jusqu'au coin vers une voisine en
// diagonale (seulement si aucune case de route ne les relie déjà par un côté). Un virage (deux voisines à angle
// droit, rien d'autre) devient un quart de cercle : un escalier de virages devient une route sinueuse.
float road_in_cell(vec2 q, ivec2 n) {
	vec2 center = (vec2(n) + 0.5) * ART;
	bool east = road_at(n + ivec2(1, 0));
	bool west = road_at(n + ivec2(-1, 0));
	bool south = road_at(n + ivec2(0, 1));
	bool north = road_at(n + ivec2(0, -1));
	int count = int(east) + int(west) + int(south) + int(north);
	float best = 1e5;
	// virage : quart de cercle centré sur le coin, entre les milieux des deux bords
	if (count == 2 && (east != west)) {
		vec2 d1 = east ? vec2(1.0, 0.0) : vec2(-1.0, 0.0);
		vec2 d2 = south ? vec2(0.0, 1.0) : vec2(0.0, -1.0);
		vec2 k = center + (d1 + d2) * ART * 0.5;
		vec2 v = q - k;
		if (dot(v, -d1) >= 0.0 && dot(v, -d2) >= 0.0)
			best = abs(length(v) - ART * 0.5);
		else
			best = min(length(q - (center + d1 * ART * 0.5)), length(q - (center + d2 * ART * 0.5)));
	} else {
		if (east) best = min(best, segment(q, center, center + vec2(ART * 0.5, 0.0)));
		if (west) best = min(best, segment(q, center, center - vec2(ART * 0.5, 0.0)));
		if (south) best = min(best, segment(q, center, center + vec2(0.0, ART * 0.5)));
		if (north) best = min(best, segment(q, center, center - vec2(0.0, ART * 0.5)));
	}
	// liaisons en diagonale, quand aucun côté ne relie déjà les deux cases
	bool diagonal = false;
	for (int ey = -1; ey <= 1; ey += 2) {
		for (int ex = -1; ex <= 1; ex += 2) {
			if (!road_at(n + ivec2(ex, ey))) continue;
			if (road_at(n + ivec2(ex, 0)) || road_at(n + ivec2(0, ey))) continue;
			diagonal = true;
			best = min(best, segment(q, center, center + vec2(float(ex), float(ey)) * ART * 0.5));
		}
	}
	if (count == 0 && !diagonal) best = length(q - center); // une route d'une seule case : un rond
	return best;
}

// Distance du pixel d'art p à la ligne médiane des routes proches (très grande s'il n'y en a pas)
float road_distance(vec2 p) {
	ivec2 c = ivec2(floor(p / ART));
	vec2 q = p + 0.5;
	float best = 1e5;
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			ivec2 n = c + ivec2(dx, dy);
			if (road_at(n)) best = min(best, road_in_cell(q, n));
		}
	}
	return best;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Bruit doux entre 0 et 1, le même à chaque fois pour un même endroit
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// Trame 4 × 4 (Bayer), entre 0 et 1
float bayer(vec2 p) {
	int x = int(mod(p.x, 4.0));
	int y = int(mod(p.y, 4.0));
	int m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
	return (float(m[x + y * 4]) + 0.5) / 16.0;
}

// Ondulation de la ligne au point center, entre -1 et 1 environ (la même des deux côtés de la ligne)
float wave(vec2 center) {
	return (noise(center / wave_scale) * 2.0 - 1.0)
		+ (noise(center / (wave_scale / 3.0) + vec2(41.0, 7.0)) * 2.0 - 1.0) * wave_detail;
}

// Le mélange au pixel d'art p : terrain de la case (a), autre terrain proche (b), et la part de b (0 à 1).
// La part de b vaut 0,5 sur la ligne ; la ligne d'un même couple de terrains est la même vue des deux
// cases, si bien que le mélange se raccorde sans couture.
void mixture(vec2 p, out int a, out int b, out float share) {
	ivec2 c = ivec2(floor(p / ART));
	vec2 local = p - vec2(c) * ART + 0.5;
	vec2 center = p + 0.5;
	a = terrain_at(c);
	b = a;
	share = 0.0;
	float w = wave(center);
	float limit = edge_bias + edge_wave * (1.0 + wave_detail) + blend + 1.0;
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			if (dx == 0 && dy == 0) continue;
			int t = terrain_at(c + ivec2(dx, dy));
			if (t == a) continue;
			// distance du pixel à la case voisine (jusqu'au bord, ou jusqu'au coin pour une diagonale)
			float ex = dx == 1 ? ART - local.x : (dx == -1 ? local.x : 0.0);
			float ey = dy == 1 ? ART - local.y : (dy == -1 ? local.y : 0.0);
			float d = length(vec2(ex, ey));
			if (d > limit) continue;
			// la ligne avance dans cette case si le voisin est au-dessus, recule sinon
			float side = priority[t] > priority[a] ? 1.0 : -1.0;
			float line = side * (edge_bias + w * edge_wave);
			float gap = d - line;
			float s;
			if (t == water || a == water) {
				s = gap < 0.0 ? 1.0 : 0.0; // rive : nette
			} else {
				float grain = (noise(center / grain_scale + vec2(float(min(t, a) * 7 + max(t, a)) * 3.1)) - 0.5) * blend_grain;
				s = clamp(0.5 - gap / (2.0 * blend) + grain * side, 0.0, 1.0);
			}
			if (s > share) {
				share = s;
				b = t;
			}
		}
	}
}

// Le terrain qui domine le pixel d'art p (pour les rives et l'écume)
int owner(vec2 p) {
	int a;
	int b;
	float share;
	mixture(p, a, b, share);
	return share >= 0.5 ? b : a;
}

vec3 tile_color(int t, vec2 p) {
	// la variante suit un grand bruit doux (pas les cases) : de grandes zones sans couture, la frontière est tramée
	int layer = t * variants;
	if (variants > 1) {
		float v = noise((p + 0.5) / variant_scale + vec2(float(t) * 53.0, 19.0));
		if (v + (bayer(p) - 0.5) * variant_dither > 0.5) layer += 1;
	}
	vec3 color = texelFetch(tiles, ivec3(ivec2(mod(p, ART)), layer), 0).rgb;
	// grandes zones un peu plus claires ou plus sombres, en trois niveaux tramés
	if (shade > 0.0) {
		float s = noise((p + 0.5) / shade_scale + vec2(float(t) * 31.0)) * 2.0 - 1.0;
		float level = clamp(floor(s * 1.5 + bayer(p)), -1.0, 1.0);
		color *= 1.0 + level * shade;
	}
	return color;
}

void fragment() {
	vec2 p = floor(UV * vec2(map_cells) * ART);
	int a;
	int b;
	float share;
	mixture(p, a, b, share);
	// mélange en paliers : un pixel d'art n'a que quelques teintes possibles, comme en pixel art
	if (blend_steps > 0.0 && share > 0.0 && share < 1.0)
		share = clamp(floor(share * blend_steps + bayer(p)) / blend_steps, 0.0, 1.0);
	vec3 color = mix(tile_color(a, p), tile_color(b, p), share);
	int t = share >= 0.5 ? b : a;

	// Rives : seulement entre la terre et l'eau
	bool shore_land = t != water && (a == water || b == water);
	bool shore_water = t == water;
	if (shore_land || shore_water) {
		int right = owner(p + vec2(1.0, 0.0));
		int left = owner(p - vec2(1.0, 0.0));
		int down = owner(p + vec2(0.0, 1.0));
		int up = owner(p - vec2(0.0, 1.0));
		int right2 = owner(p + vec2(2.0, 0.0));
		int left2 = owner(p - vec2(2.0, 0.0));
		int down2 = owner(p + vec2(0.0, 2.0));
		int up2 = owner(p - vec2(0.0, 2.0));
		if (shore_land) {
			// contour de la terre contre l'eau, puis berge un peu sombre
			if (right == water || left == water || down == water || up == water)
				color *= 1.0 - outline;
			else if (right2 == water || left2 == water || down2 == water || up2 == water)
				color *= 1.0 - bank;
		} else if (foam > 0.0) {
			// écume : forte contre la rive, tramée un pixel plus loin
			if (right != water || left != water || down != water || up != water)
				color = mix(color, foam_color, foam);
			else if ((right2 != water || left2 != water || down2 != water || up2 != water) && bayer(p) < 0.5)
				color = mix(color, foam_color, foam * 0.6);
		}
	}
	// La route, par-dessus le sol
	float dist = road_distance(p);
	float width = road_width + (noise((p + 0.5) / road_wave_scale + vec2(71.0, 13.0)) * 2.0 - 1.0) * road_wave;
	if (dist < width + road_blend) {
		float on_road = clamp((width - dist) / (2.0 * road_blend) + 0.5, 0.0, 1.0);
		if (blend_steps > 0.0 && on_road > 0.0 && on_road < 1.0)
			on_road = clamp(floor(on_road * blend_steps + bayer(p)) / blend_steps, 0.0, 1.0);
		vec3 road = texelFetch(road_tile, ivec2(mod(p, ART)), 0).rgb;
		// le bord de la route un peu plus sombre (ornières)
		if (dist > width - 1.5) road *= 1.0 - road_edge;
		color = mix(color, road, on_road);
	}
	COLOR = vec4(color, 1.0);
}
