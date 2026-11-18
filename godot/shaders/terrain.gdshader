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
// Le pied des montagnes (MountainField, rouge : la distance à la case de montagne la plus proche, en cases)
uniform sampler2D mountain_field : filter_linear;
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
// La rive des lacs et de la mer : arrondie (0 : elle suit les cases, en biais à 45° ; 1 : toute en courbes), et son
// ondulation en plus de edge_wave
uniform float shore_round = 0.8;
uniform float shore_wave = 1.0;
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
// de petits cailloux le long du bord des routes : la part des carrés de road_stone_cell pixels d'art (au bord) qui en ont un
uniform float road_stones = 0.3;
// et quelques-uns sur la route elle-même (la part des carrés au milieu de la route)
uniform float road_stones_inside = 0.08;
// des cailloux semés partout sur le sol, loin des routes (la part des carrés de ground_stone_cell pixels d'art qui en ont)
uniform float ground_stones = 0.08;
uniform float ground_stone_cell = 11.0;
// le sol rocheux (rock_ground : son numéro) : de grandes dalles (slab_cell pixels d'art) de tons un peu différents
// (slab_tone), séparées par des fissures (slab_crack de large, sur la part slab_cracked des bords : pas un pavage) ; le
// bord éclairé d'une fissure (slab_light), son fond (slab_dark) ; slab_wobble tord les bords. Des pierres par-dessus
// (la part rock_stones des carrés de rock_stone_cell pixels d'art)
uniform int rock_ground = 7;
uniform float slab_cell = 20.0;
uniform float slab_crack = 0.8;
uniform float slab_cracked = 0.55;
uniform float slab_tone = 0.05;
uniform float slab_dark = 0.83;
uniform float slab_light = 1.04;
uniform float slab_wobble = 3.0;
uniform float rock_stones = 0.16;
uniform float rock_stone_cell = 13.0;
// Le pied des montagnes (0 : rien ; 0,35 sans terre ni gravier, 2026-11-17 : les pentes se fondent dans l'herbe) : jusqu'à foothill_reach cases d'une montagne, l'herbe sèche (foothill),
// des plaques de terre et de gravier (foothill_soil : leur part au pied), des éboulis (foothill_scree : la part des
// carrés de 6 pixels d'art qui ont une pierre, au pied) ; le bord ondule de foothill_wave cases
uniform float foothill = 0.35;
uniform float foothill_reach = 1.2;
uniform float foothill_soil = 0.0;
uniform float foothill_scree = 0.12;
uniform float foothill_wave = 0.5;
// Le sol au cœur des montagnes (2026-11-17) : le sol de la montagne (l'herbe, le sable ou le marais qui l'entoure),
// assombri, dans l'ombre des massifs, pour que le sol vu entre deux images se lise comme un creux de la pente et pas
// comme un trou (0 : la terre et le gravier du pied ; foothill_rock_shade : la part de lumière gardée)
uniform float foothill_rock = 0.0;
uniform float foothill_rock_shade = 0.62;
uniform float road_stone_cell = 6.0;
// l'herbe qui déborde sur la route (à l'essai, 0 : rien) : par plaques le long du bord (road_grass_share du bord, plaques
// d'environ road_grass_scale pixels d'art), l'herbe avance de road_grass pixels d'art au plus, en brins de longueurs
// inégales ; de petites touffes posées sur la route (la part road_tufts des carrés de road_tuft_cell pixels d'art près du
// bord, road_tufts_middle au milieu) ; l'herbe jette une petite ombre vers le haut et la gauche (road_grass_shadow)
uniform float road_grass = 2.2;
uniform float road_grass_share = 0.45;
uniform float road_grass_scale = 10.0;
uniform float road_tufts = 0.3;
uniform float road_tufts_middle = 0.04;
uniform float road_tuft_cell = 7.0;
uniform float road_grass_shadow = 0.18;
// l'herbe change le long des routes, par tronçons d'environ road_grass_stretch pixels d'art : sous road_grass_bare (un
// bruit doux de 0 à 1), aucune herbe ; au-dessus de road_grass_lush, une route plus envahie (road_grass_more fois plus)
uniform float road_grass_stretch = 70.0;
uniform float road_grass_bare = 0.32;
uniform float road_grass_lush = 0.68;
uniform float road_grass_more = 1.6;
// sur le sable : la route et le sable se mêlent sur plus de pixels (road_sand_scatter, au lieu de road_scatter) ; le sable
// porté par le vent recouvre le bord de la route en dunes douces (road_sand_drift pixels d'art au plus, des dunes
// d'environ road_sand_drift_scale pixels, selon le tronçon comme l'herbe), en fine couche tramée à son bout ; le sable
// repoussé forme un petit bourrelet plus clair le long du bord (road_sand_berm, sur road_sand_berm_width pixels)
uniform float road_sand_scatter = 4.0;
uniform float road_sand_drift = 3.5;
uniform float road_sand_drift_scale = 14.0;
// la part du bord que le sable recouvre (environ)
uniform float road_sand_drift_share = 0.6;
uniform float road_sand_berm = 0.08;
uniform float road_sand_berm_width = 3.0;
// la route s'élargit et se rétrécit doucement le long de son chemin : de combien (pixels d'art), sur quelle longueur
uniform float road_vary = 0.5;
uniform float road_vary_scale = 12.0;
// Plages : largeur de la bande de sable côté terre (pixels d'art, 0 = aucune), ondulation de son bord,
// sable mouillé (assombrissement) sur les derniers pixels avant l'eau
uniform float beach_width = 11.0;
uniform float beach_wave = 2.0;
uniform float wet = 0.15;
uniform float wet_width = 2.0;
// Les rives changent par tronçons d'environ bank_stretch pixels d'art (un bruit doux de 0 à 1) : sous bank_grass,
// l'herbe descend jusqu'à l'eau, bordée d'une ligne de terre mouillée (mud_width pixels) ; au-dessus, la plage, plus ou
// moins large ; au-dessus de bank_pebbles, une plage de galets (la part pebbles des carrés de 4 pixels a un galet)
uniform float bank_stretch = 80.0;
uniform float bank_grass = 0.3;
uniform float bank_pebbles = 0.72;
uniform float pebbles = 0.35;
uniform float mud_width = 2.0;
uniform vec3 mud_color = vec3(0.36, 0.29, 0.2);
// La profondeur de l'eau : près des rives, sur shallow_width pixels d'art, une eau claire (mêlée de shallow à
// shallow_color) ; au large, de deep_start à deep_end pixels de la terre, l'eau fonce (mêlée de deep à deep_color), en
// paliers tramés
uniform float shallow_width = 5.0;
uniform float shallow = 0.25;
uniform vec3 shallow_color = vec3(0.45, 0.78, 0.82);
uniform float deep_start = 10.0;
uniform float deep_end = 76.0;
uniform float deep = 0.35;
uniform vec3 deep_color = vec3(0.08, 0.2, 0.42);
// des roseaux au bord de l'eau, en touffes, là où l'herbe descend à l'eau (la part reeds des carrés de 5 pixels d'art
// au bord, dans les endroits choisis par un bruit doux de reed_scale pixels) ; des rochers dans l'eau peu profonde (la
// part rocks des carrés de 12 pixels près de la rive), une écume autour
uniform float reeds = 0.6;
uniform float reed_share = 0.45;
uniform float reed_scale = 24.0;
// sur les plages de sable (pas de galets), la part reed_sand des touffes de roseaux reste
uniform float reed_sand = 0.3;
// L'herbe sur les plages : de petites plaques d'herbe au milieu du sable, ni contre l'eau ni contre l'herbe (un bruit
// doux de beach_patch_scale pixels ; plus beach_patches est grand, plus il y en a), et des touffes semées sur le sable
// (la part beach_tufts des carrés de 6 pixels, plus côté terre ; deux touffes côte à côte)
uniform float beach_patches = 0.3;
uniform float beach_patch_scale = 5.0;
uniform float beach_tufts = 0.25;
uniform float rocks = 0.08;
// Fleurs sur l'herbe : l'herbe est découpée en carrés de « flower_cell » pixels d'art ; une part « flower_density » de
// ces carrés porte une fleur (un cœur, quatre pétales et un pixel d'ombre) ou, plus rarement, un petit caillou
uniform float flower_density = 0.05;
uniform float flower_cell = 9.0;
uniform float pebble_share = 0.2;
// Grandes zones un peu plus claires ou plus sombres : force et taille (pixels d'art)
uniform float shade = 0.08;
uniform float shade_scale = 70.0;
// L'herbe (2026-11-16, choisie par l'utilisateur) : la teinte de base (grass_depth : < 1 plus foncée) ; des taches de couleur
// d'environ grass_tint_scale pixels d'art, herbe sèche (grass_dry, multiplie la couleur) et herbe drue (grass_lush),
// de force grass_tint ; des touffes (une part grass_clumps des carrés de grass_clump_cell pixels), éclairées d'en bas à
// droite, une ombre à gauche ; des bouquets de fleurs (une part flower_patches des carrés de 22 pixels, 3 à 7 fleurs
// d'une couleur)
uniform float grass_depth = 0.92;
uniform float grass_tint = 0.6;
uniform float grass_tint_scale = 40.0;
uniform vec3 grass_dry = vec3(1.28, 1.06, 0.78);
uniform vec3 grass_lush = vec3(0.72, 0.86, 0.78);
uniform float grass_clumps = 0.2;
uniform float grass_clump_cell = 7.0;
// les touffes se rassemblent (0 : partout pareil ; 1 : par endroits, d'environ grass_clump_gather_scale pixels d'art,
// aucune ailleurs)
uniform float grass_clump_gather = 0.0;
uniform float grass_clump_gather_scale = 28.0;
uniform float flower_patches = 0.1;

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
// Les ruisseaux : des mares où ils s'élargissent (stream_pools pixels d'art de plus), une eau claire contre le bord
// (stream_shallow) et plus sombre au milieu (stream_deep), une rive de terre mouillée (stream_mud, sur mud_width pixels),
// des bancs de sable par endroits (stream_bars : leur part), des pierres dans l'eau avec de l'écume (stream_stones : la
// part des carrés de 8 pixels), des roseaux (stream_reeds : la part des carrés de 5 pixels du bord), et un éventail de
// sable où ils se jettent dans un lac, la mer ou une rivière (stream_mouth : sa largeur en pixels)
uniform float stream_pools = 2.5;
uniform float stream_shallow = 0.3;
uniform float stream_deep = 0.25;
uniform float stream_mud = 0.5;
uniform float stream_bars = 0.3;
uniform float stream_stones = 0.12;
uniform float stream_reeds = 0.25;
uniform float stream_mouth = 9.0;
// à son embouchure, le ruisseau s'élargit (stream_delta pixels d'art de plus) et se partage autour d'îlots de sable
// (stream_islets : leur part), et une eau claire en éventail dans l'eau où il se jette
uniform float stream_delta = 2.0;
uniform float stream_islets = 0.5;
uniform vec3 bridge_color = vec3(0.56, 0.4, 0.24);
// Les ponts dessinés en images par le jeu (BridgeLayer), où le shader ne dessine plus de planches : rivières 1 d'ouest en
// est, 2 du nord au sud ; ruisseaux 4 et 8
uniform int bridge_art = 0;

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

