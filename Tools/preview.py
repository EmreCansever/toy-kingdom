"""WorldLayout.json'u gerçek sprite'larla PNG olarak çizer (Unity'deki yerleşimin aynısı)."""
import json, sys
from PIL import Image, ImageDraw

ART = sys.argv[1]           # .../Art/Medieval
LAYOUT = sys.argv[2]
OUT = sys.argv[3]
HW, HH, GT, WL = 45, 26, 44, 53   # piksel (100 PPU)

def load(p): return Image.open(f"{ART}/{p}").convert("RGBA")

ROAD = {15: 1, 10: 2, 5: 3, 14: 4, 7: 5, 11: 6, 13: 7, 12: 8, 3: 9, 9: 10, 6: 11, 2: 12, 4: 13, 1: 14, 8: 15}
RIVER = {15: 1, 5: 2, 10: 3, 14: 4, 7: 5, 11: 6, 13: 7, 12: 8, 3: 9, 9: 10, 6: 11, 2: 12, 4: 13, 1: 14, 8: 15}
grass = load("Enviroument/Spring/grass.png")
roads = {m: load(f"Roads/Spring/road({i}).png") for m, i in ROAD.items()}
rivers = {m: load(f"Rivers/Spring/river({i}).png") for m, i in RIVER.items()}
bridge = {"bridgeNWSE": load("Rivers/Spring/river(16).png"), "bridgeNESW": load("Rivers/Spring/river(17).png")}
fields = [load("Buildings/wheat/wheat(1).png"), load("Buildings/wheat/wheat(2).png"),
          load("Buildings/corn/corn(1).png"), load("Buildings/corn/corn(2).png")]
trees = [load(f"Enviroument/Spring/trees/tree({i}).png") for i in range(1, 5)]
stones = [load(f"Enviroument/Spring/stones/stone({i}).png") for i in range(1, 5)] + \
         [load(f"Enviroument/Spring/stones/stone_ground ({i}).png") for i in range(1, 5)]
details = [load(f"Enviroument/Spring/grass_detail({i}).png") for i in range(1, 10)]
import glob, os
houses = [Image.open(f).convert("RGBA") for f in sorted(glob.glob(f"{ART}/Buildings/house/*/*/*.png"))] + \
         [Image.open(f).convert("RGBA") for f in sorted(glob.glob(f"{ART}/Buildings/house/*.png"))]
mills = [Image.open(f).convert("RGBA") for f in sorted(glob.glob(f"{ART}/Buildings/mill/*.png"))]
wallU = load("Castle walls/castle_wall(1).png"); wallV = load("Castle walls/castle_wall(2).png")
wallX = load("Castle walls/castle_wall(10).png")
towers = {"Blue": [load(f"Towers/Blue towers/castle_tower_blue({i}).png") for i in range(1, 10)],
          "Red": [load(f"Towers/Red tower/castle_tower_red({i}).png") for i in range(1, 10)],
          "Green": [load(f"Towers/Green towers/castle_tower_green({i}).png") for i in range(1, 10)],
          "Wood": [load(f"Towers/Wood towers/castle_tower_wood({i}).png") for i in range(1, 10)]}
COLORS = {"home": "Blue", "city_01": "Wood", "city_02": "Green", "city_03": "Green", "city_04": "Red", "city_05": "Red"}

L = json.load(open(LAYOUT, encoding="utf-8"))
N = L["size"]
W = (2 * N + 2) * HW
H = (2 * N + 2) * HH + 300
img = Image.new("RGBA", (W, H), (159, 211, 240, 255))
ox = W // 2
oy = H - 120

def blit(sprite, x, y):  # (x,y) = dünya pikseli, sprite pivotu alt-orta
    img.alpha_composite(sprite, (int(ox + x - sprite.width / 2), int(oy - y - sprite.height)))

def base(u, v): return ((u - v) * HW, (u + v) * HH)

tilemap = {(t["u"], t["v"]): t for t in L["tiles"]}
draw = []  # (order, sprite, x, y)
for u in range(N):
    for v in range(N):
        t = tilemap.get((u, v))
        spr = grass
        if t:
            if t["t"] == "road": spr = roads[t["m"]]
            elif t["t"] == "river": spr = rivers.get(t["m"], grass)
            elif t["t"] in bridge: spr = bridge[t["t"]]
            elif t["t"] == "field": spr = fields[t.get("var", 0) % 4]
        x, y = base(u, v)
        draw.append((-20000 - (u + v) * 2, spr, x, y))

for p in L["props"]:
    u, v = p["u"], p["v"]
    x, y = base(u, v)
    sx = x + (p.get("ox", 0) - p.get("oy", 0)) * HW
    sy = y + GT + HH + (p.get("ox", 0) + p.get("oy", 0)) * HH
    k = p["k"]
    if k == "tree": spr = trees[p["var"] % 4]; sy -= 4
    elif k == "stone": spr = stones[p["var"] % 8]; sy -= 8
    elif k == "detail": spr = details[p["var"] % 9]; sy -= 4
    elif k == "house": spr = houses[p["var"] % len(houses)]; sy -= 14
    elif k == "mill": spr = mills[p["var"] % len(mills)]; sy -= 14
    depth = (sy - GT - HH) / HH
    draw.append((-round(depth * 20) + (0 if k == "detail" else 5), spr, sx, sy))

for c in L["cities"]:
    u0, v0, s = c["u"], c["v"], c["size"]
    col = COLORS[c["id"]]
    for i in range(s):
        for j in range(s):
            u, v = u0 + i, v0 + j
            x, y = base(u, v)
            corner = i in (0, s - 1) and j in (0, s - 1)
            if corner or s == 2:
                spr = towers[col][(u * 7 + v * 3) % 9]
                draw.append((-(u + v) * 20 + 1, spr, x, y + GT))
            elif i in (0, s - 1):
                draw.append((-(u + v) * 20 + 1, wallV, x, y + GT))
            elif j in (0, s - 1):
                draw.append((-(u + v) * 20 + 1, wallU, x, y + GT))
            else:
                draw.append((-(u + v) * 20 + 1, wallX, x, y + GT))
                draw.append((-(u + v) * 20 + 2, towers[col][4], x, y + GT + WL))

draw.sort(key=lambda d: d[0])
for _, spr, x, y in draw:
    blit(spr, x, y)
d = ImageDraw.Draw(img)
for c in L["cities"]:
    x, y = base(c["u"] + c["size"] / 2 - 0.5, c["v"] + c["size"] / 2 - 0.5)
    d.text((ox + x - 30, oy - y - GT - 40), c["name"], fill=(40, 20, 0, 255))
img.save(OUT)
print("saved", img.size)
