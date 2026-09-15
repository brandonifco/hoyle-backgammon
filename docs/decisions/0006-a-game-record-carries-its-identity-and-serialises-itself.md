# 0006: A game record carries its identity and serialises itself

**Status:** accepted.

## Context

The end-to-end tests of PR #11 found that a `GameRecord` could not be compared with another one
on its own. It held the game (start, opening roll, turns, winner, value, next opener) and nothing
that said which engine played it. `Game.Identity` had the ruleset and the replay schema, but no
map version; that was reachable only by parsing `EngineProvenance.ReadBytes()`. And the record had
no byte form, so `SeededGameReplayTests` built its own line rendering out of public members and
hashed that. Two tools hashing the same game would each have had to invent the same rendering.

## Decision

**A `GameRecord` carries `Identity` and `Map`.** `Identity` is the `ReplayCompatibilityIdentity`
the game was played under (`Game.Identity`). `Map` is a `MapPackage(PackageId, Version)` read at
run time from the embedded `provenance.json` (`map.packageId`, `map.version`), which
`factory produce` writes and the build embeds. The engine never types the version out, so it
cannot drift from the package the engine was produced from. The factory version and commit are
left out on purpose: re-producing the same map with a newer factory does not change a game.

**`GameRecord.ToCanonicalJson()` is the record as bytes**, and replays hash those bytes.

The format is JSON in the canonical form of RFC 8785 (JCS), over the part of JSON a record needs:

- objects, arrays, strings, integers, `true`, `false`, `null`; no floating-point numbers;
- object members sorted by the UTF-16 code units of their names, no insignificant whitespace;
- strings escape `"`, `\` and the C0 controls only: `\b \t \n \f \r` by name, the rest as
  lower-case `\u00xx`. Every other character is written as itself;
- UTF-8, no byte-order mark. A string that is not well-formed UTF-16 throws rather than being
  replaced.

The writer is hand-written (`GameRecordJson`). It does not go through `System.Text.Json`'s
serializer or its encoders, whose escaping is a runtime default rather than a contract. The same
game gives the same bytes on net8.0 and net10.0 (checked: both give `878936f1…04da3d` for the
seeded game).

The shape, with members shown in the order they are written:

```
{ "identity": { "randomAlgorithm": string|null, "replaySchema": int,
                "ruleset": { "id": string, "version": int },
                "sourceBaselines": [ { "asOf": "yyyy-MM-dd"|null, "contentHash": string,
                                       "hashDerivation": string, "sourceId": string } ] },
  "map": { "packageId": string, "version": string },
  "next": "WinnerThrowsFirst"|"ThrowAgainForTheRight",
  "openingRoll": null | { "attempts": [[white, black], ...], "opener": "White"|"Black" },
  "openingThrowAdopted": bool,
  "start": { "assertedBy": string, "justification": null | { "citation": string, "sourceId": string },
             "position": POSITION },
  "turns": [ { "moves": null | [ { "authority": entry id, "die": int, "from": int,
                                   "kind": MoveKind name, "takesUpBlot": bool, "to": int } ],
               "player": "White"|"Black", "position": POSITION,
               "thrown": null | [first, second] } ],
  "value": "Hit"|"Gammon"|"Backgammon",
  "winner": "White"|"Black" }
```

`POSITION` is `{ "Black": [26 ints], "White": [26 ints] }`: each player's men by his own pip,
index 0 borne off, 1 to 24 the points, 25 the bar. A turn whose player was wholly suspended has
`"thrown": null` and `"moves": null`. A turn where nothing could be played has `"moves": []`.
Enum values are their C# names, and a move's `authority` is its map entry id (`move-by-pip`,
`enter-from-bar`, ...). `SeededGameReplayTests.The_canonical_serialisation_is_rfc_8785_json_of_every_field_of_the_record`
spells out one record byte for byte.

**The replay schema is version 2.** The kernel defines the replay schema as "the shape of the
recorded replay". The record now has two more fields and a byte form, so a schema 1 record cannot
be read as a schema 2 one.

**The ruleset stays at version 3.** A serialisation that did not exist before is not a rule
change. No rule returns anything different, so no entry's `implementedIn` moves. The check: the
test's former line rendering, run over the new record with the schema number read as 1, still
hashes to the literal pinned before this change (`606eb924…e8500b9a`). That is the same 65-turn
gammon for White, with the same decisions and the same draws.

## Consequences

- `SeededGameReplayTests` hashes `ToCanonicalJson()`, and its pinned SHA-256 moves from
  `606eb92458af7754b365c76b3d3eba436cd919a2c7db4d1c593b9a65e8500b9a` to
  `878936f1b49d970059f7231cecd9feb80ae93d27ea77d2cc8b5498100604da3d`, only because the bytes
  moved. The test no longer reads the provenance to build a rendering. It still reads it once, to
  check that the record's `Map` is the one the provenance names.
- `GameRecord`'s constructor takes `Identity` and `Map` first. That is a source-breaking change
  for anyone who built a record by hand. `Game.Play` is the only producer in this repository.
- Changing a member name, adding a field, or changing how any value is written changes the replay
  bytes. That is a replay schema change and gets a record like this one. A change to what a rule
  returns is a ruleset change, as before (decisions 0004, 0005).
- A new map version changes `Map` and therefore the bytes, even when no rule changes. That is
  intended: two records are comparable only under the same map. It is not a schema change.
