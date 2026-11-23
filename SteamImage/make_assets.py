# -*- coding: utf-8 -*-
"""Steam store / library images for Stock Whale Simulator.

Everything is cut from real in-game captures (no drawn UI, no added copy).  Run:

    python SteamImage/make_assets.py

Inputs
  Validation/ChatProbe/shots-pump-en/scene-*.png   4K room scenes shot during a real price surge
                                                   (PlayerChatProbe request: `pump` then `scenes`)
  Validation/Screens/steam/*.png                   flat 1080p screens from the ScreenTourCapture "steam" tour
Outputs (under SteamImage/Review)
  Original/     the previous set, copied once and never overwritten
  Candidates/   alternative compositions that were compared
  Final/        Store, Library, Screenshots - the files to upload
  Preview/      contact sheets, before/after, shrink tests, hero + logo check
  Reports/      checks.json (sizes, alpha) written by this script

To change a picture, edit its entry in IMAGES below and run again.
"""
import json, os, shutil
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))).replace('\\', '/') + '/'
SCENES = ROOT + 'Validation/ChatProbe/shots-pump-en/'
FLAT = ROOT + 'Validation/Screens/steam/'
OLD = ROOT + 'SteamImage/'
REVIEW = ROOT + 'SteamImage/Review/'
FONT = ROOT + 'Assets/Fonts/News/Pretendard-SemiBold.otf'
RED, BLUE, WHITE, TEAL, INK = (236, 78, 92), (66, 133, 235), (238, 242, 246), (46, 230, 208), (6, 9, 13)
for sub in ('Original/Store', 'Original/Library', 'Original/Screenshots', 'Candidates', 'Final/Store', 'Final/Library', 'Final/Screenshots', 'Preview', 'Reports'):
    os.makedirs(REVIEW + sub, exist_ok=True)


# ── logo ─────────────────────────────────────────────────────────────────────
def candles(size):
    """The app icon's mark: three rising red candles and one tall blue one."""
    s = size / 512.0
    im = Image.new('RGBA', (size, size), (0, 0, 0, 0)); d = ImageDraw.Draw(im)

    def candle(cx, top, bottom, wick_top, wick_bottom, color, width=60):
        d.rounded_rectangle([(cx - 5) * s, wick_top * s, (cx + 5) * s, wick_bottom * s], radius=5 * s, fill=color)
        d.rounded_rectangle([(cx - width / 2) * s, top * s, (cx + width / 2) * s, bottom * s], radius=8 * s, fill=color)
    candle(110, 333, 357, 320, 372, RED); candle(207, 277, 332, 258, 346, RED)
    candle(304, 141, 275, 118, 292, RED); candle(401, 152, 444, 42, 464, BLUE)
    return im.crop(im.getbbox())


def spaced(draw, xy, text, font, fill, spacing):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=font, fill=fill); x += draw.textlength(ch, font=font) + spacing


def text_width(text, font, spacing):
    d = ImageDraw.Draw(Image.new('RGBA', (8, 8)))
    return sum(d.textlength(ch, font=font) for ch in text) + spacing * (len(text) - 1)


