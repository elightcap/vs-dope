"""Draw the 16x16 seed-bag label textures for poppy, coca and cannabis seeds.

Vanilla seed textures (assets/survival/textures/item/resource/seeds/) are a small
picture of the grown plant with a 1px black outline; the seedbag shape maps the
whole image onto the bag's front label. Run: python3 tools/build_seed_icons.py
"""
import math
import random
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "assets/vs-dope/textures/item/seeds"
SIZE = 16
OUTLINE = (0, 0, 0, 255)


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


class Canvas:
    def __init__(self, seed):
        self.px = {}
        self.rng = random.Random(seed)

    def put(self, x, y, c):
        if 0 <= x < SIZE and 0 <= y < SIZE:
            self.px[(x, y)] = c

    def palette(self, colors, x, y):
        return self.rng.choice(colors)

    def ellipse(self, cx, cy, rx, ry, angle, colors, rib=None):
        a = math.radians(angle)
        ca, sa = math.cos(a), math.sin(a)
        for y in range(SIZE):
            for x in range(SIZE):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                u = dx * ca + dy * sa
                v = -dx * sa + dy * ca
                if (u / rx) ** 2 + (v / ry) ** 2 <= 1:
                    c = rib if rib and abs(v) < 0.5 and abs(u) < rx - 0.8 else self.palette(colors, x, y)
                    self.put(x, y, c)

    def disc(self, cx, cy, r, colors):
        self.ellipse(cx, cy, r, r, 0, colors)

    def line(self, x0, y0, x1, y1, colors):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(n + 1):
            t = i / n
            self.put(round(x0 + (x1 - x0) * t - 0.5), round(y0 + (y1 - y0) * t - 0.5), self.palette(colors, 0, 0))

    def save(self, name):
        img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        for (x, y), c in self.px.items():
            img.putpixel((x, y), c)
        for y in range(SIZE):
            for x in range(SIZE):
                if (x, y) in self.px:
                    continue
                if any((x + dx, y + dy) in self.px for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    img.putpixel((x, y), OUTLINE)
        OUT.mkdir(parents=True, exist_ok=True)
        img.save(OUT / f"{name}.png")
        return img


def poppy():
    c = Canvas(1)
    stem = [rgb("405e13"), rgb("4b691d")]
    leaf = [rgb("587629"), rgb("506c1e"), rgb("4e6c1f")]
    c.line(7.5, 8, 7.5, 14.5, stem)
    c.ellipse(5.2, 12, 2.6, 0.9, -35, leaf)
    c.ellipse(10, 11, 2.6, 0.9, 30, leaf)
    red = [rgb("c81e1e"), rgb("d93226"), rgb("b5161a")]
    c.ellipse(5.3, 5.3, 2.6, 2.2, -20, red)
    c.ellipse(9.7, 5.3, 2.6, 2.2, 20, red)
    c.ellipse(7.5, 7, 3.2, 2.0, 0, [rgb("e04030"), rgb("c81e1e"), rgb("d93226")])
    c.disc(7.5, 5.8, 1.1, [rgb("1c1410"), rgb("2a1c14")])
    c.put(7, 5, rgb("5a6b2a"))
    return c.save("poppy")


def coca():
    c = Canvas(2)
    stem = [rgb("5a4a22"), rgb("4e4020")]
    leaf = [rgb("4e8a2a"), rgb("58962f"), rgb("467d24")]
    rib = rgb("8fbf5a")
    c.line(8, 14.5, 9.5, 3, stem)
    c.ellipse(10.5, 4.2, 3.2, 1.6, -70, leaf, rib)
    c.ellipse(4.8, 7.2, 3.4, 1.7, 35, leaf, rib)
    c.ellipse(12, 8.8, 2.9, 1.5, -25, leaf, rib)
    berry = [rgb("c42a1c"), rgb("d8392a"), rgb("a81f16")]
    c.disc(5.2, 12, 1.3, berry)
    c.disc(7.3, 13.2, 1.1, berry)
    c.disc(11.6, 12.6, 1.1, berry)
    c.put(4, 11, rgb("f07060"))
    c.put(11, 12, rgb("f07060"))
    return c.save("coca")


CANNABIS = [
    "................",
    ".......L........",
    ".......L........",
    "......LlL.......",
    "..L...LlL...L...",
    "..LL..LlL..LL...",
    "...Ll.LlL.lL....",
    "...LLl.l.lLL....",
    "....LLLlLLL.....",
    ".LLl..LlL..lLL..",
    "...LLLLsLLLL....",
    ".......s........",
    ".......s........",
    ".......s........",
    "................",
    "................",
]


def cannabis():
    # Hand-placed: at 16px the palmate leaflets merge when drawn as ellipses.
    c = Canvas(3)
    leaf = [rgb("3f7a24"), rgb("47862a"), rgb("386e1f")]
    colors = {"L": leaf, "l": [rgb("6ea344")], "s": [rgb("5c7a2a"), rgb("4e6c22")]}
    for y, row in enumerate(CANNABIS):
        for x, ch in enumerate(row):
            if ch in colors:
                c.put(x, y, c.palette(colors[ch], x, y))
    return c.save("cannabis")


if __name__ == "__main__":
    for fn in (poppy, coca, cannabis):
        fn()
    print("wrote", sorted(p.name for p in OUT.glob("*.png")))
