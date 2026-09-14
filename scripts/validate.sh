#!/usr/bin/env bash
# validate.sh -- the single canonical gate for this repository.
#
# Modelled on rules-kernel's scripts/validate.sh, and differently shaped: this repository has
# no packable output and no python tooling, so it drops those steps, and it adds the ones an
# engine built from a corpus needs and a kernel does not --
#
#   * the pinned corpus still hashes to the baseline the engine cites, verified under the
#     posture the manifest declares for it, and reported NOT VERIFIED -- never ok -- where
#     that posture leaves the bytes out of reach;
#   * the map does too, because it is the oracle the next two steps judge the code against and
#     the engine writes into it;
#   * the code and the map name the same entries and spell the same citations;
#   * and those citations resolve in the corpus: the page exists, it falls in the section
#     named, and a quoted sentence is on the page it is attributed to;
#   * and every test the map names as proving an implemented entry exists and actually ran.
#
#   ./scripts/validate.sh full   merge-equivalent gate (default)
#   ./scripts/validate.sh fast   Debug only; for the inner loop
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

MODE="${1:-full}"
SOLUTION="HoyleBackgammon.slnx"
FAILED=0
STEP=0

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

if [[ -t 1 ]]; then
  BOLD=$'\033[1m'; RED=$'\033[31m'; GREEN=$'\033[32m'; YEL=$'\033[33m'; OFF=$'\033[0m'
else
  BOLD=""; RED=""; GREEN=""; YEL=""; OFF=""
fi

step() { STEP=$((STEP + 1)); printf '\n%s==> [%d] %s%s\n' "$BOLD" "$STEP" "$1" "$OFF"; }
fail() { printf '%sFAIL%s %s\n' "$RED" "$OFF" "$1"; FAILED=1; }
run() {
  local label="$1"; shift
  if "$@"; then printf '%sok%s   %s\n' "$GREEN" "$OFF" "$label"; return 0; fi
  fail "$label"; return 1
}
# Without this, a failed build is followed by `dotnet test --no-build` against stale
# binaries, which prints "ok". Misleading output is its own defect.
skipped() { printf '%sskip%s %s (depends on a step that failed)\n' "$YEL" "$OFF" "$1"; }

# `dotnet test` exits 0 when it finds nothing to test, so the exit code alone is not
# evidence that this solution's tests ran. Two independent questions are asked of the
# actual TRX output instead:
#   1. did every test project ON DISK (<IsTestProject>true</IsTestProject> in its own
#      .csproj) produce a result file? Deriving the expectation from the repository rather
#      than from $SOLUTION is the point: a project dropped from the solution also drops out
#      of a solution-derived count, so the expectation would fall in step with the actual.
#   2. did those files record more than zero tests?
# Both libraries multi-target net8.0 and net10.0, and `dotnet test` writes one TRX per
# target framework, so the expected file count is (test projects x target frameworks).
expected_result_files() {
  python3 - "$REPO_ROOT" <<'PYEXPECT'
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1])
IGNORED = {"bin", "obj", ".git", "artifacts", "TestResults"}

props = (root / "Directory.Build.props").read_text(encoding="utf-8")
frameworks = re.search(r"<TargetFrameworks>([^<]+)</TargetFrameworks>", props)
per_project = len(frameworks.group(1).split(";")) if frameworks else 1

count = 0
for csproj in sorted(root.rglob("*.csproj")):
    if any(part in IGNORED for part in csproj.parts):
        continue
    text = csproj.read_text(encoding="utf-8", errors="replace")
    if re.search(r"<IsTestProject>\s*true\s*</IsTestProject>", text, re.IGNORECASE):
        count += 1
print(count * per_project)
PYEXPECT
}

assert_tests_ran() {
  local results_dir="$1" expected="$2"
  python3 - "$results_dir" "$expected" <<'PY'
import glob
import sys
import xml.etree.ElementTree as ET

results_dir, expected = sys.argv[1], int(sys.argv[2])
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}

trx_files = sorted(glob.glob(results_dir + "/**/*.trx", recursive=True))
total = 0
for f in trx_files:
    counters = ET.parse(f).getroot().find(".//t:ResultSummary/t:Counters", NS)
    if counters is not None:
        total += int(counters.get("total", "0"))

problems = []
if len(trx_files) != expected:
    problems.append(
        f"expected {expected} result file(s) (test projects on disk x target frameworks), "
        f"found {len(trx_files)}. A test project silently stopped running."
    )
if total == 0:
    problems.append("zero tests were discovered/executed across all test projects")

if problems:
    for p in problems:
        print(f"error: {p}", file=sys.stderr)
    sys.exit(1)
print(f"{total} test(s) across {len(trx_files)} result file(s) actually ran")
PY
}

