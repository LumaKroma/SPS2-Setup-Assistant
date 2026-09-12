# IcePop display asset provenance

Issue 121 comment 5644512003 records the owner's permission to reuse and include
LumaToys IcePop display assets in this private implementation. This is an asset
copy, not a Blender edit or a regenerated model.

| Included file | Source |
| --- | --- |
| `Assets/IcePop/IcePop.fbx` | LumaGimmics `Assets/LumaKroma/LumaToys/Models/IcePop.fbx` |
| `Assets/IcePop/IcePop.mat` | LumaGimmics `Assets/LumaKroma/LumaToys/Materials/IcePop.mat` |

Model bytes and material bytes are copied unchanged. Model import settings are
preserved. Copied assets use new stable .meta GUIDs to avoid collisions when the
source LumaToys assets are installed in the same project.

The material requires lilToon, which is a separate dependency and is not
bundled. The generated Plug assigns the copied material to its display
renderers. The original IcePop Prefab, menus, Modular Avatar integrations,
Tracker assets and other gimmick components are excluded.

Original package code/docs remain MIT licensed. This private inclusion does not
transfer ownership or establish a public redistribution license for the display
assets. No public release or publication is authorized by this record.
