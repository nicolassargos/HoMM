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
uniform int priority[10];
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
uniform float shore_wave = 1.4;
// Mélange entre deux terres : demi-largeur (pixels d'art), grain qui rend le mélange irrégulier,
// et nombre de paliers (0 = mélange continu ; 4 = 0, 25, 50, 75, 100 %, plus « pixel art »)
uniform float blend = 3.5;
uniform float blend_grain = 0.35;
uniform float grain_scale = 3.0;
uniform float blend_steps = 4.0;
// Rives : contour et berge (assombrissement de la terre), écume (éclaircissement de l'eau)
uniform float outline = 0.05;
uniform float bank = 0.08;
uniform float foam = 0.15;
// les deux rangées de pixels de terre qui touchent l'eau, mêlées à l'eau claire : de tant la première, de moitié moins
// la deuxième
uniform float water_edge = 0.66;
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
// le sol rocheux (rock_ground : son numéro) : des dalles (slab_cell pixels d'art) de tons un peu différents (slab_tone),
// séparées par des fissures (slab_crack de large, sur la part slab_cracked des bords : pas un pavage) ; le bord éclairé
// d'une fissure (slab_light), son fond (slab_dark) ; slab_wobble tord les bords ; chaque dalle penche un peu de son côté
// et la lumière la prend en biais (slab_relief)
uniform int rock_ground = 7;
// la terre brûlée (burnt_ground : son numéro) : des nappes de cendre (burnt_ash : leur force), des braises isolées (la part
// burnt_embers des carrés de 9 pixels d'art) et des fissures (burnt_veins : la part des carrés de burnt_vein_cell pixels
// d'art d'où part une fissure, plus ou moins selon la région) : une fente noire en zigzag, avec des branches, large de
// burnt_vein_width au milieu des plus grandes et fine à ses bouts ; au fond de sa partie large, le feu passe du rouge
// sombre au rouge (burnt_vein_glow), vif au plus large (burnt_vein_hot), sauf par endroits (burnt_vein_gaps) ;
// burnt_vein_rim assombrit le sol tout près, burnt_vein_light éclaire la lèvre du bas, burnt_vein_halo réchauffe le sol.
// Le feu et les braises respirent : un cycle de burnt_glow_period secondes, chaque fissure à son rythme, la chaleur
// courant le long d'elle ; burnt_glow_depth : de combien il baisse (burnt_glow_at : l'instant, pour figer une capture)
uniform int burnt_ground = 8;
// la neige (snow_ground : son numéro) : des congères poussées par le vent, longues de snow_drift_scale pixels d'art (et
// snow_drift_stretch fois moins hautes),
// une ombre bleutée (snow_shadow, de force snow_drifts) du côté abrité et une crête plus claire (snow_crest) ; quelques
// pixels qui scintillent (la part snow_sparkle)
uniform int snow_ground = 9;
uniform float snow_drifts = 0.4;
uniform float snow_drift_scale = 44.0;
uniform float snow_drift_stretch = 0.75;
uniform vec3 snow_shadow = vec3(0.63, 0.71, 0.86);
uniform float snow_crest = 0.04;
uniform float snow_sparkle = 0.015;
// sur la neige, la part snow_stones des cailloux semés reste (sombres, ils se voient trop sur le blanc)
uniform float snow_stones = 0.3;
// le marais de la carte (swamp_ground : son numéro, -1 : rien, comme au combat qui a ses flaques ; 2026-11-24, « de la
// terre avec des points verts » selon l'utilisateur) : de grandes plaques de mousse (swamp_moss : leur force, environ
// swamp_moss_scale pixels d'art) et de boue plus sombre (swamp_mire) ; des mares d'eau trouble (un bruit doux aplati
// d'environ swamp_pool_scale pixels ; plus swamp_pool_level est bas, plus il y en a), ni près du bord du marais, ni des
// routes, ni des rives : sous leur rive du haut le reflet sombre de la rive, puis une bande de ciel, des rides claires ;
// une bande de boue mouillée autour (swamp_mud_width pixels) ; des nénuphars (la part swamp_lilies des carrés de
// 10 pixels, dans les grandes mares), des roseaux au bord (la part swamp_reeds des carrés de 5 pixels du bord), des
// touffes de joncs sur la terre (la part swamp_tufts des carrés de 7 pixels)
uniform int swamp_ground = -1;
// les cases sans mares (rouge, un pixel par case, adouci entre les cases) : sous les objets, les forêts et les montagnes
uniform sampler2D dry_field : filter_linear, hint_default_black;
uniform float swamp_moss = 0.6;
uniform float swamp_moss_scale = 46.0;
uniform vec3 swamp_moss_color = vec3(0.78, 1.0, 0.62);
uniform float swamp_mire = 0.18;
// par endroits, une boue plus sombre et bleutée (swamp_peat : sa force, de grandes plaques d'environ swamp_peat_scale
// pixels d'art) ; l'eau des mares change un peu de teinte d'une mare à l'autre (swamp_water_vary : vers le brun ou le vert)
uniform float swamp_peat = 0.7;
uniform float swamp_peat_scale = 60.0;
uniform vec3 swamp_peat_color = vec3(0.62, 0.66, 0.78);
uniform float swamp_water_vary = 0.5;
uniform float swamp_pool_scale = 26.0;
uniform float swamp_pool_level = 0.72;
uniform float swamp_dry = 0.35;
uniform float swamp_dry_scale = 70.0;
uniform float swamp_mud_width = 2.0;
uniform vec3 swamp_mud = vec3(0.2, 0.15, 0.08);
uniform vec3 swamp_water = vec3(0.2, 0.29, 0.25);
uniform vec3 swamp_water_deep = vec3(0.13, 0.2, 0.18);
uniform vec3 swamp_sky = vec3(0.45, 0.58, 0.55);
uniform vec3 swamp_lily = vec3(0.31, 0.47, 0.22);
uniform float swamp_lilies = 0.3;
uniform float swamp_reeds = 0.55;
uniform float swamp_tufts = 0.12;
uniform float burnt_ash = 0.5;
uniform vec3 ash_color = vec3(0.46, 0.43, 0.41);
uniform float burnt_embers = 0.04;
uniform float burnt_veins = 0.36;
uniform float burnt_vein_width = 1.45;
uniform float burnt_vein_gaps = 0.3;
uniform float burnt_vein_glow = 0.8;
uniform float burnt_vein_hot = 0.35;
uniform float burnt_vein_rim = 0.25;
uniform float burnt_vein_light = 0.35;
uniform float burnt_vein_halo = 0.14;
uniform float burnt_glow_period = 5.0;
uniform float burnt_glow_depth = 0.55;
uniform float burnt_glow_at = -1.0;
uniform float burnt_vein_cell = 44.0;
uniform vec3 ember_hot = vec3(1.0, 0.68, 0.24);
uniform vec3 ember_color = vec3(0.86, 0.27, 0.07);
uniform float slab_cell = 22.0;
uniform float slab_crack = 0.8;
uniform float slab_cracked = 0.55;
uniform float slab_tone = 0.03;
uniform float slab_dark = 0.83;
uniform float slab_light = 1.04;
uniform float slab_wobble = 1.2;
uniform float slab_relief = 0.11;
// le grain fin de l'image du sol rocheux qu'on garde (rock_grain, de 0 à 1) ; slab_stretch allonge les dalles dans le sens
// des strates de la roche ; rock_cool : de grandes parties un peu plus froides
uniform float rock_grain = 0.5;
uniform float slab_stretch = 1.7;
uniform float rock_cool = 0.04;
// au combat (rock_outcrops > 0 : la part de roche) : la roche en affleurements (des taches de rock_outcrop_scale pixels
// d'art, le bord du haut éclairé, la face du bas dans l'ombre, une ombre portée à leur pied) dans une terre de gravier
// (rock_soil), dont les cailloux (rock_gravel) se serrent près de la roche ; sur la carte, la roche partout
uniform float rock_outcrops = 0.0;
uniform float rock_outcrop_scale = 60.0;
// une terre désolée, entre le désert et la terre brûlée (2026-11-20) : gris-brun, des fissures sèches sans feu (celles de
// la terre brûlée, plus petites ; rock_cracks : la part du sol où elles se montrent), des cailloux et des pierres
uniform vec3 rock_soil = vec3(0.43, 0.40, 0.36);
uniform float rock_gravel = 0.5;
uniform float rock_cracks = 0.5;
// pour que ce ne soit pas qu'un sol uni : de grandes plaques plus claires ou plus sombres (rock_patches), des ravins secs de
// gravier pâle (rock_washes) ; les touffes d'herbe sèche sont des images (battle/detail-rock)
uniform float rock_patches = 0.08;
uniform float rock_washes = 1.0;
uniform vec2 rock_shift = vec2(0.0);
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
uniform float wet = 0.25;
uniform float wet_width = 4.0;
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
// shallow_color), qui s'efface vers le large en shallow_steps paliers tramés au bord ondulé ; au large, de deep_start à
// deep_end pixels de la terre, l'eau fonce (mêlée de deep à deep_color), en paliers tramés ; les rivières, étroites,
// foncent plus tôt et plus fort (river_deep_start, river_deep_end, river_deep)
uniform float shallow_width = 14.0;
uniform float shallow = 0.75;
uniform float shallow_steps = 3.0;
uniform vec3 shallow_color = vec3(0.45, 0.78, 0.82);
uniform float deep_start = 10.0;
uniform float deep_end = 76.0;
uniform float deep = 0.35;
uniform float river_deep_start = 4.0;
uniform float river_deep_end = 30.0;
uniform float river_deep = 0.65;
// Les vagues des lacs et de la mer : une ligne d'écume qui vient de wave_reach pixels d'art du bord jusqu'à la rive en
// wave_period secondes (wave_foam : sa force ; wave_stretch : la longueur des tronçons de côte où elles arrivent ensemble ;
// wave_at : l'instant, pour figer une capture)
uniform float wave_foam = 1.0;
uniform float wave_reach = 10.0;
uniform float wave_period = 4.0;
uniform float wave_stretch = 40.0;
uniform float wave_at = -1.0;
// la part des vagues qui manquent (sur un tronçon de côte, une vague sur tant ne vient pas), et la terre mouillée
// qu'elles laissent sur wave_wash pixels de la rive
uniform float wave_skip = 0.3;
uniform float wave_wash = 3.0;
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
// les ruisseaux nés d'une source : un cercle de pierres autour de leur mare (springs : le milieu de chaque mare, en pixels
// d'art ; spring_stones : la part des carrés de 4 pixels du cercle qui ont une pierre)
uniform vec2 springs[48];
uniform int spring_count = 0;
uniform float spring_stones = 0.9;
// et la terre mouillée autour (spring_wet : sa force ; spring_wet_reach : jusqu'où, en pixels d'art)
uniform float spring_wet = 1.0;
uniform float spring_wet_reach = 24.0;
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

