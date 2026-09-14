#!/usr/bin/env python3
"""The map this engine is built from: the factory's package, plus this engine's overlay.

rules-factory decision 0015 publishes the map as the NuGet package
RulesFactory.Maps.HoyleBackgammon. The engine references it and never copies it. What the engine
owns is corpus-map.overlay.json, `{ "<entry id>": { "status", "implementedIn", "tests" } }`:
the build facts only the engine can know. corpus-map.json in this repository is
merge(package, overlay), committed because every other step of scripts/validate.sh reads it,
and checked here to be exactly that.

merge(package, overlay), as 0015 defines it -- each rule is also a failure below:

  1. every overlay key names an entry in the package map;
  2. every overlay item sets `status`, and holds no key outside the three;
  3. for a named entry, the three fields come from the overlay alone: they are removed from the
     upstream entry, then the ones the overlay item carries are set. Every other field is
     upstream's;
  4. an entry the overlay does not name is upstream's verbatim, and so are the entry order and
     the top-level fields;
  5. the committed corpus-map.json equals the merge as parsed JSON.

(Rule 6, the consumer-phase checks on the merge, is scripts/check-map-consumer.py.)

Where the overlay's fields land inside an entry is serialisation, not meaning: they are placed,
in the order status, implementedIn, tests, where upstream's `status` was. That keeps
`merge --out` byte-identical to the committed file, so a regenerated map diffs only where the
overlay changed.

  map-overlay.py merge --package-map P --overlay O --out corpus-map.json
  map-overlay.py check --package-map P --package-id ID --package-version V
                       --overlay O --map corpus-map.json --manifest corpus-manifest.json
"""
import argparse
import json
import pathlib
import sys

OWNED = ("status", "implementedIn", "tests")


def load(path):
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8"))


def serialise(document):
    return json.dumps(document, indent=2, ensure_ascii=False) + "\n"


def merge(package, overlay):
    """merge(package, overlay), and every way the overlay breaks rules 1 and 2."""
    problems = []
    if not isinstance(overlay, dict):
        return None, ["the overlay is not an object of entry id -> owned fields"]
    ids = [e.get("id") for e in package.get("entries", [])]
    for entry_id, item in overlay.items():
        if entry_id not in ids:
            problems.append(f"overlay names {entry_id!r}, which the package map has no entry for "
                            "(renamed or removed upstream?)")
        if not isinstance(item, dict):
            problems.append(f"overlay item {entry_id!r} is not an object")
            continue
        if "status" not in item:
            problems.append(f"overlay item {entry_id!r} does not set status")
        for key in item:
            if key not in OWNED:
                problems.append(f"overlay item {entry_id!r} sets {key!r}; an engine owns only "
                                f"{', '.join(OWNED)}, and every other field is the package's")
    if problems:
        return None, problems

    merged = {key: value for key, value in package.items() if key != "entries"}
    merged_entries = []
    for entry in package.get("entries", []):
        item = overlay.get(entry.get("id"))
        if item is None:
            merged_entries.append(entry)
            continue
        out, placed = {}, False
        for key, value in entry.items():
            if key in OWNED:
                if key == "status" and not placed:
                    out.update({k: item[k] for k in OWNED if k in item})
                    placed = True
                continue
            out[key] = value
        if not placed:
            out.update({k: item[k] for k in OWNED if k in item})
        merged_entries.append(out)
    # Keep the package's top-level key order, with entries where the package had them.
    result = {}
    for key in package:
        result[key] = merged_entries if key == "entries" else merged[key]
    return result, []


def canonical(document):
    return json.dumps(document, sort_keys=True, ensure_ascii=False)


def main(argv=None):
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    m = sub.add_parser("merge")
    m.add_argument("--package-map", required=True)
    m.add_argument("--overlay", required=True)
    m.add_argument("--out", required=True)
    c = sub.add_parser("check")
    for flag in ("--package-map", "--package-id", "--package-version", "--overlay", "--map", "--manifest"):
        c.add_argument(flag, required=True)
    args = parser.parse_args(argv)

    package = load(args.package_map)
    overlay = load(args.overlay)
    merged, problems = merge(package, overlay)

    if args.command == "merge":
        if problems:
            for p in problems:
                print(f"error: {p}", file=sys.stderr)
            return 1
        pathlib.Path(args.out).write_text(serialise(merged), encoding="utf-8")
        return 0

    manifest = load(args.manifest)
    pins = [p for p in manifest.get("maps", []) if p.get("path") == "corpus-map.json"]
    if len(pins) != 1:
        problems.append("corpus-manifest.json does not declare corpus-map.json exactly once")
    else:
        pin = pins[0]
        upstream = pin.get("upstream") or {}
        if upstream.get("package") != args.package_id or upstream.get("version") != args.package_version:
            problems.append(f"corpus-manifest.json says the map comes from "
                            f"{upstream.get('package')}@{upstream.get('version')}, but the restored "
                            f"package is {args.package_id}@{args.package_version}")
        if pin.get("overlay") != pathlib.Path(args.overlay).name:
            problems.append(f"corpus-manifest.json names overlay {pin.get('overlay')!r}, the gate "
                            f"merged {pathlib.Path(args.overlay).name!r}")

    if merged is not None:
        committed = load(args.map)
        if canonical(committed) != canonical(merged):
            differing = [e.get("id") for e, n in zip(committed.get("entries", []), merged["entries"])
                         if canonical(e) != canonical(n)]
            if len(committed.get("entries", [])) != len(merged["entries"]):
                differing.append(f"(entry count {len(committed.get('entries', []))} vs "
                                 f"{len(merged['entries'])})")
            top = [k for k in set(committed) | set(merged)
                   if k != "entries" and canonical(committed.get(k)) != canonical(merged.get(k))]
            problems.append(
                "corpus-map.json is not merge(package, overlay). Differing top-level fields: "
                f"{', '.join(sorted(top)) or 'none'}; differing entries: "
                f"{', '.join(map(str, differing)) or 'none'}. Edit the overlay, not the map, and "
                "regenerate with: scripts/map-overlay.py merge --package-map <restored map> "
                "--overlay corpus-map.overlay.json --out corpus-map.json")

    for p in problems:
        print(f"error: {p}", file=sys.stderr)
    if problems:
        return 1
    print(f"     corpus-map.json = merge({args.package_id}@{args.package_version}, "
          f"{pathlib.Path(args.overlay).name}): {len(overlay)} of {len(package['entries'])} entries "
          f"overlaid on {', '.join(OWNED)} only; every other byte of meaning is the package's")
    return 0


if __name__ == "__main__":
    sys.exit(main())