# rules-factory#2: an `implemented` entry names the tests that prove it, each with the mutation
# that turned it red. The map alone cannot show that a named test exists or ran -- a renamed or
# deleted test leaves a map that still validates, claiming proof by a test nobody has. So this
# reads the same TRX files as assert_tests_ran and requires every named test to have a result
# that actually executed (Passed or Failed; a failure is the suite's to report) in every target
# framework. Names are `Class.Method`, as the map writes them; a short class name that resolves
# to two classes is refused rather than guessed. The mutations are recorded, not re-run.
assert_named_tests_ran() {
  local results_dir="$1" frameworks="$2"
  python3 - "$results_dir" "$frameworks" <<'PYNAMED'
import glob
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

results_dir, frameworks = sys.argv[1], int(sys.argv[2])
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
EXECUTED = {"Passed", "Failed"}

ran = {}           # "Class.Method" -> number of result files in which it executed
classes = {}       # short class name -> fully qualified class names seen
for f in sorted(glob.glob(results_dir + "/**/*.trx", recursive=True)):
    root = ET.parse(f).getroot()
    names = {}
    for unit in root.iterfind(".//t:UnitTest", NS):
        method = unit.find("t:TestMethod", NS)
        full = method.get("className")
        short = full.rsplit(".", 1)[-1]
        classes.setdefault(short, set()).add(full)
        names[unit.get("id")] = f"{short}.{method.get('name')}"
    executed_here = {names[r.get("testId")] for r in root.iterfind(".//t:UnitTestResult", NS)
                     if r.get("outcome") in EXECUTED and r.get("testId") in names}
    for name in executed_here:
        ran[name] = ran.get(name, 0) + 1

mapped = json.loads(pathlib.Path("corpus-map.json").read_text(encoding="utf-8"))
problems, named = [], 0
for entry in mapped["entries"]:
    tests = entry.get("tests") or []
    if entry.get("status") == "implemented" and not tests:
        problems.append(f"{entry['id']}: implemented, and names no test")
    for item in tests:
        named += 1
        test = item.get("test", "")
        short = test.rsplit(".", 1)[0] if "." in test else ""
        if len(classes.get(short, ())) > 1:
            problems.append(f"{entry['id']}: {test!r} is ambiguous; class {short} is "
                            f"{sorted(classes[short])}")
        elif ran.get(test, 0) == 0:
            problems.append(f"{entry['id']}: names {test!r}, which no result file shows running -- "
                            "renamed, deleted, skipped, or never a test")
        elif ran[test] != frameworks:
            problems.append(f"{entry['id']}: {test!r} ran in {ran[test]} result file(s), expected "
                            f"one per target framework ({frameworks})")

for p in problems:
    print(f"error: {p}", file=sys.stderr)
if problems:
    sys.exit(1)
print(f"     {named} test(s) named by {sum(1 for e in mapped['entries'] if e.get('tests'))} entries, "
      f"every one found and executed in all {frameworks} target framework(s)")
PYNAMED
}

EXPECTED_RESULT_FILES="$(expected_result_files)"
TARGET_FRAMEWORKS="$(python3 -c 'import re;m=re.search(r"<TargetFrameworks>([^<]+)</TargetFrameworks>",open("Directory.Build.props").read());print(len(m.group(1).split(";")) if m else 1)')"
RESULTS_DIRS=()
cleanup_results() { if [[ "${#RESULTS_DIRS[@]}" -gt 0 ]]; then rm -rf "${RESULTS_DIRS[@]}"; fi; }
trap cleanup_results EXIT

test_pass() {
  local config="$1" results_dir="$2"; shift 2
  local status=0
  env "$@" dotnet test "$SOLUTION" -c "$config" --no-build --nologo \
    --logger "trx" --results-directory "$results_dir" || status=$?
  if ! assert_tests_ran "$results_dir" "$EXPECTED_RESULT_FILES"; then
    status=1
  fi
  return "$status"
}

step "SDK pin"
pinned="$(python3 -c 'import json;print(json.load(open("global.json"))["sdk"]["version"])')"
roll="$(python3 -c 'import json;print(json.load(open("global.json"))["sdk"].get("rollForward",""))')"
status=0
actual="$(dotnet --version 2>/dev/null)" || status=$?
if [[ "$roll" != "disable" ]]; then
  fail "global.json rollForward is '$roll', expected 'disable'"
elif [[ "$status" -ne 0 ]]; then
  fail "no installed SDK satisfies global.json's pin of $pinned"
elif [[ "$pinned" != "$actual" ]]; then
  fail "global.json pins $pinned but 'dotnet --version' reports $actual"
else
  printf '%sok%s   SDK %s (rollForward=%s)\n' "$GREEN" "$OFF" "$actual" "$roll"
fi

# The engine's SourceBaselineId claims a digest for a named derivation, and whether anyone can
# check that claim is a property of the corpus, not of this script (rules-factory decision
# 0013). Each corpus in corpus-manifest.json declares its verification posture:
#
#   committed-copy  the bytes are at committedPath; this step hashes them, here and in CI.
#   local-copy      the bytes are not in the repository. A holder of a legal copy points the
#                   corpus's envVar at it and this step hashes that. Everyone else -- every CI
#                   run included -- is told NOT VERIFIED, with the reason. Never ok.
#
# NOT VERIFIED is its own outcome, neither ok nor FAIL: a never-commit corpus cannot be verified
# in CI, and failing every run for it would teach people to ignore the gate. It does not pass
# silently either -- the final line names it. This corpus is pin-in-repo and committed, so it
# verifies; the step is written for the posture it does not have, and has been run under it.
step "Corpus verification posture"
NOT_VERIFIED=()
posture_status=0
python3 - <<'PY' || posture_status=$?
import hashlib
import json
import os
import pathlib
import re
import sys

