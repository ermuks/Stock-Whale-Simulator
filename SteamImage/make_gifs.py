# -*- coding: utf-8 -*-
"""Looping GIFs for the Steam "About This Game" section, built from in-game frame runs.

    python SteamImage/make_gifs.py

Frames: Validation/ChatProbe/shots-gifrun/s-g-<name>-NNN.png  (PlayerChatProbe request: `gifrun`)
Output: SteamImage/StorePage/gifs/<name>.gif   616 px wide (the width of the description column)

Steam asks for every clip to run 12 seconds or less, every image under 5 MB and the whole description under 15 MB; the script prints both.
"""
import glob, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))).replace('\\', '/') + '/'
FRAMES = ROOT + 'Validation/ChatProbe/shots-gifrun/'
OUT = ROOT + 'SteamImage/StorePage/gifs/'
os.makedirs(OUT, exist_ok=True)
WIDTH = 616

# name: (output file, ms per frame, take every Nth frame, colours, ms to hold the last frame, crop box of the 924x520 frame or None)
GIFS = {
    'desk': ('01_desk.gif', 200, 1, 255, 600, None),
    'surge': ('02_surge.gif', 110, 1, 255, 1800, None),
    'chat': ('03_chat.gif', 200, 1, 255, 1000, None),
    'room': ('04_room.gif', 240, 1, 255, 1000, None),
    'calendar': ('05_calendar.gif', 70, 1, 128, 2200, (150, 0, 780, 520)),
    # 'ignite' frames exist too, but the price falls back within seconds and the clip shows only a thin wick, so it is left out.
}


def build(name, spec):
    out, ms, step, colours, hold, box = spec
    files = sorted(glob.glob(FRAMES + 's-g-' + name + '-*.png'))[::step]
    if not files:
        print(name, 'no frames'); return 0
    frames = []
    for f in files:
        im = Image.open(f).convert('RGB')
        if box: im = im.crop(box)
        frames.append(im.resize((WIDTH, int(im.height * WIDTH / im.width)), Image.LANCZOS))
    # One palette for the whole clip (taken from a strip of sample frames) so colours do not shimmer between frames.
    sample = Image.new('RGB', (WIDTH, frames[0].height * 4))
    for i, k in enumerate((0, len(frames) // 3, 2 * len(frames) // 3, len(frames) - 1)): sample.paste(frames[k], (0, i * frames[0].height))
    palette = sample.quantize(colours, method=Image.MEDIANCUT)
    quantised = [f.quantize(palette=palette, dither=Image.NONE) for f in frames]
    durations = [ms] * len(quantised); durations[-1] = hold
    quantised[0].save(OUT + out, save_all=True, append_images=quantised[1:], duration=durations, loop=0, optimize=True, disposal=1)
    size = os.path.getsize(OUT + out)
    seconds = sum(durations) / 1000.0
    print('%-16s %3d frames  %dx%d  %.2f MB  %.1f s%s%s' % (out, len(quantised), WIDTH, frames[0].height, size / 1048576.0, seconds, '  OVER 5 MB' if size > 5 * 1048576 else '', '  OVER 12 s' if seconds > 12 else ''))
    return size


if __name__ == '__main__':
    total = sum(build(n, s) for n, s in GIFS.items())
    print('total %.2f MB%s' % (total / 1048576.0, '  OVER 15 MB' if total > 15 * 1048576 else ''))
