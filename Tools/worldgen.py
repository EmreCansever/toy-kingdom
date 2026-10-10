"""Toy Kingdom dünya haritası üretici (büyük harita, üç bölge).

Çıktı: WorldLayout.json (Unity'deki ToyKingdom > Örnek içeriği oluştur menüsü okur).
Izgara: u -> ekranda sağ-yukarı (NE), v -> sol-yukarı (NW). Maske bitleri: NE=1, SE=2, SW=4, NW=8.
Bölgeler: S = yeşil vadi (başlangıç), D = çöl (doğu), W = kış (kuzey/batı).

Kullanım: python worldgen.py ../Assets/ToyKingdom/Data/WorldLayout.json [tohum]
"""
import json, math, random, sys

SIZE = 56
NE, SE, SW, NW = 1, 2, 4, 8
DIRS = {NE: (1, 0), SE: (0, -1), SW: (-1, 0), NW: (0, 1)}

NAMES = {
    "S": ["Çam Köyü", "Değirmen Hisarı", "Taş Kale", "Meşe Köyü", "Söğütlü", "Nehir Kalesi",
          "Yeşilyurt", "Kartal Yuvası", "Gümüşsu", "Demirkapı", "Kızılkaya", "Bozkır Kalesi"],
    "D": ["Kumtepe", "Serap Kalesi", "Altın Vaha", "Akrep Hisarı", "Güneşburcu", "Kervansaray",
          "Kızıl Kum", "Hurma Kalesi", "Yakut Vaha"],
    "W": ["Buzdağı", "Karakış Kalesi", "Ayaz Hisarı", "Kurtini", "Kardelen", "Fırtına Kalesi",
          "Kara Kale", "Ejderkaya", "Donmuş Kule"],
}


def biome_at(u, v):
    """Organik sınırlı üç bölge."""
    winter = v > 34 + 3.5 * math.sin(u * 0.21) + 2.0 * math.sin(u * 0.53 + 1.3)
    desert = u > 34 + 3.5 * math.sin(v * 0.19 + 0.7) + 2.0 * math.sin(v * 0.47)
    if winter and desert:
        return "W" if v > u else "D"
    if winter:
        return "W"
    if desert:
        return "D"
    return "S"


def river_walk(rnd, start, main, sides, avoid, length=200):
    """Ana yön + kısa yan adımlardan oluşan kıvrımlı nehir."""
    u, v = start
    cells = [(u, v)]
    side = rnd.choice(sides)
    steps = 0
    while 0 <= u < SIZE and 0 <= v < SIZE and steps < length:
        run = rnd.randint(3, 7)
        for _ in range(run):
            u += DIRS[main][0]; v += DIRS[main][1]
            if not (0 <= u < SIZE and 0 <= v < SIZE):
                return cells
            cells.append((u, v))
        if rnd.random() < 0.55:
            side = rnd.choice(sides)
        for _ in range(rnd.randint(1, 3)):
            nu, nv = u + DIRS[side][0], v + DIRS[side][1]
            if not (2 <= nu < SIZE - 2 and 2 <= nv < SIZE - 2):
                break
            u, v = nu, nv
            cells.append((u, v))
        steps += 1
    return cells


def footprint(c):
    return {(c["u"] + i, c["v"] + j) for i in range(c["size"]) for j in range(c["size"])}


