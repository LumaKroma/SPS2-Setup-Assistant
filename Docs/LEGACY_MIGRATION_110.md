# Legacy Assets migration — unpublished 1.1.0

User approved minimal migration implementation and disposable validation on
2026-10-02. This supersedes the previous research-only boundary. It does not
authorize migration of the user's current projects or publication.

## Exact metadata and preservation boundary

The sole `legacyFolders` entry is
`Assets/LumaKroma/SPS2SetupAssistant` ->
`1824306e4bd4439aaffbd9eab24486e5`. The path and root GUID are observed in the
published 1.0.2 Assets unitypackage, not inferred from the current VPM layout.
No `legacyFiles` or `legacyPackages` entry is added. Never target the parent
`Assets/LumaKroma` or settings directory `Assets/SPS2Settings`.

Keep all product script/asset .meta GUIDs stable. Settings, scene and prefab
files elsewhere are outside the deletion target. Existing user-authored files
inside the legacy root are NOT protected by metadata. Moving the entire folder
does not necessarily protect it: when the original path is missing, matching
root GUID can identify its new location. Do not put a backup under Assets.

## Official mechanism and required precheck

VPM metadata is consumed by VCC/ALCOM and the official VPAI installer. The
observed VPAI 1.1.6 source first resolves matching paths, then uses GUID fallback
only for missing paths. GUID is not an additional match requirement. The normal
Install/Cancel prompt lists legacy paths; retain it and do not enable NO_PROMPT.
Package installation precedes old-folder/.meta removal. There is no verified
automatic legacy backup or rollback in that flow. Treat deletion as permanent.

Before accepting migration:

1. Close Unity and back up the entire project outside the project directory.
   Confirm the backup includes scene/prefab/settings files and .meta files.
2. Inspect the old folder for user additions, modified files or moved assets.
   Preserve needed modifications separately. If ownership is uncertain, cancel
   and resolve it before installing; matching path/GUID is not proof of ownership.
3. Check the installer deletion list. Only the intended SPS2 legacy folder
   should be listed for this product. A relocated root must be reviewed too.
4. After migration, check compilation, saved settings, scene/prefab references
   and existing setup behavior. If unexpected loss or missing references appear,
   close Unity and restore the complete pre-migration backup.

VPAI can return Nothing TO DO when all requested packages already satisfy their
versions. Reimporting an installer is not guaranteed to remove an old folder
added later. A duplicate VPM+Assets installation must be investigated with a
backup; do not promise unconditional cleanup or use force/reinstall blindly.

Standard metadata cannot enforce backup or protect unknown nested user files
across every package-manager caller. A VPAI-only custom guard would not protect
VCC/ALCOM installs. No fork or custom deletion script is introduced. If guaranteed
automatic backup/unknown-file blocking becomes a product requirement, it needs
a separately designed migration route before claiming that guarantee.

## Validation distinction

Artifact tests verify exact metadata, old root/GUID, stable shared GUIDs and
propagation through the real VPM ZIP/listing builders. These are not actual
Unity import/migration tests.

Required disposable native cases: pristine legacy install, nested user addition
and modified file (confirm deletion risk with a restorable fixture), relocated
root, VPM already installed, duplicate VPM+Assets, Cancel, and settings/scene/
prefab reimport without missing scripts. Confirm prompt/backup/restore behavior
and capture actual results before promoting the migration candidate. Current
user folders must never be used as deletion fixtures.

References: https://vcc.docs.vrchat.com/vpm/packages/#vpm-manifest-additions and
https://github.com/anatawa12/VPMPackageAutoInstaller . Official installer source
used: 965a08e896ce39790fd0d46bcb89d2ffba99ea13, version 1.1.6.
