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
// les formes douces des rivières (rouge) et des ruisseaux (vert), plusieurs points par case (RiverField)
uniform sampler2D water_field : filter_linear;
// L'image de la route (24 × 24)
// les images des sortes de routes : terre, gravier, pavés (RoadKind - 1)
uniform sampler2DArray road_tiles : filter_nearest;
// Les images de chaque terrain (24 × 24) : « variants » couches par terrain, à la suite
// (couche = numéro du terrain × variants + variante)
uniform sampler2DArray tiles : filter_nearest;
uniform int variants = 1;
uniform ivec2 map_cells = ivec2(1, 1);
// Rang de chaque terrain : un terrain de rang plus élevé déborde davantage sur l'autre
uniform int priority[8];
// Numéro du terrain « eau » (rives nettes et écume), du sable (les plages) et de l'herbe (les fleurs)
uniform int water = 5;
uniform int sand = 3;
uniform int grass = 0;

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
// le bord de la route : cranté (road_jag pixels d'art), la route et le sol s'y mêlent pixel par pixel sur road_scatter
// pixels (comme l'herbe qui s'éclaircit là où l'on marche) ; une bande de terre battue (l'image de la terre, assombrie)
// d'épaisseur changeante, jusqu'à road_frame pixels ; la route un peu ombrée près de son bord (road_shade)
uniform float road_jag = 1.0;
uniform float road_scatter = 2.5;
uniform float road_frame = 0.0;
uniform float road_frame_dark = 0.8;
// la terre battue n'est que par endroits (la part du bord qui en a, environ) et laisse voir l'herbe ou la route dessous,
// plus ou moins selon la sorte de la route (elle se voit peu sur la terre, beaucoup sur les pavés gris)
uniform float road_frame_share = 0.45;
uniform float road_soil_dirt = 0.45;
uniform float road_soil_gravel = 0.45;
uniform float road_soil_cobbles = 0.45;
// la terre se pose en multipliant (elle teinte et assombrit sans cacher les pierres) et va surtout dans les creux : les
// pixels sombres (les joints des pavés, l'ombre entre les cailloux) ; road_soil_low : la part qui ne va que dans les creux
uniform float road_soil_low = 0.7;
// la clarté de la teinte de terre qui multiplie (plus bas : plus sombre)
uniform float road_soil_tint = 0.75;
uniform float road_shade = 0.1;
// la route est un peu plus basse que l'herbe : le soleil vient d'en bas à droite (les ombres de la carte vont vers la
// gauche et le haut) ; le talus du côté du soleil jette une ombre douce sur la route (road_shadow : sa force, sur
// road_shadow_length pixels d'art), le talus d'en face reçoit le soleil (road_rim : un liseré plus clair sur l'herbe)
uniform float road_shadow = 0.15;
uniform float road_shadow_length = 3.0;
uniform float road_rim = 0.07;
uniform vec2 sun = vec2(0.67, 0.74);
// la route s'élargit et se rétrécit doucement le long de son chemin : de combien (pixels d'art), sur quelle longueur
uniform float road_vary = 0.5;
uniform float road_vary_scale = 12.0;
// Plages : largeur de la bande de sable côté terre (pixels d'art, 0 = aucune), ondulation de son bord,
// sable mouillé (assombrissement) sur les derniers pixels avant l'eau
uniform float beach_width = 8.0;
uniform float beach_wave = 2.0;
uniform float wet = 0.15;
uniform float wet_width = 2.0;
// Fleurs sur l'herbe : l'herbe est découpée en carrés de « flower_cell » pixels d'art ; une part « flower_density » de
// ces carrés porte une fleur (un cœur, quatre pétales et un pixel d'ombre) ou, plus rarement, un petit caillou
uniform float flower_density = 0.1;
uniform float flower_cell = 9.0;
uniform float pebble_share = 0.2;
// Grandes zones un peu plus claires ou plus sombres : force et taille (pixels d'art)
uniform float shade = 0.08;
uniform float shade_scale = 70.0;

// Rivières (2026-11-15) : le bleu de chaque case dit sa rivière (× 64 : 0 rien, 1 ruisseau, 2 rivière, 3 pont).
// Un ruisseau suit le milieu des cases, comme une route (demi-largeur et ondulation) ; une rivière remplit la bande de
// ses cases en laissant une berge d'herbe (river_bank) de chaque côté ; un pont de planches (demi-largeur bridge_width)
// sur les cases de pont, et là où une route croise un ruisseau.
uniform float stream_width = 4.7;
uniform float stream_wave = 1.3;
uniform float river_bank = 9.3;
uniform float river_wave = 3.3;
uniform float bridge_width = 6.0;
uniform float stream_bend = 6.0;
uniform vec3 bridge_color = vec3(0.56, 0.4, 0.24);

