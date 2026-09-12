# Compatibility discovery and independent read-only review

Baseline e8bf98f111517fa00f364561785ff84c4eebb980. Unknown/external/version-drifted
source contract; installed1.1403 public creation/build path directly observed through
owned CoplayDev Editor. This document distinguishes observed source from runtime.

Official source repository: https://github.com/VRCFury/VRCFury
Official guidance: https://vrcfury.com/sps/ and https://vrcfury.com/download/
Read-only reference checkout .codex-staging/issue121-definition/vrcfury-reference.

Observed official source identities:
- 47a441997610d101848deeee049bd6074dc6aa6f: initial SPS, Plug.configureSps,
  Socket name/addMenuItem/enableAuto/Hole/Ring; no oscId/new depth/RingOneWay.
- 3b9a8aa039fa9666349cbc63cf38af1eab4a8394: last source before first SPS2.
- dbf99c4c7782ce2002ac6002b8bcb6d9955b2a3f: first SPS2 beta, guidedPath list,
  native Dual Mode (not Legacy), so no tool Instant support.
- b8d9bf30d1aa428e8b8e8328db9900c60d3f83c0: guidedPathStops, Legacy menu,
  Auto16 cap. Merely detecting useLights would incorrectly enable early Instant.
- f13c11795c63bb6e664a121848b130cdabc7e47a: vector tangent overrides.
- 058c9e37f466f7d72dc560cd90d94d8cf716f51e: Collapse/shrink for path stops.
- df9144c8f5769e1f76af29d97d99436f4cfe2836: tangentInLocal uses current
  stop.lossyScale.x; tangentOutLocal uses previous stop scale (Socket for segment0).
  Set offsetsInLocalUnits to prevent obsolete-field migration overriding new values.

Stable tag ancestry observed: first SPS in1.171.0
(6b06f54efc508c8a95784d4d3cdd051d61985792); SPS2/stops/vector/Collapse in1.1349.0
(1cfd31b493212ec95014559a4f230099c9887a2e); local tangents in1.1409.0
(6df6cee2d5ffc95b3dd182877b97245df1dc2302). Intermediate beta schemas exist.

Initial/native menu source: Auto and Stealth labels match current integration;
initial top-level Holes and Dual Mode are retained via control/parameter identity,
not a guessed hierarchy name. Callback order is -10000 at initial SPS and pre-SPS2;
old callback also checks clone name suffix. Initial duplicate-name allocation shares
one list across Plugs (processed first) and Sockets; reserved Plug-name collision
checks therefore precede building. Exact Socket name tokens can recover external
asset identity, with user-visible Inspector/OSC-name limitation until migration.

Independent read-only review by compat_review found three concrete defects, all
addressed: recognize configureSps; obey PublicAttachment fallback; require Socket
factory for depth availability. Exact API parameter/return signatures and null
results also guarded. Name fallback, Plug collision checks, local tangent mapping,
initial-beta feature flags, missing-SPS UI gate and assembly decoupling reviewed.

Inferred compatibility: inspected historical schemas and menu contracts. Unverified:
full historical-version Editor/SDK/VRChat integration, absent-dependency install and
all beta combinations. Synthetic test component schemas exercise policy only, not
captured external runtime evidence. Do not promote this to all-version certification.
No dependency patch/copy or installed version replacement was used. Existing owned
warm-host split procedure remains applicable; no new launch or unowned Editor work.