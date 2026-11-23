# -*- coding: utf-8 -*-
"""Second pass over the Steam images.  The first set (Review/Final) is left as it is;
everything here goes to Review/Round2 so the two can be compared before anything is replaced.

    python SteamImage/make_round2.py

New captures used: Validation/ChatProbe/shots-steamrun/s-*.png  (PlayerChatProbe request: `steamrun`)
"""
import json, os, shutil
from PIL import Image, ImageDraw, ImageFont
import make_assets as A

R1 = A.REVIEW                      # first pass
R2 = A.REVIEW + 'Round2/'
RUN = A.ROOT + 'Validation/ChatProbe/shots-steamrun/'
for sub in ('Candidates', 'Final/Store', 'Final/Library', 'Final/Screenshots', 'Preview', 'Reports'):
    os.makedirs(R2 + sub, exist_ok=True)


def run(name):
    return Image.open(RUN + 's-' + name + '.png').convert('RGB')


def have(name):
    return os.path.exists(RUN + 's-' + name + '.png')


# ── Main capsule: same three-monitor picture, the lettering made to carry at small sizes ─────────
MAIN = dict(size=(1232, 706), source=lambda: A.scene('seat'), gain=1.3, contrast=1.06)
MAIN_CANDIDATES = {
    'main_0_current': dict(MAIN, focus=(.50, .52), zoom=1.04, spot=((.5, .40), .70, .45), veil=dict(bottom=.97, reach=.30), logo=(112, 'bottom')),
    # +18% lettering, same place.
    'main_1_logo_plus18': dict(MAIN, focus=(.50, .52), zoom=1.04, spot=((.5, .40), .70, .45), veil=dict(bottom=1.0, reach=.34), logo=(132, 'bottom')),
    # +30% lettering; the picture is slid up a little so the monitors keep their room.
    'main_2_logo_plus30_shifted': dict(MAIN, focus=(.50, .57), zoom=1.04, spot=((.5, .36), .70, .45), veil=dict(bottom=1.0, reach=.38), logo=(146, 'bottom')),
    # Lettering on top instead (over the wall; the clock ends up behind it).
    'main_3_logo_top': dict(MAIN, focus=(.50, .44), zoom=1.04, spot=((.5, .56), .70, .45), veil=dict(top=1.0, reach=.34), logo=(132, 'top')),
}
MAIN_PICK = 'main_2_logo_plus30_shifted'

# ── Vertical capsule: shot upright in the game, not cut from a wide frame ───────────────────────
VERTICAL = dict(size=(748, 896), gain=1.3, contrast=1.06)
VERTICAL_CANDIDATES = {
    'vertical_0_current': None,   # copied from the first pass
    'vertical_A_chart': dict(VERTICAL, source=lambda: run('tall-chart'), focus=(.5, .40), zoom=1.0, spot=((.5, .36), .75, .4), veil=dict(bottom=1.0, reach=.50), logo=(290, 'bottom', dict(stacked=True))),
    'vertical_A2_chart_far': dict(VERTICAL, source=lambda: run('tall-chart2'), focus=(.5, .42), zoom=1.0, spot=((.5, .38), .75, .4), veil=dict(bottom=1.0, reach=.48), logo=(290, 'bottom', dict(stacked=True))),
    'vertical_B_room': dict(VERTICAL, source=lambda: run('tall-room'), focus=(.5, .42), zoom=1.0, spot=((.55, .40), .8, .35), veil=dict(bottom=1.0, reach=.48), logo=(290, 'bottom', dict(stacked=True))),
    'vertical_B2_room': dict(VERTICAL, source=lambda: run('tall-room2'), focus=(.5, .42), zoom=1.0, spot=((.45, .40), .8, .35), veil=dict(bottom=1.0, reach=.48), logo=(290, 'bottom', dict(stacked=True))),
}
VERTICAL_PICK = 'vertical_B_room'

