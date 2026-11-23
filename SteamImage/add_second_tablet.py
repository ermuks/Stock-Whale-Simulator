# -*- coding: utf-8 -*-
"""Adds a second tablet to prologue illustrations 10 and 11 (the game has two tablets on the desk).

    python SteamImage/add_second_tablet.py <source Art folder> <output Art folder>

The tablet drawn in scene 10 is copied, made slightly smaller, and placed on the free desk space to its right.
Scene 11 gets the same copy (there the first tablet is half hidden behind the figure). Nothing else in the pictures changes.
"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter

# Outline of the drawn tablet in Intro_10 (frame included), in picture pixels.
TABLET = [(481, 495), (509, 472), (577, 482), (553, 510)]
CENTER = (528.0, 491.0)
TARGET = (619.0, 497.0)   # where the copy's centre goes
SCALE = 0.8
FILES = ('Intro_10_tiny_room.png', 'Intro_11_monitors.png')


def main(source, output):
    first = Image.open(os.path.join(source, FILES[0])).convert('RGBA')
    mask = Image.new('L', first.size, 0)
    ImageDraw.Draw(mask).polygon(TABLET, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(1.2))
    xs = [p[0] for p in TABLET]; ys = [p[1] for p in TABLET]
    box = (min(xs) - 4, min(ys) - 4, max(xs) + 4, max(ys) + 4)
    patch, patch_mask = first.crop(box), mask.crop(box)
    size = (round(patch.width * SCALE), round(patch.height * SCALE))
    patch, patch_mask = patch.resize(size, Image.LANCZOS), patch_mask.resize(size, Image.LANCZOS)
    # Keep the copy's centre on TARGET.
    left = round(TARGET[0] - (CENTER[0] - box[0]) * SCALE); top = round(TARGET[1] - (CENTER[1] - box[1]) * SCALE)
    os.makedirs(output, exist_ok=True)
    for name in FILES:
        picture = Image.open(os.path.join(source, name)).convert('RGBA')
        alpha = picture.getchannel('A')
        picture.paste(patch, (left, top), patch_mask)
        picture.putalpha(alpha)   # the transparent surround stays exactly as it was
        picture.save(os.path.join(output, name))
        print(name, 'second tablet at', (left, top), size)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
