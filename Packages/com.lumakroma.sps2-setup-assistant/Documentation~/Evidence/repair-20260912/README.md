# Repair evidence

Owned session: repair-session-6, Unity 2022.3.22f1, SDK 3.10.4, VRCFury 1.1403.0,
CoplayDev 10.2.0. Pragmatic Development; source baseline 71d891c6cedf44066dfbac6d1ff3fd420d127ef2.

`validated-source-hashes.json` records source and observed host SHA-256 values.
`normalizedTextSha256` normalizes CRLF to LF and trailing file whitespace;
98 package files matched under that normalization. Binary files match exactly.

The disposable probe sources require the separately licensed MANUKA fixture.
They create and remove their own avatar clones, retaining their generated settings
snapshots and test Prefabs in the owned sandbox. They are not customer setup scripts.
`storage-probe-result-7.json` predates the small addition of the discard fixture
in `discard-probe.cs`; its underlying generator source is the same. The native
`menu-probe-result.json` predates the final immutable storage snapshot correction;
`menu-ma-probe-result.json` validates the corrected generation and full SDK route.

Placement research: https://github.com/wholesomevr/SPS-Configurator/ at
`a8102b875b9a5a51982a0201685a0ee42cb14025`. Observed baked mesh surface queries,
bone-weighted vertices, viseme-delta mouth centers and hand/sole measurements.
No reference code/assets were copied, vendored or shipped; no license assumed.
The new placement implementation uses public Unity Mesh and Collider APIs.

Independent read-only review found saved-scene discard and legacy Plug migration
issues. Both were fixed and exercised by discard/migration probes. Remaining
manual/visual/VRC gates are listed in ../../VALIDATION.md.
