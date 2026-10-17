# 1.2.0-beta.3 dev.1 candidate validation — 2026-10-17

Status: **unpublished candidate generated and statically audited**. No tag,
Release, VPM listing, upload or formal promotion was performed. Publication
requires a separate decision, including the unresolved scopes below.

## Source and reproducibility

- Product source commit: `756ce732fcb256f01e7c0c21b0a13cd62d6d1e4b`.
- Functional base: `c3d25d24b1bdc3f306df68e74e8d6d49365f6d8e` plus reviewed
  warning/diagnostic patch `6d1922254718e82e569fec62c98b214cddafb420d912da8ef63d6c1d79cb7837`.
- Final version preparation changed metadata/docs and fixed-version packaging
  references; all 110 C# files remained byte-identical to the reviewed source.
- Both VPM and both family installers were generated twice in fresh directories
  from a clean, short-path checkout of the product source commit. All five
  archive hashes matched between the two runs.
- This validation document is a subsequent documentation-only record. It does
  not change any archive input under the package or Distribution directories.

| Artifact | SHA256 |
| --- | --- |
| Common VPM ZIP | `c8db9c2a8cb98ef6728deeb8e231f347d4bdfcd5d543b819c0ff9fc6e84dec85` |
| Fury installer unitypackage | `07e9ee58a87f96cac6076d89854fca655aed8fa7205ec68a6eb9558c606194ea` |
| NDMF installer unitypackage | `191590d0439c0e5f6e601e22be2c1c6b00d4329c495545cbaba5e98708bca158` |
| Fury installer customer ZIP | `57fc174ba8d81d060a23c1d389df842ca1ef7d8ef023ca7ec564d6904f3ee18f` |
| NDMF installer customer ZIP | `3dfcb3af34f9fb242364c716d8ec8208a3608307d8f1a9c06df1ac88c2155725` |

## Content, GUID and update audit

The common ZIP contains 93 product entries. Every entry matches the clean source
bytes; no tests, private contracts, comparison scenes, purchased avatars,
ProjectSettings, backend temporary output or dependency bodies are included.
The approved IcePop display assets and their license remain included.

The existing local beta.2 ZIP matches the public GitHub asset digest
`35724dab28aab7b711f410cc8628522c7745b7bb28db63f94cb618009ca7ce70`.
Compared with that actual distributed ZIP, the candidate keeps all paths and
all 52 metadata files unchanged. Only FingerRingAutoCalibration.cs,
FullSetupGenerator.cs, Sps2Localization.cs, README.md and package.json differ.
Runtime/SetupSettings.cs and Editor/Generation/Sps2SetupAsset.cs are unchanged.
The package manifest changes only version; dependency ranges and the single
legacy-folder path/GUID remain unchanged. Assets/SPS2Settings is outside that
migration target.

The two VPAI unitypackages each contain exactly the official three GUIDs/assets.
Every non-config member matches the official 1.1.6 template. Configs change only
the SPS2 version relative to beta.2, retain includePrerelease=true,
silentIfInstalled=false and minimumUnity=2022.3, and keep their existing family
dependency boundaries. Each customer ZIP contains exactly the unitypackage,
README and two unchanged license notices. Creator and template came from
existing local copies matching the previously pinned official hashes; no new
external asset was fetched and no Unity instance was started for packaging.

## Existing executable evidence and limits

Native Play cases passed 5/5. Package-wide regression is **206 passed / 2 failed /
0 skipped**, including passing FingerRingAutoCalibration 19/19,
FingerRingCalibration 42/42, FullSetupSettings 46/46 and Localization 37/37.
Both failures reproduce at the same assertion with the same null Object target
on exact baseline c3 in the same environment. Track them as
[SPS2-NDMF-JSON-REF-20261017](KNOWN_ISSUES.md); this is not an all-pass result.

Existing save/reload evidence retains nine setups/twelve sockets, calibration,
manual adjustments and constraint sources. Existing actual-FBX native-constraint
reference/open/return observations cover three models and both hands. These
are separate from an SDK avatar build and a VRC client authored Gesture test.
Compared with the compiled comparison project, 106 of 110 C# files match raw
bytes and four match after CRLF/LF normalization only. The clean export source
matches all 110 authoring-source files exactly. Metadata/docs preparation did
not alter that executable source.

Final-version VPAI/VCC/ALCOM installation/update was not run. The unpublished
version is not resolvable through the public listing yet, and a fresh Unity
delivery gate needs separate scheduling. GUID/data-code identity plus the
existing retention results are structural evidence, not a fresh-install claim.
Before publication, resolve or separately review the actual NDMF Object-action
save/update impact. VRC PC, authored Gestures and final material/prefab behavior
remain unverified. Airi is unowned and untested.

## Comparison-project residue

The validation Editor exited normally from saved non-Play state. The user's
other Unity was not operated. Original FBX/meta, original saved comparison scene,
manifest and lock hashes remain unchanged.

VRCFury automatically removed 23 old temporary build controller/log/meta files.
Earlier Fury/NDMF SDK bundles remain: two original bundles still match their
recorded hashes, and both separately preserved extended bundles still exist.
That does not reconstruct the 23 individual files or prove that no unloaded
scene referred to them. Twenty regenerated lilToon shaders were captured and
restored to known baseline bytes. The old lilToonSetting.json bytes were not
found in inspected existing evidence/backups; restoration was not attempted.
The old hash and current bytes/hash are retained in private candidate evidence.
No additional comparison-setting change or cleanup was performed for packaging.
The comparison project was never used as the package source.