// La taille d'une case en pixels d'art (32 ; réglée par le jeu : MapRenderer.ArtTile)
uniform float ART = 32.0;

int terrain_at(ivec2 c) {
	c = clamp(c, ivec2(0), map_cells - ivec2(1));
	return int(texelFetch(terrain_map, c, 0).r * 255.0 + 0.5);
}

bool road_at(ivec2 c) {
	if (c.x < 0 || c.y < 0 || c.x >= map_cells.x || c.y >= map_cells.y) return false;
	return texelFetch(terrain_map, c, 0).g > 0.1;
}

// La sorte de la route de la case c (1 terre, 2 gravier, 3 pavés ; 0 sans route)
int road_kind_at(ivec2 c) {
	if (c.x < 0 || c.y < 0 || c.x >= map_cells.x || c.y >= map_cells.y) return 0;
	return int(texelFetch(terrain_map, c, 0).g * 255.0 / 64.0 + 0.5);
}

// La rivière de la case c (0 hors de la carte)
int river_at(ivec2 c) {
	if (c.x < 0 || c.y < 0 || c.x >= map_cells.x || c.y >= map_cells.y) return 0;
	return int(texelFetch(terrain_map, c, 0).b * 255.0 / 64.0 + 0.5);
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

// Distance du pixel d'art p à la ligne médiane des routes proches (très grande s'il n'y en a pas) ; kind : la sorte de
// la route la plus proche (chaque case dessine sa moitié de ses liaisons : la sorte change au milieu entre deux cases)
float road_distance(vec2 p, out int kind) {
	ivec2 c = ivec2(floor(p / ART));
	vec2 q = p + 0.5;
	float best = 1e5;
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			ivec2 n = c + ivec2(dx, dy);
			if (road_at(n)) {
				float d = road_in_cell(q, n);
				if (d < best) {
					best = d;
					kind = road_kind_at(n);
				}
			}
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

// La part d'eau de la bande des rivières au pixel d'art p (water_field, rouge : la bande floutée, son bord à 0,5)
float river_part(vec2 p) {
	return texture(water_field, (p + 0.5) / (vec2(map_cells) * ART)).r;
}

// L'eau d'une rivière au pixel d'art p : de combien de pixels il est dans l'eau (négatif : sur la rive, ou loin). La
// distance au bord de la bande vient de sa part d'eau et de sa pente ; l'eau commence à river_bank pixels du bord.
float river_depth(vec2 p) {
	float f = river_part(p);
	if (f <= 0.002) return -1e5;
	vec2 slope = vec2(river_part(p + vec2(2.0, 0.0)) - river_part(p - vec2(2.0, 0.0)),
		river_part(p + vec2(0.0, 2.0)) - river_part(p - vec2(0.0, 2.0))) / 4.0;
	float d = (f - 0.5) / max(length(slope), 1e-4);
	float edge = river_bank + (noise((p + 0.5) / 11.0 + vec2(23.0, 47.0)) * 2.0 - 1.0) * river_wave;
	return clamp(d, -1e4, 1e4) - edge;
}

// Le décalage d'un ruisseau au pixel d'art p : il ondule de part et d'autre du milieu des cases (pixels d'art)
vec2 stream_wobble(vec2 p) {
	return vec2(noise(p / 19.0 + vec2(3.0, 71.0)), noise(p / 19.0 + vec2(59.0, 11.0))) * 2.0 - 1.0;
}

// Distance du pixel d'art p au milieu des ruisseaux proches (water_field, vert : le long d'une ligne arrondie)
float stream_distance(vec2 p) {
	vec2 q = p + stream_wobble(p) * stream_bend;
	return texture(water_field, (q + 0.5) / (vec2(map_cells) * ART)).g;
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

// La part du terrain t autour du pixel d'art p, de 0 à 1 : les quatre centres de cases autour du pixel, mélangés selon
// sa place entre eux (« marching squares »). La ligne entre deux terrains passe là où la part vaut 0,5 : au milieu
// entre deux centres pour un bord droit, en biais (à 45°) le long d'un escalier de cases, en arrondi au coin d'un
// rectangle. Le même calcul vu de toute case : le mélange se raccorde sans couture.
float part_of(int t, vec2 p) {
	vec2 g = (p + 0.5) / ART - 0.5;
	ivec2 c0 = ivec2(floor(g));
	vec2 f = g - vec2(c0);
	return mix(mix(float(terrain_at(c0) == t), float(terrain_at(c0 + ivec2(1, 0)) == t), f.x),
		mix(float(terrain_at(c0 + ivec2(0, 1)) == t), float(terrain_at(c0 + ivec2(1, 1)) == t), f.x), f.y);
}

// Le mélange au pixel d'art p : terrain de la case (a), autre terrain proche (b), et la part de b (0 à 1).
// La part de b vaut 0,5 sur la ligne ; la ligne d'un même couple de terrains est la même vue des deux
// cases, si bien que le mélange se raccorde sans couture.
void mixture(vec2 p, out int a, out int b, out float share) {
	vec2 center = p + 0.5;
	a = terrain_at(ivec2(floor(p / ART)));
	b = a;
	share = 0.0;
	// les terrains des quatre centres de cases autour du pixel
	vec2 g = center / ART - 0.5;
	ivec2 c0 = ivec2(floor(g));
	int around[4] = { terrain_at(c0), terrain_at(c0 + ivec2(1, 0)), terrain_at(c0 + ivec2(0, 1)), terrain_at(c0 + ivec2(1, 1)) };
	float w = wave(center);
	for (int i = 0; i < 4; i++) {
		int t = around[i];
		if (t == a) continue;
		bool seen = false;
		for (int j = 0; j < i; j++) seen = seen || around[j] == t;
		if (seen) continue;
		// « distance » du pixel au terrain t (pixels d'art ; négative dedans)
		float d = (0.5 - part_of(t, p)) * ART;
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

// Le terrain qui domine le pixel d'art p (pour les rives et l'écume)
int owner(vec2 p) {
	int a;
	int b;
	float share;
	mixture(p, a, b, share);
	return share >= 0.5 ? b : a;
}

// Distance du pixel d'art p (qui appartient à la terre) à la rive, en pixels d'art (très grande loin de l'eau). La rive
// est la même ligne que dans mixture() : du côté de l'eau, à edge_bias + ondulation pixels de la ligne du milieu.
float shore_distance(vec2 p) {
	float part = part_of(water, p);
	if (part <= 0.0) return 1e5;
	return (0.5 - part) * ART + edge_bias + wave(p + 0.5) * edge_wave;
}

// L'eau au pixel d'art p : un lac, la mer ou une rivière
bool water_at(vec2 p) {
	return owner(p) == water || river_depth(p) > 0.0;
}

// Une fleur (ou un caillou) au pixel d'art p, s'il y en a une : sa couleur dans « flower », et vrai
bool flower_at(vec2 p, vec3 under, out vec3 flower) {
	flower = under;
	if (flower_density <= 0.0) return false;
	float size = floor(max(flower_cell, 6.0));
	vec2 cell = floor(p / size);
	if (hash(cell + vec2(17.3, 5.1)) >= flower_density) return false;
	// la place dans le carré, assez loin de ses bords pour que pétales et ombre y restent
	vec2 spot = cell * size + 1.0 + floor(vec2(hash(cell + vec2(3.1, 9.2)), hash(cell + vec2(7.7, 1.3))) * (size - 3.0));
	vec2 d = p - spot;
	float kind = hash(cell + vec2(11.0, 23.0));
	if (kind < pebble_share) {
		// un petit caillou de 2 × 2, éclairé en haut à gauche
		if (d == vec2(0.0)) { flower = vec3(0.62, 0.62, 0.58); return true; }
		if (d == vec2(1.0, 0.0)) { flower = vec3(0.52, 0.52, 0.49); return true; }
		if (d == vec2(0.0, 1.0)) { flower = vec3(0.44, 0.44, 0.41); return true; }
		if (d == vec2(1.0, 1.0)) { flower = vec3(0.36, 0.36, 0.34); return true; }
		if (d == vec2(0.0, 2.0) || d == vec2(1.0, 2.0)) { flower = under * 0.8; return true; }
		return false;
	}
	float k = (kind - pebble_share) / (1.0 - pebble_share);
	vec3 petal = k < 0.4 ? vec3(0.96, 0.95, 0.9) : (k < 0.75 ? vec3(0.95, 0.82, 0.28) : vec3(0.68, 0.5, 0.8));
	vec3 heart = k < 0.4 ? vec3(0.9, 0.78, 0.25) : (k < 0.75 ? vec3(0.78, 0.48, 0.16) : vec3(0.96, 0.88, 0.5));
	if (d == vec2(0.0)) { flower = heart; return true; }
	if (abs(d.x) + abs(d.y) == 1.0) { flower = petal; return true; }
	if (d == vec2(0.0, 2.0)) { flower = under * 0.75; return true; }
	return false;
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

// De combien le pixel d'art p est dans la route (pixels d'art ; négatif dehors) : la distance à sa ligne médiane, sa
// largeur qui change le long du chemin et son bord cranté ; kind : la sorte de la route la plus proche
float road_inside(vec2 p, out int kind) {
	float dist = road_distance(p, kind);
	float width = road_width + (noise((p + 0.5) / road_wave_scale + vec2(71.0, 13.0)) * 2.0 - 1.0) * road_wave
		+ (noise((p + 0.5) / road_vary_scale + vec2(37.0, 83.0)) * 2.0 - 1.0) * road_vary
		+ (noise((p + 0.5) / 3.0 + vec2(13.0, 29.0)) * 2.0 - 1.0) * road_jag;
	return width - dist;
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

	// Les rivières : de l'eau sur le sol, avec les mêmes rives que les lacs et la mer (une seule sorte de rive)
	float river = river_depth(p);
	if (river > 0.0 && t != water) {
		color = tile_color(water, p);
		share = 1.0;
	}
	bool in_water = t == water || river > 0.0;

	// Rives : entre la terre et l'eau
	if (a == water || b == water || abs(river) < 3.0) {
		bool right = water_at(p + vec2(1.0, 0.0));
		bool left = water_at(p - vec2(1.0, 0.0));
		bool down = water_at(p + vec2(0.0, 1.0));
		bool up = water_at(p - vec2(0.0, 1.0));
		bool right2 = water_at(p + vec2(2.0, 0.0));
		bool left2 = water_at(p - vec2(2.0, 0.0));
		bool down2 = water_at(p + vec2(0.0, 2.0));
		bool up2 = water_at(p - vec2(0.0, 2.0));
		if (!in_water) {
			// contour de la terre contre l'eau, puis berge un peu sombre
			if (right || left || down || up)
				color *= 1.0 - outline;
			else if (beach_width <= 0.0 && (right2 || left2 || down2 || up2))
				color *= 1.0 - bank;
		} else if (foam > 0.0) {
			// écume : forte contre la rive, tramée un pixel plus loin
			if (!right || !left || !down || !up)
				color = mix(color, foam_color, foam);
			else if ((!right2 || !left2 || !down2 || !up2) && bayer(p) < 0.5)
				color = mix(color, foam_color, foam * 0.6);
		}
	}
	// Plage : une bande de sable côté terre, au bord ondulé et tramé, mouillée (plus sombre) contre l'eau
	if (!in_water && beach_width > 0.0) {
		float sd = min(shore_distance(p), -river);
		float width = beach_width + (noise((p + 0.5) / 9.0 + vec2(5.0, 61.0)) * 2.0 - 1.0) * beach_wave;
		if (sd < width + 1.0 && (sd < width - 1.0 || bayer(p) < (width + 1.0 - sd) / 2.0)) {
			vec3 beach = tile_color(sand, p);
			if (sd < 1.5) beach *= 1.0 - outline;
			else if (sd < wet_width + 1.5) beach *= 1.0 - wet;
			color = beach;
			t = sand;
		}
	}
	// Fleurs et cailloux, sur l'herbe franche (pas dans les mélanges)
	if (t == grass && !in_water && (share <= 0.0 || share >= 1.0)) {
		vec3 flower;
		if (flower_at(p, color, flower)) color = flower;
	}
	// Les ruisseaux (pas dans l'eau où ils se jettent) : l'eau, une écume contre la rive, un contour sombre
	float depth = stream_width + (noise((p + 0.5) / 7.0 + vec2(5.0, 91.0)) * 2.0 - 1.0) * stream_wave - stream_distance(p);
	if (!in_water && depth > -1.5) {
		if (depth <= 0.0) color *= 1.0 - outline;
		else {
			color = tile_color(water, p);
			if (depth < 1.5) color = mix(color, foam_color, foam);
			else if (depth < 2.5 && bayer(p) < 0.5) color = mix(color, foam_color, foam * 0.6);
		}
	}
	// La route, par-dessus le sol
	int road_kind = 1;
	float inside = road_inside(p, road_kind); // positif sur la route
	float spread = max(road_scatter, 0.01);
	int other_kind;
	// le talus d'en face, au soleil : un liseré plus clair sur l'herbe
	if (road_rim > 0.0 && inside < 0.0 && inside > -2.0 && road_inside(p - sun * 2.0, other_kind) > 0.0)
		color *= 1.0 + road_rim;
	if (inside > -spread * 0.5 && hash(p + vec2(0.37, 0.71)) < clamp(inside / spread + 0.5, 0.0, 1.0)) {
		vec3 road = texelFetch(road_tiles, ivec3(ivec2(mod(p, ART)), max(road_kind, 1) - 1), 0).rgb;
		if (inside < 1.5)
			road *= 1.0 - road_shade;
		// l'ombre du talus du côté du soleil, douce, en paliers tramés comme le reste
		if (road_shadow > 0.0 && inside < road_width * 2.0 + 2.0) {
			float shadow = 0.0;
			for (float k = 1.0; k <= 6.0; k += 1.0) {
				if (k > road_shadow_length) break;
				if (road_inside(p + sun * k, other_kind) < 0.0) {
					shadow = 1.0 - (k - 1.0) / road_shadow_length;
					break;
				}
			}
			shadow = clamp(floor(shadow * 3.0 + bayer(p)) / 3.0, 0.0, 1.0);
			road *= 1.0 - road_shadow * shadow;
		}
		color = road;
	}
	// la terre battue, par endroits, à cheval sur le bord : son épaisseur change le long de la route, on voit au travers
	float frame = road_frame * smoothstep(1.0 - road_frame_share - 0.1, 1.0 - road_frame_share + 0.15,
		noise((p + 0.5) / 9.0 + vec2(91.0, 7.0)));
	if (frame > 0.3 && inside > -frame * 0.6 && inside < frame) {
		vec3 soil = texelFetch(road_tiles, ivec3(ivec2(mod(p, ART)), 0), 0).rgb * road_frame_dark;
		float alpha = road_kind == 3 ? road_soil_cobbles : (road_kind == 2 ? road_soil_gravel : road_soil_dirt);
		float light = dot(color, vec3(0.299, 0.587, 0.114));
		float low = 1.0 - smoothstep(0.2, 0.6, light);
		alpha *= (1.0 - road_soil_low + road_soil_low * low) * (0.6 + 0.4 * hash(p + vec2(5.3, 1.9)));
		// la terre, ramenée à une teinte claire, multiplie la couleur dessous
		vec3 tint = clamp(soil / max(dot(soil, vec3(0.299, 0.587, 0.114)), 0.05) * road_soil_tint, 0.0, 1.0);
		color = mix(color, color * tint, alpha);
	}
	// Les ponts : sur les deux cases d'un pont de rivière, en travers de la bande, et au-delà tant qu'il y a de l'eau (la
	// rivière lissée déborde un peu des cases) ; là où une route croise un ruisseau, au-dessus de l'eau
	ivec2 here = ivec2(floor(p / ART));
	int r = river_at(here);
	// le sens du pont de rivière (ses deux cases à la suite), vu de ses cases ou d'une case au bout
	bool deck = false;
	bool across_y = false;
	if (r == 3) {
		deck = true;
		bool bridge_x = river_at(here + ivec2(1, 0)) == 3 || river_at(here + ivec2(-1, 0)) == 3;
		bool bridge_y = river_at(here + ivec2(0, 1)) == 3 || river_at(here + ivec2(0, -1)) == 3;
		across_y = bridge_y || (!bridge_x && (river_at(here + ivec2(1, 0)) >= 2 || river_at(here + ivec2(-1, 0)) >= 2));
	} else if (in_water && r != 1) {
		ivec2 dirs[4] = { ivec2(1, 0), ivec2(-1, 0), ivec2(0, 1), ivec2(0, -1) };
		for (int i = 0; i < 4; i++)
			if (river_at(here + dirs[i]) == 3 && river_at(here + dirs[i] * 2) == 3) {
				deck = true;
				across_y = dirs[i].y != 0;
			}
	} else if (r == 1 && road_at(here) && depth > -2.0) {
		deck = true;
		across_y = river_at(here + ivec2(1, 0)) == 1 || river_at(here + ivec2(-1, 0)) == 1;
	}
	if (deck) {
		vec2 off = p + 0.5 - (vec2(here) + 0.5) * ART;
		float side = abs(across_y ? off.x : off.y);
		float along = across_y ? p.y : p.x;
		if (side < bridge_width) {
			vec3 plank = bridge_color;
			if (mod(along, 4.0) < 1.0) plank *= 0.8; // les joints des planches
			if (side > bridge_width - 1.5) plank *= 0.6; // le bord
			color = plank;
		} else if (side < bridge_width + 1.0 && (in_water || depth > 0.0))
			color *= 0.7; // l'ombre du pont sur l'eau
	}
	COLOR = vec4(color, 1.0);
}
