#!/usr/bin/env bash
# validate.sh -- the single canonical gate for this repository.
#
# Modelled on rules-kernel's scripts/validate.sh, and narrower: this repository has no
# packable output and no python tooling, so it drops those steps and adds one this engine
# needs and the kernel does not -- re-checking the pinned corpus against the baseline hash
# the engine cites.
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
declared = re.findall(r'Entry\("([^"]+)", "([^"]+)", (\w+|"[^"]+")\)', source)

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
