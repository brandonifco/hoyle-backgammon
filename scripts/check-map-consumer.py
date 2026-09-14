#!/usr/bin/env python3
"""The consumer phase of the factory's map checks, run on merge(package, overlay).

rules-factory decision 0015: a map's structure is checked before it can be a version, so the
engine does not repeat those checks. It re-runs only the ones its overlay (status,
implementedIn, tests) can change -- `STATUS_DEPENDENT` in rules-factory tools/check-map.py:
`vocabulary`, `status`, `absent` and `correspondence`. 0015 leaves how an engine obtains them to
the engine. This engine transcribes the overlay-dependent part of each, from check-map.py at the
factory commit the package's nuspec names (394de8aa02ef341b2d1782edc08465c222ccdc03 for
RulesFactory.Maps.HoyleBackgammon 1.0.0), so the gate stays offline and has no dependency on the
factory's repository. A new package version whose nuspec names a different commit is the cue to
re-read STATUS_DEPENDENT there.

What each transcription keeps, and what it leaves to publish:

  vocabulary      the closed vocabularies, status among them. The shape checks on beyondAdapter,
                  definedElsewhere and absentFrom read no overlay field and are not repeated.
  status          implementedIn exactly when implemented; an implemented entry names tests; every
                  tests item names a test, once, with a recorded mutation.
  absent          an entry carrying absentFrom is status: declined. Its other rules (scope out,
                  no ambiguity, nothing depends on it) read no overlay field.
  correspondence  a declined entry matches some correspondence row, since it claims no
                  implemented path and owes a runtime reason. Rows 2 and 5, which also branch on
                  status, are held against the code by validate.sh's correspondence step, which
                  is stronger than check-map.py's placement check. The row 3/4 and row 8 overlap
                  rules read no overlay field.

Usage: check-map-consumer.py <merged corpus-map.json>
"""
import json
import pathlib
import sys

KINDS = {"value", "operation", "assertion"}
SCOPES = {"in", "out"}
CLARITIES = {"clear", "ambiguous"}
STATUSES = {"mapped", "blocked", "implemented", "declined"}
FATES = {"decision", "unresolved"}
UNRESOLVED_REASONS = {"OutsideCurrentScope", "UnsupportedRule", "MissingRulesData",
                      "RequiresInterpretation", "UnsupportedInteraction"}


def check_vocabulary(entries):
    bad = []
    for e in entries:
        for field, allowed in (("kind", KINDS), ("scope", SCOPES), ("clarity", CLARITIES), ("status", STATUSES)):
            if field in e and e[field] not in allowed:
                bad.append(f"{e['id']}: {field} is {e[field]!r}, outside {{{', '.join(sorted(allowed))}}}")
        ambiguity = e.get("ambiguity") if isinstance(e.get("ambiguity"), dict) else {}
        if "fate" in ambiguity and ambiguity["fate"] not in FATES:
            bad.append(f"{e['id']}: ambiguity.fate is {ambiguity['fate']!r}")
        reason = ambiguity.get("unresolvedReason")
        if reason is not None and reason not in UNRESOLVED_REASONS:
            bad.append(f"{e['id']}: unresolvedReason is {reason!r}")
    return bad


def check_status(entries):
    bad = []
    for e in entries:
        name = e["id"]
        if "tests" in e:
            tests = e["tests"]
            if not isinstance(tests, list):
                bad.append(f"{name}: tests is not a list")
            else:
                seen = set()
                for i, item in enumerate(tests):
                    if not isinstance(item, dict):
                        bad.append(f"{name}: tests[{i}] is not an object naming a test and its mutation")
                        continue
                    test, mutation = item.get("test"), item.get("mutation")
                    if not isinstance(test, str) or not test.strip():
                        bad.append(f"{name}: tests[{i}] names no test")
                    elif test in seen:
                        bad.append(f"{name}: names test {test!r} twice")
                    else:
                        seen.add(test)
                    if not isinstance(mutation, str) or not mutation.strip():
                        bad.append(f"{name}: tests[{i}] ({test!r}) records no mutation")
        if e.get("status") == "implemented":
            if not e.get("implementedIn"):
                bad.append(f"{name}: status is implemented but no implementedIn names the ruleset revision")
            if not e.get("tests"):
                bad.append(f"{name}: status is implemented but tests names no test that proves it")
        elif e.get("implementedIn"):
            bad.append(f"{name}: carries implementedIn while status is {e.get('status')!r}")
    return bad


def check_absent(entries):
    return [f"{e['id']}: carries absentFrom while status is {e.get('status')!r}; there is no rule to have built"
            for e in entries if "absentFrom" in e and e.get("status") != "declined"]


def matches_a_row(entry, by_id):
    """Whether any correspondence row (1-6, 8; row 7 is unevaluable) holds for the entry."""
    if entry.get("scope") == "out" or entry.get("status") in ("mapped", "blocked"):
        return True
    if "definedElsewhere" in entry or "beyondAdapter" in entry or entry.get("kind") == "assertion":
        return True
    if entry.get("kind") == "operation":
        for dep in entry.get("dependsOn") or []:
            target = by_id.get(dep)
            if isinstance(target, dict) and target.get("kind") == "value" and target.get("status") != "implemented":
                return True
    ambiguity = entry.get("ambiguity") if isinstance(entry.get("ambiguity"), dict) else {}
    return ambiguity.get("fate") == "unresolved"


def check_correspondence(entries):
    by_id = {e["id"]: e for e in entries}
    return [f"{e['id']}: status is declined -- no implemented path at all -- and no correspondence "
            "row says what the engine returns instead"
            for e in entries if e.get("status") == "declined" and not matches_a_row(e, by_id)]


CHECKS = [("vocabulary", check_vocabulary), ("status", check_status),
          ("absent", check_absent), ("correspondence", check_correspondence)]


def main(argv):
    if len(argv) != 2:
        print(__doc__.strip().splitlines()[-1], file=sys.stderr)
        return 2
    entries = json.loads(pathlib.Path(argv[1]).read_text(encoding="utf-8")).get("entries", [])
    if not entries:
        print("error: the map has no entries, so nothing was checked", file=sys.stderr)
        return 1
    failed = False
    for name, check in CHECKS:
        problems = check(entries)
        for p in problems:
            print(f"error: [{name}] {p}", file=sys.stderr)
        print(f"     [{'fail' if problems else 'ok'}] {name}")
        failed |= bool(problems)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