// Distance de q à la courbe de Bézier du second degré a, b, c (Inigo Quilez, « sdBezier »)
float bezier(vec2 q, vec2 a, vec2 b, vec2 c) {
	vec2 A = b - a;
	vec2 B = a - 2.0 * b + c;
	// trois points alignés à égale distance : un segment
	if (dot(B, B) < 1e-4) return segment(q, a, c);
	vec2 C = A * 2.0;
	vec2 D = a - q;
	float kk = 1.0 / dot(B, B);
	float kx = kk * dot(A, B);
	float ky = kk * (2.0 * dot(A, A) + dot(D, B)) / 3.0;
	float kz = kk * dot(D, A);
	float p = ky - kx * kx;
	float p3 = p * p * p;
	float qq = kx * (2.0 * kx * kx - 3.0 * ky) + kz;
	float h = qq * qq + 4.0 * p3;
	float res;
	if (h >= 0.0) {
		h = sqrt(h);
		vec2 x = (vec2(h, -h) - qq) / 2.0;
		vec2 uv = sign(x) * pow(abs(x), vec2(1.0 / 3.0));
		float t = clamp(uv.x + uv.y - kx, 0.0, 1.0);
		vec2 v = D + (C + B * t) * t;
		res = dot(v, v);
	} else {
		float z = sqrt(-p);
		float v = acos(qq / (p * z * 2.0)) / 3.0;
		float m = cos(v);
		float n = sin(v) * 1.732050808;
		vec3 t = clamp(vec3(m + m, -n - m, n - m) * z - kx, 0.0, 1.0);
		vec2 v1 = D + (C + B * t.x) * t.x;
		vec2 v2 = D + (C + B * t.y) * t.y;
		res = min(dot(v1, v1), dot(v2, v2));
	}
	return sqrt(res);
}

