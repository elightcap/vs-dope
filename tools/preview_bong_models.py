"""Software preview of production meshes with alpha blending; not a game screenshot.
Requires Pillow and numpy. Transparent faces are sorted back to front.
"""
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]


def render(path, size=520, yaw=25, elevation=18):
    model = json.loads(path.read_text())
    textures = {key: np.array(Image.open(ROOT / 'assets/vs-dope/textures' / (value + '.png')).convert('RGBA'))
                for key, value in model['textures'].items()}
    a, b = math.radians(yaw), math.radians(elevation)
    eye = np.array([math.sin(a) * math.cos(b), math.sin(b), math.cos(a) * math.cos(b)])
    right = np.array([math.cos(a), 0, -math.sin(a)])
    camera = np.array([right, -np.cross(eye, right), eye])
    faces = []
    for element in model['elements']:
        x0, y0, z0 = element['from']
        x1, y1, z1 = element['to']
        origin = np.array(element.get('rotationOrigin', [0, 0, 0]))
        ay, az = map(math.radians, [element.get('rotationY', 0), element.get('rotationZ', 0)])
        cy, sy, cz, sz = math.cos(ay), math.sin(ay), math.cos(az), math.sin(az)
        rotation = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]) @ np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
        corners = {'up': [(x0, y1, z0), (x1, y1, z0), (x0, y1, z1)],
                   'down': [(x0, y0, z1), (x1, y0, z1), (x0, y0, z0)],
                   'north': [(x1, y1, z0), (x0, y1, z0), (x1, y0, z0)],
                   'south': [(x0, y1, z1), (x1, y1, z1), (x0, y0, z1)],
                   'east': [(x1, y1, z1), (x1, y1, z0), (x1, y0, z1)],
                   'west': [(x0, y1, z0), (x0, y1, z1), (x0, y0, z0)]}
        for side, material in element['faces'].items():
            world = (np.array(corners[side]) - origin) @ rotation.T + origin
            projected = (world - np.array([8, 7.3, 8])) @ camera.T
            projected[:, :2] = projected[:, :2] * size / 17 + size / 2
            normal = np.cross(world[1] - world[0], world[2] - world[0])
            normal /= max(np.linalg.norm(normal), 1e-8)
            light = .80 + .20 * abs(normal @ np.array([.3, .85, .43]))
            faces.append((projected[:, 2].mean(), projected, material, light))
    output = np.full((size, size, 3), (43, 52, 51), dtype=float)
    for _, points, material, light in sorted(faces, key=lambda face: face[0]):
        p, u, v = points[0], points[1] - points[0], points[2] - points[0]
        matrix = np.array([u[:2], v[:2]]).T
        if abs(np.linalg.det(matrix)) < 1e-6:
            continue
        corners = np.array([p, p + u, p + v, p + u + v])
        lo = np.maximum(np.floor(corners[:, :2].min(0)).astype(int), 0)
        hi = np.minimum(np.ceil(corners[:, :2].max(0)).astype(int), size - 1)
        if np.any(lo > hi):
            continue
        xx, yy = np.meshgrid(np.arange(lo[0], hi[0] + 1), np.arange(lo[1], hi[1] + 1))
        uv = np.stack([xx + .5 - p[0], yy + .5 - p[1]], axis=-1) @ np.linalg.inv(matrix).T
        valid = (uv[:, :, 0] >= 0) & (uv[:, :, 0] <= 1) & (uv[:, :, 1] >= 0) & (uv[:, :, 1] <= 1)
        rect = material['uv']
        texture = textures[material['texture'][1:]]
        tu = (rect[0] + uv[:, :, 0] * (rect[2] - rect[0])) / model['textureWidth']
        tv = (rect[1] + uv[:, :, 1] * (rect[3] - rect[1])) / model['textureHeight']
        rgba = texture[np.clip((tv * texture.shape[0]).astype(int), 0, texture.shape[0] - 1),
                       np.clip((tu * texture.shape[1]).astype(int), 0, texture.shape[1] - 1)]
        alpha = rgba[:, :, 3:4] / 255.0 * valid[:, :, None]
        region = output[lo[1]:hi[1] + 1, lo[0]:hi[0] + 1]
        region[:] = region * (1 - alpha) + rgba[:, :, :3] * light * alpha
    return Image.fromarray(np.clip(output, 0, 255).astype(np.uint8))


def main():
    image = Image.new('RGB', (1080, 600), (43, 52, 51))
    draw = ImageDraw.Draw(image)
    draw.text((24, 18), 'BONG  /  Production model preview', fill=(220, 230, 215))
    for i, (state, label) in enumerate([('empty', 'Bong'), ('loaded', 'Loaded up Bong')]):
        model = ROOT / f'assets/vs-dope/shapes/item/bong-{state}.json'
        image.paste(render(model), (i * 540 + 10, 40))
        draw.text((i * 540 + 24, 565), label, fill=(220, 230, 215))
    path = ROOT / 'docs/previews/bong-items.png'
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)
    print(path)


if __name__ == '__main__':
    main()
