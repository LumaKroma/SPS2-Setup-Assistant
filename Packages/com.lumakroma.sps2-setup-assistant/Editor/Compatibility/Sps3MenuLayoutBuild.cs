using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // SDK-only entrypoints. No menu authoring asset is installed or rewritten.
    internal sealed class Sps3MenuCaptureBeforeBackends
    {
        public int callbackOrder => -11002;
        public bool OnPreprocessAvatar(GameObject avatar) => Sps3MenuLayoutBuild.Capture(avatar);
    }
    internal sealed class Sps3MenuVerifyBeforeFury
    {
        public int callbackOrder => -10001;
        public bool OnPreprocessAvatar(GameObject avatar) => Sps3MenuLayoutBuild.VerifyBeforeFury(avatar);
    }
    internal sealed class Sps3MenuArrangeAfterFury
    {
        public int callbackOrder => -9998;
        public bool OnPreprocessAvatar(GameObject avatar) {
#if LUMAKROMA_SPS2_HAS_NATIVE_NDMF
            return true; // The NDMF optimizing pass owns the post-Fury transaction.
#else
            return Sps3MenuLayoutBuild.Finish(avatar);
#endif
        }
    }

    internal static class Sps3MenuLayoutBuild
    {
        internal sealed class Expected
        {
            internal string[] Tokens;
            internal string SettingsLabel;
            internal bool Auto, AutoDefault, AutoSaved, Legacy, LegacyDefault, LegacySaved, SaveSockets;
            internal string NextLabel = "Next";
        }
        private sealed class CaptureState
        {
            internal Sps2SetupAsset asset;
            internal string identity;
            internal Component options;
            internal string optionsState;
            internal bool borrowed;
            internal Expected expected;
            internal string[] tokens;
            internal VRCExpressionsMenu inputMenu;
        }
        private static readonly ConditionalWeakTable<GameObject, CaptureState> Pending = new ConditionalWeakTable<GameObject, CaptureState>();
        private static string Normalize(string s) => Regex.Replace(Regex.Replace(s ?? "", "<.*?>", "")
            .Replace("\\n", " ").Replace("\n", " "), @"\s+", " ").Trim().ToLowerInvariant();
        internal static string SettingsLabel(DisplayLanguage language)
        {
            switch (language) {
                case DisplayLanguage.English: return "Settings";
                case DisplayLanguage.Korean: return "\uC124\uC815";
                case DisplayLanguage.ChineseSimplified: return "\u8BBE\u7F6E";
                case DisplayLanguage.ChineseTraditional: return "\u8A2D\u5B9A";
                default: return "\u8A2D\u5B9A";
            }
        }
        internal static bool CanAdopt(Component options)
        {
            if (options == null || !Sps3MenuLayoutSources.Supported()) return false;
            var p = new SerializedObject(options).FindProperty("content.menuPath");
            return p != null && (string.IsNullOrWhiteSpace(p.stringValue) || p.stringValue == "SPS");
        }
        internal static string AdoptionWarning(Sps2SetupContext root, Component options, string settingsLabel)
        {
            try {
                if(!CanAdopt(options))return "SPS menu layout remains native: this version or custom menu path is outside the reviewed layout contract.";
                ValidateInputRoot(root.avatar.expressionsMenu);
                ValidateSocketNames(root.sockets.Select(s => new SerializedObject(s.socket).FindProperty("name")?.stringValue),settingsLabel,NativeNextLabel(root.avatar.gameObject));
                return null;
            } catch(Exception e) { return "SPS menu layout remains native: "+e.Message; }
        }
        private static string NativeNextLabel(GameObject avatar)
        {
            // Same FirstOrDefault selection as the reviewed native finalizer; read only.
            foreach(var component in avatar.GetComponentsInChildren<Component>(true)) {
                if(component==null || component.GetType().FullName!="VF.Model.VRCFury")continue;
                var content=new SerializedObject(component).FindProperty("content");
                if(content?.managedReferenceValue?.GetType().FullName!="VF.Model.Feature.OverrideMenuSettings")continue;
                var next=content.FindPropertyRelative("nextText")?.stringValue;
                return string.IsNullOrEmpty(next)?"Next":next;
            }
            return "Next";
        }
        private static bool Guard(GameObject avatar, Action action)
        {
            try { action(); return true; }
            catch (Exception e) {
                if (avatar != null) Pending.Remove(avatar);
                Debug.LogWarning("SPS menu arrangement skipped; native output is unchanged: " + e.Message, avatar);
                return true;
            }
        }
        internal static bool Capture(GameObject avatar) => Guard(avatar, () => {
            if (avatar == null) return;
            Pending.Remove(avatar); // An aborted previous build may have retained only read-only state.
            var legacyMarkers = avatar.GetComponentsInChildren<Sps3OptionsOwnership>(true);
            if (legacyMarkers.Any(m => m.menuLayoutVersion != 0) &&
                (legacyMarkers.Length != 1 || legacyMarkers[0].menuLayoutVersion != 1))
                throw new InvalidOperationException("Unknown or ambiguous Setup menu ownership.");
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null || EditorUtility.IsPersistent(avatar) || !avatar.scene.IsValid())
                throw new InvalidOperationException("An SDK avatar clone is required.");
            var root = Sps2SetupStorage.Find(descriptor, true, SpsFamily.VrcFury);
            if (root == null || root.AuthoringContract != SpsAuthoringContract.Sps3Beta14) {
                if (legacyMarkers.Any(m => m.menuLayoutVersion != 0))
                    throw new InvalidOperationException("Setup provenance does not identify the menu owner.");
                return;
            }
            var marker = Sps3OwnedGlobalOptions.Marker(root);
            bool borrowed = marker == null;
            if (!borrowed && marker.menuLayoutVersion == 0) return;
            if (!borrowed && marker.menuLayoutVersion != 1) throw new InvalidOperationException("Unknown Setup menu ownership.");
            var options = borrowed ? Sps3OwnedGlobalOptions.AllOptions(descriptor).SingleOrDefault() : marker.options;
            var settingsLabel = borrowed ? SettingsLabel(root.settings.menuLanguage) : marker.menuSettingsLabel;
            if (borrowed && (options == null || options.gameObject != avatar)) throw new InvalidOperationException("A unique avatar-root SPS Options is required.");
            Sps3OwnedGlobalOptions.RequirePinnedSchema();
            Sps3MenuLayoutSources.Require();
            Sps3OwnedGlobalOptions.ValidateOwnership(descriptor, root);
            if (!CanAdopt(options)) throw new InvalidOperationException("Custom SPS menu paths remain native; regenerate only after resolving this layout conflict.");
            if (string.IsNullOrWhiteSpace(settingsLabel) || settingsLabel.Contains("/"))
                throw new InvalidOperationException("Missing or invalid saved Settings folder label.");
            for (var t = root.transform; t != null; t = t.parent) {
                if (t.CompareTag("EditorOnly")) throw new InvalidOperationException("The Setup owner would be removed before Fury.");
                if (t == avatar.transform) break;
            }
            ValidateInputRoot(descriptor.expressionsMenu);
            RequireNoNativeWitness(avatar);
            var sockets = SpsBackend.AllSockets(avatar).ToArray();
            int MenuOrder(string id) {
                var part = root.settings.parts.FirstOrDefault(p => p != null && p.id == id);
                if (part == null) return 2;
                if (LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.IsCasualPart(part)) return 0;
                return !part.custom && !part.IsFingerRing && part.id != "earLeft" && part.id != "earRight" && part.id != "thighs" ? 1 : 2;
            }
            var tokens = root.sockets.OrderBy(s => MenuOrder(s.id)).Select(s => Sps2SetupStorage.Token(root, s.id)).ToArray();
            if (tokens.Length == 0 || sockets.Length != tokens.Length || tokens.Distinct().Count() != tokens.Length)
                throw new InvalidOperationException("Owned sockets are missing, duplicated, or mixed with foreign sockets.");
            bool participating = false;
            foreach (var socket in sockets) {
                var so = new SerializedObject(socket);
                var token = VrcFuryCompatibility.ReadIdentity(socket);
                if (!tokens.Contains(token) || so.FindProperty("addMenuItem")?.boolValue != true)
                    throw new InvalidOperationException("A native socket is not an owned enabled menu control.");
                var name = so.FindProperty("name")?.stringValue;
                if (string.IsNullOrWhiteSpace(name) || name.Contains("/") || name.Contains(".dup."))
                    throw new InvalidOperationException("Hierarchical or ambiguous socket names are preserved, not flattened automatically.");
                participating |= so.FindProperty("enableAuto")?.boolValue == true;
            }
            var data = new SerializedObject(options);
            bool B(string name) => VrcFuryCompatibility.Require(data,"content."+name,SerializedPropertyType.Boolean).boolValue;
            var expected = new Expected {
                Tokens=tokens, SettingsLabel=settingsLabel,
                Auto=B("autoModeAvailable") && participating, AutoDefault=B("autoModeDefaultOn"), AutoSaved=B("autoModeSaved"),
                Legacy=B("legacyModeAvailable") || B("legacySocketCompatibility") && B("addDpsTipLight"),
                LegacyDefault=B("legacyModeEnabledOnAvatarLoad"), LegacySaved=B("legacyModeSaved"), SaveSockets=B("saveSockets")
            };
            expected.NextLabel=NativeNextLabel(avatar);
            ValidateSocketNames(sockets.Select(s => new SerializedObject(s).FindProperty("name").stringValue), expected.SettingsLabel, expected.NextLabel);
            Pending.Add(avatar,new CaptureState { asset=root.asset, identity=root.identity, options=options, borrowed=borrowed,
                optionsState=EditorJsonUtility.ToJson(options), expected=expected, tokens=tokens,
                inputMenu=descriptor.expressionsMenu });
        });
        internal static bool VerifyBeforeFury(GameObject avatar) => Guard(avatar, () => {
            if (avatar == null || !Pending.TryGetValue(avatar,out var state)) return;
            RequireCapturedOwner(avatar,state);
            RequireNoNativeWitness(avatar);
            ValidateInputRoot(avatar.GetComponent<VRCAvatarDescriptor>().expressionsMenu);
        });
        private static void RequireCapturedOwner(GameObject avatar, CaptureState state)
        {
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            var root = descriptor == null ? null : Sps2SetupStorage.Find(descriptor, true, SpsFamily.VrcFury);
            var owner = root == null ? null : Sps3OwnedGlobalOptions.Marker(root);
            bool ownerMatches = root != null && (state.borrowed
                ? owner == null && Sps3OwnedGlobalOptions.AllOptions(descriptor).SequenceEqual(new[] { state.options }) && SettingsLabel(root.settings.menuLanguage) == state.expected.SettingsLabel
                : owner != null && owner.options == state.options && owner.menuLayoutVersion == 1 && owner.menuSettingsLabel == state.expected.SettingsLabel);
            if (root == null || root.asset != state.asset || root.identity != state.identity ||
                !ownerMatches || state.options == null ||
                state.options.gameObject != avatar || EditorJsonUtility.ToJson(state.options) != state.optionsState)
                throw new InvalidOperationException("Setup ownership/options changed or disappeared before native completion.");
            var actual = SpsBackend.AllSockets(avatar).Select(VrcFuryCompatibility.ReadIdentity).ToArray();
            if (actual.Length != state.tokens.Length || !new HashSet<string>(actual).SetEquals(state.tokens))
                throw new InvalidOperationException("Owned socket identities changed during the build.");
        }
        private static Type WitnessType() => VrcFuryApi.FindType("VF.Model.VRCFuryTest");
        private static void RequireNoNativeWitness(GameObject avatar)
        {
            var type = WitnessType();
            if (type == null || avatar.GetComponent(type) != null)
                throw new InvalidOperationException("A fresh, not-yet-Fury-built SDK clone is required.");
        }
        internal static bool Finish(GameObject avatar) => FinishCore(avatar, null);
        internal static bool FinishWithSaver(GameObject avatar, Action<UnityEngine.Object> save) => FinishCore(avatar, save);
        private static bool FinishCore(GameObject avatar, Action<UnityEngine.Object> save) => Guard(avatar, () => {
            if (avatar == null || !Pending.TryGetValue(avatar,out var state)) return;
            try {
                RequireCapturedOwner(avatar,state);
                if (save == null) Sps3MenuLayoutSources.RequireCollector(avatar);
                var type = WitnessType();
                if (type == null || avatar.GetComponent(type) == null)
                    throw new InvalidOperationException("Fury did not finish building this clone.");
                var descriptor=avatar.GetComponent<VRCAvatarDescriptor>();
                if (descriptor.expressionsMenu == state.inputMenu)
                    throw new InvalidOperationException("Native menu generation did not produce a separate output.");
                ArrangeBuiltMenu(descriptor,state.expected,save);
            } finally { Pending.Remove(avatar); }
        });
        internal static void ValidateSocketNames(IEnumerable<string> names, string settingsLabel, string nextLabel)
        {
            var seen = new HashSet<string> { Normalize(settingsLabel), Normalize(nextLabel), "auto", "legacy support" };
            foreach(var name in names) {
                if(string.IsNullOrWhiteSpace(name) || name.Contains("/") || name.Contains(".dup."))
                    throw new InvalidOperationException("Socket path/escaped slash is preserved in the native menu: " + name);
                // Conservatively include HTML-separated slugs, matching native fuzzy menu merge behavior.
                var normalized=Normalize(name);
                var slugs=Regex.Replace(name,@"<.*?>","`").Split('`').Select(Normalize).Where(n=>n.Length>0).Append(normalized).Distinct().ToArray();
                if(normalized.Length==0 || slugs.Any(seen.Contains))
                    throw new InvalidOperationException("Socket/menu label collision is preserved in the native menu: " + name);
                foreach(var slug in slugs)seen.Add(slug);
            }
        }
        internal static void ValidateInputRoot(VRCExpressionsMenu menu)
        {
            if (menu == null) return;
            if (menu.controls == null) throw new InvalidOperationException("Input menu has no control list.");
            if (menu.controls.Any(c => c == null || new[] {"sps","holes","sockets"}.Contains(Normalize(c.name))))
                throw new InvalidOperationException("Existing root SPS/Holes/Sockets entries are not owned by this Setup and cannot be merged safely.");
        }

        private sealed class Edge
        {
            internal VRCExpressionsMenu parent;
            internal int index;
        }
        // Test seam: caller supplies a proven build expectation. It never changes authoring data.
        internal static void ArrangeBuiltMenu(VRCAvatarDescriptor avatar, Expected expected, Action<UnityEngine.Object> save = null)
        {
            if (avatar == null || avatar.expressionsMenu == null || avatar.expressionParameters == null ||
                expected == null || expected.Tokens == null || expected.Tokens.Length == 0 ||
                expected.Tokens.Any(string.IsNullOrEmpty) || expected.Tokens.Distinct().Count()!=expected.Tokens.Length ||
                string.IsNullOrWhiteSpace(expected.SettingsLabel) || expected.SettingsLabel.Contains("/"))
                throw new InvalidOperationException("Native menu output/expectation is missing.");
            var originalRoot=avatar.expressionsMenu;
            var graph=new List<VRCExpressionsMenu>();
            var incoming=new Dictionary<VRCExpressionsMenu,List<Edge>>();
            var seen=new HashSet<VRCExpressionsMenu>();
            var queue=new Queue<VRCExpressionsMenu>();queue.Enqueue(originalRoot);
            while (queue.Count>0) {
                var menu=queue.Dequeue();if (!seen.Add(menu)) continue;
                if (seen.Count>256 || menu.controls==null) throw new InvalidOperationException("Unsupported menu graph size or shape.");
                graph.Add(menu);
                for(int i=0;i<menu.controls.Count;i++) {
                    var c=menu.controls[i];
                    if(c==null) throw new InvalidOperationException("Null menu control.");
                    if(c.type!=VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                    if(c.subMenu==null) continue; // Preserve unrelated native controls, including incomplete foreign submenus.
                    if(!incoming.TryGetValue(c.subMenu,out var parents)) incoming[c.subMenu]=parents=new List<Edge>();
                    parents.Add(new Edge { parent=menu,index=i });queue.Enqueue(c.subMenu);
                }
            }
            bool IsOwnedParameter(string parameter) => parameter!=null &&
                expected.Tokens.Any(t=>parameter.EndsWith("_"+t,StringComparison.Ordinal));
            bool ContainsOwned(VRCExpressionsMenu menu) {
                var todo=new Queue<VRCExpressionsMenu>();var visited=new HashSet<VRCExpressionsMenu>();todo.Enqueue(menu);
                while(todo.Count>0) {
                    var m=todo.Dequeue();if(!visited.Add(m))continue;
                    foreach(var c in m.controls) {
                        if(IsOwnedParameter(c.parameter?.name))return true;
                        if(c.type==VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu!=null)todo.Enqueue(c.subMenu);
                    }
                }
                return false;
            }
            var candidates=graph.SelectMany(m=>m.controls.Select((c,i)=>new {m,c,i})).Where(x=>
                x.c.type==VRCExpressionsMenu.Control.ControlType.SubMenu && x.c.subMenu!=null && x.c.name=="SPS" && ContainsOwned(x.c.subMenu)).ToArray();
            if(candidates.Length!=1)throw new InvalidOperationException("The owned native SPS menu is missing or ambiguous.");
            var top=candidates[0].c.subMenu;
            var pageMenus=new List<VRCExpressionsMenu>();var leaves=new List<VRCExpressionsMenu.Control>();
            var leafLocations=new List<(int page,int index)>();
            var current=top;
            while(current!=null) {
                if(pageMenus.Contains(current) || !incoming.TryGetValue(current,out var parents) || parents.Count!=1)
                    throw new InvalidOperationException("Owned menu pages are cyclic or shared.");
                if(current.controls.Count>8 || current.controls.Count==0) throw new InvalidOperationException("Native page size is unsupported.");
                int pageIndex=pageMenus.Count;pageMenus.Add(current);
                VRCExpressionsMenu next=null;
                for(int i=0;i<current.controls.Count;i++) {
                    var c=current.controls[i];
                    if(c.type==VRCExpressionsMenu.Control.ControlType.SubMenu) {
                        if(i!=current.controls.Count-1 || current.controls.Count!=8 || c.name!=expected.NextLabel ||
                            !string.IsNullOrEmpty(c.parameter?.name) || next!=null)
                            throw new InvalidOperationException("Unknown submenu inside the owned SPS branch.");
                        if(c.subMenu==null)throw new InvalidOperationException("Missing native Next page.");
                        next=c.subMenu;
                    } else {
                        if(c.type!=VRCExpressionsMenu.Control.ControlType.Toggle || c.value!=1 || c.subMenu!=null ||
                            string.IsNullOrEmpty(c.parameter?.name))
                            throw new InvalidOperationException("Unsupported native toggle payload.");
                        leaves.Add(c);leafLocations.Add((pageIndex,i));
                    }
                }
                current=next;
            }
            var socketParams=new List<string>();string prefix=null;
            foreach(var token in expected.Tokens) {
                var matches=leaves.Where(c=>c.parameter.name.EndsWith("_"+token,StringComparison.Ordinal)).ToArray();
                if(matches.Length!=1)throw new InvalidOperationException("Missing or duplicate owned socket control: "+token);
                var name=matches[0].parameter.name;
                var candidate=name.Substring(0,name.Length-token.Length);
                if(!Regex.IsMatch(candidate,@"\AVF[0-9]+_\z") || prefix!=null && prefix!=candidate)
                    throw new InvalidOperationException("Native parameter ownership prefix is not exact.");
                prefix=candidate;socketParams.Add(name);
            }
            string auto=expected.Auto?prefix+"autoMode":null, legacy=expected.Legacy?prefix+"legacy":null;
            var plan=Sps3MenuLayoutPlan.Create(leaves.Select(c=>c.parameter.name).ToArray(),socketParams,auto,legacy);
            var parameters=avatar.expressionParameters;
            var parameterJson=EditorJsonUtility.ToJson(parameters);
            void RequireParam(string name,bool def,bool saved,bool synced) {
                var matching=parameters.parameters.Where(p=>p.name==name).ToArray();
                if(matching.Length!=1 || matching[0].valueType!=VRCExpressionParameters.ValueType.Bool ||
                    matching[0].defaultValue!=(def?1:0) || matching[0].saved!=saved || matching[0].networkSynced!=synced)
                    throw new InvalidOperationException("Native parameter/default/save/sync contract changed: "+name);
            }
            foreach(var p in socketParams)RequireParam(p,false,expected.SaveSockets,true);
            if(auto!=null)RequireParam(auto,expected.AutoDefault,expected.AutoSaved,false);
            if(legacy!=null)RequireParam(legacy,expected.LegacyDefault,expected.LegacySaved,true);
            var baseLayers=avatar.baseAnimationLayers;
            var specialLayers=avatar.specialAnimationLayers;
            // Only this ancestor path is cloned; unrelated submenu references remain exactly native.
            var ancestorMenus=new List<VRCExpressionsMenu>();var ancestorIndices=new List<int>();
            var node=top;var chainSeen=new HashSet<VRCExpressionsMenu>();
            while(node!=originalRoot) {
                if(!chainSeen.Add(node) || !incoming.TryGetValue(node,out var es) || es.Count!=1)
                    throw new InvalidOperationException("The owned menu has shared/cyclic ancestors.");
                ancestorMenus.Add(es[0].parent);ancestorIndices.Add(es[0].index);node=es[0].parent;
            }
            if(incoming.ContainsKey(originalRoot))throw new InvalidOperationException("Root menu is cyclic.");
            var originals=pageMenus.Concat(ancestorMenus).Distinct().ToDictionary(m=>m,EditorJsonUtility.ToJson);
            var allocated=new List<VRCExpressionsMenu>();
            bool committed=false;
            try {
                VRCExpressionsMenu Clone(VRCExpressionsMenu source) {
                    var clone=Object.Instantiate(source);allocated.Add(clone);
                    // Verify serialization made independent mutable control payloads before touching any.
                    if(ReferenceEquals(source.controls,clone.controls) || source.controls.Count!=clone.controls.Count)
                        throw new InvalidOperationException("Menu controls were not independently copied.");
                    for(int i=0;i<source.controls.Count;i++) {
                        var a=source.controls[i];var b=clone.controls[i];
                        if(ReferenceEquals(a,b) || a.parameter!=null && ReferenceEquals(a.parameter,b.parameter) ||
                            a.subParameters!=null && a.subParameters.Length>0 && ReferenceEquals(a.subParameters,b.subParameters) ||
                            a.labels!=null && a.labels.Length>0 && ReferenceEquals(a.labels,b.labels))
                            throw new InvalidOperationException("Native menu payload is shared with its copy.");
                    }
                    clone.name=source.name+" (Setup layout)";
                    clone.hideFlags=source.hideFlags & ~HideFlags.DontSave;
                    return clone;
                }
                var copies=pageMenus.Select(Clone).ToArray();
                var copiedLeaves=leafLocations.Select(loc=>copies[loc.page].controls[loc.index]).ToArray();
                var rootItems=new List<VRCExpressionsMenu.Control>();
                VRCExpressionsMenu settings=null;
                if(plan.SettingsIndices.Length>0) {
                    settings=ScriptableObject.CreateInstance<VRCExpressionsMenu>();allocated.Add(settings);
                    settings.name="SPS Settings";
                    settings.controls=plan.SettingsIndices.Select(i=>copiedLeaves[i]).ToList();
                }
                foreach(var index in plan.RootIndices) {
                    if(index==Sps3MenuLayoutPlan.SettingsItem)rootItems.Add(new VRCExpressionsMenu.Control {
                        name=expected.SettingsLabel,type=VRCExpressionsMenu.Control.ControlType.SubMenu,subMenu=settings,
                        parameter=new VRCExpressionsMenu.Control.Parameter {name=""},subParameters=Array.Empty<VRCExpressionsMenu.Control.Parameter>(),
                        labels=Array.Empty<VRCExpressionsMenu.Control.Label>()
                    });
                    else rootItems.Add(copiedLeaves[index]);
                }
                var layoutPages=Sps3MenuLayoutPlan.Paginate(Enumerable.Range(0,rootItems.Count).ToArray());
                if(layoutPages.Length>copies.Length)throw new InvalidOperationException("The new layout needs an unavailable native page.");
                for(int p=0;p<layoutPages.Length;p++) {
                    copies[p].controls=layoutPages[p].Select(i=>rootItems[i]).ToList();
                    if(p+1<layoutPages.Length) {
                        // Use the deep-copied native Next control, retaining its label/icon/value/style.
                        var nextCopy=Clone(pageMenus[p]);
                        var next=nextCopy.controls.Last();
                        next.subMenu=copies[p+1];copies[p].controls.Add(next);
                    }
                }
                var replacement=copies[0];
                for(int i=0;i<ancestorMenus.Count;i++) {
                    var clone=Clone(ancestorMenus[i]);clone.controls[ancestorIndices[i]].subMenu=replacement;replacement=clone;
                }
                if(avatar.expressionParameters!=parameters || EditorJsonUtility.ToJson(parameters)!=parameterJson ||
                    !ReferenceEquals(avatar.baseAnimationLayers,baseLayers) || !ReferenceEquals(avatar.specialAnimationLayers,specialLayers))
                    throw new InvalidOperationException("Non-menu native output changed while planning.");
                foreach(var pair in originals)
                    if(EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new InvalidOperationException("An original native menu changed while planning.");
                for(int i=0;i<leaves.Count;i++)
                    if(JsonUtility.ToJson(leaves[i])!=JsonUtility.ToJson(copiedLeaves[i]))throw new InvalidOperationException("Native toggle payload changed.");
                // Destroy only temporary clone holders that are not reachable from the proposed output.
                var retained=new HashSet<VRCExpressionsMenu>();var todo=new Queue<VRCExpressionsMenu>();todo.Enqueue(replacement);
                while(todo.Count>0) {
                    var m=todo.Dequeue();if(!retained.Add(m))continue;
                    foreach(var c in m.controls)if(c.type==VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu!=null)todo.Enqueue(c.subMenu);
                }
                foreach(var m in allocated.Where(m=>!retained.Contains(m)).ToArray()) {Object.DestroyImmediate(m);allocated.Remove(m);}
                if (save != null) foreach (var menu in allocated.Where(retained.Contains)) {
                    save(menu);
                    if (!EditorUtility.IsPersistent(menu)) throw new InvalidOperationException("Menu saver did not persist the new build asset.");
                }
                avatar.expressionsMenu=replacement;committed=true;
            } finally {
                if(!committed)foreach(var m in allocated)if(m!=null && !EditorUtility.IsPersistent(m))Object.DestroyImmediate(m);
            }
        }
    }
    public static class Sps3MenuNdmfBridge
    {
        public static bool Finish(GameObject avatar, Action<UnityEngine.Object> save) {
            if (save == null) return false;
            return Sps3MenuLayoutBuild.FinishWithSaver(avatar, save);
        }
    }
}
