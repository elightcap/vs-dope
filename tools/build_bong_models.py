"""Portable Vintage Story item shapes. Glass bitmap is kept separately; no external texture paths."""
import math
from build_marijuana_models import ASSETS, bud, write

# Small, quiet atlas regions keep narrow faces readable instead of stretching the whole tile.
UV = {'glass': [43, 41, 45, 43], 'rim': [35, 52, 37, 54]}
TEXTURES = {'glass': 'item/bong-glass', 'rim': 'item/bong-glass',
            'bud': 'block/marijuana/marijuana-atlas', 'leaf': 'block/marijuana/marijuana-atlas'}


def box(elements, name, lo, hi, texture='glass', origin=None, yaw=0, pitch=0):
    element = {'name': name, 'from': list(lo), 'to': list(hi),
               'renderPass': 3,  # Same transparent pass as vanilla clutter/art/bottle.json.
               'faces': {face: {'texture': '#' + texture, 'uv': UV[texture]}
                         for face in ('north', 'east', 'south', 'west', 'up', 'down')}}
    if origin is not None:
        element.update(rotationOrigin=list(origin), rotationY=yaw, rotationZ=pitch)
    elements.append(element)


def ring(elements, name, center, radius, bottom, top, thickness, texture='glass'):
    # Eight connected flat walls leave an actual hollow mouth and chamber.
    cx, cz = center
    for side in range(8):
        angle = side * math.pi / 4
        x, z = cx + radius * math.cos(angle), cz + radius * math.sin(angle)
        half_width = (radius + thickness / 2) * math.tan(math.pi / 8)
        box(elements, f'{name}-{side}', (x - half_width, bottom, z - thickness / 2),
            (x + half_width, top, z + thickness / 2), texture,
            origin=(x, bottom, z), yaw=-math.degrees(angle) - 90)


def shape(loaded):
    elements = []
    center = (6.5, 8)
    # Stepped beaker, narrow neck, thick foot and open mouthpiece.
    ring(elements, 'foot', center, 2.7, .3, .85, .65, 'rim')
    box(elements, 'bottom', (4, .45, 5.5), (9, .85, 10.5))
    ring(elements, 'chamber', center, 2.45, .75, 5.1, .20)
    ring(elements, 'lower-shoulder', center, 2.08, 5.0, 5.65, .95)
    ring(elements, 'middle-shoulder', center, 1.50, 5.6, 6.2, .95)
    ring(elements, 'upper-shoulder', center, 1.03, 6.15, 6.75, .70)
    ring(elements, 'neck', center, .91, 6.65, 13.7, .20)
    ring(elements, 'mouth-rim', center, 1.0, 13.7, 14.15, .34, 'rim')
    # Decorative water surface: the requested load recipe needs only a bud.
    # Keep every corner inside the octagonal chamber's inner wall.
    box(elements, 'water', (4.85, .85, 6.35), (8.15, 2.65, 9.65))
    # Downstem intersects the chamber and the bottom of the side bowl.
    start, end = (7.5, 2.6, 8), (11.2, 7.5, 8)
    length = math.dist(start, end)
    pitch = math.degrees(math.atan2(end[1] - start[1], end[0] - start[0]))
    box(elements, 'downstem', (start[0], start[1] - .32, 7.68),
        (start[0] + length, start[1] + .32, 8.32), 'rim', origin=start, pitch=pitch)
    ring(elements, 'bowl', (11.2, 8), .86, 7.25, 8.25, .32)
    ring(elements, 'bowl-lip', (11.2, 8), 1.0, 8.15, 8.55, .34, 'rim')
    box(elements, 'bowl-floor', (10.48, 7.15, 7.28), (11.92, 7.4, 8.72), 'rim')
    if loaded:
        bud(elements, 'packed-buds', (11.2, 7.65, 8), .72, 1.15)
    return {'textureWidth': 64, 'textureHeight': 64, 'textures': TEXTURES, 'elements': elements}


def main():
    for state in ('empty', 'loaded'):
        write(ASSETS / f'shapes/item/bong-{state}.json', shape(state == 'loaded'))
    write(ASSETS / 'itemtypes/bong.json', {
        'code': 'bong', 'class': 'vs-dope.bong',
        'variantgroups': [{'code': 'contents', 'states': ['empty', 'loaded']}],
        'creativeinventory': {'general': ['*'], 'items': ['*']}, 'maxStackSize': 1,
        'shapeByType': {f'*-{state}': {'base': f'item/bong-{state}'} for state in ('empty', 'loaded')},
        'textures': {key: {'base': value} for key, value in TEXTURES.items()},
        'guiTransform': {'rotation': {'x': -15, 'y': -25, 'z': -10}, 'scale': 1.25},
        'groundTransform': {'scale': 1},
        'tpHandTransform': {'translation': {'x': 0, 'y': -.30, 'z': -.15},
                            'rotation': {'x': 0, 'y': 0, 'z': 0}, 'scale': .70},
        'fpHandTransform': {'translation': {'x': 0, 'y': -.20, 'z': 0},
                            'rotation': {'x': 0, 'y': 0, 'z': 0}, 'scale': .70},
        'heldTpIdleAnimation': 'helditemready'
    })


if __name__ == '__main__':
    main()