def door(c):
    return (c["u"] + c["size"] // 2, c["v"] - 1)


def l_route(a, b, u_first):
    (u0, v0), (u1, v1) = a, b
    cells = [(u0, v0)]
    u, v = u0, v0
    def su():
        nonlocal u
        while u != u1:
            u += 1 if u1 > u else -1
            cells.append((u, v))
    def sv():
        nonlocal v
        while v != v1:
            v += 1 if v1 > v else -1
            cells.append((u, v))
    if u_first: su(); sv()
    else: sv(); su()
    return cells


def z_routes(a, b):
    (u0, v0), (u1, v1) = a, b
    out = []
    lo, hi = sorted((u0, u1))
    for um in range(lo, hi + 1):
        out.append(l_route(a, (um, v0), True)[:-1] + l_route((um, v0), (um, v1), False)[:-1] + l_route((um, v1), b, True))
    lo, hi = sorted((v0, v1))
    for vm in range(lo, hi + 1):
        out.append(l_route(a, (u0, vm), False)[:-1] + l_route((u0, vm), (u1, vm), True)[:-1] + l_route((u1, vm), b, False))
    return out


def mask_of(cells, cell, extra=()):
    m = 0
    u, v = cell
    for bit, (du, dv) in DIRS.items():
        if (u + du, v + dv) in cells or (u + du, v + dv) in extra:
            m |= bit
    return m


def route_ok(route, river, blocked):
    for i, c in enumerate(route):
        if c in blocked:
            return False
        if c in river:
            if i == 0 or i == len(route) - 1:
                return False
            pu, pv = route[i - 1]; nu, nv = route[i + 1]
            straight_u = pv == c[1] == nv
            straight_v = pu == c[0] == nu
            rm = mask_of(river, c)
            if straight_u and rm != (NW | SE): return False
            if straight_v and rm != (NE | SW): return False
            if not (straight_u or straight_v): return False
            if (pu, pv) in river or (nu, nv) in river: return False
    return True


def generate(seed=11):
    rnd = random.Random(seed)

    # --- nehirler (önce, şehirler bunlardan kaçar)
    river = []
    river += river_walk(rnd, (0, 15), NE, [NW, SE], set(), 40)
    river += river_walk(rnd, (22, SIZE - 1), SE, [NE, SW], set(), 40)
    river_set = set(river)
    # nehir hücrelerinin çakıştığı yerlerde düzensizlik olmasın diye 2x2 blokları ayıkla
    for (u, v) in list(river_set):
        if {(u + 1, v), (u, v + 1), (u + 1, v + 1)} <= river_set:
            river_set.discard((u + 1, v + 1))

    # --- şehirler
    cities = [{"id": "home", "name": "Başkent", "u": 4, "v": 4, "size": 3, "home": True}]
    used_names = {b: list(n) for b, n in NAMES.items()}
    for b in used_names: rnd.shuffle(used_names[b])
    attempts = 0
    while len(cities) < 25 and attempts < 6000:
        attempts += 1
        u = rnd.randint(4, SIZE - 8)
        v = rnd.randint(4, SIZE - 8)
        ok = True
        for c in cities:
            if math.hypot(c["u"] - u, c["v"] - v) < 8.2:
                ok = False; break
        if not ok:
            continue
        s = 3
        fp = {(u + i, v + j) for i in range(-1, s + 1) for j in range(-2, s + 1)}
        if fp & river_set:
            continue
        b = biome_at(u + 1, v + 1)
        if not used_names[b]:
            continue
        cities.append({"id": "", "name": used_names[b].pop(), "u": u, "v": v, "size": s, "home": False})

    # zorluk: başkente uzaklığa göre
    hu, hv = cities[0]["u"], cities[0]["v"]
    maxd = max(math.hypot(c["u"] - hu, c["v"] - hv) for c in cities)
    cities[1:] = sorted(cities[1:], key=lambda c: math.hypot(c["u"] - hu, c["v"] - hv))
    for i, c in enumerate(cities):
        c["biome"] = biome_at(c["u"] + 1, c["v"] + 1)
        if c["home"]:
            c["difficulty"] = 0
            continue
        c["id"] = "city_%02d" % i
        # sıraya göre dengeli zorluk: en yakın 3 şehir 1, sonra 4'er-5'er şehirlik basamaklar
        rank = i - 1
        c["difficulty"] = 1 if rank < 3 else 2 if rank < 7 else 3 if rank < 12 else 4 if rank < 17 else 5
        c["size"] = 2 if c["difficulty"] <= 1 else 3
        c["door"] = list(door(c))
    cities[0]["door"] = list(door(cities[0]))

    # --- yol ağı (Gabriel grafı: ara şehir yoksa iki şehri bağla)
    pts = [(c["u"] + c["size"] / 2, c["v"] + c["size"] / 2) for c in cities]
    edges = []
    for i in range(len(cities)):
        for j in range(i + 1, len(cities)):
            (x1, y1), (x2, y2) = pts[i], pts[j]
            d = math.hypot(x1 - x2, y1 - y2)
            if d > 17:
                continue
            mx, my, r2 = (x1 + x2) / 2, (y1 + y2) / 2, (d / 2) ** 2
            if any((pts[k][0] - mx) ** 2 + (pts[k][1] - my) ** 2 < r2 for k in range(len(cities)) if k not in (i, j)):
                continue
            edges.append((d, i, j))
    edges.sort()

    blocked_all = set()
    for c in cities:
        blocked_all |= footprint(c)
    door_cells = {tuple(c["door"]): c for c in cities}
    blocked_all -= set(door_cells)

    road = set()
    links = []
    parent = list(range(len(cities)))
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    for d, i, j in edges:
        a, b = cities[i], cities[j]
        da, db = tuple(a["door"]), tuple(b["door"])
        others = blocked_all | {dc for dc in door_cells if dc not in (da, db)}
        best = None
        for route in [l_route(da, db, True), l_route(da, db, False)] + z_routes(da, db):
            if not route_ok(route, river_set, others):
                continue
            score = len(route) + sum(1 for c in route if c in river_set) * 3 - sum(1 for c in route if c in road) * 0.5
            if best is None or score < best[0]:
                best = (score, route)
        if best is None:
            continue
        route = best[1]
        road |= set(route)
        links.append({"a": a["id"], "b": b["id"], "cells": [x for c in route for x in c]})
        parent[find(i)] = find(j)

    roots = {find(i) for i in range(len(cities))}
    if len(roots) > 1:
        print("UYARI: yol ağı bağlı değil", len(roots), file=sys.stderr)

    # --- karolar
    tiles = []
    for (u, v) in sorted(road):
        extra = set()
        if (u, v) in door_cells:
            extra.add((u, v + 1))
        if (u, v) in river_set:
            rm = mask_of(river_set, (u, v))
            tiles.append({"u": u, "v": v, "t": "bridgeNWSE" if rm == (NE | SW) else "bridgeNESW", "m": 0})
        else:
            tiles.append({"u": u, "v": v, "t": "road", "m": mask_of(road, (u, v), extra)})
    for (u, v) in river_set:
        if (u, v) in road:
            continue
        m = mask_of(river_set, (u, v))
        if u == 0: m |= SW
        if u == SIZE - 1: m |= NE
        if v == 0: m |= SE
        if v == SIZE - 1: m |= NW
        if m == 0: m = NE | SW
        tiles.append({"u": u, "v": v, "t": "river", "m": m})

    taken = set(road) | river_set | blocked_all | set(door_cells)
    near_city = set()
    for c in cities:
        for (u, v) in footprint(c):
            for du in range(-3, 4):
                for dv in range(-3, 4):
                    near_city.add((u + du, v + dv))

    props = []
    for c in cities:
        cu, cv, s = c["u"], c["v"], c["size"]
        ring = [(u, v) for u in range(cu - 3, cu + s + 3) for v in range(cv - 3, cv + s + 3)
                if 0 <= u < SIZE and 0 <= v < SIZE and (u, v) not in taken]
        rnd.shuffle(ring)
        for cell in ring[:rnd.randint(2, 4)]:
            props.append({"u": cell[0], "v": cell[1], "k": "house", "var": rnd.randrange(32)})
            taken.add(cell)
        if c["biome"] == "S":
            ring = [r for r in ring if r not in taken]
            for cell in ring[:rnd.randint(2, 4)]:
                tiles.append({"u": cell[0], "v": cell[1], "t": "field", "m": 0, "var": rnd.randrange(4)})
                taken.add(cell)
            ring = [r for r in ring if r not in taken]
            if ring and rnd.random() < 0.5:
                props.append({"u": ring[0][0], "v": ring[0][1], "k": "mill", "var": rnd.randrange(8)})
                taken.add(ring[0])

    centers = [(rnd.randrange(SIZE), rnd.randrange(SIZE)) for _ in range(34)]
    for u in range(SIZE):
        for v in range(SIZE):
            if (u, v) in taken:
                continue
            b = biome_at(u, v)
            d = min(((u - a) ** 2 + (v - w) ** 2) ** 0.5 for a, w in centers)
            p = 0.8 if d < 2.2 else 0.45 if d < 3.8 else 0.05
            if b == "D": p *= 0.35   # çölde seyrek kaktüs
            if (u, v) in near_city: p *= 0.25
            r = rnd.random()
            if r < p:
                props.append({"u": u, "v": v, "k": "tree", "var": rnd.randrange(5),
                              "ox": round(rnd.uniform(-0.18, 0.18), 2), "oy": round(rnd.uniform(-0.18, 0.18), 2)})
                if b != "D" and rnd.random() < 0.35:
                    props.append({"u": u, "v": v, "k": "tree", "var": rnd.randrange(5),
                                  "ox": round(rnd.uniform(-0.3, 0.3), 2), "oy": round(rnd.uniform(-0.3, 0.3), 2)})
                taken.add((u, v))
            elif r < p + (0.07 if b == "D" else 0.04):
                props.append({"u": u, "v": v, "k": "stone", "var": rnd.randrange(8)})
                taken.add((u, v))
            elif r < p + 0.18:
                props.append({"u": u, "v": v, "k": "detail", "var": rnd.randrange(9),
                              "ox": round(rnd.uniform(-0.25, 0.25), 2), "oy": round(rnd.uniform(-0.2, 0.2), 2)})

    biome_rows = ["".join(biome_at(u, v) for u in range(SIZE)) for v in range(SIZE)]
    out_cities = [{"id": c["id"], "name": c["name"], "u": c["u"], "v": c["v"], "size": c["size"], "home": c["home"],
                   "door": c["door"], "biome": c["biome"], "difficulty": c["difficulty"]} for c in cities]
    return {"size": SIZE, "biomeRows": biome_rows, "tiles": tiles, "props": props,
            "cities": out_cities, "links": links}


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "WorldLayout.json"
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 11
    layout = generate(seed)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False)
    from collections import Counter
    print("şehir", len(layout["cities"]), "yol", len(layout["links"]), "karo", len(layout["tiles"]), "obje", len(layout["props"]))
    print("bölge:", Counter(c["biome"] for c in layout["cities"]), "zorluk:", Counter(c["difficulty"] for c in layout["cities"]))
