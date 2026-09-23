"""
Předzpracování loga hlavičky.

Vstup:  assets/logo_main.png  (šedé line-art, RGBA, průhledné pozadí)
Výstup: assets/logo_main_white.png  – přebarveno na čistě bílé (pro tmavou hlavičku)
        assets/logo_main_dark.png   – přebarveno na tmavě modré #0e274a (pro světlý podklad)

Zachovává se tvar a vyhlazení (alfa kanál), mění se jen barva – tzn. „bílý overlay"
udělaný v grafice, ne přes CSS filtr.

Spuštění:  python assets/build-logo.py
Vyžaduje:  Pillow  (pip install Pillow)
"""

from pathlib import Path
from PIL import Image

SRC = Path(__file__).with_name("logo_main.png")
ALPHA_NOISE = 8      # pixely s alfou <= této hodnoty se zprůhlední (odstranění šumu)
ALPHA_GAIN = 1.12    # jemné zesílení tenkých linek


def recolor(rgb, out_name):
    im = Image.open(SRC).convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            a = px[x, y][3]
            if a <= ALPHA_NOISE:
                px[x, y] = (*rgb, 0)
            else:
                px[x, y] = (*rgb, min(255, int(a * ALPHA_GAIN + 0.5)))
    # ořez průhledných okrajů, aby logo v hlavičce nemělo kolem sebe zbytečnou mezeru
    bbox = im.getbbox()
    if bbox:
        im = im.crop(bbox)
    out = SRC.with_name(out_name)
    im.save(out, optimize=True)
    print("uloženo:", out.name, im.size)


if __name__ == "__main__":
    recolor((255, 255, 255), "logo_main_white.png")
    recolor((14, 39, 74), "logo_main_dark.png")