def logo(height, stacked=False):
    """Mark + STOCK WHALE / SIMULATOR on a transparent background."""
    big = ImageFont.truetype(FONT, int(height * (.30 if stacked else .40)))
    small = ImageFont.truetype(FONT, int(height * (.15 if stacked else .20)))
    sp_big = big.size * .04
    w_big = text_width('STOCK WHALE', big, sp_big)
    sp_small = (w_big - text_width('SIMULATOR', small, 0)) / 8.0   # second line as wide as the first
    if stacked:
        mark = candles(int(height * .46)); width = int(max(w_big, mark.width)) + 8
        im = Image.new('RGBA', (width, height), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
        im.alpha_composite(mark, ((width - mark.width) // 2, 0))
        y = int(height * .50)
        spaced(d, ((width - w_big) / 2, y), 'STOCK WHALE', big, WHITE, sp_big)
        spaced(d, ((width - w_big) / 2, y + big.size * 1.12), 'SIMULATOR', small, TEAL, sp_small)
        return im
    mark = candles(int(height * .96)); gap = int(height * .16); width = mark.width + gap + int(w_big) + 8
    im = Image.new('RGBA', (width, height), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    im.alpha_composite(mark, (0, (height - mark.height) // 2))
    x, top = mark.width + gap, int(height * .13)
    spaced(d, (x, top), 'STOCK WHALE', big, WHITE, sp_big)
    spaced(d, (x, top + big.size * 1.18), 'SIMULATOR', small, TEAL, sp_small)
    return im


# ── picture helpers ──────────────────────────────────────────────────────────
def scene(name):
    return Image.open(SCENES + 'scene-' + name + '.png').convert('RGB')


def flat(name):
    return Image.open(FLAT + name + '.png').convert('RGB')


def cover(im, size, focus=(.5, .5), zoom=1.0):
    """Scale and crop to fill `size`, keeping `focus` (0..1 of the source) in the middle."""
    w, h = size
    scale = max(w / im.width, h / im.height) * zoom
    r = im.resize((int(im.width * scale + .5), int(im.height * scale + .5)), Image.LANCZOS)
    x = int(min(max(r.width * focus[0] - w / 2, 0), r.width - w)); y = int(min(max(r.height * focus[1] - h / 2, 0), r.height - h))
    return r.crop((x, y, x + w, y + h))


def veil(im, top=0.0, bottom=0.0, left=0.0, reach=.5):
    """Dark gradients where the lettering sits (strength 0..1).  Only the named edges are touched."""
    w, h = im.size
    for strength, vertical, flip in ((top, True, False), (bottom, True, True), (left, False, False)):
        if strength <= 0: continue
        n = h if vertical else w
        line = Image.new('L', (1, n) if vertical else (n, 1))
        for i in range(n):
            t = i / (n - 1.0); t = 1 - t if flip else t
            line.putpixel((0, i) if vertical else (i, 0), int(255 * strength * max(0.0, 1 - t / reach) ** 1.3))
        layer = Image.new('RGBA', (w, h), INK + (255,)); layer.putalpha(line.resize((w, h)))
        im.alpha_composite(layer)
    return im


def spotlight(im, center, radius=.55, edge=.45):
    """Keep the area around `center` (0..1) as it is and darken away from it, so the eye lands there."""
    w, h = im.size
    small = Image.new('L', (64, 64)); px = small.load()
    for y in range(64):
        for x in range(64):
            dx, dy = (x / 63.0 - center[0]) * (w / float(h)), (y / 63.0 - center[1])
            d = (dx * dx + dy * dy) ** .5 / radius
            px[x, y] = int(255 * edge * min(1.0, max(0.0, d - .55) / .9))
    layer = Image.new('RGBA', (w, h), INK + (255,)); layer.putalpha(small.resize((w, h), Image.BICUBIC))
    im.alpha_composite(layer)
    return im


def grade(im, gain=1.0, contrast=1.0):
    im = ImageEnhance.Brightness(im.convert('RGB')).enhance(gain)
    return ImageEnhance.Contrast(im).enhance(contrast).convert('RGBA')


def place_logo(base, height, where, stacked=False, max_share=.94):
    mark = logo(height, stacked)
    limit = base.width * (max_share if where not in ('left', 'top-left') else min(max_share, .56))
    if mark.width > limit:
        k = limit / mark.width; mark = mark.resize((int(mark.width * k), int(mark.height * k)), Image.LANCZOS)
    w, h = base.size
    x = {'left': int(w * .05), 'top-left': int(w * .045)}.get(where, (w - mark.width) // 2)
    y = {'center': (h - mark.height) // 2, 'left': (h - mark.height) // 2, 'top': int(h * .06), 'top-left': int(h * .07),
         'bottom': h - mark.height - int(h * .07)}[where]
    if stacked:
        # The candle mark would read as part of the chart behind it, so the whole lockup gets a soft dark bed.
        bed = Image.new('RGBA', base.size, (0, 0, 0, 0))
        ImageDraw.Draw(bed).rounded_rectangle([x - mark.width * .10, y - mark.height * .10, x + mark.width * 1.10, y + mark.height * 1.25], radius=40, fill=INK + (235,))
        base.alpha_composite(bed.filter(ImageFilter.GaussianBlur(max(18, mark.height // 6))))
    halo = Image.new('RGBA', base.size, (0, 0, 0, 0))
    black = Image.new('RGBA', mark.size, (0, 0, 0, 255)); black.putalpha(mark.split()[3].point(lambda a: int(a * .8)))
    halo.alpha_composite(black, (x, y)); halo = halo.filter(ImageFilter.GaussianBlur(max(5, mark.height // 10)))
    base.alpha_composite(halo); base.alpha_composite(mark, (x, y))
    return base


def build(spec):
    """One picture from one entry of IMAGES."""
    base = cover(spec['source'](), spec['size'], spec.get('focus', (.5, .5)), spec.get('zoom', 1.0))
    if spec.get('blur'): base = base.filter(ImageFilter.GaussianBlur(spec['blur']))
    base = grade(base, spec.get('gain', 1.0), spec.get('contrast', 1.0))
    if spec.get('spot'): spotlight(base, *spec['spot'])
    veil(base, **spec.get('veil', {}))
    if spec.get('logo'): place_logo(base, *spec['logo'][:2], **(spec['logo'][2] if len(spec['logo']) > 2 else {}))
    return base.convert('RGB')


# ── what goes where ──────────────────────────────────────────────────────────
# source: which capture · focus/zoom: the crop · gain/contrast: overall tone · spot: (centre, radius, edge darkening)
# veil: dark gradient under the lettering · logo: (height px, position, options)
S, L = 'Final/Store/', 'Final/Library/'
IMAGES = {
    # Main capsule - B: all three monitors from a little above, the surge in the middle, chat on the right.
    S + 'main_capsule_1232x706.png': dict(size=(1232, 706), source=lambda: scene('seat'), focus=(.50, .52), zoom=1.04, gain=1.3, contrast=1.06,
                                         spot=((.5, .40), .70, .45), veil=dict(bottom=.97, reach=.30), logo=(112, 'bottom')),
    # Header - logo left, the surging chart and the chat on the right.
    S + 'header_capsule_920x430.png': dict(size=(920, 430), source=lambda: scene('chartangle'), focus=(.46, .40), zoom=1.2, gain=1.18, contrast=1.05,
                                           veil=dict(left=.99, reach=.58), logo=(112, 'left', dict(max_share=.40))),
    # Small - the name has to read at 120x45, so the lettering fills it over a dimmed clock.
    S + 'small_capsule_462x174.png': dict(size=(462, 174), source=lambda: scene('chartclose'), focus=(.45, .40), gain=.34, blur=2, logo=(118, 'center')),
    # Vertical - chart, chat and the desk in one upright frame; lettering in the dark lower third.
    S + 'vertical_capsule_748x896.png': dict(size=(748, 896), source=lambda: scene('tall-angle'), focus=(.52, .36), zoom=1.12, gain=1.3, contrast=1.06,
                                             spot=((.45, .36), .7, .45), veil=dict(bottom=1.0, reach=.60), logo=(300, 'bottom', dict(stacked=True))),
    S + 'page_background_1438x810.png': dict(size=(1438, 810), source=lambda: scene('wide'), focus=(.5, .5), gain=.62, blur=3),
    # Library capsule - the room from the chair: clock, the chart in the middle, desk below.
    L + 'library_capsule_600x900.png': dict(size=(600, 900), source=lambda: scene('tall-center'), focus=(.5, .36), zoom=1.1, gain=1.32, contrast=1.06,
                                            spot=((.5, .34), .7, .45), veil=dict(bottom=1.0, reach=.56), logo=(270, 'bottom', dict(stacked=True))),
    L + 'library_header_920x430.png': dict(size=(920, 430), source=lambda: scene('chartangle'), focus=(.46, .40), zoom=1.2, gain=1.18, contrast=1.05,
                                           veil=dict(left=.99, reach=.58), logo=(112, 'left', dict(max_share=.40))),
    # Hero - no lettering.  Cut from the 4K capture; the middle monitor sits in the 860x380 safe area.
    L + 'library_hero_3840x1240.png': dict(size=(3840, 1240), source=lambda: scene('seat'), focus=(.5, .40), gain=1.22, contrast=1.05, spot=((.5, .5), .75, .4)),
}
CANDIDATES = {
    # Compared and not chosen (kept so the choice can be reviewed).
    'main_A_topdown_1232x706.png': dict(size=(1232, 706), source=lambda: scene('tablets'), focus=(.5, .62), gain=1.15, veil=dict(top=.92, reach=.45), logo=(170, 'top')),
    'main_B_three_monitors_1232x706.png': IMAGES[S + 'main_capsule_1232x706.png'],
    'vertical_A_upward_748x896.png': dict(size=(748, 896), source=lambda: scene('lowleft'), focus=(.66, .5), gain=1.15, veil=dict(bottom=.92, reach=.5), logo=(330, 'bottom', dict(stacked=True))),
    'vertical_B_room_748x896.png': IMAGES[S + 'vertical_capsule_748x896.png'],
    'library_A_chat_600x900.png': dict(size=(600, 900), source=lambda: scene('chatside'), focus=(.5, .5), gain=1.15, veil=dict(bottom=.94, reach=.5), logo=(290, 'bottom', dict(stacked=True))),
    'library_B_room_600x900.png': IMAGES[L + 'library_capsule_600x900.png'],
    'library_C_angle_600x900.png': dict(size=(600, 900), source=lambda: scene('tall-angle'), focus=(.52, .36), zoom=1.12, gain=1.3, contrast=1.06,
                                        veil=dict(bottom=.97, reach=.42), logo=(270, 'bottom', dict(stacked=True))),
}
# Screenshots: one feature each, the differences from an ordinary trading game first.
SCREENSHOTS = [
    ('01_trading_desk', lambda: scene('seat'), 'three monitors during a live surge'),
    ('02_market_surge', lambda: scene('chartclose'), 'chart, volume and order book as a stock runs'),
    ('03_investors_react', lambda: scene('chartangle'), 'the chat room reacting to that move'),
    ('04_whale_tactics', lambda: flat('58-tablet2-tactics'), 'ignite, slam, paint the tape, fake bid wall'),
    ('05_take_over_a_company', lambda: flat('47-tablet1-small-company'), '50.01% stake, controlled'),
    ('06_grow_the_company', lambda: flat('48-tablet1-small-growth'), 'investments and IPO readiness'),
    ('07_investor_profiles', lambda: scene('chatfront'), 'tap an investor to see trust and experience'),
    ('08_order_ticket', lambda: flat('41-tablet1-orders'), 'limit orders through a real order book'),
    ('09_unlocks', lambda: flat('54-tablet1-unlocks'), 'new tools as you grow'),
    ('10_the_room', lambda: scene('calendar'), 'the room, days crossed off the calendar'),
]

SIZES = {'main_capsule_1232x706.png': (1232, 706), 'header_capsule_920x430.png': (920, 430), 'small_capsule_462x174.png': (462, 174),
         'vertical_capsule_748x896.png': (748, 896), 'page_background_1438x810.png': (1438, 810), 'library_capsule_600x900.png': (600, 900),
         'library_header_920x430.png': (920, 430), 'library_hero_3840x1240.png': (3840, 1240)}


def sheet(items, path, cell_height=300, columns=3, pad=14, label=True):
    """Contact sheet of (caption, image) pairs."""
    font = ImageFont.truetype(FONT, 18)
    thumbs = [(c, im.convert('RGB').resize((max(1, int(im.width * cell_height / im.height)), cell_height), Image.LANCZOS)) for c, im in items]
    cw = max(t.width for _, t in thumbs); rows = (len(thumbs) + columns - 1) // columns
    out = Image.new('RGB', (pad + columns * (cw + pad), pad + rows * (cell_height + pad + (26 if label else 0))), (30, 32, 38)); d = ImageDraw.Draw(out)
    for i, (c, t) in enumerate(thumbs):
        x = pad + (i % columns) * (cw + pad); y = pad + (i // columns) * (cell_height + pad + (26 if label else 0))
        out.paste(t, (x, y))
        if label: d.text((x, y + cell_height + 3), c, font=font, fill=(200, 205, 212))
    out.save(path); return out


def main():
    checks = {'sizes': {}, 'logo': {}, 'screenshots': {}}
    # Keep the previous set once.
    for sub, src in (('Store', 'SteamResources/Store'), ('Library', 'SteamResources/Library'), ('Screenshots', 'Screenshots')):
        if os.path.isdir(OLD + src):
            for name in os.listdir(OLD + src):
                if not os.path.exists(REVIEW + 'Original/' + sub + '/' + name): shutil.copy2(OLD + src + '/' + name, REVIEW + 'Original/' + sub + '/' + name)
    for name, spec in CANDIDATES.items(): build(spec).save(REVIEW + 'Candidates/' + name)
    for path, spec in IMAGES.items():
        im = build(spec); im.save(REVIEW + path)
        want = SIZES[os.path.basename(path)]; checks['sizes'][os.path.basename(path)] = {'size': list(im.size), 'ok': im.size == want}
    # Logo: real transparency, corners fully clear.
    mark = logo(360); mark = mark.resize((1280, int(mark.height * 1280 / mark.width)), Image.LANCZOS)
    mark.save(REVIEW + L + 'library_logo_1280w.png')
    alpha = mark.split()[3]
    corners = [alpha.getpixel(p) for p in ((0, 0), (mark.width - 1, 0), (0, mark.height - 1), (mark.width - 1, mark.height - 1))]
    hist = alpha.histogram()
    checks['logo'] = {'size': list(mark.size), 'mode': mark.mode, 'corner_alpha': corners, 'clear_pixels_share': round(hist[0] / float(mark.width * mark.height), 3),
                      'partly_clear_pixels': sum(hist[1:255]), 'ok': mark.mode == 'RGBA' and max(corners) == 0 and mark.width == 1280}
    # Screenshots.
    for old in os.listdir(REVIEW + 'Final/Screenshots'): os.remove(REVIEW + 'Final/Screenshots/' + old)
    shots = []
    for name, source, note in SCREENSHOTS:
        im = source()
        if im.size != (1920, 1080): im = im.resize((1920, 1080), Image.LANCZOS)
        im.save(REVIEW + 'Final/Screenshots/' + name + '.png'); shots.append((name, im))
        checks['screenshots'][name] = {'size': list(im.size), 'ok': im.size == (1920, 1080), 'shows': note}

    # ── previews ──
    P = REVIEW + 'Preview/'
    final = lambda p: Image.open(REVIEW + p)
    original = lambda sub, n: Image.open(REVIEW + 'Original/' + sub + '/' + n) if os.path.exists(REVIEW + 'Original/' + sub + '/' + n) else None
    store = ['main_capsule_1232x706.png', 'header_capsule_920x430.png', 'small_capsule_462x174.png', 'vertical_capsule_748x896.png', 'page_background_1438x810.png']
    library = ['library_capsule_600x900.png', 'library_header_920x430.png', 'library_hero_3840x1240.png', 'library_logo_1280w.png']
    sheet([(n, original('Store', n)) for n in store if original('Store', n)], P + 'store_before.png')
    sheet([(n, final(S + n)) for n in store], P + 'store_after.png')
    sheet([(n, original('Library', n)) for n in library if original('Library', n)], P + 'library_before.png')
    sheet([(n, final(L + n)) for n in library], P + 'library_after.png')
    sheet(shots, P + 'screenshots_contact_sheet.png', cell_height=270, columns=4)
    sheet([(n, Image.open(REVIEW + 'Candidates/' + n)) for n in sorted(CANDIDATES)], P + 'candidates.png', cell_height=360, columns=4)
    for n in ('main_capsule_1232x706.png', 'vertical_capsule_748x896.png', 'header_capsule_920x430.png'):
        if original('Store', n): sheet([('before', original('Store', n)), ('after', final(S + n))], P + 'before_after_' + n, cell_height=420, columns=2)
    if original('Library', 'library_capsule_600x900.png'):
        sheet([('before', original('Library', 'library_capsule_600x900.png')), ('after', final(L + 'library_capsule_600x900.png'))], P + 'before_after_library_capsule_600x900.png', cell_height=600, columns=2)
    # Shrink tests at the sizes Steam really shows.
    small = final(S + 'small_capsule_462x174.png')
    test = Image.new('RGB', (462 + 184 + 120 + 80, 174 + 40), (30, 32, 38))
    test.paste(small, (20, 20)); test.paste(small.resize((184, 69), Image.LANCZOS), (502, 20)); test.paste(small.resize((120, 45), Image.LANCZOS), (706, 20))
    test.save(P + 'small_capsule_shrink_462_184_120.png')
    cap = final(L + 'library_capsule_600x900.png'); half = Image.new('RGB', (600 + 300 + 150 + 80, 940), (30, 32, 38))
    half.paste(cap, (20, 20)); half.paste(cap.resize((300, 450), Image.LANCZOS), (640, 20)); half.paste(cap.resize((150, 225), Image.LANCZOS), (960, 20)); half.save(P + 'library_capsule_shrink_600_300_150.png')
    main_c = final(S + 'main_capsule_1232x706.png'); m = Image.new('RGB', (616 + 308 + 60, 353 + 40), (30, 32, 38))
    m.paste(main_c.resize((616, 353), Image.LANCZOS), (20, 20)); m.paste(main_c.resize((308, 176), Image.LANCZOS), (656, 20)); m.save(P + 'main_capsule_shrink.png')
    # Hero as the library shows it: logo laid over, the 860x380 safe area (of the 1920x620 half size) outlined.
    hero = final(L + 'library_hero_3840x1240.png').resize((1920, 620), Image.LANCZOS).convert('RGBA')
    lg = mark.resize((520, int(mark.height * 520 / mark.width)), Image.LANCZOS); hero.alpha_composite(lg, (60, 620 - lg.height - 50))
    ImageDraw.Draw(hero).rectangle([530, 120, 530 + 860, 120 + 380], outline=(255, 220, 0, 255), width=2)
    hero.convert('RGB').save(P + 'library_hero_with_logo_and_safe_area.png')
    json.dump(checks, open(REVIEW + 'Reports/checks.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    bad = [k for group in ('sizes', 'screenshots') for k, v in checks[group].items() if not v['ok']] + ([] if checks['logo']['ok'] else ['logo'])
    print('images', len(IMAGES) + 1, 'screenshots', len(shots), 'candidates', len(CANDIDATES), 'failed checks', bad)


if __name__ == '__main__':
    main()
