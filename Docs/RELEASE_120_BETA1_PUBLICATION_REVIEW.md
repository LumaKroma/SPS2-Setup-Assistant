Updated distribution scope: see RELEASE_120_BETA1_DISTRIBUTION.md. The earlier two-asset-only/no-beta-installer proposal below is superseded by the user's Fury/NDMF installer decision. Commercial-use questions are separate from this noncommercial free package publication; individual author permission was not identified as mandatory for that scope.

# 1.2.0-beta.1 publication review — unpublished

Prepared 2026-10-11. This document specifies a future approval scope; no commit,
push, merge, tag, Release, listing deployment or installer update was performed.
The user's open Unity Editor and manual project were not operated in this phase.

## Candidate and payload

- Source: `issue/186-sps2-ndmf-option`, current base HEAD
  `b6ff22b6c73a4d78fde272ef12439c16135ba9f7` plus uncommitted candidate changes.
  The final reviewed commit SHA does not exist yet and must be recorded before release.
- Final local asset: `.artifacts/issue186-family-final-vpm-a/com.lumakroma.sps2-setup-assistant-1.2.0-beta.1.zip`.
  89 entries; SHA256 `3d1e489bbda3c31beb7845630c62cb2b64cbf0d230df1d881ac564b523602591`.
  Independent `issue186-family-final-vpm-b` build has the same hash. Each build
  checks included bytes against source. Validate-Package and git diff --check pass.
- Release attachments: this ZIP and the generated sibling `package.json` only.
  SHA256SUMS stays local; no Assets edition, VPAI, avatar, validation evidence or dependency archive.
- ZIP includes own Editor/Runtime/Assets code and `.meta`, root manifest/README/LICENSE.
  Tests, Docs, Documentation~, Tools and private evidence are excluded by the builder.
  There are zero DLL/shader/cginc/HLSL entries. IcePop FBX, two materials and its
  LICENSE are included; lilToon shader is referenced by material, separately installed.
- VPM automatically requests `com.vrchat.avatars >=3.10.4 <4.0.0` only. Other
  families/dependencies are manually installed. Own adapters and conditional type/schema
  references are included; VRCFury/SPSNDMF/MA/NDMF/lilToon/Gesture Manager payloads are not.
- LICENSE.md's obsolete private-only IcePop scope header was corrected from the
  existing 2026-09-13 public MIT authorization in Documentation~/ASSET_PROVENANCE.md.
  Stable archives were not edited. Earlier hash `a585eb...` is superseded locally.

## Actual license evidence and unresolved scope

Installed-version complete LICENSE/NOTICE text, path and SHA256 are preserved in
the task's `issue186-installed-license-intake.json`. This table reports those
files and official sources; it does not establish legal clearance for every use.

| Package/version | Actual notice and required distinction |
| --- | --- |
| Own package / IcePop | MIT, copyright 2026 LumaKroma. Root LICENSE.md and Assets/IcePop/LICENSE.txt ship in ZIP. Keep copyright and permission text on copies/substantial portions. External dependencies are not relicensed. |
| SPSNDMF 1.1430.0 | LICENSE.md: VRCFury (c) 2022 Senky; Personal, Commercial and VRCA/VRCW alternatives. Personal/noncommercial copying, modification and distribution require copyright/license retention. Commercial purpose explicitly includes donations; commercial terms require official Senky-designated distribution and restrict redistribution, reliance on modified/fork/derivative versions and directing users to those versions. VRCA/VRCW permission concerns SPS shader in VRChat-hosted uploaded bundles, not an Editor package redistribution grant. |
| VRCFury 1.1403.0 | LICENSE.md has the same alternatives and Senky attribution. Official unmodified VRCFury use and SPSNDMF fork use need separate scope assessment. |
| NDMF 1.14.3 | COPYING.md MIT, copyright 2023–2024 bd_ and other contributors. Retain notice if copying substantial portions. |
| MA 1.18.1 | COPYING.md: MIT copyright 2022 bd_ for other files; Editor/images redistribution only as official MA package, modified distributions replace/delete images; advertising logo has separate policy. No MA images/logo in candidate. |
| lilToon 2.3.4 | LICENSE MIT copyright 2020-present lilxyzw. Third Party Notices.md includes Unlit_WF_ShaderSuite (whiteflare), GTAvaCrypt (Ryan Goodrich), Poiyomi MIT notices and separately identified references/assets. No lilToon code/shader/third-party payload in candidate; do not infer all referenced assets are MIT. |
| Gesture Manager 3.9.9 | LICENSE.md MIT copyright 2019–2023 BlackStartx. Optional manual test tool; no package payload included. |
| VRChat Base / Avatars 3.10.4 | license.txt points to SDK License, not MIT. Official SDK installation/use stays subject to those terms. No SDK payload is redistributed in candidate. |

