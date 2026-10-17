# Known unresolved issues

## SPS2-NDMF-JSON-REF-20261017 — Object-reference JSON roundtrip

Status: **open, not fixed by the finger-ring warning change**. This file is the
local tracking destination; no GitHub Issue has been created for it.

- Cases: `SpsBackendTests.NativeNdmfObjectActionRoundTripRetainsExplicitMode(False)`
  and `(True)`, both failing at `Tests/Editor/SpsBackendTests.cs:115`.
- The test creates an Object action on an installed native NDMF Socket, copies the
  component with `EditorJsonUtility.ToJson` / `FromJsonOverwrite`, and compares
  its target reference. Both cases expect `Depth source` and receive null.
- Candidate package regression: 206 passed / 2 failed / 0 skipped. Replacing the
  five candidate C# files in the same validation project with exact baseline
  `c3d25d24b1bdc3f306df68e74e8d6d49365f6d8e` reproduces the same two failures at
  the same assertion. Candidate files were subsequently restored and hash-checked.
- Existing XML identities: full run
  `6cfba4e224a9d037137568268bdc2cffc8e2ab44e374ea128b83ec8a5d7a476e`;
  baseline two-case run
  `c2b8f1a2f91704b50640eb239d34358b9c862724a21107e48208bcdbb17e731b`.

The failure reproduces without the warning change. This does not identify its
root cause as a dependency defect, a test-method limitation or a user-data bug.
The finger-ring generator and FullSetupGenerator are not called by these cases.
The relevant test and NDMF adapter are unchanged by the candidate.

Before public release, check a representative Object-action configuration through
actual save/reload and package update, then resolve the issue or record a separately
reviewed scope/disposition. Keep the failing results visible. Do not exclude the
cases, weaken the assertion or treat baseline reproduction as an all-pass result.
The candidate README/release notes disclose the unresolved scope. Publication and
any acceptance of that scope require a separate decision.
