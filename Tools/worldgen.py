"""Toy Kingdom dünya haritası üretici.
Çıktı: WorldLayout.json (Unity builder okur) ve isteğe bağlı önizleme PNG.
Izgara: u -> NE, v -> NW. Maske bitleri: NE=1, SE=2, SW=4, NW=8.
"""
import json, random, sys

SIZE = 36
NE, SE, SW, NW = 1, 2, 4, 8
DIRS = {NE: (1, 0), SE: (0, -1), SW: (-1, 0), NW: (0, 1)}
OPP = {NE: SW, SW: NE, SE: NW, NW: SE}

# id, ad, u, v, boyut, başkent mi
CITIES = [
    ("home",    "Başkent",          3,  3, 3, True),
    ("city_01", "Çam Köyü",        11,  5, 2, False),
    ("city_02", "Değirmen Hisarı",  5, 13, 2, False),
    ("city_03", "Taş Kale",        17, 11, 3, False),
    ("city_04", "Nehir Kalesi",    12, 23, 3, False),
    ("city_05", "Kara Kale",       26, 24, 3, False),
]
LINKS = [("home", "city_01"), ("home", "city_02"), ("city_01", "city_03"),
         ("city_02", "city_04"), ("city_03", "city_04"), ("city_03", "city_05"),
         ("city_04", "city_05")]


def river_cells():
    """Haritayı soldan sağa kesen kıvrımlı nehir (4-komşulu hücre listesi)."""
    pts = []
    u, v = 0, 18
    path = [(NE, 6), (SE, 2), (NE, 7), (NW, 3), (NE, 6), (SE, 4), (NE, 9), (SE, 3), (NE, 4)]
    pts.append((u, v))
    for d, n in path:
        du, dv = DIRS[d]
        for _ in range(n):
            u += du; v += dv
            if 0 <= u < SIZE and 0 <= v < SIZE:
                pts.append((u, v))
    return pts


def footprint(c):
    _, _, u, v, s, _ = c
    return {(u + i, v + j) for i in range(s) for j in range(s)}


