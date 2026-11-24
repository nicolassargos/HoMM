"""La page des artefacts : chacun avec son image et ses effets, par rareté (docs/artefacts.html).

À relancer quand les artefacts changent : python -I tools/docs/artefacts.py
Les noms restent ceux du jeu ; les effets sont écrits en anglais (la langue de l'utilisateur).
"""
import base64, html, json, os, struct, sys

root = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
out = os.path.join(root, 'docs', 'artefacts.html')
modele = os.path.join(root, 'tools', 'docs', 'artefacts-modele.html')
data = json.load(open(root + '/godot/data/artifacts.json', encoding='utf-8'))['artifacts']

STATS = [('attack', 'Attack'), ('defense', 'Defense'), ('power', 'Power'), ('knowledge', 'Knowledge'),
         ('charisma', 'Charisma'), ('morale', 'Morale'), ('luck', 'Luck')]
SCHOOLS = {'feu': 'Fire', 'terre': 'Earth', 'eau': 'Water', 'air': 'Air'}
RES = {'Gold': 'gold', 'Wood': 'wood', 'Ore': 'ore'}
SLOTS = {'Weapon': 'Weapon', 'Shield': 'Shield', 'Head': 'Head', 'Neck': 'Neck', 'Torso': 'Torso',
         'Shoulders': 'Shoulders', 'Feet': 'Feet', 'Ring': 'Ring', 'Belt': 'Belt', 'Misc': 'Misc'}
POWERS = {
    'lame-boit': 'Each troop heals 10% of the damage it deals.',
    'bouclier-marais': 'The army is immune to fire spells.',
    'masque-epouvante': '−2 morale for all enemy troops.',
    'collier-cloches': 'Wandering monsters of equal or lower strength join the hero without a fight, for a price.',
    'cape-ombre': "Opponents can't see the hero on the map unless they're close.",
    'ailes-cousues': 'The hero flies over forests, mountains and water.',
    'bottes-maree': 'The hero walks on water, no boat needed.',
    'anneau-semeur': '+1 creature per week in every dwelling of your towns.',
    'sablier-envers': 'After a victory, 20% of lost troops come back.',
}
RANKS = [('Minor', 'Minor', 'Common finds, guarded lightly or not at all.'),
         ('Major', 'Major', 'Strong pieces, usually behind a guard.'),
         ('Relic', 'Relic', 'Rare treasures with a power of their own.')]


def signed(n):
    return f'+{n}' if n > 0 else f'−{-n}'


def effects(a):
    parts = []
    for key, label in STATS:
        if a.get(key):
            parts.append((f'{signed(a[key])} {label}', a[key] < 0))
    if a.get('vision'):
        parts.append((f"{signed(a['vision'])} tiles of vision", False))
    if a.get('spellPoints'):
        parts.append((f"{signed(a['spellPoints'])} max spell points", False))
    if a.get('landMovement'):
        parts.append((f"{signed(a['landMovement'])}% movement on land", False))
    if a.get('seaMovement'):
        parts.append((f"{signed(a['seaMovement'])}% movement at sea", False))
    if a.get('noTerrainPenalty'):
        parts.append(('No terrain slows the hero', False))
    if a.get('school'):
        parts.append((f"{SCHOOLS[a['school']]} spells {signed(a['schoolBonus'])}%", False))
    for res, n in a.get('income', {}).items():
        parts.append((f'{signed(n)} {RES[res]} per day', False))
    if a.get('factionResource'):
        parts.append((f"{signed(a['factionResource'])} per day of the faction's own resource", False))
    if a.get('necromancy'):
        parts.append((f"{signed(a['necromancy'])}% necromancy", False))
    if a.get('revealsTowers'):
        parts.append(('Every day, reveals the area around all observation towers', False))
    if a.get('exactArmies'):
        parts.append(('Shows the exact size of enemy troops', False))
    return parts


def png_size(b):
    return struct.unpack('>II', b[16:24])


cards = {r[0]: [] for r in RANKS}
for a in data:
    raw = open(f"{root}/godot/art/artifacts/{a['id']}.png", 'rb').read()
    w, h = png_size(raw)
    src = 'data:image/png;base64,' + base64.b64encode(raw).decode()
    items = ''.join(f'<li class="{"neg" if neg else ""}">{html.escape(t)}</li>' for t, neg in effects(a))
    power = POWERS.get(a['id'])
    power_html = f'<p class="power">{html.escape(power)}</p>' if power else ''
    cards[a['rank']].append(
        f'<article class="card" data-slot="{a["slot"]}">'
        f'<div class="frame"><img src="{src}" width="{w * 2}" height="{h * 2}" alt=""></div>'
        f'<div class="text"><span class="slot">{SLOTS[a["slot"]]}</span>'
        f'<h3>{html.escape(a["name"])}</h3>'
        f'{"<ul>" + items + "</ul>" if items else ""}{power_html}</div></article>')

sections = ''
for key, label, blurb in RANKS:
    sections += (f'<section class="rank rank-{key.lower()}" aria-labelledby="h-{key}">'
                 f'<header><h2 id="h-{key}">{label}</h2><span class="count">{len(cards[key])}</span>'
                 f'<p>{blurb}</p></header><div class="grid">{"".join(cards[key])}</div></section>')

slots_used = []
for a in data:
    if a['slot'] not in slots_used:
        slots_used.append(a['slot'])
chips = '<button type="button" class="chip" aria-pressed="true" data-filter="">All</button>' + ''.join(
    f'<button type="button" class="chip" aria-pressed="false" data-filter="{s}">{SLOTS[s]}</button>' for s in slots_used)

page = open(modele, encoding='utf-8').read()
page = page.replace('{{CHIPS}}', chips).replace('{{SECTIONS}}', sections).replace('{{TOTAL}}', str(len(data)))
open(out, 'w', encoding='utf-8').write(page)
print(len(data), 'artifacts')
