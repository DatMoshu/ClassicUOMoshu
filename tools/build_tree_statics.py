#!/usr/bin/env python3
"""Build tree-statics.json from the latest static_classify_*.jsonl dump.

Heuristics (per the plan in C:\\Users\\kaise\\.claude\\plans\\851-...md):
  WholeTree   : name has tree-word AND tiledata height >= 10 AND no Foliage flag
  LeafOverlay : name in {leaves, needles, foliage}-words AND has Foliage flag AND height <= 5
  TrunkOnly   : name in {stump, trunk}
  Bush        : everything else flagged isTree by tree.txt but not matched above

After classifying, pair each LeafOverlay to the nearest preceding WholeTree
(within +/-2 graphic IDs) — UO sprite atlas places the canopy overlay
adjacent to its tree.

Run: python prototypes/3DCUO/Tools/build_tree_statics.py
"""
import json
import os
import sys
import glob
import re
from datetime import datetime

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DUMP_DIR = os.path.join(ROOT, "dumps")
OUT_PATH = os.path.join(ROOT, "Data", "tree-statics.json")

TREE_WORDS  = re.compile(r"\b(tree|oak|cedar|walnut|pine|cypress|willow|yew|sapling|spider tree)\b", re.I)
LEAF_WORDS  = re.compile(r"\b(leaves|leaf|needles|foliage|fronds)\b", re.I)
STUMP_WORDS = re.compile(r"\b(stump|trunk|log)\b", re.I)


def latest_dump() -> str:
    paths = sorted(glob.glob(os.path.join(DUMP_DIR, "static_classify_*.jsonl")))
    if not paths:
        sys.exit(f"No dump files found in {DUMP_DIR}")
    return paths[-1]


def classify(name: str, height: int, flags, is_tree: bool) -> tuple[str, str]:
    """Return (kind, tree_type)."""
    name_l = (name or "").lower()
    has_foliage = "Foliage" in flags

    # Identify tree species from name (oak, cedar, ...) — used to pair
    # WholeTree / LeafOverlay later.
    species = "generic"
    for sp in ("oak", "cedar", "walnut", "pine", "cypress", "willow", "yew", "spider"):
        if sp in name_l:
            species = sp
            break

    if STUMP_WORDS.search(name_l):
        return "TrunkOnly", species

    # The "leaves"/"needles" companion overlay: short, Foliage-flagged.
    if LEAF_WORDS.search(name_l) and has_foliage and height <= 5:
        return "LeafOverlay", species

    # The whole-tree sprite: tall, no Foliage flag, named "tree" or species.
    if TREE_WORDS.search(name_l) and height >= 10 and not has_foliage:
        return "WholeTree", species

    # Everything tagged as tree by tree.txt that didn't match → bush/edge case.
    if is_tree:
        return "Bush", species

    return None, species


# Per-species autumn appearance + deciduous status.
# fallHueDeg: HSV hue shift (negative rotates green → yellow → orange → red).
# fallSatBoost: saturation multiplier at peak autumn (>1 = punchier).
# deciduous: drops LeafOverlay statics in winter; evergreens keep needles.
SPECIES_PROFILES = {
    "oak":     {"deciduous": True,  "fallHueDeg": -35.0, "fallSatBoost": 1.55},  # red-orange
    "walnut":  {"deciduous": True,  "fallHueDeg": -55.0, "fallSatBoost": 1.45},  # yellow
    "willow":  {"deciduous": True,  "fallHueDeg": -50.0, "fallSatBoost": 1.35},  # yellow-gold
    "spider":  {"deciduous": True,  "fallHueDeg": -25.0, "fallSatBoost": 1.40},  # orange
    "generic": {"deciduous": True,  "fallHueDeg": -40.0, "fallSatBoost": 1.50},  # orange-red
    "cedar":   {"deciduous": False, "fallHueDeg":   0.0, "fallSatBoost": 1.00},  # stays green
    "pine":    {"deciduous": False, "fallHueDeg":   0.0, "fallSatBoost": 1.00},
    "cypress": {"deciduous": False, "fallHueDeg":  -8.0, "fallSatBoost": 1.05},  # slight warm tinge
    "yew":     {"deciduous": False, "fallHueDeg":   0.0, "fallSatBoost": 1.00},
}


def main() -> None:
    src = latest_dump()
    print(f"[build_tree_statics] reading {src}")

    raw = []
    with open(src, encoding="utf-8-sig") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            raw.append(json.loads(line))

    entries = {}
    for r in raw:
        gid = int(r["graphic"])
        kind, species = classify(
            r.get("name", ""),
            int(r.get("height", 0)),
            r.get("flags", []),
            bool(r.get("isTree", False)),
        )
        if kind is None:
            continue
        prof = SPECIES_PROFILES.get(species, SPECIES_PROFILES["generic"])
        entries[gid] = {
            "kind": kind,
            "name": r.get("name", ""),
            "tree": species,
            "tdHeight": int(r.get("height", 0)),
            "flags": r.get("flags", []),
            "deciduous": prof["deciduous"],
            "fallHueDeg": prof["fallHueDeg"],
            "fallSatBoost": prof["fallSatBoost"],
        }

    # Pair WholeTree ↔ LeafOverlay by adjacency (overlay graphic = tree+1
    # in UO's atlas about 90% of the time; allow +/- 2 fallback).
    wholes = {gid for gid, e in entries.items() if e["kind"] == "WholeTree"}
    for gid, e in entries.items():
        if e["kind"] != "LeafOverlay":
            continue
        for delta in (-1, +1, -2, +2):
            cand = gid + delta
            if cand in wholes:
                e["pairWith"] = cand
                break

    # Stable order — sort by graphic id ascending.
    ordered = {str(k): entries[k] for k in sorted(entries.keys())}

    out = {
        "version": 1,
        "generatedFrom": os.path.basename(src),
        "generatedAt": datetime.now().isoformat(timespec="seconds"),
        "kinds": ["WholeTree", "TrunkOnly", "LeafOverlay", "Bush"],
        "graphics": ordered,
    }

    os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
    with open(OUT_PATH, "w", encoding="utf-8") as fh:
        json.dump(out, fh, indent=2)
    print(f"[build_tree_statics] wrote {len(ordered)} entries -> {OUT_PATH}")

    # Quick stats so the operator can sanity-check.
    by_kind = {}
    for e in ordered.values():
        by_kind[e["kind"]] = by_kind.get(e["kind"], 0) + 1
    for k, n in sorted(by_kind.items()):
        print(f"  {k:<12} {n}")


if __name__ == "__main__":
    main()
