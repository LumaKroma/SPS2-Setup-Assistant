# IcePop display asset provenance

The owner explicitly authorized public distribution of these IcePop assets under
MIT on 2026-09-13 in Issue 121's follow-up. This supersedes the earlier private-only
inclusion permission in comment 5644512003. See `Assets/IcePop/LICENSE.txt`.

| Included file | Source |
| --- | --- |
| `Assets/IcePop/IcePop.fbx` | LumaGimmics `Assets/LumaKroma/LumaToys/Models/IcePop.fbx` |
| `Assets/IcePop/IcePop.mat` | LumaGimmics `Assets/LumaKroma/LumaToys/Materials/IcePop.mat` |
| `Assets/IcePop/IcePopStick.mat` | Authored through Unity for this package; opaque wood brown |

Model geometry, original material bytes and model import settings are unchanged.
Copied model/material assets have stable distinct .meta GUIDs. The normal test
Plug uses local scale (0.7, 0.7, 0.7); the Ice renderer uses IcePop.mat and the
Stick renderer uses IcePopStick.mat. These defaults are reapplied when shown again.
The long capsule is unaffected. Both materials require separately installed lilToon;
no third-party shader is bundled.

The original LumaToys Prefab, menus, Modular Avatar integrations, Tracker assets
and other gimmick components are excluded. Code, documentation and these included
IcePop assets are MIT licensed; this permission does not relicense external
VRCFury, VRChat SDK, Modular Avatar or lilToon dependencies. Actual repository/VPM
publication is a separate action and was not performed by this change.
