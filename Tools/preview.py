"""WorldLayout.json'u gerçek sprite'larla PNG olarak çizer (Unity'deki yerleşimin aynısı).
Kullanım: python preview.py <Art/Medieval klasörü> <WorldLayout.json> <çıktı.png> [ölçek]
"""
import glob, json, sys
from PIL import Image, ImageDraw

ART, LAYOUT, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
SCALE = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
HW, HH, GT, WL = 45, 26, 44, 53

def load(p): return Image.open(f"{ART}/{p}").convert("RGBA")

ROAD = {15: 1, 10: 2, 5: 3, 14: 4, 7: 5, 11: 6, 13: 7, 12: 8, 3: 9, 9: 10, 6: 11, 2: 12, 4: 13, 1: 14, 8: 15}
RIVER = {15: 1, 5: 2, 10: 3, 14: 4, 7: 5, 11: 6, 13: 7, 12: 8, 3: 9, 9: 10, 6: 11, 2: 12, 4: 13, 1: 14, 8: 15}
B = {}
for key, folder, sfx, ground, trees, stones, details in [
    ("S", "Spring", "", "grass.png", "Enviroument/Spring/trees/tree({}).png", "Enviroument/Spring/stones/stone({}).png", "Enviroument/Spring/grass_detail({}).png"),
    ("D", "Desert", "_desert", "sand.png", "Enviroument/Desert/cactuses/cactus({}).png", "Enviroument/Desert/stones/stone_deser({}).png", "Enviroument/Desert/sand_detail({}).png"),
    ("W", "Winter", "_winter", "snow.png", "Enviroument/Winter/trees/tree({}).png", "Enviroument/Spring/stones/stone({}).png", "Enviroument/Winter/snow_detail({}).png"),
]:
    B[key] = {
        "ground": load(f"Enviroument/{folder}/{ground}"),
        "road": {m: load(f"Roads/{folder}/road{sfx}({i}).png") for m, i in ROAD.items()},
        "river": {m: load(f"Rivers/{folder}/river{sfx}({i}).png") for m, i in RIVER.items()},
        "bridgeNWSE": load(f"Rivers/{folder}/river{sfx}(16).png"),
        "bridgeNESW": load(f"Rivers/{folder}/river{sfx}(17).png"),
        "trees": [load(trees.format(i)) for i in range(1, 5)],
        "stones": [load(stones.format(i)) for i in range(1, 5)],
        "details": [load(details.format(i)) for i in range(1, 10)],
    }
fields = [load("Buildings/wheat/wheat(1).png"), load("Buildings/wheat/wheat(2).png"),
          load("Buildings/corn/corn(1).png"), load("Buildings/corn/corn(2).png")]
houses = [Image.open(f).convert("RGBA") for f in sorted(glob.glob(f"{ART}/Buildings/house/*/*/*.png"))]
mills = [Image.open(f).convert("RGBA") for f in sorted(glob.glob(f"{ART}/Buildings/mill/*.png"))]
wallU = load("Castle walls/castle_wall(1).png"); wallV = load("Castle walls/castle_wall(2).png")
wallX = load("Castle walls/castle_wall(10).png")
T = {"Blue": "Blue towers/castle_tower_blue", "Red": "Red tower/castle_tower_red",
     "Green": "Green towers/castle_tower_green", "Wood": "Wood towers/castle_tower_wood"}
towers = {k: [load(f"Towers/{p}({i}).png") for i in range(1, 10)] for k, p in T.items()}

L = json.load(open(LAYOUT, encoding="utf-8"))
N = L["size"]
rows = L["biomeRows"]
W = (2 * N + 2) * HW
H = (2 * N + 2) * HH + 300
img = Image.new("RGBA", (W, H), (159, 211, 240, 255))
ox, oy = W // 2, H - 120

def base(u, v): return ((u - v) * HW, (u + v) * HH)
def biome(u, v): return rows[v][u]

tilemap = {(t["u"], t["v"]): t for t in L["tiles"]}
draw = []
for u in range(N):
    for v in range(N):
        b = B[biome(u, v)]
        t = tilemap.get((u, v))
        spr = b["ground"]
        if t:
            if t["t"] == "road": spr = b["road"][t["m"]]
            elif t["t"] == "river": spr = b["river"].get(t["m"], b["ground"])
            elif t["t"] in ("bridgeNWSE", "bridgeNESW"): spr = b[t["t"]]
            elif t["t"] == "field": spr = fields[t.get("var", 0) % 4]
        x, y = base(u, v)
        draw.append((-20000 - (u + v) * 2, spr, x, y))

for p in L["props"]:
    u, v = p["u"], p["v"]
    b = B[biome(u, v)]
    x, y = base(u, v)
    sx = x + (p.get("ox", 0) - p.get("oy", 0)) * HW
    sy = y + GT + HH + (p.get("ox", 0) + p.get("oy", 0)) * HH
    k = p["k"]
    if k == "tree": spr = b["trees"][p["var"] % 4]; sy -= 4
    elif k == "stone": spr = b["stones"][p["var"] % 4]; sy -= 8
    elif k == "detail": spr = b["details"][p["var"] % 9]; sy -= 4
    elif k == "house": spr = houses[p["var"] % len(houses)]; sy -= 14
    else: spr = mills[p["var"] % len(mills)]; sy -= 14
    depth = (sy - GT - HH) / HH
    draw.append((-round(depth * 20) + (0 if k == "detail" else 5), spr, sx, sy))

for c in L["cities"]:
    u0, v0, s = c["u"], c["v"], c["size"]
    col = "Blue" if c["home"] else ("Wood" if c["difficulty"] <= 1 else "Green" if c["difficulty"] <= 2 else "Red")
    for i in range(s):
        for j in range(s):
            u, v = u0 + i, v0 + j
            x, y = base(u, v)
            corner = i in (0, s - 1) and j in (0, s - 1)
            if corner or s == 2: draw.append((-(u + v) * 20 + 1, towers[col][(u * 7 + v * 3) % 9], x, y + GT))
            elif i in (0, s - 1): draw.append((-(u + v) * 20 + 1, wallV, x, y + GT))
            elif j in (0, s - 1): draw.append((-(u + v) * 20 + 1, wallU, x, y + GT))
            else:
                draw.append((-(u + v) * 20 + 1, wallX, x, y + GT))
                draw.append((-(u + v) * 20 + 2, towers[col][4], x, y + GT + WL))

draw.sort(key=lambda d: d[0])
for _, spr, x, y in draw:
    img.alpha_composite(spr, (int(ox + x - spr.width / 2), int(oy - y - spr.height)))
d = ImageDraw.Draw(img)
for c in L["cities"]:
    x, y = base(c["u"] + c["size"] / 2 - 0.5, c["v"] + c["size"] / 2 - 0.5)
    d.text((ox + x - 30, oy - y - GT - 60), f'{c["name"]} ({c["difficulty"]})', fill=(40, 20, 0, 255))
if SCALE != 1.0:
    img = img.resize((int(img.width * SCALE), int(img.height * SCALE)))
img.save(OUT)
print("saved", img.size)