# The derivations this step knows how to recompute. A declared derivation it does not know is a
# failure rather than a pass: a digest nobody can re-derive has not been checked.
DERIVATIONS = {
    "gutenberg-plain-text-including-boilerplate": lambda b: hashlib.sha256(b).hexdigest(),
}

manifest = json.loads(pathlib.Path("corpus-manifest.json").read_text(encoding="utf-8"))
mapped = json.loads(pathlib.Path("corpus-map.json").read_text(encoding="utf-8"))
source = pathlib.Path("src/HoyleBackgammon/MapEntries.cs").read_text(encoding="utf-8")
cited = re.search(r'contentHash: "([0-9a-f]{64})"', source).group(1)

problems, verified, unverified = [], [], []
corpora = manifest.get("corpora") or []
if not corpora:
    problems.append("corpus-manifest.json declares no corpora, so nothing was verified")

for corpus in corpora:
    sid = corpus.get("sourceId", "?")
    posture = corpus.get("verification")
    boundary = corpus.get("boundaryPolicy")
    expected = corpus.get("contentHash")
    derive = DERIVATIONS.get(corpus.get("hashDerivation"))

    if sid == mapped.get("corpus"):
        if mapped["baseline"]["contentHash"] != expected:
            problems.append(f"{sid}: the map's baseline is {mapped['baseline']['contentHash']}, "
                            f"the manifest's is {expected}")
        if cited != expected:
            problems.append(f"{sid}: MapEntries.Baseline cites {cited}, the manifest says {expected}")

    if posture not in ("committed-copy", "local-copy"):
        problems.append(f"{sid}: verification is {posture!r}; it must be declared committed-copy or "
                        "local-copy, and missing is not a default")
        continue
    if boundary == "never-commit" and posture == "committed-copy":
        problems.append(f"{sid}: a never-commit corpus cannot be committed-copy")
        continue
    if derive is None:
        problems.append(f"{sid}: this step cannot recompute hashDerivation "
                        f"{corpus.get('hashDerivation')!r}, so the baseline is unchecked")
        continue

    if posture == "committed-copy":
        path = corpus.get("committedPath")
        if not path or not pathlib.Path(path).is_file():
            problems.append(f"{sid}: committed-copy names committedPath {path!r}, which is not a file")
            continue
        where = f"committed at {path}"
    else:
        var = corpus.get("envVar")
        if not var:
            problems.append(f"{sid}: local-copy names no envVar")
            continue
        path = os.environ.get(var)
        if not path:
            unverified.append(f"{sid} (local-copy, {boundary}): ${var} is not set, so the corpus "
                              "bytes are not here to hash. Set it to a legal copy to verify.")
            continue
        if not pathlib.Path(path).is_file():
            problems.append(f"{sid}: ${var} is set to {path!r}, which is not a file")
            continue
        where = f"local copy at ${var}"

    digest = derive(pathlib.Path(path).read_bytes())
    if digest != expected:
        problems.append(f"{sid}: the {where} hashes to {digest}, the manifest pins {expected}")
    else:
        verified.append(f"{sid} ({posture}, {boundary}): {where} hashes to the pinned baseline")

for p in problems:
    print(f"error: {p}", file=sys.stderr)
for v in verified:
    print(f"     verified: {v}")
for u in unverified:
    print(f"     NOT VERIFIED: {u}")
sys.exit(1 if problems else 3 if unverified else 0)
PY
case "$posture_status" in
  0) printf '%sok%s   every corpus verified under its declared posture\n' "$GREEN" "$OFF" ;;
  3) printf '%sNOT VERIFIED%s a corpus could not be verified under its declared posture (not ok, not FAIL)\n' "$YEL" "$OFF"
     NOT_VERIFIED+=("corpus verification posture") ;;
  *) fail "every corpus verified under its declared posture" ;;
esac

# The map is the oracle the two steps below validate against, and until now only one of the
# two files they read was pinned. The corpus was; the map was not -- and this engine writes
# into the map, stamping status, implementedIn and tests on the entries it implements. An oracle
# that the thing under test edits, and that nothing re-derives, is not an oracle. So the map
# is hashed the same way the corpus is, in corpus-manifest.json, which is the one file in the
# repository nothing edits in the course of building the engine.
run "corpus-map.json matches its pinned hash" python3 - <<'PY'
import hashlib
import json
import pathlib
import sys

manifest = json.loads(pathlib.Path("corpus-manifest.json").read_text(encoding="utf-8"))
pins = [m for m in manifest.get("maps", []) if m.get("path") == "corpus-map.json"]
if len(pins) != 1:
    print("error: corpus-manifest.json does not pin corpus-map.json exactly once", file=sys.stderr)
    sys.exit(1)

digest = hashlib.sha256(pathlib.Path("corpus-map.json").read_bytes()).hexdigest()
if digest != pins[0]["contentHash"]:
    print(
        f"error: corpus-map.json hashes to {digest}, corpus-manifest.json pins "
        f"{pins[0]['contentHash']}. If the map was meant to change, update the pin in the "
        "same commit and say in the message what changed and why.",
        file=sys.stderr)
    sys.exit(1)