// Distance de q à la ligne médiane de la route dans la case n (qui a une route). Chaque case dessine sa moitié de
// chaque liaison : jusqu'au milieu du bord vers une voisine en ligne droite, ou jusqu'au coin vers une voisine en
// diagonale (seulement si aucune case de route ne les relie déjà par un côté). Une case qui n'a que deux liaisons (la
// route la traverse) les joint par une courbe qui passe près de son centre (Bézier : du bout d'une liaison au bout de
// l'autre, le centre pour point de contrôle) : la courbe d'une case continue celle de la suivante sans cassure, une
// route en escalier ou qui passe de la ligne droite à la diagonale ondule en douceur, comme dans Heroes 3.
float road_in_cell(vec2 q, ivec2 n) {
	vec2 center = (vec2(n) + 0.5) * ART;
	vec2 ends[8];
	int count = 0;
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			if (dx == 0 && dy == 0) continue;
			if (!road_at(n + ivec2(dx, dy))) continue;
			// une diagonale déjà reliée par un côté n'est pas une liaison
			if (dx != 0 && dy != 0 && (road_at(n + ivec2(dx, 0)) || road_at(n + ivec2(0, dy)))) continue;
			ends[count] = center + vec2(float(dx), float(dy)) * ART * 0.5;
			count++;
		}
	}
	if (count == 0) return length(q - center); // une route d'une seule case : un rond
	if (count == 2) return bezier(q, ends[0], center, ends[1]);
	float best = 1e5;
	for (int i = 0; i < count; i++)
		best = min(best, segment(q, center, ends[i]));
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
	// l'ondulation ne ferme jamais un ruisseau étroit
	return stream_width + pool + (noise((p + 0.5) / 7.0 + vec2(5.0, 91.0)) * 2.0 - 1.0) * min(stream_wave, stream_width * 0.35)
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
		// la rive exacte ne mesure pas plus loin qu'environ une demi-case (et ses ondulations la font revenir au large) :
		// l'image des distances, comptée depuis le bord des cases, prend le relais, moins de quoi passer la rive ondulée
		lake = max(lake, texture(water_field, (p + 0.5) / (vec2(map_cells) * ART)).b - 10.0);
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
bool ground_stone_at(vec2 p, vec3 under, float share, out vec3 stone) {
	stone = under;
	float size = floor(max(ground_stone_cell, 6.0));
	vec2 cell = floor(p / size);
	if (hash(cell + vec2(73.1, 19.9)) >= ground_stones * share) return false;
	vec2 spot = cell * size + 1.0 + floor(vec2(hash(cell + vec2(8.6, 2.2)), hash(cell + vec2(3.7, 6.6))) * (size - 4.0));
	int kind;
	if (road_inside(spot, kind) > -4.0) return false;
	return stone_shape(p - spot, stone_size(cell + vec2(0.5)), cell, under, stone);
}

// La distance du pixel d'art p au bord de sa dalle (pixels d'art, à peu près) ; id : la dalle ; from : p depuis le centre
// de la dalle (en tailles de dalle)
float slab_edge(vec2 p, out vec2 id, out vec2 from) {
	vec2 twist = vec2(noise(p / 9.0 + vec2(41.0, 7.0)), noise(p / 9.0 + vec2(5.0, 63.0))) - 0.5;
	vec2 q = p + twist * 2.0 * slab_wobble;
	vec2 g = vec2(q.x * 0.94 + q.y * 0.34, (q.y * 0.94 - q.x * 0.34) * slab_stretch) / slab_cell;
	vec2 base = floor(g);
	float first = 1e9;
	float second = 1e9;
	id = base;
	from = vec2(0.0);
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 n = base + vec2(float(x), float(y));
		vec2 away = g - n - 0.05 - vec2(hash(n + vec2(3.1, 8.2)), hash(n + vec2(6.4, 1.7))) * 0.9;
		float d = length(away);
		if (d < first) {
			second = first;
			first = d;
			id = n;
			from = away;
		} else if (d < second) second = d;
	}
	return (second - first) * slab_cell * 0.5;
}

// Le souffle du feu au pixel d'art p, de 0 à 1, par paliers tramés
float burnt_breath(vec2 p, float phase) {
	float at = burnt_glow_at >= 0.0 ? burnt_glow_at : TIME;
	return floor((0.5 + 0.5 * sin(TAU * (at / burnt_glow_period + phase))) * 3.0 + bayer(p)) / 3.0;
}

// Garde le tronçon de fissure de a à b s'il est le plus proche de p : (marge au bord de la fente, distance au trait, largeur, phase du feu)
vec4 vein_segment(vec4 best, vec2 p, vec2 a, vec2 b, float wide_a, float wide_b, float phase_a, float phase_b) {
	vec2 ab = b - a;
	float s = clamp(dot(p - a, ab) / max(dot(ab, ab), 0.001), 0.0, 1.0);
	float d = length(p - a - ab * s);
	float wide = mix(wide_a, wide_b, s);
	float margin = d - 0.55 - wide * burnt_vein_width;
	return margin < best.x ? vec4(margin, d, wide, mix(phase_a, phase_b, s)) : best;
}