# ── Screenshots: (file name, source, what it is there to say) ───────────────────────────────────
SCREENSHOTS = [
    ('01_trading_desk', lambda: run('calm-seat'), 'the room and the three monitors on an ordinary morning'),
    ('02_market_surge', lambda: run('surge-chartclose'), 'a stock running: candles, volume, order book'),
    ('03_investors_react', lambda: run('tactic-chat'), 'investors split over another runner: is this the top, or is the train leaving'),
    ('04_whale_tactics', lambda: run('tactic-tablet-after'), 'the tactics tablet in hand, just after Ignite'),
    ('05_take_over_a_company', lambda: A.flat('47-tablet1-small-company'), '50.01% stake, controlled'),
    ('06_grow_the_company', lambda: A.flat('48-tablet1-small-growth'), 'investments and IPO readiness'),
    ('07_investor_profiles', lambda: run('crash-psych'), 'an investor picked from the chat: trust, betrayal, record, and what the lounge is buzzing about'),
    ('08_order_ticket', lambda: A.flat('41-tablet1-orders'), 'limit orders through a real order book'),
    ('09_unlocks', lambda: A.flat('54-tablet1-unlocks'), 'new tools as you grow'),
    ('10_signal_room', lambda: A.flat('view-chat'), 'a signal room: live orders of the host and the analysis AI'),
]


def shrink_strip(items, path, sizes=((1232, 706), (616, 353), (308, 176))):
    """Each candidate at the sizes Steam shows it, side by side, one row per candidate."""
    font = ImageFont.truetype(A.FONT, 18)
    pad = 16; width = pad + sum(w + pad for w, _ in sizes); row = sizes[0][1] + 30 + pad
    out = Image.new('RGB', (width, pad + row * len(items)), (30, 32, 38)); d = ImageDraw.Draw(out)
    for r, (name, im) in enumerate(items):
        x = pad; y = pad + r * row
        d.text((x, y), name, font=font, fill=(200, 205, 212))
        for w, h in sizes:
            out.paste(im.convert('RGB').resize((w, h), Image.LANCZOS), (x, y + 26)); x += w + pad
    out.save(path)


def pair(before, after, path, height=520):
    A.sheet([('before', before), ('after', after)], path, cell_height=height, columns=2)