PY

# Every rule in this engine names a map entry. If the code names an entry the map does not
# carry, or spells a citation differently from the map, the promise that a reader can get
# from code to page is broken. This check is the reason that promise is a property rather
# than an intention.
step "Map correspondence"
run "MapEntries matches corpus-map.json entry for entry" python3 - <<'PY'
import json
import pathlib
import re
import sys

mapped = json.loads(pathlib.Path("corpus-map.json").read_text(encoding="utf-8"))
by_id = {e["id"]: e for e in mapped["entries"]}
source = pathlib.Path("src/HoyleBackgammon/MapEntries.cs").read_text(encoding="utf-8")

citations = dict(re.findall(r'private const string (\w+) = "([^"]+)";', source))
# Tolerant of how the declaration is wrapped: a long name pushes the arguments onto their
# own lines, and a formatting choice must not be able to hide an entry from this check.
declared = re.findall(
    r'\bEntry\(\s*"([^"]+)",\s*"([^"]+)",\s*(\w+|"[^"]+")\s*\)', source)
# A derived entry (rules-factory decision 0012) cites nothing, so what code and map must agree
# on is the entries it is derived from. The code names them as MapEntries properties.
properties = dict(re.findall(r'public static MapEntry (\w+) \{ get; \} =\s*Entry\(\s*"([^"]+)"', source))
derived = re.findall(r'\bDerived\(\s*"([^"]+)",\s*"([^"]+)",\s*([\w\s,]+?)\s*\)', source)

problems = []
for entry_id, name, citation in declared:
    citation = citations.get(citation, citation.strip('"'))
    entry = by_id.get(entry_id)
    if entry is None:
        problems.append(f"code declares '{entry_id}', which the map does not carry")
        continue
    if entry["name"] != name:
        problems.append(f"{entry_id}: code names it '{name}', map names it '{entry['name']}'")
    if "locator" not in entry:
        problems.append(f"{entry_id}: code cites '{citation}', and the map entry has no locator")
    elif entry["locator"]["citation"] != citation:
        problems.append(
            f"{entry_id}: code cites '{citation}', map cites "
            f"'{entry['locator']['citation']}'")

for entry_id, name, sources in derived:
    entry = by_id.get(entry_id)
    if entry is None:
        problems.append(f"code declares derived '{entry_id}', which the map does not carry")
        continue
    if entry["name"] != name:
        problems.append(f"{entry_id}: code names it '{name}', map names it '{entry['name']}'")
    code_sources = [properties.get(s.strip(), f"<unknown {s.strip()}>") for s in sources.split(",")]
    if code_sources != entry.get("derivedFrom"):
        problems.append(
            f"{entry_id}: code derives it from {code_sources}, map from {entry.get('derivedFrom')}")

missing = sorted(set(by_id) - {e[0] for e in declared} - {e[0] for e in derived})
if missing:
    problems.append(f"the map carries entries the code never names: {', '.join(missing)}")

for p in problems:
    print(f"error: {p}", file=sys.stderr)
sys.exit(1 if problems else 0)
PY

# The step above compares two spellings of the same promise -- id, name, citation -- and would
# pass on a map that contradicted the engine in every other field. This one makes the stronger
# claim rules-factory/docs/corpus-map.md calls "the first thing the factory should enforce once
# it exists": that an engine's honest answer about what it cannot do is DERIVABLE from its map.
# The correspondence table there, ordered and first-match-wins since factory decision 0005,
# turns each entry's fields into a prediction of the UnresolvedReason the engine returns for it.
# Both directions are failures. A reason the engine can return whose entry predicts a different
# one -- or none -- means something was implemented without being mapped; an entry predicting a
# reason no code path returns means the map asserts something the code disproves. The check
# prints, every run, the rows it could not evaluate and the rows this map gives it no instance
# of; a gate is trusted, so it must not read as though it proved more than it did.
run "every unresolved reason and every map entry agree with the correspondence table" python3 - <<'PYCORR'
import json
import pathlib
import re
import sys

# The kernel's closed UnresolvedReason vocabulary, transcribed from the correspondence table
# rather than read out of RulesKernel: an expectation taken from the thing under test proves
# nothing. A reason the kernel gains and this list has not is caught below as unreadable.
REASONS = {
    "OutsideCurrentScope",
    "UnsupportedRule",
    "MissingRulesData",
    "RequiresInterpretation",
    "UnsupportedInteraction",
}