Official sources: [SPSNDMF license](https://github.com/niriziki/VRCFury/blob/ndmf/LICENSE.md),
[SPSNDMF installation/package roles](https://spsndmf.nrzk.net/guides/install/),
[SPSNDMF extraction/repackaging description](https://spsndmf.nrzk.net/guides/about/),
[MA logo policy](https://modular-avatar.nadena.dev/docs/distributing-prefabs/logo-usage),
[lilToon license](https://github.com/lilxyzw/lilToon/blob/master/Assets/lilToon/LICENSE),
[Gesture Manager 3.9.9 license](https://github.com/BlackStartx/VRC-Gesture-Manager/blob/v3.9.9/LICENSE.md),
[VRChat SDK terms](https://hello.vrchat.com/legal/sdk).

Inference limited to distribution boundary: external reference alone does not place
the dependency's payload in this ZIP, so a bundled dependency notice is not omitted
from an otherwise bundled copy. This does not settle commercial reliance conditions.
**Unresolved before publication:** intended paid/donation/commercial scope and relevant
rightsholder authorization for SPSNDMF-based use or product/service guidance.
No permission/exception from Senky or SPSNDMF maintainer was found in the supplied evidence.
Do not describe beta status or absence of bundling as commercial clearance.

## Verified public destination and stable preservation

- Public repository: [LumaKroma/SPS2-Setup-Assistant](https://github.com/LumaKroma/SPS2-Setup-Assistant).
- Stable [1.1.1 release](https://github.com/LumaKroma/SPS2-Setup-Assistant/releases/tag/1.1.1)
  points to `7a08706298b91b678a8324b4e60186792cebf539` and has ZIP + package.json assets.
  ZIP SHA256 `d31a5d61c52c6fa17e64e39d736d8dc5a1372e86eba17ee0b9cb5e09a735e2af`.
- [Existing listing](https://lumakroma.github.io/SPS2-Setup-Assistant/index.json):
  `com.lumakroma.sps2-listing`; package `com.lumakroma.sps2-setup-assistant`;
  versions 1.0.0, 1.0.1, 1.0.2, 1.1.0, 1.1.1. No public beta precedent found.
- main currently `4902a77f31e37ccfb6e8b22d6e987c40a6a797bb`, package version 1.0.2.
  Never build this candidate from main. Remote Issue186 branch is currently absent.
- Existing release.yml uses workflow_dispatch selected ref, validates/builds its SHA,
  creates draft with exact target_commitish and prerelease inferred from hyphen,
  and attaches ZIP/package.json. It does not explicitly set make_latest=false.
- Existing listing.yml collects all public nondraft semver releases including beta,
  checks downloaded ZIP manifest/hash and every prior version hash, then deploys Pages.
  The successful stable listing execution used main as a listing-only executor:
  [run 37046056188](https://github.com/LumaKroma/SPS2-Setup-Assistant/actions/runs/37046056188).
  Tag-triggered Pages [run 37046023683](https://github.com/LumaKroma/SPS2-Setup-Assistant/actions/runs/37046023683)
  failed the existing deployment route. No Pages permission/policy change is proposed.

## Concrete future action scope for one approval

1. Review and commit the candidate changes on `issue/186-sps2-ndmf-option`; record
   exact new SHA and confirm resulting package hash. No main merge is required.
   Commit includes UI/localization/tests, beta manifest/version assertion, README,
   changelog, corrected LICENSE, VPM_RELEASE and these release review documents.
   Existing #186 backend commits on this branch become public with that push too.
2. Push only this Issue branch to the existing public origin. Public Git source
   exposes tracked repository contents, including Editor/Runtime/Assets/Tests,
   Documentation~, root/docs/build scripts and history; ZIP exclusions do not make
   those tracked files private. Review `git diff --name-only 7a087...` plus untracked
   candidate Docs before staging; never stage .artifacts or task evidence/user assets.
3. Authenticated read: exclude existing tag/draft/release collision. Dispatch release.yml
   on the pushed Issue branch; require run head SHA equals the approved new SHA.
   Tag/release name `1.2.0-beta.1`, release title `SPS2 Setup Assistant 1.2.0-beta.1`.
   GitHub's generated source archives also expose the tagged tracked repository.
4. Verify draft target SHA, ZIP hash and exact two attachments. Use public release body
   from RELEASE_120_BETA1.md plus final SHA and license scope. Publish with
   `draft=false`, `prerelease=true`, **`make_latest="false"` explicitly**.
   Keep stable 1.1.1 as Latest and do not replace/edit its assets or tag.
5. Dispatch existing listing.yml on reviewed main solely to read published Release
   assets and deploy listing. This is not product build/merge from old main.
   Pages public output includes generated index.json, existing Website/index.html
   and downloaded package ZIP copies in the site artifact. Approval covers this
   existing Pages destination; no new domain, permissions or listing registration.
6. Verify old five versions/hashes unchanged, beta as sixth version with approved
   URL/hash, stable Latest unchanged and explicit beta visibility/selection in VPM.
   Keep existing stable VPAI/includePrerelease=false unchanged; no beta installer.

Before actual publication, resolve commercial scope above and review final-source
validation. User manual PASS scope remains unspecified. Final ZIP native import,
NDMF new setup and existing Fury load/Apply/family guard remain explicit final-artifact
checks; do not infer VRC client compatibility or rerun SDK/Controller for docs alone.
Public read-only intake evidence lives in the task's live-release/publication-route/
pages-deployment-status JSON files. All approval-dependent actions remain unperformed.