// La fissure de terre brûlée la plus proche du pixel d'art p : un tronc en zigzag, plus large au milieu, et ses branches
vec4 vein_at(vec2 p) {
	vec2 base = floor(p / burnt_vein_cell);
	vec4 best = vec4(1e9, 1e9, 0.0, 0.0);
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 n = base + vec2(float(x), float(y));
		if (hash(n + vec2(31.7, 64.2)) >= burnt_veins * (0.35 + 1.3 * noise(n / 3.0 + vec2(14.0, 37.0)))) continue;
		float angle = hash(n + vec2(12.4, 5.9)) * TAU;
		// beaucoup de petites fissures sans feu, quelques grandes
		float size = hash(n + vec2(2.2, 41.3));
		size = 0.45 + 0.55 * size * (2.0 - size) * size;
		float step_size = burnt_vein_cell * 0.18 * size;
		vec2 a = (n + 0.3 + vec2(hash(n + vec2(5.3, 2.6)), hash(n + vec2(1.4, 7.9))) * 0.4) * burnt_vein_cell;
		a -= vec2(cos(angle), sin(angle)) * step_size * 3.5;
		float side = hash(n + vec2(8.8, 19.1)) < 0.5 ? -1.0 : 1.0;
		float phase = hash(n + vec2(6.6, 33.1));
		for (int i = 0; i < 7; i++) {
			float f = float(i);
			side = -side;
			float heading = angle + side * (0.15 + 0.8 * hash(n + vec2(f * 1.7, 9.1)));
			vec2 b = a + vec2(cos(heading), sin(heading)) * step_size * (0.45 + 1.1 * hash(n + vec2(f * 2.3, 27.4)));
			float wide_a = sin(PI * f / 7.0) * size;
			float wide_b = sin(PI * (f + 1.0) / 7.0) * size;
			float phase_b = phase + (f + 1.0) / 14.0;
			best = vein_segment(best, p, a, b, wide_a, wide_b, phase + f / 14.0, phase_b);
			if (i >= 1 && i <= 4 && hash(n + vec2(f * 3.1, 53.6)) < 0.45) {
				float turn = heading + side * (0.8 + 0.5 * hash(n + vec2(f * 4.3, 15.2)));
				vec2 c = b + vec2(cos(turn), sin(turn)) * step_size * 0.9;
				turn -= side * 0.6;
				vec2 e = c + vec2(cos(turn), sin(turn)) * step_size * 0.7;
				best = vein_segment(best, p, b, c, wide_b * 0.5, wide_b * 0.2, phase_b, phase_b);
				best = vein_segment(best, p, c, e, wide_b * 0.2, 0.0, phase_b, phase_b);
			}
			a = b;
		}
	}
	return best;
}

// La roche affleure-t-elle au pixel d'art p ? (sa valeur, à comparer à outcrop_level)
float outcrop_at(vec2 p) {
	return noise((p + 0.5) / rock_outcrop_scale + vec2(37.0, 81.0)) * 0.75 + noise((p + 0.5) / (rock_outcrop_scale * 0.3) + vec2(12.0, 7.0)) * 0.25;
}