def first_matching_row(entry, by_id):
    """The correspondence table of rules-factory/docs/corpus-map.md, in order.

    First match wins (factory decision 0005): not-in-scope and not-built dominate, and the
    remaining rows describe what a *built* entry returns. Returns the row, the reason it
    predicts (None where the row predicts no decline), and why it fired.

    Row 7 is deliberately absent -- see ROW7.
    """
    if entry.get("scope") == "out":
        return 1, "OutsideCurrentScope", "scope: out"
    if entry.get("status") in ("mapped", "blocked"):
        return 2, "UnsupportedRule", f"status: {entry.get('status')} -- read, not built"
    if "definedElsewhere" in entry:
        return 3, "MissingRulesData", "carries definedElsewhere"
    if "beyondAdapter" in entry:
        return 4, "MissingRulesData", "carries beyondAdapter"
    if entry.get("kind") == "operation":
        for dep in entry.get("dependsOn") or []:
            target = by_id.get(dep)
            if target and target.get("kind") == "value" and target.get("status") != "implemented":
                return 5, "MissingRulesData", f"operation whose value dependency {dep} is unimplemented"
    if (entry.get("ambiguity") or {}).get("fate") == "unresolved":
        return 6, "RequiresInterpretation", "ambiguity.fate: unresolved"
    if entry.get("kind") == "assertion":
        return 8, None, "kind: assertion -- the engine demands the value and proceeds"
    # Rows 1-8 are not exhaustive by design: a built, clear, unambiguous rule matches none of
    # them, because the engine simply answers it. That is not a failure -- but it does predict
    # that no code path declines citing the entry, and the map -> code direction checks that.
    return None, None, "matches no row: a built, clear, unambiguous rule the engine answers"


ROW7 = (
    "row 7 (two implemented entries with no entry for their combination -> "
    "UnsupportedInteraction) is a fact about a PAIR, and about interactions the map does not "
    "enumerate. No per-entry predicate can express it, so it is not evaluated. What IS checked "
    "for an UnsupportedInteraction site is row 7's PLACE in the order -- it is reached only "
    "when rows 1-6 do not match -- and that its entry is implemented, as row 7 requires"
)

problems = []

mapped = json.loads(pathlib.Path("corpus-map.json").read_text(encoding="utf-8"))
by_id = {e["id"]: e for e in mapped["entries"]}

# --- what the engine can actually return ------------------------------------------------
#
# Every `new UnresolvedResult(...)` in src/, paired with the MapEntries property it cites.
# Doc comments come out first: several of them name a reason in prose, and a sentence is not
# a return.
SITE = re.compile(
    r"new UnresolvedResult\(\s*UnresolvedReason\.(\w+)\s*,(?:(?!new UnresolvedResult).)*?"
    r"MapEntries\.(\w+)\.Locator\s*\)")

sites = []          # (file, reason, MapEntries property)
constructions = 0   # every `new UnresolvedResult(` on disk, readable by the regex or not
for source in sorted(pathlib.Path("src").rglob("*.cs")):
    if "obj" in source.parts:
        continue
    code = " ".join(
        line for line in source.read_text(encoding="utf-8").splitlines()
        if not line.lstrip().startswith("///"))
    code = re.sub(r"\s+", " ", code)
    constructions += len(re.findall(r"new UnresolvedResult\(", code))
    sites.extend(
        (str(source), m.group(1), m.group(2)) for m in SITE.finditer(code))

# A site this check cannot read is a site it cannot vouch for, and the code -> map direction
# claims to cover every one of them. So the count is asserted rather than assumed.
if len(sites) != constructions:
    problems.append(
        f"src/ constructs UnresolvedResult {constructions} time(s) but only {len(sites)} of "
        "them have the shape this check reads (a literal UnresolvedReason and a "
        "MapEntries.<Entry>.Locator). The unread one is unchecked: give it that shape, or "
        "teach this check the new one.")

# MapEntries property -> map entry id, so a citation in code resolves to a row of the map.
entries_source = re.sub(
    r"\s+", " ", pathlib.Path("src/HoyleBackgammon/MapEntries.cs").read_text(encoding="utf-8"))
declared = dict(re.findall(
    r"public static MapEntry (\w+) \{ get; \} = Entry\( ?\"([^\"]+)\"", entries_source))

if not sites:
    problems.append("no UnresolvedResult site was found in src/ at all, so nothing was checked")
if not declared:
    problems.append("no MapEntry declaration was found in MapEntries.cs, so nothing was checked")

# --- code -> map: every reason the engine can return, against its cited entry -------------

verified_code, partial = [], []
cited_by_reason = {}
for path, reason, prop in sites:
    where = f"{path}: {reason} citing MapEntries.{prop}"
    if reason not in REASONS:
        problems.append(
            f"{where}: {reason!r} is outside the UnresolvedReason vocabulary the "
            "correspondence table is written against")
        continue
    entry_id = declared.get(prop)
    if entry_id is None:
        problems.append(f"{where}: MapEntries declares no entry named {prop}")
        continue
    entry = by_id.get(entry_id)
    if entry is None:
        problems.append(
            f"{where}: cites map entry {entry_id!r}, which corpus-map.json does not carry -- "
            "something was implemented without being mapped")
        continue
    cited_by_reason.setdefault(reason, set()).add(entry_id)

    row, predicted, why = first_matching_row(entry, by_id)
    if reason == "UnsupportedInteraction":
        if row is not None and row != 8:
            problems.append(
                f"{where}: row 7 is reached only when rows 1-6 do not match, but {entry_id} "
                f"matches row {row} ({why}), which predicts {predicted}")
        elif entry.get("status") != "implemented":
            problems.append(
                f"{where}: row 7 is about two IMPLEMENTED entries, and {entry_id} is status "
                f"{entry.get('status')!r}")
        else:
            partial.append(f"{entry_id} in {path}")
        continue
    if row is None:
        problems.append(
            f"{where}: {entry_id} matches no correspondence row -- the map says the engine "
            f"answers it ({why}) -- yet the code declines")
    elif predicted is None:
        problems.append(
            f"{where}: {entry_id} matches row {row} ({why}), which says the engine returns "
            "nothing and demands the value")
    elif predicted != reason:
        problems.append(
            f"{where}: {entry_id} matches row {row} ({why}), which predicts {predicted}")
    else:
        verified_code.append(f"{entry_id} (row {row} -> {reason})")