// De combien de pixels d'art le pixel p est dans un ruisseau (négatif : sur la rive), avec ses mares
float stream_depth(vec2 p) {
	float pool = stream_pools * smoothstep(0.62, 0.8, noise((p + 0.5) / 34.0 + vec2(17.0, 29.0)));
	return stream_width + pool + (noise((p + 0.5) / 7.0 + vec2(5.0, 91.0)) * 2.0 - 1.0) * stream_wave
		- stream_distance(p);
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

// La part de l'eau autour du pixel d'art p : celle de part_of, mêlée (shore_round) à une courbe douce sur les neuf
// cases autour (B-spline quadratique), qui arrondit les coins et les biais de l'escalier des cases. Le bord d'une
// rangée droite de cases reste au milieu entre leurs centres.
float water_part(vec2 p) {
	float flat_part = part_of(water, p);
	if (shore_round <= 0.0) return flat_part;
	vec2 g = (p + 0.5) / ART - 0.5;
	ivec2 c = ivec2(round(g));
	vec2 t = g - vec2(c);
	vec3 wx = vec3(0.5 * (0.5 - t.x) * (0.5 - t.x), 0.75 - t.x * t.x, 0.5 * (0.5 + t.x) * (0.5 + t.x));
	vec3 wy = vec3(0.5 * (0.5 - t.y) * (0.5 - t.y), 0.75 - t.y * t.y, 0.5 * (0.5 + t.y) * (0.5 + t.y));
	float smooth_part = 0.0;
	for (int y = -1; y <= 1; y++)
		for (int x = -1; x <= 1; x++)
			smooth_part += wx[x + 1] * wy[y + 1] * float(terrain_at(c + ivec2(x, y)) == water);
	return mix(flat_part, smooth_part, shore_round);
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
		// (la rive : la part de l'eau arrondie, vue des deux côtés)
		bool shore = t == water || a == water;
		float d = (shore ? (t == water ? 0.5 - water_part(p) : water_part(p) - 0.5) : 0.5 - part_of(t, p)) * ART;
		// la ligne avance dans cette case si le voisin est au-dessus, recule sinon
		float side = priority[t] > priority[a] ? 1.0 : -1.0;
		float line = side * (edge_bias + w * edge_wave * (shore ? shore_wave : 1.0));
		float gap = d - line;
		float s;
		if (shore) {
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
	float part = water_part(p);
	if (part <= 0.0) return 1e5;
	return (0.5 - part) * ART + edge_bias + wave(p + 0.5) * edge_wave * shore_wave;
}

// La sorte de rive autour du pixel d'art p : un bruit doux de 0 à 1 (voir bank_grass, bank_pebbles)
float bank_kind(vec2 p) {
	return noise((p + 0.5) / bank_stretch + vec2(13.0, 77.0));
}

// De combien de pixels d'art le pixel p (dans l'eau) est loin de la terre : près des rives, la rive exacte (celle des
// lacs, de la mer et des rivières) ; plus loin, l'image des distances (water_field, bleu), plus grossière
float water_depth(vec2 p, float river) {
	float d = max(river, 0.0);
	if (water_part(p) > 0.0) {
		float lake = -shore_distance(p);
		if (lake > shallow_width + 3.0)
			lake = max(lake, texture(water_field, (p + 0.5) / (vec2(map_cells) * ART)).b);
		d = max(d, lake);
	}
	return d;
}

// L'eau au pixel d'art p : un lac, la mer ou une rivière
bool water_at(vec2 p) {
	return owner(p) == water || river_depth(p) > 0.0 || stream_depth(p) > 0.0;
}

// La taille d'un caillou, tirée pour son carré : 1 × 1, 2 × 1, 2 × 2, 3 × 2 ou 3 × 3 pixels d'art
vec2 stone_size(vec2 cell) {
	float pick = hash(cell + vec2(9.4, 7.7));
	return pick < 0.25 ? vec2(1.0) : (pick < 0.45 ? vec2(2.0, 1.0) : (pick < 0.75 ? vec2(2.0) : (pick < 0.9 ? vec2(3.0, 2.0) : vec2(3.0))));
}

// Le pixel d de la forme d'un caillou de taille dim (d : depuis son coin en haut à gauche) : sa couleur dans « stone ».
// Éclairé comme la carte : le soleil vient de la droite et un peu d'en bas (les ombres des objets vont vers la gauche,
// un peu vers le haut) ; son ombre, une colonne à sa gauche, remontée d'un pixel
bool stone_shape(vec2 d, vec2 dim, vec2 cell, vec3 under, out vec3 stone) {
	stone = under;
	if (d.x >= 0.0 && d.y >= 0.0 && d.x < dim.x && d.y < dim.y) {
		float lit = (dim.x > 1.0 ? d.x / (dim.x - 1.0) : 0.5) * 0.75 + (dim.y > 1.0 ? d.y / (dim.y - 1.0) : 0.5) * 0.25;
		float tone = hash(cell + vec2(1.3, 5.9)) * 0.08;
		stone = vec3(0.48 + tone) + vec3(0.3, 0.29, 0.26) * lit;
		return true;
	}
	if (d.x == -1.0 && d.y >= -1.0 && d.y < dim.y - 1.0) {
		stone = under * 0.68;
		return true;
	}
	return false;
}

// De combien de pixels d'art le pixel p est sur la terre (négatif : dans l'eau), près des rives des lacs, de la mer et
// des rivières
float shore_side(vec2 p) {
	return min(shore_distance(p), -river_depth(p));
}

// Des roseaux au pixel d'art p : 0 rien, 1 le pied (sombre), 2 la tige, 3 la pointe (claire), 4 un épi brun. Chaque carré
// de 5 pixels peut avoir une touffe, son pied sur la ligne de la rive ; ses trois à quatre tiges montent de 4 à 7 pixels
// (stream : au bord des ruisseaux, la part stream_reeds des carrés, au lieu des lacs, de la mer et des rivières)
int reed_at(vec2 p, bool stream) {
	float size = 5.0;
	for (float row = 0.0; row < 3.0; row += 1.0) {
		vec2 cell = floor(p / size) + vec2(0.0, row);
		if (hash(cell + vec2(5.7, 21.1)) >= (stream ? stream_reeds : reeds)) continue;
		vec2 base = cell * size + vec2(floor(hash(cell + vec2(2.2, 7.4)) * size), size - 1.0);
		if (noise((base + 0.5) / reed_scale + vec2(33.0, 9.0)) < 1.0 - reed_share) continue;
		float side;
		if (stream) side = -stream_depth(base);
		else {
			float stretch = bank_kind(base);
			if (stretch > bank_pebbles - 0.04) continue;
			if (stretch > bank_grass + 0.15 && hash(cell + vec2(12.6, 3.3)) >= reed_sand) continue;
			side = shore_side(base);
		}
		if (side < -3.0 || side > 1.5) continue;
		vec2 q = p - base;
		if (q.y > 0.0 || q.y < -7.0) continue;
		for (int i = -1; i <= 2; i++) {
			float h = 4.0 + floor(hash(cell + vec2(float(i) * 3.1, 4.6)) * 4.0);
			float x = float(i) + (q.y < -h * 0.6 && i != 0 ? sign(float(i)) : 0.0);
			if (q.x == x && -q.y < h) {
				if (q.y == 0.0) return 1;
				if (-q.y >= h - 2.0 && i == 0 && hash(cell + vec2(8.8, 1.1)) < 0.5) return 4;
				return -q.y >= h - 1.0 ? 3 : 2;
			}
		}
	}
	return 0;
}

// Un rocher dans l'eau peu profonde au pixel d'art p : sa couleur dans « rock » (et l'écume autour), et vrai
bool rock_at(vec2 p, vec3 under, out vec3 rock) {
	rock = under;
	float size = 12.0;
	vec2 cell = floor(p / size);
	if (hash(cell + vec2(61.3, 7.9)) >= rocks) return false;
	vec2 spot = cell * size + 3.0 + floor(vec2(hash(cell + vec2(1.9, 3.3)), hash(cell + vec2(4.7, 2.1))) * (size - 7.0));
	float side = shore_side(spot);
	if (side > -2.0 || side < -9.0) return false;
	vec2 dim = hash(cell + vec2(3.9, 9.4)) < 0.5 ? vec2(3.0, 2.0) : vec2(2.0);
	if (stone_shape(p - spot, dim, cell, under, rock)) return true;
	vec2 d = p - spot;
	// l'écume autour du rocher, sur l'eau
	if (d.x >= -1.0 && d.x <= dim.x && d.y >= -1.0 && d.y <= dim.y) {
		rock = mix(under, foam_color, foam * 0.8);
		return true;
	}
	return false;
}

// Une pierre dans un ruisseau au pixel d'art p : sa couleur dans « stone » (et l'écume autour), et vrai
bool stream_stone_at(vec2 p, vec3 under, out vec3 stone) {
	stone = under;
	float size = 8.0;
	vec2 cell = floor(p / size);
	if (hash(cell + vec2(37.1, 15.2)) >= stream_stones) return false;
	vec2 spot = cell * size + 2.0 + floor(vec2(hash(cell + vec2(8.1, 2.7)), hash(cell + vec2(5.5, 9.3))) * 4.0);
	if (stream_depth(spot) < 2.0) return false;
	vec2 dim = hash(cell + vec2(2.6, 6.2)) < 0.5 ? vec2(2.0, 1.0) : vec2(1.0);
	if (stone_shape(p - spot, dim, cell, under, stone)) return true;
	vec2 d = p - spot;
	if (d.x >= -1.0 && d.x <= dim.x && d.y >= -1.0 && d.y <= dim.y) {
		stone = mix(under, foam_color, foam);
		return true;
	}
	return false;
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
		// un petit caillou de 2 × 2, éclairé comme la carte
		return stone_shape(d, vec2(2.0), cell, under, flower);
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
	if (t == grass) {
		color *= grass_depth;
		if (grass_tint > 0.0) {
			// deux bruits doux : le sec et le dru ne se recouvrent pas ; deux niveaux, au bord tramé
			float dry = smoothstep(0.55, 0.75, noise((p + 0.5) / grass_tint_scale + vec2(23.0, 71.0)));
			float lush = smoothstep(0.55, 0.75, noise((p + 0.5) / (grass_tint_scale * 0.8) + vec2(88.0, 14.0))) * (1.0 - dry);
			dry = floor(dry * 2.0 + bayer(p)) / 2.0;
			lush = floor(lush * 2.0 + bayer(p + 2.0)) / 2.0;
			color = mix(color, color * grass_dry, dry * grass_tint);
			color = mix(color, color * grass_lush, lush * grass_tint);
		}
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

// Un petit caillou au bord d'une route au pixel d'art p, s'il y en a un : sa couleur dans « stone », et vrai. Un caillou
// de 2 × 2 (parfois 3 × 2) pixels dans un carré sur deux ou trois, posé sur la ligne du bord (à moitié sur la route),
// éclairé d'en bas à droite comme le reste de la carte, une petite ombre vers le haut et la gauche
bool road_stone_at(vec2 p, vec3 under, out vec3 stone) {
	stone = under;
	float size = floor(max(road_stone_cell, 5.0));
	vec2 cell = floor(p / size);
	float chance = hash(cell + vec2(41.7, 3.3));
	if (chance >= max(road_stones, road_stones_inside)) return false;
	vec2 spot = cell * size + 1.0 + floor(vec2(hash(cell + vec2(2.9, 8.1)), hash(cell + vec2(6.2, 4.4))) * (size - 4.0));
	int kind;
	float edge = road_inside(spot, kind);
	bool on_edge = edge >= -3.0 && edge <= 1.5;
	if (!(on_edge && chance < road_stones) && !(edge > 1.5 && chance < road_stones_inside)) return false;
	return stone_shape(p - spot, stone_size(cell), cell, under, stone);
}

// Un caillou semé sur le sol au pixel d'art p (loin des routes), s'il y en a un
bool ground_stone_at(vec2 p, vec3 under, out vec3 stone) {
	stone = under;
	float size = floor(max(ground_stone_cell, 6.0));
	vec2 cell = floor(p / size);
	if (hash(cell + vec2(73.1, 19.9)) >= ground_stones) return false;
	vec2 spot = cell * size + 1.0 + floor(vec2(hash(cell + vec2(8.6, 2.2)), hash(cell + vec2(3.7, 6.6))) * (size - 4.0));
	int kind;
	if (road_inside(spot, kind) > -4.0) return false;
	return stone_shape(p - spot, stone_size(cell + vec2(0.5)), cell, under, stone);
}

// La distance du pixel d'art p au bord de sa dalle (pixels d'art, à peu près) ; id : la dalle
float slab_edge(vec2 p, out vec2 id) {
	vec2 twist = vec2(noise(p / 9.0 + vec2(41.0, 7.0)), noise(p / 9.0 + vec2(5.0, 63.0))) - 0.5;
	vec2 g = (p + twist * 2.0 * slab_wobble) / slab_cell;
	vec2 base = floor(g);
	float first = 1e9;
	float second = 1e9;
	id = base;
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 n = base + vec2(float(x), float(y));
		float d = length(g - n - 0.05 - vec2(hash(n + vec2(3.1, 8.2)), hash(n + vec2(6.4, 1.7))) * 0.9);
		if (d < first) {
			second = first;
			first = d;
			id = n;
		} else if (d < second) second = d;
	}
	return (second - first) * slab_cell * 0.5;
}

// Une fissure du sol rocheux au pixel d'art p ?
bool crack_at(vec2 p) {
	vec2 id;
	return slab_edge(p, id) < slab_crack && noise(p / (slab_cell * 0.8) + vec2(12.0, 77.0)) < slab_cracked;
}

// Une pierre du sol rocheux au pixel d'art p (plus grosses que les cailloux, loin des routes)
bool rock_stone_at(vec2 p, vec3 under, out vec3 stone) {
	stone = under;
	float size = floor(max(rock_stone_cell, 8.0));
	vec2 cell = floor(p / size);
	if (hash(cell + vec2(31.7, 52.3)) >= rock_stones) return false;
	vec2 spot = cell * size + 1.0 + floor(vec2(hash(cell + vec2(4.9, 7.3)), hash(cell + vec2(9.1, 3.8))) * (size - 6.0));
	int kind;
	if (road_inside(spot, kind) > -4.0) return false;
	float pick = hash(cell + vec2(6.2, 8.8));
	vec2 dim = pick < 0.35 ? vec2(3.0, 2.0) : (pick < 0.65 ? vec2(3.0) : (pick < 0.88 ? vec2(4.0, 3.0) : vec2(5.0, 3.0)));
	return stone_shape(p - spot, dim, cell, under, stone);
}

// Combien d'herbe sur ce tronçon de route, autour du pixel d'art p : 0 aucune, 1 l'ordinaire, road_grass_more envahie
float grass_amount(vec2 p) {
	float n = noise((p + 0.5) / road_grass_stretch + vec2(61.0, 29.0));
	return smoothstep(road_grass_bare - 0.04, road_grass_bare + 0.04, n)
		* mix(1.0, road_grass_more, smoothstep(road_grass_lush - 0.04, road_grass_lush + 0.04, n));
}

// De combien de pixels d'art l'herbe avance sur la route au pixel d'art p : par plaques le long du bord, en brins (chaque
// colonne de pixels, par bouts de trois, prend une longueur au hasard)
float grass_reach(vec2 p) {
	float amount = grass_amount(p);
	if (amount <= 0.0) return 0.0;
	float share = min(road_grass_share * min(amount, 1.35), 0.9);
	float clump = smoothstep(1.0 - share - 0.1, 1.0 - share + 0.25,
		noise((p + 0.5) / road_grass_scale + vec2(17.0, 53.0)));
	float row = floor((p.y + floor(hash(vec2(p.x, 7.0)) * 3.0)) / 3.0);
	return road_grass * amount * clump * (0.35 + 0.65 * hash(vec2(p.x, row) + vec2(3.1, 9.7)));
}

// Le pixel q (depuis le pied d'une touffe) dans cette touffe : 0 dehors, 1 le pied, 2 un brin, 3 une pointe. Trois brins,
// celui du milieu plus haut, ceux des côtés penchés vers l'extérieur
int tuft_pixel(vec2 q, vec2 cell) {
	if (q.y > 0.0 || q.y < -2.0 || abs(q.x) > 2.0) return 0;
	for (int i = -1; i <= 1; i++) {
		float b = float(i);
		float h = i == 0 ? 2.0 + floor(hash(cell + vec2(1.3, 5.1)) * 2.0) : 1.0 + floor(hash(cell + vec2(b * 2.7, 8.3)) * 2.0);
		for (float r = 0.0; r < 3.0; r += 1.0) {
			if (r >= h) break;
			if (q == vec2(b + (r >= 2.0 ? b : 0.0), -r))
				return r == 0.0 ? 1 : (r == h - 1.0 ? 3 : 2);
		}
	}
	return 0;
}

// Une touffe dans l'herbe au pixel d'art p, s'il y en a une : sa couleur dans « tuft », et vrai. Ses brins (tuft_pixel),
// sombres au pied et clairs au bout, parfois doublés ; l'ombre à leur gauche
bool grass_clump_at(vec2 p, vec3 under, out vec3 tuft) {
	tuft = under;
	if (grass_clumps <= 0.0) return false;
	float size = floor(max(grass_clump_cell, 6.0));
	vec2 cell = floor(p / size);
	float gather = mix(1.0, 2.0 * smoothstep(0.4, 0.7, noise((cell * size + 0.5) / grass_clump_gather_scale + vec2(6.0, 44.0))),
		grass_clump_gather);
	if (hash(cell + vec2(31.7, 4.9)) >= grass_clumps * gather) return false;
	vec2 spot = cell * size + vec2(2.0 + floor(hash(cell + vec2(5.3, 2.2)) * (size - 4.0)),
		3.0 + floor(hash(cell + vec2(9.1, 6.4)) * (size - 3.0)));
	vec2 twin = spot + vec2(2.0, 1.0);
	// trois formes : un brin seul (le brin du milieu), une touffe, une touffe doublée
	float shape = hash(cell + vec2(2.6, 8.8));
	bool lone = shape < 0.35;
	bool two = shape > 0.75;
	vec2 q = p - spot;
	int blade = max(lone && q.x != 0.0 ? 0 : tuft_pixel(q, cell), two ? tuft_pixel(p - twin, cell + 3.0) : 0);
	if (blade == 1) { tuft = under * 0.68; return true; }
	if (blade == 2) { tuft = under * 0.86; return true; }
	if (blade == 3) { tuft = under * vec3(1.22, 1.2, 1.0); return true; }
	if ((tuft_pixel(p + vec2(1.0, 0.0) - spot, cell) > 0 && (!lone || q.x == -1.0)) || (two && tuft_pixel(p + vec2(1.0, 0.0) - twin, cell + 3.0) > 0)) {
		tuft = under * 0.84;
		return true;
	}
	return false;
}

// Un bouquet de fleurs au pixel d'art p, s'il y en a un : de 3 à 7 fleurs d'une couleur, serrées dans le carré
bool flower_patch_at(vec2 p, vec3 under, out vec3 flower) {
	flower = under;
	if (flower_patches <= 0.0) return false;
	float size = 22.0;
	vec2 cell = floor(p / size);
	if (hash(cell + vec2(51.3, 17.9)) >= flower_patches) return false;
	vec2 center = cell * size + 7.0 + floor(vec2(hash(cell + vec2(1.9, 6.6)), hash(cell + vec2(4.2, 3.1))) * (size - 14.0));
	float k = hash(cell + vec2(7.4, 2.8));
	vec3 petal = k < 0.35 ? vec3(0.96, 0.95, 0.9) : (k < 0.65 ? vec3(0.95, 0.82, 0.28) : (k < 0.85 ? vec3(0.68, 0.5, 0.8) : vec3(0.9, 0.42, 0.38)));
	int count = 3 + int(floor(hash(cell + vec2(8.3, 0.7)) * 5.0));
	for (int i = 0; i < 7; i++) {
		if (i >= count) break;
		vec2 at = center + floor(vec2(hash(cell + vec2(float(i) * 3.7, 1.1)), hash(cell + vec2(2.3, float(i) * 5.3))) * vec2(9.0, 7.0)) - vec2(4.0, 3.0);
		vec2 d = p - at;
		// une fleur sur deux d'un seul pixel, les autres en croix ; un pixel d'ombre dessous
		bool small = hash(cell + vec2(float(i), 9.9)) < 0.5;
		if (d == vec2(0.0)) { flower = small ? petal : petal * 0.85 + 0.12; return true; }
		if (!small && abs(d.x) + abs(d.y) == 1.0 && d.y <= 0.0) { flower = petal; return true; }
		if (d == vec2(-1.0, 1.0)) { flower = under * 0.75; return true; }
	}
	return false;
}

// Une petite touffe d'herbe sur la route près du pixel d'art p : 0 rien, 1 à 3 la touffe (tuft_pixel), 4 son ombre
int road_tuft_at(vec2 p) {
	float size = floor(max(road_tuft_cell, 6.0));
	vec2 cell = floor(p / size);
	float chance = hash(cell + vec2(29.3, 61.7)) / max(grass_amount(cell * size), 0.001);
	if (chance >= max(road_tufts, road_tufts_middle)) return 0;
	vec2 spot = cell * size + vec2(3.0 + floor(hash(cell + vec2(4.4, 1.2)) * (size - 5.0)),
		3.0 + floor(hash(cell + vec2(7.7, 3.9)) * (size - 3.0)));
	int kind;
	float edge = road_inside(spot, kind);
	if (!(edge > 0.5 && edge <= 3.0 && chance < road_tufts) && !(edge > 3.0 && chance < road_tufts_middle)) return 0;
	int part = tuft_pixel(p - spot, cell);
	if (part > 0) return part;
	return tuft_pixel(p + 1.0 - spot, cell) > 0 ? 4 : 0;
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

	// La profondeur de l'eau : claire près des rives, sombre au large
	if (in_water && (shallow > 0.0 || deep > 0.0)) {
		float d = water_depth(p, river);
		if (d < shallow_width - 1.0 || (d < shallow_width + 1.0 && bayer(p) < (shallow_width + 1.0 - d) / 2.0))
			color = mix(color, shallow_color, shallow);
		float wobble = (noise((p + 0.5) / 17.0 + vec2(91.0, 3.0)) * 2.0 - 1.0) * 6.0;
		float level = clamp((d + wobble - deep_start) / max(deep_end - deep_start, 1.0), 0.0, 1.0);
		level = clamp(floor(level * 5.0 + bayer(p)) / 5.0, 0.0, 1.0);
		color = mix(color, deep_color, deep * level);
	}
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
		float kind = bank_kind(p);
		// la plage s'amincit jusqu'à disparaître là où l'herbe descend à l'eau, s'élargit un peu ailleurs
		float width = (beach_width + (noise((p + 0.5) / 9.0 + vec2(5.0, 61.0)) * 2.0 - 1.0) * beach_wave)
			* smoothstep(bank_grass - 0.06, bank_grass + 0.1, kind) * (0.7 + 0.6 * smoothstep(0.4, 0.8, kind));
		// un éventail de sable là où un ruisseau se jette
		if (stream_mouth > 0.0) width = max(width, stream_mouth * (1.0 - smoothstep(3.0, 16.0, stream_distance(p))));
		// de petites plaques d'herbe au milieu du sable
		float patch = noise((p + 0.5) / beach_patch_scale + vec2(41.0, 7.0));
		bool grassy = sd > wet_width + 2.5 && sd < width - 2.0 && patch > 1.0 - beach_patches;
		if (width > 1.0 && !grassy && sd < width + 1.0 && (sd < width - 1.0 || bayer(p) < (width + 1.0 - sd) / 2.0)) {
			vec3 beach = tile_color(sand, p);
			if (sd < 1.5) beach *= 1.0 - outline;
			else if (sd < wet_width + 1.5) beach *= 1.0 - wet;
			color = beach;
			t = sand;
			// des galets, sur les plages de galets
			float rocky = smoothstep(bank_pebbles - 0.04, bank_pebbles + 0.04, kind);
			vec2 cell = floor(p / 4.0);
			if (rocky > 0.0 && sd >= 1.5 && hash(cell + vec2(17.3, 2.9)) < pebbles * rocky) {
				vec2 spot = cell * 4.0 + floor(vec2(hash(cell + vec2(3.3, 8.8)), hash(cell + vec2(6.1, 1.4))) * 2.0);
				vec2 dim = hash(cell + vec2(9.9, 4.2)) < 0.5 ? vec2(1.0) : vec2(2.0, 1.0);
				vec3 pebble;
				if (stone_shape(p - spot, dim, cell, color, pebble)) color = pebble;
			}
			// des touffes d'herbe sur le sable, plus côté terre
			vec2 tcell = floor(p / 6.0);
			if (beach_tufts > 0.0 && sd >= wet_width + 1.5
				&& hash(tcell + vec2(23.1, 5.6)) < beach_tufts * smoothstep(0.2, 0.9, sd / width)) {
				vec2 spot = tcell * 6.0 + vec2(2.0, 2.0 + floor(hash(tcell + vec2(7.2, 2.5)) * 2.0));
				vec2 twin = spot + vec2(1.0, 1.0);
				int tuft = max(tuft_pixel(p - spot, tcell), tuft_pixel(p - twin, tcell + 7.0));
				vec3 blade = tile_color(grass, p);
				if (tuft == 1) color = blade * 0.7;
				else if (tuft == 2) color = blade;
				else if (tuft == 3) color = blade * 1.2;
				else if (tuft_pixel(p + 1.0 - spot, tcell) > 0 || tuft_pixel(p + 1.0 - twin, tcell + 7.0) > 0) color *= 0.82;
			}
		} else if (sd < mud_width + 1.0 && kind < bank_grass + 0.1) {
			// l'herbe jusqu'à l'eau : une ligne de terre mouillée, tramée vers l'herbe
			if (sd < mud_width || bayer(p) < 0.5)
				color = mix(color, mud_color, 0.7);
		}
	}
	// Le pied des montagnes : de 1 (contre la roche) à 0 (à foothill_reach cases), au bord ondulé
	float foot = 0.0;
	vec3 slope = color; // le sol avant le pied : celui du cœur des montagnes
	if (foothill > 0.0 && !in_water && t != water) {
		vec2 field = texture(mountain_field, (p + 0.5) / (vec2(map_cells) * ART)).rg;
		float inside = field.r;
		float md = inside
			+ (noise((p + 0.5) / 11.0 + vec2(43.0, 17.0)) - 0.5) * foothill_wave
			+ (noise((p + 0.5) / 3.5 + vec2(7.0, 91.0)) - 0.5) * foothill_wave * 0.35;
		foot = 1.0 - smoothstep(0.0, max(foothill_reach, 0.1), md);
		// l'herbe sèche, en paliers tramés
		float dry = clamp(floor(foot * 3.0 + bayer(p)) / 3.0, 0.0, 1.0);
		color = mix(color, color * vec3(1.06, 0.96, 0.74) * 0.9, dry * 0.7 * foothill);
		// des plaques de terre et de gravier, plus serrées contre la roche
		// (dans les cases de montagne elles-mêmes, entre les sommets, rien que de la terre et du gravier)
		float bare = noise((p + 0.5) / 7.0 + vec2(29.0, 61.0)) - 1.0 + foot * foothill_soil
			+ (1.0 - smoothstep(0.0, 0.25, inside)) * foothill_soil;
		if (bare > 0.0 || (bare > -0.04 && bayer(p) < 0.5)) {
			vec3 dirt = texelFetch(road_tiles, ivec3(ivec2(mod(p, ART)), 0), 0).rgb;
			vec3 gravel = texelFetch(road_tiles, ivec3(ivec2(mod(p, ART)), 1), 0).rgb;
			vec3 soil = mix(dirt, gravel, smoothstep(0.3, 0.7, noise((p + 0.5) / 5.0 + vec2(3.0, 3.0))));
			color = mix(color, soil, foothill);
			t = -1; // plus d'herbe : ni fleurs ni touffes
		}
		// au cœur des montagnes (à plus d'un tiers de case du bord), la roche sombre, au bord ondulé et tramé
		float rocky = smoothstep(0.3, 0.6, field.g + (noise((p + 0.5) / 6.0 + vec2(71.0, 23.0)) - 0.5) * 0.4
			+ (bayer(p) - 0.5) * 0.12);
		if (foothill_rock > 0.0 && rocky > 0.0) {
			vec3 hollow = slope * foothill_rock_shade * (0.9 + 0.2 * noise((p + 0.5) / 4.0 + vec2(11.0, 5.0)));
			color = mix(color, hollow, rocky * foothill_rock);
			t = -1;
		}
		// des éboulis : des pierres, plus nombreuses contre la roche
		vec2 cell = floor(p / 6.0);
		if (foothill_scree > 0.0 && hash(cell + vec2(14.1, 77.3)) < foothill_scree * foot * foot) {
			vec2 spot = cell * 6.0 + 1.0 + floor(vec2(hash(cell + vec2(2.2, 9.4)), hash(cell + vec2(5.8, 1.6))) * 2.0);
			vec3 stone;
			if (stone_shape(p - spot, stone_size(cell + vec2(3.3)), cell, color, stone)) color = stone;
		}
	}
	// Le sol rocheux : ses dalles, leurs fissures et ses pierres
	if (t == rock_ground && !in_water && stream_depth(p) <= 0.0 && shore_distance(p) > 2.0) {
		vec2 slab;
		slab_edge(p, slab);
		color *= 1.0 + (floor(hash(slab + vec2(2.7, 4.4)) * 3.0) - 1.0) * slab_tone;
		if (crack_at(p)) color *= slab_dark;
		else if (crack_at(p + vec2(1.0, 0.0))) color *= slab_light;
		vec3 stone;
		if (rock_stones > 0.0 && rock_stone_at(p, color, stone)) color = stone;
	}
	// Des cailloux semés sur le sol (l'herbe, le sable), loin des routes
	if (ground_stones > 0.0 && !in_water && t != water) {
		vec3 stone;
		if (ground_stone_at(p, color, stone)) color = stone;
	}
	// Fleurs et cailloux, sur l'herbe franche (pas dans les mélanges)
	if (t == grass && !in_water && (share <= 0.0 || share >= 1.0)) {
		vec3 flower;
		if (grass_clump_at(p, color, flower)) color = flower;
		if (flower_patch_at(p, color, flower)) color = flower;
		else if (flower_at(p, color, flower)) color = flower;
	}
	// Les rochers dans l'eau peu profonde, puis les roseaux du bord (dans l'eau et sur la rive)
	// le delta d'un ruisseau : une eau claire qui s'ouvre en éventail dans l'eau où il se jette
	if (stream_delta > 0.0 && in_water) {
		float axis = stream_distance(p);
		float offshore = -shore_side(p);
		if (axis < 20.0 && offshore < 14.0) {
			float reach = (1.0 - smoothstep(stream_width, 20.0, axis)) * (1.0 - smoothstep(2.0, 14.0, offshore));
			reach = floor(reach * 4.0 + bayer(p)) / 4.0;
			color = mix(color, shallow_color, stream_shallow * 1.5 * reach);
		}
	}
	if (rocks > 0.0 && in_water) {
		vec3 rock;
		if (rock_at(p, color, rock)) color = rock;
	}
	if (reeds > 0.0 && abs(river) < 12.0 || (reeds > 0.0 && (a == water || b == water))) {
		int reed = reed_at(p, false);
		if (reed == 1) color = vec3(0.2, 0.27, 0.11);
		else if (reed == 2) color = vec3(0.32, 0.44, 0.17);
		else if (reed == 3) color = vec3(0.5, 0.6, 0.26);
		else if (reed == 4) color = vec3(0.42, 0.26, 0.13);
	}
	// Les ruisseaux (pas dans l'eau où ils se jettent) : l'eau, une écume contre la rive, un contour sombre
	// (des mares, l'eau claire au bord et sombre au milieu, la terre mouillée de la rive, des bancs de sable, des pierres)
	float depth = stream_depth(p);
	// le delta : plus large près de l'eau où il se jette, des îlots de sable entre ses bras
	float mouth = 0.0;
	if (stream_delta > 0.0 && !in_water && depth > -stream_delta - 4.0)
		mouth = 1.0 - smoothstep(0.0, stream_mouth + 4.0, shore_side(p));
	depth += mouth * mouth * stream_delta;
	bool islet = mouth > 0.15 && depth > 1.5
		&& depth < stream_width + stream_delta * mouth * mouth - 1.0
		&& noise((p + 0.5) / 4.0 + vec2(13.0, 57.0)) > 1.0 - stream_islets * (0.2 + 0.5 * mouth);
	if (!in_water && depth > -1.5 - mud_width) {
		// un banc de sable : il mange le bord de l'eau et couvre la rive, par endroits
		float bar = stream_bars > 0.0
			? smoothstep(1.0 - stream_bars, 1.0 - stream_bars + 0.12, noise((p + 0.5) / 9.0 + vec2(71.0, 3.0))) * 3.5 : 0.0;
		if (islet || (bar > 1.0 && depth < bar - 1.0 && depth > -1.0)) {
			color = tile_color(sand, p);
			if (depth > bar - 2.5) color *= 1.0 - wet;
		} else if (depth <= 0.0 && mouth > 0.2) {
			// à l'embouchure, la rive est de sable mouillé, sans contour
			if (depth > -1.5 - mud_width * mouth) color = tile_color(sand, p) * (1.0 - wet * mouth);
		} else if (depth <= 0.0) {
			if (stream_mud > 0.0 && (depth > -0.5 - mud_width || bayer(p) < 0.5))
				color = mix(color, mud_color, stream_mud);
			if (depth > -1.5) color *= 1.0 - outline;
		} else {
			color = tile_color(water, p);
			if (depth < 1.5) color = mix(color, foam_color, foam);
			else if (depth < 2.5 && bayer(p) < 0.5) color = mix(color, foam_color, foam * 0.6);
			else if (depth < 3.0 || (depth < 4.0 && bayer(p) < 0.5)) color = mix(color, shallow_color, stream_shallow);
			float middle = depth - (stream_width - 1.2);
			if (stream_deep > 0.0 && (middle > 1.0 || (middle > 0.0 && bayer(p) < 0.5)))
				color = mix(color, deep_color, stream_deep);
			// à l'embouchure, l'eau prend peu à peu la teinte de l'eau où il se jette (claire contre la rive, et
			// l'éventail clair du delta)
			if (mouth > 0.0) {
				vec3 joined = mix(mix(tile_color(water, p), shallow_color, shallow), shallow_color, stream_shallow * 1.5);
				color = mix(color, joined, clamp(floor(mouth * 4.0 + bayer(p)) / 4.0, 0.0, 1.0));
			}
			vec3 stone;
			if (stream_stones > 0.0 && stream_stone_at(p, color, stone)) color = stone;
		}
	}
	if (stream_reeds > 0.0 && !in_water && depth > -12.0) {
		int reed = reed_at(p, true);
		if (reed == 1) color = vec3(0.2, 0.27, 0.11);
		else if (reed == 2) color = vec3(0.32, 0.44, 0.17);
		else if (reed == 3) color = vec3(0.5, 0.6, 0.26);
		else if (reed == 4) color = vec3(0.42, 0.26, 0.13);
	}
	// La route, par-dessus le sol
	int road_kind = 1;
	float inside = road_inside(p, road_kind); // positif sur la route
	float spread = max(t == sand ? road_sand_scatter : road_scatter, 0.01);
	int other_kind;
	// le talus d'en face, au soleil : un liseré plus clair sur l'herbe (sur le sable, le bourrelet le remplace)
	if (road_rim > 0.0 && t != sand && inside < 0.0 && inside > -2.0 && road_inside(p - sun * 2.0, other_kind) > 0.0)
		color *= 1.0 + road_rim;
	vec3 ground = color; // le sol sous la route
	bool paved = false;
	if (inside > -spread * 0.5 && hash(p + vec2(0.37, 0.71)) < clamp(inside / spread + 0.5, 0.0, 1.0)) {
		paved = true;
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
	// l'herbe qui déborde sur la route, et ses touffes (seulement là où le sol est de l'herbe)
	if (paved && t == grass && !in_water && (road_grass > 0.0 || road_tufts > 0.0 || road_tufts_middle > 0.0)) {
		float reach = road_grass > 0.0 ? grass_reach(p) : 0.0;
		if (inside < reach) {
			color = ground;
			if (inside >= reach - 1.0) color *= 1.1; // le bout des brins, au soleil
		} else {
			int tuft = road_tufts > 0.0 || road_tufts_middle > 0.0 ? road_tuft_at(p) : 0;
			vec3 blade = tile_color(grass, p);
			if (tuft == 1) color = blade * 0.7;
			else if (tuft == 2) color = blade;
			else if (tuft == 3) color = blade * 1.2;
			else if (tuft == 4) color *= 1.0 - road_grass_shadow;
			else if (road_grass > 0.0 && road_grass_shadow > 0.0) {
				// l'ombre de l'herbe en bas à droite, sur la route
				float below = road_inside(p + 1.0, other_kind);
				if (below > 0.0 && below < grass_reach(p + 1.0)) color *= 1.0 - road_grass_shadow;
			}
		}
	}
	// sur le sable : le sable porté par le vent sur le bord de la route, et le bourrelet de sable repoussé le long du bord
	// qui reste visible (sur le sable posé sur la route comme à côté : le même sable, la même lumière)
	if (t == sand && !in_water && inside > -road_sand_berm_width - road_sand_drift - 1.0) {
		float drift = road_sand_drift * grass_amount(p + vec2(311.0, 173.0))
			* smoothstep(0.85 - road_sand_drift_share, 1.25 - road_sand_drift_share,
				noise((p + 0.5) / road_sand_drift_scale + vec2(83.0, 41.0)));
		float edge = inside - drift; // de combien le pixel est dans la route qui reste visible
		bool buried = paved && drift > 0.0 && (edge < -1.0 || (edge < 1.0 && bayer(p) < (1.0 - edge) / 2.0));
		if (buried) color = ground;
		// pas de bourrelet là où le sable recouvre toute la largeur de la route (ce qui reste visible en son milieu)
		int k;
		float visible = inside + road_distance(p, k) - drift;
		if ((!paved || buried) && road_sand_berm > 0.0 && visible > 1.0 && edge < -0.5 && edge > -road_sand_berm_width
				&& bayer(p) < 1.0 - (-edge - 0.5) / road_sand_berm_width)
			color *= 1.0 + road_sand_berm;
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
	// Les cailloux du bord des routes (pas dans l'eau)
	if ((road_stones > 0.0 || road_stones_inside > 0.0) && !in_water && inside > -6.0) {
		vec3 stone;
		if (road_stone_at(p, color, stone)) color = stone;
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
	// un pont qui a son image : rien à dessiner ici
	if (deck && (bridge_art & ((across_y ? 2 : 1) * (r == 1 ? 4 : 1))) != 0) deck = false;
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