// Une fissure du sol rocheux au pixel d'art p ?
bool crack_at(vec2 p) {
	vec2 id;
	vec2 from;
	return slab_edge(p, id, from) < slab_crack && noise(p / (slab_cell * 0.8) + vec2(12.0, 77.0)) < slab_cracked;
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

// Les mares du marais au pixel d'art p, sans les routes ni le bord du marais : de combien de pixels d'art (à peu près) il
// est dans l'eau (négatif : dehors). Un bruit doux aplati (vues en biais, les mares sont plus larges que hautes), au bord
// déchiqueté par un bruit plus fin
float swamp_pool_raw(vec2 p) {
	float n = noise((p + 0.5) / vec2(swamp_pool_scale, swamp_pool_scale * 0.6) + vec2(19.0, 83.0)) * 0.85
		+ noise((p + 0.5) / vec2(9.0, 6.0) + vec2(5.0, 37.0)) * 0.15;
	return (n - swamp_pool_level) * swamp_pool_scale * 0.9;
}

// L'instant de la vague au pixel d'art p (en vagues : la partie entière compte les vagues, la partie décimale avance de
// 0, au large, à 1, sur la rive), décalé le long de la côte
float wave_time(vec2 p) {
	return (wave_at >= 0.0 ? wave_at : TIME) / wave_period + noise((p + 0.5) / wave_stretch + vec2(7.0, 41.0)) * 1.5;
}

// Si la vague t ne vient pas sur ce tronçon de côte (environ wave_skip des vagues)
bool wave_missing(vec2 p, float t) {
	return hash(floor((p + 0.5) / wave_stretch * 0.8) + vec2(floor(t) * 3.1, 7.7)) < wave_skip;
}

// La place laissée aux mares autour du pixel d'art p, de 0 à 1 : aucune près du bord du marais, des routes, des rives et
// des ruisseaux, ni sous les objets, les forêts et les montagnes (dry_field), ni dans les étendues sèches (là où un bruit
// doux de swamp_dry_scale pixels est sous swamp_dry : environ cette part du marais)
float swamp_room(vec2 p) {
	int kind;
	float room = smoothstep(0.75, 1.0, part_of(swamp_ground, p));
	room *= 1.0 - smoothstep(0.2, 0.45, texture(dry_field, (p + 0.5) / (vec2(map_cells) * ART)).r);
	room *= 1.0 - smoothstep(-9.0, -4.0, road_inside(p, kind));
	room *= 1.0 - smoothstep(-9.0, -4.0, stream_depth(p));
	room *= smoothstep(4.0, 9.0, shore_side(p));
	// des étendues sèches, sans mares (moins de mares, demande de l'utilisateur)
	room *= smoothstep(swamp_dry - 0.08, swamp_dry + 0.08, noise((p + 0.5) / swamp_dry_scale + vec2(61.0, 23.0)));
	return room;
}

// La mare du marais au pixel d'art p (comme swamp_pool_raw), room : la place laissée aux mares (swamp_room)
float swamp_pool(vec2 p, float room) {
	return swamp_pool_raw(p) - (1.0 - room) * 8.0;
}

// Des roseaux au bord d'une mare du marais au pixel d'art p : 0 rien, 1 le pied, 2 la tige, 3 la pointe, 4 un épi (comme
// reed_at) ; des touffes plus hautes qu'au bord des lacs, plus souvent avec un épi
int swamp_reed_at(vec2 p, float room) {
	float size = 5.0;
	for (float row = 0.0; row < 3.0; row += 1.0) {
		vec2 cell = floor(p / size) + vec2(0.0, row);
		if (hash(cell + vec2(45.7, 11.3)) >= swamp_reeds) continue;
		vec2 base = cell * size + vec2(floor(hash(cell + vec2(2.9, 6.1)) * size), size - 1.0);
		// en groupes, par endroits du bord
		if (noise((base + 0.5) / 20.0 + vec2(71.0, 15.0)) < 0.35) continue;
		float rim = swamp_pool(base, room);
		if (rim < -2.5 || rim > 1.0) continue;
		vec2 q = p - base;
		if (q.y > 0.0 || q.y < -8.0) continue;
		for (int i = -1; i <= 2; i++) {
			float h = 4.0 + floor(hash(cell + vec2(float(i) * 3.1, 4.6)) * 5.0);
			float x = float(i) + (q.y < -h * 0.6 && i != 0 ? sign(float(i)) : 0.0);
			if (q.x == x && -q.y < h) {
				if (q.y == 0.0) return 1;
				if (-q.y >= h - 2.0 && i == 0 && hash(cell + vec2(8.8, 1.1)) < 0.6) return 4;
				return -q.y >= h - 1.0 ? 3 : 2;
			}
		}
	}
	return 0;
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
		float ripple = (noise((p + 0.5) / 14.0 + vec2(17.0, 29.0)) * 2.0 - 1.0) * 1.5;
		// toute claire sur le premier tiers, puis de moins en moins
		float clear = 1.0 - clamp((d + ripple - shallow_width * 0.3) / max(shallow_width * 0.7, 1.0), 0.0, 1.0);
		clear = clamp(floor(clear * shallow_steps + bayer(p)) / shallow_steps, 0.0, 1.0);
		color = mix(color, shallow_color, shallow * clear);
		// la rivière cède la place au lac ou à la mer en douceur, là où l'eau des cases l'emporte
		float open = river > 0.0 ? smoothstep(0.3, 0.9, water_part(p)) : 1.0;
		float start = mix(river_deep_start, deep_start, open);
		float end = mix(river_deep_end, deep_end, open);
		float wobble = (noise((p + 0.5) / 17.0 + vec2(91.0, 3.0)) * 2.0 - 1.0) * 6.0;
		float level = clamp((d + wobble - start) / max(end - start, 1.0), 0.0, 1.0);
		level = clamp(floor(level * 5.0 + bayer(p)) / 5.0, 0.0, 1.0);
		color = mix(color, deep_color, mix(river_deep, deep, open) * level);
		// Les vagues (lacs et mer, pas les rivières) : une ligne d'écume brisée qui vient du large jusqu'à la rive et
		// s'efface, toutes les wave_period secondes ; l'instant change le long de la côte (un bruit doux)
		if (wave_foam > 0.0 && open > 0.0 && d < wave_reach + 2.0) {
			float t = wave_time(p);
			float cycle = fract(t);
			// elle ralentit en arrivant (et certaines ne viennent pas)
			float front = wave_reach * pow(1.0 - cycle, 1.6) + ripple * 0.6;
			if (wave_missing(p, t)) cycle = 1.0;
			float band = 1.0 - smoothstep(0.4, 1.4, abs(d - front));
			float fade = smoothstep(0.0, 0.2, cycle) * (1.0 - smoothstep(0.7, 1.0, cycle));
			// brisée : des morceaux d'écume, autres à chaque vague
			float broken = smoothstep(0.3, 0.5, noise((p + 0.5) / 5.0 + vec2(floor(t) * 13.7, 3.0)));
			if (bayer(p) < band * fade * broken * open * wave_foam)
				color = mix(color, foam_color, 0.6);
		}
	}
	// Rives : entre la terre et l'eau
	float lapped = 0.0; // la terre contre l'eau : 1 au premier pixel, 0,5 au deuxième (voir water_edge)
	if (a == water || b == water || abs(river) < 3.0) {
		bool right = water_at(p + vec2(1.0, 0.0));
		bool left = water_at(p - vec2(1.0, 0.0));
		bool down = water_at(p + vec2(0.0, 1.0));
		bool up = water_at(p - vec2(0.0, 1.0));
		bool right2 = water_at(p + vec2(2.0, 0.0));
		bool left2 = water_at(p - vec2(2.0, 0.0));
		bool down2 = water_at(p + vec2(0.0, 2.0));
		bool up2 = water_at(p - vec2(0.0, 2.0));
		lapped = in_water ? 0.0 : (right || left || down || up) ? 1.0 : (right2 || left2 || down2 || up2) ? 0.5 : 0.0;
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
	// la rangée de terre contre l'eau, à demi couverte d'eau claire : un passage doux plutôt qu'un bord net
	// la vague qui monte un peu sur la rive : de l'écume au bout, puis une terre mouillée qui sèche
	// (sur le sable seulement : contre l'herbe, l'écume ferait de la neige)
	if (!in_water && t == sand && wave_foam > 0.0 && wave_wash > 0.0 && (a == water || b == water)) {
		float sd = shore_distance(p);
		if (sd < wave_wash + 1.0) {
			float t = wave_time(p);
			float cycle = fract(t);
			if (!wave_missing(p, t)) {
				// monte vite à la fin de la vague, puis sèche pendant la suivante
				float up = smoothstep(0.82, 0.97, cycle) * wave_wash;
				float dry = fract(t + 0.1);
				float soaked = (1.0 - smoothstep(0.0, 0.8, dry)) * (1.0 - smoothstep(0.0, wave_wash, sd));
				if (bayer(p) < soaked * 0.8) color *= 1.0 - wet * 1.5;
				if (cycle > 0.82 && abs(sd - up) < 0.8 && bayer(p) < 0.7 * wave_foam) color = mix(color, foam_color, 0.6);
			}
		}
	}
	if (lapped > 0.0 && water_edge > 0.0)
		color = mix(color, mix(tile_color(water, p), shallow_color, shallow), water_edge * lapped);
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
	// Le sol rocheux : ses dalles et leurs fissures
	if (t == rock_ground && !in_water && stream_depth(p) <= 0.0 && shore_distance(p) > 2.0) {
		// moins de grain fin : on rapproche la case de la moyenne de ses voisines (seulement pour la part de roche du mélange)
		vec3 around = (tile_color(rock_ground, p + vec2(3.0, 1.0)) + tile_color(rock_ground, p + vec2(-1.0, 3.0))
			+ tile_color(rock_ground, p + vec2(-3.0, -1.0)) + tile_color(rock_ground, p + vec2(1.0, -3.0))) * 0.25;
		color += (around - tile_color(rock_ground, p)) * (1.0 - rock_grain) * (b == rock_ground ? share : 1.0 - share);
		if (rock_cool > 0.0) {
			float cool = floor(smoothstep(0.35, 0.65, noise((p + 0.5) / 70.0 + vec2(61.0, 29.0))) * 2.0 + bayer(p)) / 2.0;
			color *= mix(vec3(1.0), vec3(1.0 - rock_cool, 1.0, 1.0 + rock_cool * 1.2), cool);
		}
		bool on_rock = true;
		if (rock_outcrops > 0.0) {
			float level = mix(1.01, 0.32, rock_outcrops);
			float o = outcrop_at(p);
			on_rock = o > level;
			if (!on_rock) {
				// la terre de gravier : la couleur rock_soil, avec le relief de l'image ; des cailloux, serrés près de la roche
				color = rock_soil * (0.8 + 0.45 * dot(color, vec3(0.3, 0.59, 0.11)) / 0.45);
				vec2 q = p + rock_shift; // le même dessin décalé : chaque combat a ses ravins et ses fissures
				// de grandes plaques : une poussière plus claire et chaude, une terre plus sombre et pierreuse (en trame)
				float dust = noise((q + 0.5) / 46.0 + vec2(13.0, 88.0)) * 0.7 + noise((q + 0.5) / 15.0 + vec2(40.0, 2.0)) * 0.3;
				float patch = floor(clamp((dust - 0.5) * 4.0, -1.0, 1.0) * 2.0 + bayer(p)) / 2.0;
				color *= 1.0 + patch * rock_patches * (patch > 0.0 ? vec3(1.0, 0.95, 0.75) : vec3(1.0));
				float stony = smoothstep(0.45, 0.25, dust);
				// un ravin sec qui serpente : une bande de gravier pâle, plus de cailloux
				float wash_line = abs(noise((q + 0.5) / vec2(240.0, 130.0) + vec2(7.0, 51.0)) - 0.5)
					+ (noise((q + 0.5) / 7.0 + vec2(3.0, 9.0)) - 0.5) * 0.03;
				float wash = rock_washes > 0.0 ? smoothstep(0.05, 0.025, wash_line) * rock_washes : 0.0;
				color = mix(color, rock_soil * vec3(1.22, 1.2, 1.14), floor(wash * 2.0 + bayer(p)) / 2.0 * 0.8);
				vec2 cell = floor(p / 3.0);
				if (hash(cell + vec2(5.1, 3.3)) < rock_gravel * (0.25 + smoothstep(level - 0.14, level, o) + stony * 0.6 + wash * 1.2)) {
					vec2 spot = cell * 3.0 + floor(vec2(hash(cell + vec2(1.7, 8.2)), hash(cell + vec2(6.4, 2.9))) * 2.0);
					if (p == spot) color = mix(vec3(0.56, 0.53, 0.49), vec3(0.36, 0.34, 0.32), hash(cell + vec2(9.0, 2.0)));
					else if (p == spot + vec2(0.0, 1.0)) color *= 0.72;
				}
				// des pierres plus grosses, çà et là
				vec2 big = floor(p / 7.0);
				if (hash(big + vec2(33.4, 70.2)) < rock_gravel * (0.12 + stony * 0.4 + wash * 0.3)) {
					vec2 spot = big * 7.0 + 2.0 + floor(vec2(hash(big + vec2(4.6, 1.3)), hash(big + vec2(7.2, 5.5))) * 3.0);
					vec3 stone;
					if (stone_shape(p - spot, stone_size(big + vec2(8.1)), big, color, stone)) color = mix(stone, rock_soil * 1.05, 0.45); // pas les cailloux clairs de la carte
				}
				// les fissures sèches : celles de la terre brûlée, plus petites et plus fines, sans feu, par endroits
				if (rock_cracks > 0.0 && noise((q + 0.5) / 30.0 + vec2(71.0, 6.0)) > 1.0 - rock_cracks) {
					vec4 crack = vein_at((q + 0.5) * 1.4 + vec2(517.0, 203.0));
					if (crack.x < 0.0 && crack.y < 0.5 + crack.z * 1.2) color *= 0.6;
					else if (crack.x < 1.0 && vein_at((q + vec2(0.5, -0.5)) * 1.4 + vec2(517.0, 203.0)).x < 0.0) color *= 1.08;
				}
				// quelques grandes fissures, plus larges, loin des ravins
				if (rock_cracks > 0.0 && wash < 0.1 && noise((q + 0.5) / 60.0 + vec2(27.0, 64.0)) > 1.0 - rock_cracks * 0.6) {
					vec4 crack = vein_at((q + 0.5) * 0.75 + vec2(91.0, 377.0));
					if (crack.x < 0.0 && crack.y < 0.6 + crack.z * 1.6) color *= crack.y < 0.5 + crack.z * 0.8 ? 0.45 : 0.68;
					else if (crack.x < 1.5 && vein_at((q + vec2(0.5, -0.5)) * 0.75 + vec2(91.0, 377.0)).x < 0.0) color *= 1.1;
				}
				if (outcrop_at(p - vec2(0.0, 2.0)) > level) color *= 0.72; // l'ombre au pied de la roche
			} else {
				color *= 1.06;
				if (outcrop_at(p + vec2(0.0, 2.0)) <= level) color *= 0.7; // la face du bas, dans l'ombre
				else if (outcrop_at(p - vec2(0.0, 1.0)) <= level) color *= 1.18; // le bord du haut, éclairé
			}
		}
		vec2 slab;
		vec2 from;
		slab_edge(p, slab, from);
		if (on_rock) {
			color *= 1.0 + (floor(hash(slab + vec2(2.7, 4.4)) * 3.0) - 1.0) * slab_tone;
			float tilt = hash(slab + vec2(8.3, 1.9)) * 6.2832;
			float facet = clamp(dot(from, vec2(cos(tilt), sin(tilt))) * 2.0, -1.0, 1.0);
			// certaines dalles penchent à peine, et par endroits aucune : l'effet ne dessine pas un pavage
			facet *= mix(0.35, 1.0, hash(slab + vec2(4.1, 9.6))) * smoothstep(0.25, 0.6, noise((p + 0.5) / 48.0 + vec2(19.0, 43.0)));
			color *= 1.0 + clamp(floor(facet * 2.5 + bayer(p)) / 2.5, -1.0, 1.0) * slab_relief;
			if (crack_at(p)) color *= slab_dark;
			else if (crack_at(p + vec2(1.0, 0.0))) color *= slab_light;
		}
	}
	// La terre brûlée : la cendre, les fissures rougeoyantes et les braises
	if (t == burnt_ground && !in_water && stream_depth(p) <= 0.0 && shore_distance(p) > 2.0) {
		float drift = noise((p + 0.5) / 24.0 + vec2(53.0, 8.0)) * 0.65 + noise((p + 0.5) / 6.0 + vec2(16.0, 34.0)) * 0.35;
		color = mix(color, ash_color, floor(smoothstep(0.52, 0.72, drift) * 3.0 + bayer(p)) / 3.0 * burnt_ash);
		if (burnt_veins > 0.0) {
			vec4 vein = vein_at(p + 0.5);
			float breath = burnt_breath(p, vein.w);
			float heat = 1.0 - burnt_glow_depth * (1.0 - breath);
			if (vein.x < 0.0) {
				color *= 0.22;
				float fire = vein.z * burnt_vein_width * 0.7;
				if (vein.z > 0.3 && vein.y < fire && noise((p + 0.5) / 9.0 + vec2(23.0, 58.0)) > burnt_vein_gaps) {
					if (vein.z > 0.62 && vein.y < fire * 0.7) color = mix(mix(color, ember_color, heat), ember_hot, burnt_vein_hot * breath * 2.0);
					else color = mix(color, ember_color, burnt_vein_glow * heat * (vein.z > 0.42 ? 1.0 : 0.5));
				}
			} else if (vein.x < 2.5) {
				if (vein.x < 1.0) color *= 1.0 - burnt_vein_rim;
				if (vein_at(p + vec2(0.5, -0.5)).x < 0.0) color = mix(color, mix(ash_color, ember_color, vein.z * 0.35 * heat), burnt_vein_light);
				else if (vein.z > 0.5 && bayer(p) < 0.3) color = mix(color, ember_color, burnt_vein_halo * (0.4 + 1.2 * breath));
			}
		}
		vec2 cell = floor(p / 9.0);
		if (hash(cell + vec2(47.3, 12.8)) < burnt_embers) {
			vec2 spot = cell * 9.0 + 2.0 + floor(vec2(hash(cell + vec2(3.3, 6.1)), hash(cell + vec2(8.7, 2.4))) * 5.0);
			float ember = 1.0 - burnt_glow_depth * (1.0 - burnt_breath(p, hash(cell + vec2(21.9, 40.2))));
			if (p == spot) color = mix(ember_color, ember_hot, ember * ember);
			else if (p == spot + vec2(1.0, 0.0) || p == spot + vec2(0.0, 1.0)) color = mix(color, ember_color, 0.8 * ember);
		}
	}
	// La neige : les congères et leur ombre bleutée, le scintillement
	if (t == snow_ground && !in_water && stream_depth(p) <= 0.0 && shore_distance(p) > 2.0) {
		vec2 q = (p + 0.5) / vec2(snow_drift_scale, snow_drift_scale * snow_drift_stretch) + vec2(61.0, 17.0);
		q.x += noise((p + 0.5) / 40.0 + vec2(5.0, 72.0)) * 1.5;
		float here = noise(q) * 0.75 + noise((p + 0.5) / 9.0 + vec2(38.0, 11.0)) * 0.25;
		float below = noise(q + vec2(0.0, 2.5 / (snow_drift_scale * snow_drift_stretch))) * 0.75 + noise((p + 0.5 + vec2(0.0, 2.5)) / 9.0 + vec2(38.0, 11.0)) * 0.25;
		float lee = clamp((here - below) * 9.0, -1.0, 1.0);
		if (lee > 0.0) color = mix(color, color * snow_shadow, floor(lee * 3.0 + bayer(p)) / 3.0 * snow_drifts);
		else color *= 1.0 + floor(-lee * 2.0 + bayer(p)) / 2.0 * snow_crest;
		if (hash(p + vec2(17.3, 91.7)) < snow_sparkle) color = vec3(1.0);
	}
	// Le marais : la mousse et la boue, les mares, leurs roseaux et leurs nénuphars, les joncs
	bool swamp_wet = false;
	if (t == swamp_ground && !in_water && stream_depth(p) <= 0.0 && shore_distance(p) > 2.0) {
		// la mousse et la boue, par grandes plaques, en paliers tramés
		float m = noise((p + 0.5) / swamp_moss_scale + vec2(7.0, 52.0)) * 0.7 + noise((p + 0.5) / 12.0 + vec2(44.0, 3.0)) * 0.3;
		float moss = floor(smoothstep(0.5, 0.68, m) * 2.0 + bayer(p)) / 2.0;
		float mire = floor((1.0 - smoothstep(0.26, 0.42, m)) * 2.0 + bayer(p + 1.0)) / 2.0;
		color = mix(color, color * swamp_moss_color, moss * swamp_moss);
		color *= 1.0 - mire * swamp_mire;
		// la boue sombre et bleutée, par grandes plaques (plutôt hors de la mousse), en paliers tramés
		float peat_n = noise((p + 0.5) / swamp_peat_scale + vec2(33.0, 18.0)) * 0.8 + noise((p + 0.5) / 10.0 + vec2(2.0, 66.0)) * 0.2;
		float peat = floor(smoothstep(0.46, 0.64, peat_n) * (1.0 - moss * 0.5) * 3.0 + bayer(p + 2.0)) / 3.0;
		color = mix(color, color * swamp_peat_color, peat * swamp_peat);
		float room = swamp_room(p);
		float pool = swamp_pool(p, room);
		if (pool > 0.0) {
			swamp_wet = true;
			// l'eau : sous la rive du haut, son ombre (la rive reflétée) ; contre la rive du bas, un liseré plus clair ; plus
			// sombre au milieu des grandes mares ; de courtes rides claires, à plat, loin du haut
			// (2026-11-24 : une bande de ciel claire contre le haut faisait le bord éclairé d'une dalle de pierre)
			float from_top = 9.0;
			for (float k = 1.0; k <= 4.0; k += 1.0)
				if (swamp_pool(p - vec2(0.0, k), room) <= 0.0) { from_top = k; break; }
			bool bottom = swamp_pool(p + vec2(0.0, 1.0), room) <= 0.0;
			float streak = noise((p + 0.5) / vec2(6.0, 1.2) + vec2(9.0, 29.0));
			vec3 water_color = pool > 3.0 && noise((p + 0.5) / 11.0 + vec2(3.0, 70.0)) < 0.55 ? swamp_water_deep : swamp_water;
			if (from_top <= 2.0) water_color = swamp_water_deep * (from_top <= 1.0 ? 0.62 : 0.8);
			else if (bottom) water_color = mix(swamp_water, swamp_sky, 0.45);
			else if (from_top > 3.0 && streak > 0.74) water_color = swamp_sky;
			else if (from_top > 3.0 && streak > 0.66 && bayer(p) < 0.5) water_color = mix(water_color, swamp_sky, 0.5);
			// la teinte de la mare : un bruit très doux, plus grand qu'une mare, pour que deux mares voisines diffèrent un peu
			float hue = noise((p + 0.5) / 70.0 + vec2(12.0, 91.0)) * 2.0 - 1.0;
			vec3 tint = hue > 0.0 ? vec3(1.12, 1.02, 0.78) : vec3(0.9, 1.06, 0.92);
			color = mix(water_color, water_color * tint, abs(hue) * swamp_water_vary * 2.0);
			// des nénuphars dans les grandes mares : une feuille ronde entaillée, parfois une fleur
			vec2 cell = floor(p / 10.0);
			if (swamp_lilies > 0.0 && hash(cell + vec2(14.2, 3.7)) < swamp_lilies) {
				vec2 spot = cell * 10.0 + 3.0 + floor(vec2(hash(cell + vec2(2.1, 8.8)), hash(cell + vec2(7.3, 1.6))) * 4.0);
				if (swamp_pool(spot, room) > 3.0) {
					vec2 d = p - spot;
					if ((d.x * d.x) / 5.8 + d.y * d.y / 2.0 <= 1.0 && !(d.x > 0.0 && d.y == 0.0)) {
						vec3 leaf = swamp_lily * (0.9 + 0.2 * hash(cell + vec2(4.4, 4.4)));
						color = d.y > 0.0 ? leaf * 0.72 : (d.y < 0.0 && d.x < 0.0 ? leaf * 1.22 : leaf);
					}
					if (hash(cell + vec2(5.5, 5.5)) < 0.3) {
						if (d == vec2(-1.0, -1.0)) color = vec3(0.95, 0.9, 0.92);
						else if (d == vec2(-1.0, 0.0)) color = vec3(0.92, 0.7, 0.78);
					}
				}
			}
		} else if (pool > -swamp_mud_width - 1.0) {
			// la boue mouillée autour, tramée vers la terre, un trait sombre contre l'eau (seulement contre l'eau : une mare
			// effacée près d'une route ou d'un objet ne laisse qu'une tache de boue, pas un trou noir)
			bool edge = pool > -1.0 && (swamp_pool(p + vec2(1.0, 0.0), room) > 0.0 || swamp_pool(p - vec2(1.0, 0.0), room) > 0.0
				|| swamp_pool(p + vec2(0.0, 1.0), room) > 0.0 || swamp_pool(p - vec2(0.0, 1.0), room) > 0.0);
			if (edge) color = swamp_mud * 0.7;
			else if (pool > -swamp_mud_width || bayer(p) < 0.5) color = mix(color, swamp_mud, 0.5);
		} else if (swamp_tufts > 0.0) {
			// des touffes de joncs sur la terre, éclairées d'en bas à droite, une ombre à leur gauche
			vec2 cell = floor(p / 7.0);
			if (hash(cell + vec2(66.1, 20.4)) < swamp_tufts) {
				vec2 spot = cell * 7.0 + vec2(2.0 + floor(hash(cell + vec2(3.6, 1.8)) * 3.0), 3.0 + floor(hash(cell + vec2(8.2, 5.1)) * 4.0));
				int blade = max(tuft_pixel(p - spot, cell), tuft_pixel(p - spot - vec2(2.0, 1.0), cell + 3.0));
				vec3 rush = vec3(0.3, 0.4, 0.16);
				if (blade == 1) color = rush * 0.7;
				else if (blade == 2) color = rush;
				else if (blade == 3) color = rush * vec3(1.45, 1.4, 1.2);
				else if (tuft_pixel(p + vec2(1.0, 0.0) - spot, cell) > 0) color *= 0.8;
			}
		}
		if (swamp_reeds > 0.0) {
			int reed = swamp_reed_at(p, room);
			if (reed == 1) color = vec3(0.2, 0.27, 0.11);
			else if (reed == 2) color = vec3(0.32, 0.44, 0.17);
			else if (reed == 3) color = vec3(0.5, 0.6, 0.26);
			else if (reed == 4) color = vec3(0.42, 0.26, 0.13);
		}
	}
	// Des cailloux semés sur le sol (l'herbe, le sable), loin des routes
	if (ground_stones > 0.0 && !in_water && !swamp_wet && t != water && t != rock_ground && t != burnt_ground) {
		vec3 stone;
		if (ground_stone_at(p, color, t == snow_ground ? snow_stones : 1.0, stone)) color = stone;
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
		// sans jamais couvrir le milieu : le ruisseau ne s'assèche pas
		bar = min(bar, stream_width * 0.25 + 1.0);
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
			// les deux rangées de terre contre l'eau, mêlées à l'eau claire comme aux rives des rivières, moitié moins
			// (un ruisseau est si étroit qu'il paraîtrait deux fois plus large)
			float lap = depth > -1.0 ? 0.5 : depth > -2.0 ? 0.25 : 0.0;
			color = mix(color, mix(tile_color(water, p), shallow_color, stream_shallow), water_edge * lap);
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
	// autour d'une source : la terre mouillée, plus sombre près de la mare (au bord ondulé, en paliers tramés), et des pierres
	for (int k = 0; k < 48; k++) {
		if (k >= spring_count) break;
		float far = length(p - springs[k]);
		if (far > spring_wet_reach + 3.0) continue;
		if (spring_wet > 0.0 && !in_water && depth <= 0.0) {
			float soak = 1.0 - smoothstep(5.0, spring_wet_reach, far + (noise((p + 0.5) / 5.0 + vec2(31.0, 17.0)) * 2.0 - 1.0) * 3.0);
			soak = clamp(floor(soak * 3.0 + bayer(p)) / 3.0, 0.0, 1.0);
			color = mix(color, color * vec3(0.6, 0.64, 0.58), soak * spring_wet);
		}
		if (spring_stones <= 0.0 || far > 13.0) continue;
		vec2 cell = floor(p / 4.0);
		vec2 spot = cell * 4.0 + vec2(1.0, 0.0) + vec2(0.0, floor(hash(cell + vec2(4.4, 1.1)) * 2.0)); // la pierre et son ombre (la colonne de gauche) restent dans leur carré
		float ring = length(spot - springs[k]);
		vec3 stone;
		if (ring > 6.0 && ring < 11.5 && hash(cell + vec2(7.7, 3.3)) < spring_stones && stream_depth(spot) < -1.5
			&& stone_shape(p - spot, vec2(hash(cell + vec2(6.6, 2.4)) < 0.6 ? 3.0 : 2.0, 2.0), cell, color, stone))
			color = stone * 0.82; // plus sombres que les cailloux des prés : un cercle de pierres qui se voit
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