# --- map -> code: every reason the map predicts, against the engine -----------------------

# Row 2 says a `mapped` entry returns UnsupportedRule. Since rules-factory#2, `mapped` also
# covers an entry the engine HAS built but no test proves -- "code without them is mapped,
# whatever the repository contains" -- and for such an entry no decline path exists, because
# the code answers. The two readings cannot be told apart from the map, so the exception is a
# list, not a rule: each id is named here with its reason, the check fails for any other
# row-2 entry with no UnsupportedRule path, and fails for a listed id that is no longer
# row 2 (a stale exception is how a list like this rots).
UNTESTED_BUT_BUILT = {
    "player-count": "Player has two values and every rule is player-relative, but no test goes "
                    "red under any sensible mutation of 'two persons'; demoted under rules-factory#2",
}
untested_seen = []

verified_map, rows_seen = [], set()
for entry in mapped["entries"]:
    row, predicted, why = first_matching_row(entry, by_id)
    if row is not None:
        rows_seen.add(row)
    cited = {r for r, ids in cited_by_reason.items() if entry["id"] in ids}
    # The map cannot predict an UnsupportedInteraction, row 7 being unevaluable, so a site
    # returning one is evidence neither for nor against its entry in this direction.
    cited.discard("UnsupportedInteraction")

    if predicted is None:
        if cited:
            problems.append(
                f"{entry['id']}: the map says the engine answers it ({why}) but the code "
                f"returns {', '.join(sorted(cited))} citing it -- the map asserts something "
                "the code disproves")
        continue

    # Where the entry names its own reason, it must name the one its row predicts; otherwise
    # the map contradicts the table before the code is even consulted.
    named = (entry.get("ambiguity") or {}).get("unresolvedReason")
    if named is not None and named != predicted:
        problems.append(
            f"{entry['id']}: ambiguity.unresolvedReason is {named!r}, but the entry matches "
            f"row {row} ({why}), which predicts {predicted}")

    if row == 2 and entry["id"] in UNTESTED_BUT_BUILT and not cited:
        untested_seen.append(entry["id"])
        continue
    if predicted not in cited:
        problems.append(
            f"{entry['id']}: matches row {row} ({why}), so the engine must be able to return "
            f"{predicted} citing it, and no code path does")
    else:
        verified_map.append(f"{entry['id']} (row {row} -> {predicted})")
    for other in sorted(cited - {predicted}):
        problems.append(
            f"{entry['id']}: matches row {row} ({why}), which predicts {predicted}, but the "
            f"code also returns {other} citing it")

for stale in sorted(set(UNTESTED_BUT_BUILT) - set(untested_seen)):
    problems.append(
        f"{stale}: listed in UNTESTED_BUT_BUILT but it is no longer a row-2 entry with no decline "
        "path; remove it from the list")

for p in problems:
    print(f"error: {p}", file=sys.stderr)
if problems:
    sys.exit(1)

unexercised = sorted({1, 2, 3, 4, 5, 6, 8} - rows_seen)
print(
    f"{len(sites)} unresolved results in src/, all of them read, against "
    f"{len(mapped['entries'])} map entries, both directions.\n"
    f"     code -> map: {len(verified_code)} fully verified -- {'; '.join(verified_code)}.\n"
    f"     map -> code: {len(verified_map)} verified -- {'; '.join(verified_map)}; every "
    f"other entry predicts no unresolved reason and no code path declines citing it.\n"
    f"     NOT VERIFIED: {ROW7}. {len(partial)} site(s) so checked: "
    f"{'; '.join(partial) or 'none'}.\n"
    f"     NOT VERIFIED: row 2 predicts UnsupportedRule for {len(untested_seen)} entry(ies) the "
    f"engine built but no test proves, and no code path returns it: "
    f"{'; '.join(f'{i} ({UNTESTED_BUT_BUILT[i]})' for i in untested_seen) or 'none'}.\n"
    f"     NOT EXERCISED: no entry in this map reaches row(s) "
    f"{', '.join(str(r) for r in unexercised) or '(none)'}. Those rows are transcribed above "
    f"and this map proves nothing about them.")
PYCORR

# BeyondAdapter's remarks say in so many words that nothing inside this engine calls it: the
# handedness of a physical board is a fact no player-relative rule can want. A decline nothing
# reaches is honest only while the code saying so is true, and that sentence would otherwise
# quietly outlive the day some rule did reach for it. So it is a fact, not a remark.
run "nothing inside the engine calls BeyondAdapter" python3 - <<'PY'
import pathlib
import re
import sys