def door(c):
    _, _, u, v, s, _ = c
    return (u + s // 2, v - 1)   # kalenin sağ-ön (SE) yüzünün önü


def l_route(a, b, u_first):
    (u0, v0), (u1, v1) = a, b
    cells = [(u0, v0)]
    u, v = u0, v0
    def step_u():
        nonlocal u
        while u != u1:
            u += 1 if u1 > u else -1
            cells.append((u, v))
    def step_v():
        nonlocal v
        while v != v1:
            v += 1 if v1 > v else -1
            cells.append((u, v))
    if u_first: step_u(); step_v()
    else: step_v(); step_u()
    return cells


def z_routes(a, b):
    (u0, v0), (u1, v1) = a, b
    out = []
    lo, hi = sorted((u0, u1))
    for um in range(lo, hi + 1):
        r = l_route(a, (um, v0), True)[:-1] + l_route((um, v0), (um, v1), False)[:-1] + l_route((um, v1), b, True)
        out.append(r)
    lo, hi = sorted((v0, v1))
    for vm in range(lo, hi + 1):
        r = l_route(a, (u0, vm), False)[:-1] + l_route((u0, vm), (u1, vm), True)[:-1] + l_route((u1, vm), b, False)
        out.append(r)
    return out


def mask_of(cells_set, cell, extra=()):
    m = 0
    u, v = cell
    for bit, (du, dv) in DIRS.items():
        if (u + du, v + dv) in cells_set or (u + du, v + dv) in extra:
            m |= bit
    return m


def generate(seed=7):
    rnd = random.Random(seed)
    river = river_cells()
    river_set = set(river)
    city_by_id = {c[0]: c for c in CITIES}
    blocked = set()
    for c in CITIES:
        blocked |= footprint(c)

    road_set = set()
    links_out = []
    for a_id, b_id in LINKS:
        a, b = city_by_id[a_id], city_by_id[b_id]
        da, db = door(a), door(b)
        best = None
        candidates = [l_route(da, db, True), l_route(da, db, False)] + z_routes(da, db)
        for route in candidates:
            if any(c in blocked for c in route):
                continue
            # nehir geçişleri düz ve dik olmalı, nehrin kıvrımına denk gelmemeli
            ok = True
            for i, c in enumerate(route):
                if c in river_set:
                    if i == 0 or i == len(route) - 1: ok = False; break
                    pu, pv = route[i - 1]; nu, nv = route[i + 1]
                    straight_u = pv == c[1] == nv
                    straight_v = pu == c[0] == nu
                    rm = mask_of(river_set, c)
                    if straight_u and rm != (NW | SE): ok = False; break
                    if straight_v and rm != (NE | SW): ok = False; break
                    if not (straight_u or straight_v): ok = False; break
                    # köprünün iki yanı nehir olmamalı
                    if (pu, pv) in river_set or (nu, nv) in river_set: ok = False; break
            if not ok:
                continue
            crossings = sum(1 for c in route if c in river_set)
            score = len(route) + crossings * 3
            if best is None or score < best[0]:
                best = (score, route)
        if best is None:
            print("UYARI: yol bulunamadı", a_id, b_id, file=sys.stderr)
            continue
        route = best[1]
        road_set |= set(route)
        links_out.append({"a": a_id, "b": b_id, "cells": [x for c in route for x in c]})

    tiles = []
    door_cells = {door(c): c for c in CITIES}
    for (u, v) in sorted(road_set):
        extra = set()
        if (u, v) in door_cells:
            extra.add((u, v + 1))  # kaleye doğru
        if (u, v) in river_set:
            rm = mask_of(river_set, (u, v))
            t = "bridgeNWSE" if rm == (NE | SW) else "bridgeNESW"
            tiles.append({"u": u, "v": v, "t": t, "m": 0})
        else:
            tiles.append({"u": u, "v": v, "t": "road", "m": mask_of(road_set, (u, v), extra)})
    for (u, v) in river:
        if (u, v) in road_set:
            continue
        m = mask_of(river_set, (u, v))
        # harita kenarından taşan uçlar
        if u == 0: m |= SW
        if u == SIZE - 1: m |= NE
        if v == 0: m |= SE
        if v == SIZE - 1: m |= NW
        tiles.append({"u": u, "v": v, "t": "river", "m": m})

    taken = set(road_set) | river_set | blocked | set(door_cells)
    near_city = set()
    for c in CITIES:
        for (u, v) in footprint(c):
            for du in range(-3, 4):
                for dv in range(-3, 4):
                    near_city.add((u + du, v + dv))

    props = []
    # tarlalar ve evler şehirlerin çevresinde
    for c in CITIES:
        cu, cv, s = c[2], c[3], c[4]
        ring = [(u, v) for u in range(cu - 3, cu + s + 3) for v in range(cv - 3, cv + s + 3)
                if 0 <= u < SIZE and 0 <= v < SIZE and (u, v) not in taken]
        rnd.shuffle(ring)
        n_house = 3 if c[5] else rnd.randint(2, 4)
        for cell in ring[:n_house]:
            props.append({"u": cell[0], "v": cell[1], "k": "house", "var": rnd.randrange(32)})
            taken.add(cell)
        ring = [r for r in ring if r not in taken]
        for cell in ring[:rnd.randint(2, 4)]:
            tiles.append({"u": cell[0], "v": cell[1], "t": "field", "m": 0, "var": rnd.randrange(4)})
            taken.add(cell)
        if c[0] in ("city_02", "home"):
            ring = [r for r in ring if r not in taken]
            if ring:
                props.append({"u": ring[0][0], "v": ring[0][1], "k": "mill", "var": rnd.randrange(8)})
                taken.add(ring[0])

    # ormanlar: birkaç merkez etrafında yoğun ağaç
    centers = [(rnd.randrange(SIZE), rnd.randrange(SIZE)) for _ in range(15)]
    for (u, v) in [(u, v) for u in range(SIZE) for v in range(SIZE)]:
        if (u, v) in taken:
            continue
        d = min(((u - a) ** 2 + (v - b) ** 2) ** 0.5 for a, b in centers)
        p = 0.8 if d < 2.2 else 0.45 if d < 3.8 else 0.06
        if (u, v) in near_city: p *= 0.25
        r = rnd.random()
        if r < p:
            props.append({"u": u, "v": v, "k": "tree", "var": rnd.randrange(4),
                          "ox": round(rnd.uniform(-0.18, 0.18), 2), "oy": round(rnd.uniform(-0.18, 0.18), 2)})
            if rnd.random() < 0.35:
                props.append({"u": u, "v": v, "k": "tree", "var": rnd.randrange(4),
                              "ox": round(rnd.uniform(-0.3, 0.3), 2), "oy": round(rnd.uniform(-0.3, 0.3), 2)})
            taken.add((u, v))
        elif r < p + 0.04:
            props.append({"u": u, "v": v, "k": "stone", "var": rnd.randrange(8)})
            taken.add((u, v))
        elif r < p + 0.16:
            props.append({"u": u, "v": v, "k": "detail", "var": rnd.randrange(9),
                          "ox": round(rnd.uniform(-0.25, 0.25), 2), "oy": round(rnd.uniform(-0.2, 0.2), 2)})

    cities = [{"id": c[0], "name": c[1], "u": c[2], "v": c[3], "size": c[4], "home": c[5],
               "door": list(door(c))} for c in CITIES]
    return {"size": SIZE, "tiles": tiles, "props": props, "cities": cities, "links": links_out}


if __name__ == "__main__":
    layout = generate()
    out = sys.argv[1] if len(sys.argv) > 1 else "WorldLayout.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False)
    print("tiles", len(layout["tiles"]), "props", len(layout["props"]), "links", len(layout["links"]))