def main():
    checks = {'sizes': {}, 'screenshots': {}, 'missing_captures': []}
    # Main capsule.
    mains = [(n, A.build(s)) for n, s in MAIN_CANDIDATES.items()]
    for n, im in mains: im.save(R2 + 'Candidates/' + n + '_1232x706.png')
    shrink_strip(mains, R2 + 'Preview/main_capsule_candidates_at_3_sizes.png')
    final_main = dict(mains)[MAIN_PICK]; final_main.save(R2 + 'Final/Store/main_capsule_1232x706.png')
    before_main = Image.open(R1 + 'Final/Store/main_capsule_1232x706.png')
    pair(before_main, final_main, R2 + 'Preview/before_after_main_capsule.png', 420)
    shrink_strip([('before', before_main), ('after', final_main)], R2 + 'Preview/main_capsule_before_after_at_3_sizes.png')

    # Vertical capsule.
    before_vertical = Image.open(R1 + 'Final/Store/vertical_capsule_748x896.png')
    verticals = [('vertical_0_current', before_vertical)]
    for n, s in VERTICAL_CANDIDATES.items():
        if s is None: continue
        im = A.build(s); im.save(R2 + 'Candidates/' + n + '_748x896.png'); verticals.append((n, im))
    A.sheet(verticals, R2 + 'Preview/vertical_capsule_candidates.png', cell_height=540, columns=5)
    final_vertical = dict(verticals).get(VERTICAL_PICK, before_vertical); final_vertical.save(R2 + 'Final/Store/vertical_capsule_748x896.png')
    pair(before_vertical, final_vertical, R2 + 'Preview/before_after_vertical_capsule.png', 600)
    shrink_strip([('before', before_vertical), ('after', final_vertical)], R2 + 'Preview/vertical_capsule_before_after_shrunk.png', sizes=((748, 896), (374, 448), (187, 224)))

    # Everything that did not change is carried over as it is.
    for sub, names in (('Store', ('header_capsule_920x430.png', 'small_capsule_462x174.png', 'page_background_1438x810.png')),
                       ('Library', ('library_capsule_600x900.png', 'library_header_920x430.png', 'library_logo_1280w.png'))):
        for n in names: shutil.copy2(R1 + 'Final/' + sub + '/' + n, R2 + 'Final/' + sub + '/' + n)

    # Library hero.  The asset rules say: "this image should only contain artwork.  There should be no words at all."
    # The first version shows the monitors sharp, so ticker names and chat lines can be read.  This one is the same
    # frame thrown out of focus: the glow, the candles and the room stay, the words do not.
    from PIL import ImageFilter
    hero_before = Image.open(R1 + 'Final/Library/library_hero_3840x1240.png')
    sharp = A.cover(A.scene('seat'), (3840, 1240), (.5, .40))
    soft = A.grade(sharp.filter(ImageFilter.GaussianBlur(9)), 1.3, 1.08); A.spotlight(soft, (.5, .5), .75, .4)
    soft = soft.convert('RGB'); soft.save(R2 + 'Final/Library/library_hero_3840x1240.png'); soft.save(R2 + 'Candidates/hero_B_defocused_3840x1240.png')
    hero_before.save(R2 + 'Candidates/hero_A_sharp_3840x1240.png')
    logo_for_hero = Image.open(R1 + 'Final/Library/library_logo_1280w.png')
    rows = []
    for name, im in (('before: sharp (words readable)', hero_before), ('after: out of focus (no readable words)', soft)):
        half = im.resize((1920, 620), Image.LANCZOS).convert('RGBA'); lg = logo_for_hero.resize((520, int(logo_for_hero.height * 520 / logo_for_hero.width)), Image.LANCZOS)
        half.alpha_composite(lg, (60, 620 - lg.height - 50)); ImageDraw.Draw(half).rectangle([530, 120, 1390, 500], outline=(255, 220, 0, 255), width=2)
        rows.append((name, half))
    A.sheet(rows, R2 + 'Preview/before_after_library_hero_with_logo.png', cell_height=620, columns=1)
    # A 1:1 cut from the middle, to check that nothing can be read.
    soft.crop((1500, 300, 2340, 940)).save(R2 + 'Preview/library_hero_centre_at_full_size.png')

    # Screenshots.
    for old in os.listdir(R2 + 'Final/Screenshots'): os.remove(R2 + 'Final/Screenshots/' + old)
    shots = []
    for name, source, note in SCREENSHOTS:
        im = source()
        if im.size != (1920, 1080): im = im.resize((1920, 1080), Image.LANCZOS)
        im.save(R2 + 'Final/Screenshots/' + name + '.png'); shots.append((name, im))
        checks['screenshots'][name] = {'size': list(im.size), 'ok': im.size == (1920, 1080), 'shows': note}
    A.sheet(shots, R2 + 'Preview/screenshots_contact_sheet.png', cell_height=270, columns=4)
    old_shots = sorted(os.listdir(R1 + 'Final/Screenshots'))
    A.sheet([(n[:-4], Image.open(R1 + 'Final/Screenshots/' + n)) for n in old_shots], R2 + 'Preview/screenshots_contact_sheet_before.png', cell_height=270, columns=4)
    for number, old_name in (('01', '01_trading_desk'), ('03', '03_investors_react'), ('04', '04_whale_tactics'), ('07', '07_investor_profiles'), ('10', '10_the_room')):
        new = [im for n, im in shots if n.startswith(number)][0]
        pair(Image.open(R1 + 'Final/Screenshots/' + old_name + '.png'), new, R2 + 'Preview/before_after_screenshot_' + number + '.png', 430)

    # Library capsule at the sizes it is shown, and the logo's alpha once more.
    cap = Image.open(R2 + 'Final/Library/library_capsule_600x900.png')
    shrink_strip([('library capsule', cap)], R2 + 'Preview/library_capsule_shrunk.png', sizes=((600, 900), (300, 450), (150, 225)))
    logo = Image.open(R2 + 'Final/Library/library_logo_1280w.png'); alpha = logo.split()[3]
    checks['logo'] = {'mode': logo.mode, 'size': list(logo.size), 'corner_alpha': [alpha.getpixel(p) for p in ((0, 0), (logo.width - 1, 0), (0, logo.height - 1), (logo.width - 1, logo.height - 1))],
                      'fully_clear_share': round(alpha.histogram()[0] / float(logo.width * logo.height), 3)}
    for sub in ('Store', 'Library'):
        for n in os.listdir(R2 + 'Final/' + sub):
            im = Image.open(R2 + 'Final/' + sub + '/' + n); want = A.SIZES.get(n)
            checks['sizes'][n] = {'size': list(im.size), 'ok': want is None or im.size == want}
    json.dump(checks, open(R2 + 'Reports/checks.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    bad = [k for g in ('sizes', 'screenshots') for k, v in checks[g].items() if not v['ok']]
    print('round2: screenshots', len(shots), 'failed checks', bad, 'missing', checks['missing_captures'])


if __name__ == '__main__':
    main()