callers = []
for source in sorted(pathlib.Path("src").rglob("*.cs")):
    if source.name == "BeyondAdapter.cs":
        continue
    # Doc comments are allowed to mention it -- Geometry's remarks point a reader at it -- so
    # the /// lines come out before looking for a call.
    code = "\n".join(
        line for line in source.read_text(encoding="utf-8").splitlines()
        if not line.lstrip().startswith("///"))
    if re.search(r"\bBeyondAdapter\s*\.", code):
        callers.append(str(source))

if callers:
    print(
        "error: BeyondAdapter is called from " + ", ".join(callers) + ". Its remarks claim no "
        "rule in this engine reaches it, and the honesty of the inner-table-handedness decline "
        "rests on that claim. Either the call is wrong or the claim is; rewrite whichever.",
        file=sys.stderr)
    sys.exit(1)
PY

# A citation is a promise, and the step above only checks that the code and the map make the
# same one. Nothing checked that the promise resolves: a map entry could cite a page the
# corpus has not got, or a section the chapter has not got, or -- the mistake that put this
# engine's whole starting-position decline on a retracted premise -- attach a real sentence of
# the corpus to a page it is not on. The corpus is committed here, so all three are decidable
# from bytes rather than from care.
step "Citations resolve in the corpus"
run "every corpus-map.json citation resolves to its page in corpus/hoyle.txt" python3 - <<'PYCITE'
import json
import pathlib
import re
import sys

# The chapter's four sections, in order. Three are headings the corpus prints; "The Board and
# Men" is the untitled span between the chapter heading and the first of them, and is the
# map's own name for it -- so it is matched by position, not by a string that is not there.
SECTIONS = [
    ("The Board and Men", None),
    ("Playing", "PLAYING."),
    ("Bearing off the Men", "BEARING OFF THE MEN."),
    ("Hints for Play", "HINTS FOR PLAY."),
]

text = pathlib.Path("corpus/hoyle.txt").read_text(encoding="utf-8")
mapped = json.loads(pathlib.Path("corpus-map.json").read_text(encoding="utf-8"))

problems = []


def offset_of(pattern, what, within=None):
    """The one place a heading occurs. The book reuses these words -- a table of contents, and
    other games with their own PLAYING section -- so every search but the chapter's own is
    confined to the chapter."""
    low, high = within if within else (0, len(text))
    found = list(re.finditer(pattern, text[low:high], re.MULTILINE))
    if len(found) != 1:
        problems.append(
            f"{what}: expected exactly one occurrence in the corpus, found {len(found)}")
        return None
    return low + found[0].start()


chapter = offset_of(r"^BACKGAMMON\.$", "the chapter heading")
after = offset_of(r"^BAGATELLE\.$", "the heading after the chapter")
if chapter is None or after is None or after <= chapter:
    for p in problems:
        print(f"error: {p}", file=sys.stderr)
    print("error: the backgammon chapter could not be bounded in the corpus", file=sys.stderr)
    sys.exit(1)

starts = {}
for name, heading in SECTIONS:
    starts[name] = chapter if heading is None else offset_of(
        "^" + re.escape(heading) + "$", f"the {name} heading", within=(chapter, after))

if problems or chapter is None or any(v is None for v in starts.values()):
    for p in problems:
        print(f"error: {p}", file=sys.stderr)
    sys.exit(1)

order = [name for name, _ in SECTIONS]
if [starts[n] for n in order] != sorted(starts[n] for n in order):
    print("error: the chapter's sections do not appear in the order the map assumes", file=sys.stderr)
    sys.exit(1)

# Each section runs to the next one's heading; the last runs to the end of the chapter.
bounds = [starts[n] for n in order] + [after]
section_span = {order[i]: (bounds[i], bounds[i + 1]) for i in range(len(order))}


def page_span(page):
    """Page N runs from its {N} marker to the next page's."""
    here = text.find("{%d}" % page)
    next_ = text.find("{%d}" % (page + 1))
    if here < 0 or next_ < 0 or next_ <= here:
        return None
    return here, next_


def flatten(s):
    """One line, with the page markers dropped, so a quotation can be found across them."""
    return re.sub(r"\s+", " ", re.sub(r"\{\d+\}", " ", s)).strip()


# What may sit between two words of a quotation without being part of it: a line break, a
# {NNN} page marker, or a [NN] footnote reference.
GAP = r"\s*(?:(?:\{\d+\}|\[\d+\])\s*)*"


def locate(quotation):
    """Where a quotation sits in the corpus, as a raw offset span, or None.

    Matched tolerantly of line breaks and of the {NNN} page markers, which fall mid-sentence
    and are not part of the prose. Ellipsis in the quotation joins fragments that must appear
    in order.
    """
    fragments = [f for f in (q.strip() for q in flatten(quotation).split("...")) if f]
    if not fragments:
        return None

    # Within a fragment only a gap may separate two words; between fragments the ellipsis
    # stands for prose the entry left out, so anything may.
    pattern = ".*?".join(
        GAP.join(re.escape(word) for word in fragment.split(" ")) for fragment in fragments)
    found = re.search(pattern, text, re.DOTALL)
    return (found.start(), found.end()) if found else None


CITATION = re.compile(r"^BACKGAMMON / (.+) / p\. (\d+)$")

