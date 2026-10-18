"""Refresh the website catalog from playable Student CardData assets.

Run with Python 3 from any directory. Existing per-club media entries are retained.
Only students with deckbuilding=1 qualify; generated units and non-students do not.
"""
import json
import re
import shutil
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[3]
ASSETS = PROJECT / 'Assets/TcgEngine'
PUBLIC = Path(__file__).resolve().parents[1] / 'public'


def field(text, key):
    match = re.search(r'^  ' + re.escape(key) + r': (.*)$', text, re.M)
    value = match[1].strip() if match else ''
    return json.loads(value) if value.startswith('"') else value


def guid(path):
    match = re.search(r'^guid: (\w+)', Path(str(path) + '.meta').read_text(encoding='utf-8-sig'), re.M)
    return match[1]


def club_refs(text):
    match = re.search(r'^  clubs:\s*\n((?:  - .*\n)*)', text, re.M)
    return re.findall(r'guid: (\w+)', match[1]) if match else []


def export():
    definitions = {}
    for path in (ASSETS / 'Resources/Clubs').rglob('*.asset'):
        definitions[guid(path)] = path
    eligible = set()
    students = []
    for path in (ASSETS / 'Resources/Cards').rglob('*.asset'):
        text = path.read_text(encoding='utf-8-sig')
        if field(text, 'type') == '10' and field(text, 'deckbuilding') == '1':
            students.append(path)
            eligible.update(club_refs(text))
    missing = eligible - definitions.keys()
    if missing:
        raise ValueError('Playable student references unknown clubs: ' + ', '.join(sorted(missing)))

    catalog = PUBLIC / 'clubs.json'
    previous = json.loads(catalog.read_text(encoding='utf-8')) if catalog.exists() else []
    media = {club['id']: club.get('media') for school in previous for club in school['clubs']}
    sprites = {}
    for path in (ASSETS / 'Sprites/Clubs').rglob('*.png'):
        sprites[guid(path)] = path
    output = PUBLIC / 'images/clubs'
    output.mkdir(parents=True, exist_ok=True)

    def copy(source, name):
        shutil.copyfile(source, output / name)
        return '/images/clubs/' + name

    copy(ASSETS / 'Sprites/Clubs/club_frame.png', 'frame.png')
    default = copy(ASSETS / 'Sprites/Clubs/club_default_icon.png', 'default.png')
    school_order = ['Trinity', 'Gehenna', 'Millennium', 'Arius', 'SRT']
    paths = [definitions[key] for key in eligible]
    extra_schools = sorted({path.parent.name for path in paths} - set(school_order))
    groups = []
    for school in school_order + extra_schools:
        clubs = []
        for path in sorted(path for path in paths if path.parent.name == school):
            text = path.read_text(encoding='utf-8-sig')
            club_id = field(text, 'id')
            card_path = ASSETS / 'Resources/Cards/Club' / school / path.name
            card = card_path.read_text(encoding='utf-8-sig') if card_path.exists() else ''
            # ClubUI uses CardData.art_board, not ClubData.icon.
            ref = re.search(r'guid: (\w+)', field(card, 'art_board'))
            icon = sprites.get(ref[1]) if ref else None
            image = ASSETS / 'Resources/CardImages' / (club_id + '.png')
            clubs.append({
                'id': club_id,
                'title': field(text, 'title'),
                'icon': copy(icon, club_id + '-icon.png') if icon else default,
                'card': copy(image, club_id + '-card.png') if image.exists() else None,
                'media': media.get(club_id),
            })
        if clubs:
            groups.append({'id': school.lower(), 'name': school, 'clubs': clubs})
    catalog.write_text(json.dumps(groups, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'{len(students)} playable students; {len(eligible)} clubs in {len(groups)} schools')
    for group in groups:
        print(group['name'] + ': ' + ', '.join(club['id'] for club in group['clubs']))


if __name__ == '__main__':
    export()
