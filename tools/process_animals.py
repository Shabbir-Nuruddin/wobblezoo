"""
Wobble Zoo — animal background remover + sprite prep.

Reads raw AI-generated animal PNGs from Assets/Resources/Art/raw/
(named tier1_hamster.png .. tier8_capybara.png), knocks out the flat studio
background to transparent, trims and centres the animal, and writes clean
game-ready sprites to Assets/Resources/Art/animals/.

- White backgrounds (golden animals) are removed via border-connected flood.
- Pale animals should be generated on a soft solid COLOR background; the same
  border-flood handles any flat colour, not just white.
"""
import os, glob
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(ROOT, "Assets", "Resources", "Art", "raw")
OUT = os.path.join(ROOT, "Assets", "Resources", "Art", "animals")
os.makedirs(OUT, exist_ok=True)
SIZE = 512

def bg_color(arr):
    # Sample the four corners; the background is whatever colour they share.
    h, w, _ = arr.shape
    corners = np.stack([arr[0,0,:3], arr[0,w-1,:3], arr[h-1,0,:3], arr[h-1,w-1,:3]])
    return corners.mean(axis=0)

def remove_bg(path, out_path):
    im = Image.open(path).convert("RGBA")
    arr = np.array(im)
    rgb = arr[:, :, :3].astype(np.int32)

    ref = bg_color(arr)
    dist = np.sqrt(((rgb - ref) ** 2).sum(axis=2))
    flat = dist < 32  # pixels close to the background colour

    # Only remove background that is connected to the image border, so pale
    # fur or a white belly inside the animal is preserved.
    lbl, _ = ndimage.label(flat)
    border = set(lbl[0, :]) | set(lbl[-1, :]) | set(lbl[:, 0]) | set(lbl[:, -1])
    border.discard(0)
    bg = np.isin(lbl, list(border))

    alpha = np.where(bg, 0, 255).astype(np.uint8)
    # Feather the edge a touch to avoid a hard/aliased cutout.
    a = Image.fromarray(alpha, "L").filter(ImageFilter.GaussianBlur(1.1))
    arr[:, :, 3] = np.array(a)
    out = Image.fromarray(arr, "RGBA")

    # Trim to the animal, pad to a square, resize.
    bbox = out.getbbox()
    if bbox:
        out = out.crop(bbox)
    w, h = out.size
    s = max(w, h)
    pad = int(s * 0.06)
    sq = s + pad * 2
    canvas = Image.new("RGBA", (sq, sq), (0, 0, 0, 0))
    canvas.paste(out, ((sq - w) // 2, (sq - h) // 2), out)
    canvas = canvas.resize((SIZE, SIZE), Image.LANCZOS)
    canvas.save(out_path)
    return out_path

def main():
    files = sorted(glob.glob(os.path.join(RAW, "*.png")))
    if not files:
        print("No raw images found in", RAW)
        return
    for f in files:
        name = os.path.basename(f)
        out = os.path.join(OUT, name)
        remove_bg(f, out)
        print("processed ->", out)

if __name__ == "__main__":
    main()