quoted, summarised, derived = [], [], []
for entry in mapped["entries"]:
    if "derivedFrom" in entry:
        # A derived entry (rules-factory decision 0012) has no locator and no evidence: no
        # passage states it, and its sources' spans are checked on their own entries. Named in
        # the summary, so the count below does not read as though it had been located.
        derived.append(entry["id"])
        continue
    citation = entry["locator"]["citation"]

    match = CITATION.match(citation)
    if not match:
        problems.append(f"{entry['id']}: '{citation}' is not a citation this check can read")
        continue

    section, page = match.group(1), int(match.group(2))
    if section not in section_span:
        problems.append(f"{entry['id']}: cites section '{section}', which the chapter has not got")
        continue

    span = page_span(page)
    if span is None:
        problems.append(f"{entry['id']}: cites p. {page}, whose marker the corpus has not got")
        continue

    low, high = section_span[section]
    if span[1] <= low or span[0] >= high:
        problems.append(
            f"{entry['id']}: cites '{section} / p. {page}', but p. {page} lies outside that "
            "section of the chapter")
        continue

    # Where an entry's evidence is a verbatim quotation rather than a summary of one, that
    # quotation must be ON the page cited. This is the half of a citation a page number alone
    # cannot check -- a real sentence of the corpus attributed to a page it is not on -- and
    # it is exactly what the retracted version of this map got wrong: starting-position's
    # evidence was a sentence about the board's handedness from the page before.
    #
    # "Verbatim quotation" is not guessed at. An evidence string is one if it is found in the
    # corpus, and a summary if it is not; nothing is inferred from its length or its shape. So
    # this arm proves nothing about the entries it skips, and the step says which those are
    # rather than reporting a count that reads as though it covered them.
    where = locate(entry["evidence"])
    if where is None:
        summarised.append(entry["id"])
        continue

    if where[1] <= span[0] or where[0] >= span[1]:
        problems.append(
            f"{entry['id']}: its evidence is a sentence of the corpus, but not one the "
            f"p. {page} it cites reaches")
        continue

    quoted.append(entry["id"])

for p in problems:
    print(f"error: {p}", file=sys.stderr)
if problems:
    sys.exit(1)

print(
    f"{len(mapped['entries']) - len(derived)} citations resolve to a page inside the section they name; "
    f"{len(derived)} derived entry(ies) cite nothing and were not located: {', '.join(derived) or 'none'}. "
    f"{len(quoted)} quote the corpus and were checked against the bytes of the page cited: "
    f"{', '.join(quoted)}. {len(summarised)} summarise their evidence instead of quoting it, "
    f"so nothing below the page was checked for them: {', '.join(summarised)}.")
PYCITE

step "Restore"
run "dotnet restore" dotnet restore "$SOLUTION" || true

step "Format"
run "dotnet format --verify-no-changes" \
    dotnet format "$SOLUTION" --verify-no-changes --no-restore || true

step "Build + test (Debug)"
DEBUG_RESULTS="$(mktemp -d)"; RESULTS_DIRS+=("$DEBUG_RESULTS")
if run "build Debug (0 warnings)" dotnet build "$SOLUTION" -c Debug --no-restore -warnaserror; then
  run "test Debug" test_pass Debug "$DEBUG_RESULTS" || true
  run "every test corpus-map.json names exists and ran (Debug)" \
      assert_named_tests_ran "$DEBUG_RESULTS" "$TARGET_FRAMEWORKS" || true
else
  skipped "test Debug"
  skipped "every test corpus-map.json names exists and ran (Debug)"
fi

if [[ "$MODE" != "fast" ]]; then
  step "Build + test (Release, CI=true)"
  RELEASE_RESULTS="$(mktemp -d)"; RESULTS_DIRS+=("$RELEASE_RESULTS")
  if run "build Release w/ CI=true (0 warnings)" env CI=true dotnet build "$SOLUTION" -c Release --no-restore -warnaserror; then
    run "test Release w/ CI=true" test_pass Release "$RELEASE_RESULTS" CI=true || true
    run "every test corpus-map.json names exists and ran (Release)" \
        assert_named_tests_ran "$RELEASE_RESULTS" "$TARGET_FRAMEWORKS" || true
  else
    skipped "test Release w/ CI=true"
    skipped "every test corpus-map.json names exists and ran (Release)"
  fi
fi

# Only meaningful against a dirty working tree; after a clean checkout it diffs nothing and
# is unconditionally green. Named so nobody mistakes that for a whitespace audit.
step "Whitespace (uncommitted working-tree changes only)"
run "git diff --check" git diff --check || true

echo
if [[ "$FAILED" -eq 0 && "${#NOT_VERIFIED[@]}" -gt 0 ]]; then
  printf '%s%svalidate.sh %s: PASS, and NOT VERIFIED: %s%s\n' \
    "$BOLD" "$YEL" "$MODE" "$(IFS=,; echo "${NOT_VERIFIED[*]}")" "$OFF"
elif [[ "$FAILED" -eq 0 ]]; then
  printf '%s%svalidate.sh %s: PASS%s\n' "$BOLD" "$GREEN" "$MODE" "$OFF"
else
  printf '%s%svalidate.sh %s: FAIL%s\n' "$BOLD" "$RED" "$MODE" "$OFF"
fi
exit "$FAILED"
