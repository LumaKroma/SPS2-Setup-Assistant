# Family distribution candidate — 2026-10-11, unpublished

User selected an existing repository-add page with Fury/NDMF choice and two
unitypackages. This supersedes the prior ZIP/package.json-only release proposal.
One common SPS2 VPM implementation is retained. No new VPM metapackage or product
family default override is introduced.

## Deliverables and creation

`Website/index.html` offers Fury or SPS for NDMF, official dependency repository
registration links, then target-project dependency installation, then SPS2
installation. addRepo buttons register repositories; they never claim to install
selected packages. Both manual URLs and no-JavaScript instructions are available.

Two official VPAI 1.1.6 configs live in `Distribution/1.2.0-beta.1`:

| Config | Explicit installer requests |
| --- | --- |
| Fury-config.json | Common SPS2 =1.2.0-beta.1, VRCFury >=1.0.0, lilToon >=1.0.0 |
| NDMF-config.json | Common SPS2 =1.2.0-beta.1, SPSNDMF =1.1430.0, MA >=1.18.1 <2.0.0, NDMF >=1.11.0 <2.0.0, lilToon >=1.0.0 |

Both set minimumUnity=2022.3, includePrerelease=true, silentIfInstalled=false.
The common product manifest still requires only Avatar SDK >=3.10.4 <4.0.0.
NDMF installer requests no Fury/Stub/Migrator. Dependency resolver can propose
updates to installed versions; it does not change saved SPS family values.
With one usable family, existing product auto-detection chooses it; with both,
users choose at new setup. Existing generated/saved setups retain their family.

Official creator source: https://github.com/anatawa12/VPMPackageAutoInstaller/releases/tag/v1.1.6
`creator.mjs` SHA256 416709b3a70dd7b3023e84b04d077f6b38873b15fb7f1042c6900e72f00285af.
Template SHA256 d3b1996a560d35098ccd9686059b6b97f8f7d7f460f68967d9b92cc7dd1e6e93.
Use `Tools/Build-FamilyInstallers.ps1 -CreatorPath <official creator.mjs>` with
a fresh output directory. Creator uses embedded official WASM/template; no Unity
Editor is needed to create these installers. Do not vendor or modify its DLL.

Each unitypackage has exactly the official VPAI folder, DLL and config assets
(three fixed GUIDs), identical to official template except config content. There
is no dependency body, SPS2 source, avatar or validation scene. DLL is installer
infrastructure; final SPS2 and dependencies download from their published sources.
Both installers share temporary VPAI GUIDs; import only one at a time.

Customer ZIP includes that unitypackage, README.txt, VPAI-LICENSE.txt and
VPAI-LICENSE-semver.net.txt. License notices are retained because VPAI itself is
redistributed. README documents network requirement and standard legacy-folder
migration removal boundary. Normal prompts are preserved; no NO_PROMPT mode.

## Verification and limits

Two fresh builds match for each unitypackage and customer ZIP. Archive audit
compares every non-config member with official template and verifies exact config,
opposite-family absence, no Stub/Migrator, and exact four-file customer ZIP payload.
Validate-Package and git diff --check pass. Browser visual QA passed using existing Edge headless with a separate temporary profile: desktop Fury/NDMF, 390px mobile, family switching, no horizontal overflow and no page errors. Static markup/link and inline JavaScript checks also pass.
No Unity launch/import/operation was performed. Native fresh VPAI import/confirmation/
dependency resolution remains unverified and cannot fully succeed until beta is
published in the listing. Any native gate must use a separately authorized disposable
project, never the user's open PID103904 Editor. No current need to export with Unity.

## Exact additional future public actions

The already reviewed common VPM ZIP/package.json are joined by these release assets:

- SPS2-Setup-Assistant-1.2.0-beta.1-Fury-Installer.zip
- SPS2-Setup-Assistant-1.2.0-beta.1-NDMF-Installer.zip

The page links customer ZIPs with their licenses. Each contains one unitypackage;
raw unitypackages remain local deliverables and are not separate public attachments.
Existing release.yml creates only VPM ZIP/package.json; after its exact-SHA draft
creation, attach these two approved installer ZIPs to the same beta draft and verify hashes.
Publish prerelease=true, make_latest=false. Existing stable assets and VPAI stay intact.

Important Pages routing: existing listing.yml checks out main, so a Website change
only on Issue186 will NOT be reflected by dispatching main. After explicit approval,
make a separate reviewed Website/index.html-only commit/PR derived from current main
and merge that page change (not the old-main product implementation). Record its new
main SHA. Dispatch existing listing.yml there after beta assets/listing availability;
this reads Release ZIPs and preserves prior hashes. No workflow/Pages policy change
is needed. Do not merge the whole Issue branch just to publish this page.
Approval scope must therefore include Issue branch push/tag/beta four assets,
the separate Website-only main update, and existing Pages listing deployment.
No part of those public actions occurred in this preparation.

Noncommercial free API integration with no dependency copying/modification has no
identified clause requiring individual author permission. Commercial avatar sales,
donations or commercial-use guidance with SPSNDMF remain distinct conditions to
assess under its modified/derivative-VRCFury restrictions. No legal clearance or
external author permission is invented. Official source and complete local license
evidence are identified in RELEASE_120_BETA1_PUBLICATION_REVIEW.md.

Publication authorized by the user on 2026-10-11. Final canonical VPM ZIP SHA256: 3d1e489bbda3c31beb7845630c62cb2b64cbf0d230df1d881ac564b523602591. Git-normalized checkout changes from the previous artifact were confirmed to be CRLF/LF only. Runtime gates remain pending until the authorized postpublication installation tests.
