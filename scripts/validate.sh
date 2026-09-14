#!/usr/bin/env bash
# validate.sh -- the single canonical gate for this repository.
#
# Modelled on rules-kernel's scripts/validate.sh, and differently shaped: this repository has
# no packable output and no python tooling, so it drops those steps, and it adds the ones an
# engine built from a corpus needs and a kernel does not --
#
#   * the pinned corpus still hashes to the baseline the engine cites;
#   * the map does too, because it is the oracle the next two steps judge the code against and
#     the engine writes into it;
#   * the code and the map name the same entries and spell the same citations;
#   * and those citations resolve in the corpus: the page exists, it falls in the section
#     named, and a quoted sentence is on the page it is attributed to.
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

EXPECTED_RESULT_FILES="$(expected_result_files)"

test_pass() {
  local config="$1"; shift
  local results_dir status
  results_dir="$(mktemp -d)"
  status=0
  env "$@" dotnet test "$SOLUTION" -c "$config" --no-build --nologo \
    --logger "trx" --results-directory "$results_dir" || status=$?
  if ! assert_tests_ran "$results_dir" "$EXPECTED_RESULT_FILES"; then
    status=1
  fi
  rm -rf "$results_dir"
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

# The engine's SourceBaselineId claims a digest for a named derivation. The corpus is
# committed here under pin-in-repo, so that claim is checkable without a network, and a
# corpus that drifted from the hash the engine cites would otherwise be invisible: every
# citation in the code would silently point into different bytes.
step "Corpus baseline"
run "corpus/hoyle.txt matches the pinned baseline hash" python3 - <<'PY'
import hashlib
import json
import pathlib
import re
import sys

digest = hashlib.sha256(pathlib.Path("corpus/hoyle.txt").read_bytes()).hexdigest()
mapped = json.loads(pathlib.Path("corpus-map.json").read_text(encoding="utf-8"))
expected = mapped["baseline"]["contentHash"]
source = pathlib.Path("src/HoyleBackgammon/MapEntries.cs").read_text(encoding="utf-8")
cited = re.search(r'contentHash: "([0-9a-f]{64})"', source).group(1)

problems = []
if digest != expected:
    problems.append(f"corpus/hoyle.txt hashes to {digest}, map says {expected}")
if cited != expected:
    problems.append(f"MapEntries.Baseline cites {cited}, map says {expected}")
for p in problems:
    print(f"error: {p}", file=sys.stderr)
sys.exit(1 if problems else 0)
PY

# The map is the oracle the two steps below validate against, and until now only one of the
# two files they read was pinned. The corpus was; the map was not -- and this engine writes
# into the map, stamping status and implementedIn on the entries it implements. An oracle
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
    r'Entry\(\s*"([^"]+)",\s*"([^"]+)",\s*(\w+|"[^"]+")\s*\)', source)

problems = []
for entry_id, name, citation in declared:
    citation = citations.get(citation, citation.strip('"'))
    entry = by_id.get(entry_id)
    if entry is None:
        problems.append(f"code declares '{entry_id}', which the map does not carry")
        continue
    if entry["name"] != name:
        problems.append(f"{entry_id}: code names it '{name}', map names it '{entry['name']}'")
    if entry["locator"]["citation"] != citation:
        problems.append(
            f"{entry_id}: code cites '{citation}', map cites "
            f"'{entry['locator']['citation']}'")

missing = sorted(set(by_id) - {e[0] for e in declared})
if missing:
    problems.append(f"the map carries entries the code never names: {', '.join(missing)}")

for p in problems:
    print(f"error: {p}", file=sys.stderr)
sys.exit(1 if problems else 0)
PY

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

quoted, summarised = [], []
for entry in mapped["entries"]:
    citation = entry["locator"]["citation"]
    if citation == "(absent)":
        # doubling-cube cites nothing because the corpus says nothing. Checking that the
        # corpus does not mention doubling is a different check and this one does not make it.
        continue

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
    f"{len(mapped['entries'])} citations resolve to a page inside the section they name. "
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
if run "build Debug (0 warnings)" dotnet build "$SOLUTION" -c Debug --no-restore -warnaserror; then
  run "test Debug" test_pass Debug || true
else
  skipped "test Debug"
fi

if [[ "$MODE" != "fast" ]]; then
  step "Build + test (Release, CI=true)"
  if run "build Release w/ CI=true (0 warnings)" env CI=true dotnet build "$SOLUTION" -c Release --no-restore -warnaserror; then
    run "test Release w/ CI=true" test_pass Release CI=true || true
  else
    skipped "test Release w/ CI=true"
  fi
fi

# Only meaningful against a dirty working tree; after a clean checkout it diffs nothing and
# is unconditionally green. Named so nobody mistakes that for a whitespace audit.
step "Whitespace (uncommitted working-tree changes only)"
run "git diff --check" git diff --check || true

echo
if [[ "$FAILED" -eq 0 ]]; then
  printf '%s%svalidate.sh %s: PASS%s\n' "$BOLD" "$GREEN" "$MODE" "$OFF"
else
  printf '%s%svalidate.sh %s: FAIL%s\n' "$BOLD" "$RED" "$MODE" "$OFF"
fi
exit "$FAILED"
