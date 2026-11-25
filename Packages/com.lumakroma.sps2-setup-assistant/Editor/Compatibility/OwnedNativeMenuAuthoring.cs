using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility {
    // Standard Fury authoring for settings and sockets emitted by Setup.
    internal static class OwnedNativeMenuAuthoring {
        private const string HostName = "Menu Order";
        private static Type TypeNamed(string n) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n,false)).FirstOrDefault(t=>t!=null);
        private static bool Segment(string v) => !string.IsNullOrWhiteSpace(v) && v==v.Trim() && v.IndexOfAny(new[]{'/', '\\','<','>','\r','\n'})<0;
        private static void CopyEvidence(Sps2SetupAsset from,Sps2SetupAsset to) {
            if(from==null || to==null) return;
            to.manualMenuPlan=from.manualMenuPlan;
            to.manualMenuModelTypes=from.manualMenuModelTypes?.ToArray(); to.manualMenuModelJson=from.manualMenuModelJson?.ToArray();
        }
        // Match only native models. A label, host transform or old random suffix is never evidence.
        private sealed class RuleSlot {
            internal Component component;
            internal int index;
            internal long id;
            internal string type, json, source, destination;
        }

        private static IEnumerable<RuleSlot> ReadRules(Component component, Type move, Type reorder) {
            var data = new SerializedObject(component);
            var features = data.FindProperty("config.features");
            if (features != null && features.isArray) {
                for (int i = 0; i < features.arraySize; i++) {
                    var item = ToRule(features.GetArrayElementAtIndex(i), i);
                    if (item != null) yield return item;
                }
            }
            var content = ToRule(data.FindProperty("content"), -1);
            if (content != null) yield return content;

            RuleSlot ToRule(SerializedProperty property, int index) {
                if (property == null || property.propertyType != SerializedPropertyType.ManagedReference) return null;
                var model = property.managedReferenceValue;
                if (model == null || (model.GetType() != move && model.GetType() != reorder)) return null;
                bool isMove = model.GetType() == move;
                return new RuleSlot {
                    component = component, index = index, id = property.managedReferenceId,
                    type = model.GetType().FullName, json = JsonUtility.ToJson(model),
                    source = property.FindPropertyRelative(isMove ? "fromPath" : "path")?.stringValue,
                    destination = isMove ? property.FindPropertyRelative("toPath")?.stringValue : null
                };
            }
        }

        private static Plan ReadAutomaticPlan(Sps2SetupAsset evidence) {
            if (string.IsNullOrEmpty(evidence?.manualMenuPlan)) return null;
            try {
                var plan = JsonUtility.FromJson<Plan>(evidence.manualMenuPlan);
                return plan != null && plan.automatic && !string.IsNullOrWhiteSpace(plan.menu) ? plan : null;
            } catch (ArgumentException) { return null; }
        }

        private static bool IsRecorded(RuleSlot slot, Sps2SetupAsset evidence, Plan plan) {
            var types = evidence?.manualMenuModelTypes;
            var payloads = evidence?.manualMenuModelJson;
            if (plan == null || !plan.Includes(slot) || types == null || payloads == null || types.Length != payloads.Length ||
                !WithinMenu(plan.menu, slot.source) || (slot.destination != null && !WithinMenu(plan.menu, slot.destination))) return false;
            for (int i = 0; i < types.Length; i++)
                if (slot.type == types[i] && slot.json == payloads[i]) return true;
            return false;
        }

        private static bool WithinMenu(string menu, string path) => path != null && path.StartsWith(menu + "/", StringComparison.Ordinal);

        internal static bool HasRecordedRulesBelow(VRCAvatarDescriptor avatar, Transform host, Sps2SetupAsset evidence) {
            var types = evidence?.manualMenuModelTypes;
            var plan = ReadAutomaticPlan(evidence);
            if (plan == null || types == null || types.Length == 0 || evidence.manualMenuModelJson?.Length != types.Length) return false;
            var fury = TypeNamed("VF.Model.VRCFury");
            if (fury == null) return false;
            var move = TypeNamed("VF.Model.Feature.MoveMenuItem");
            var reorder = TypeNamed("VF.Model.Feature.ReorderMenuItem");
            return host.Cast<Transform>().Any(child => child.GetComponentInParent<VRCAvatarDescriptor>(true) == avatar &&
                SameRules(child.GetComponents(fury).SelectMany(component => ReadRules(component, move, reorder))
                    .Where(slot => IsRecorded(slot, evidence, plan)), types, evidence.manualMenuModelJson));
        }

        private static bool SameRules(IEnumerable<RuleSlot> slots, string[] types, string[] payloads) {
            var items = slots.ToArray();
            return items.Select(slot => slot.type).SequenceEqual(types) && items.Select(slot => slot.json).SequenceEqual(payloads);
        }

        internal static bool PathsOverlap(string path, string other) {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(other)) return false;
            return path == other || path.StartsWith(other + "/", StringComparison.Ordinal) ||
                other.StartsWith(path + "/", StringComparison.Ordinal);
        }

        private static void RemoveRules(Component component, IEnumerable<RuleSlot> slots) {
            if (UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes(component))
                throw new InvalidOperationException("Unresolved native feature in the same component.");
            var data = new SerializedObject(component);
            var main = data.FindProperty("content");
            var features = data.FindProperty("config.features");
            var remove = slots.ToDictionary(slot => slot.index);
            bool clearMain = remove.ContainsKey(-1);
            long mainId = main.managedReferenceId;
            var retained = new List<long>();
            int count = features != null && features.isArray ? features.arraySize : 0;
            for (int i = 0; i < count; i++)
                if (!remove.ContainsKey(i)) retained.Add(features.GetArrayElementAtIndex(i).managedReferenceId);
            Undo.RecordObject(component, "Update SPS menu rules");
            foreach (var slot in remove.Values.OrderByDescending(slot => slot.index)) {
                var property = slot.index < 0 ? data.FindProperty("content") : features.GetArrayElementAtIndex(slot.index);
                if (property.managedReferenceId != slot.id || property.managedReferenceValue?.GetType().FullName != slot.type ||
                    JsonUtility.ToJson(property.managedReferenceValue) != slot.json) throw new InvalidOperationException("Native menu rule changed.");
                if (slot.index < 0) property.managedReferenceValue = null;
                else features.DeleteArrayElementAtIndex(slot.index);
            }
            data.ApplyModifiedProperties();
            data.Update();
            main = data.FindProperty("content");
            features = data.FindProperty("config.features");
            int remaining = features != null && features.isArray ? features.arraySize : 0;
            if ((clearMain ? main.managedReferenceValue != null : main.managedReferenceId != mainId) ||
                remaining != retained.Count || !Enumerable.Range(0, remaining).All(i =>
                    features.GetArrayElementAtIndex(i).managedReferenceId == retained[i]))
                throw new InvalidOperationException("Native sibling features changed.");
            if (main.managedReferenceValue == null && remaining == 0) Undo.DestroyObjectImmediate(component);
            else PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        internal static void ApplyAutomatic(VRCAvatarDescriptor avatar,Sps2SetupContext root,Sps2SetupAsset old,SetupSettings settings,List<string> warnings,GameObject settingsHost = null) {
            if (avatar == null || root?.asset == null || settings == null) return;
            CopyEvidence(old, root.asset);
            Plan desired;
            try {
                var options = Sps3OwnedGlobalOptions.AllOptions(avatar);
                if (options.Length != 1) { warnings.Add("SPS menu: native Options is not unique; native menu retained."); return; }
                var data = new SerializedObject(options[0]);
                var content = data.FindProperty("content");
                if (content?.managedReferenceValue?.GetType().FullName != "VF.Model.Feature.SpsOptions") { warnings.Add("SPS menu: native Options layout is unknown; native menu retained."); return; }
                var menuPath = content.FindPropertyRelative("menuPath");
                string destination = menuPath?.propertyType == SerializedPropertyType.String ? menuPath.stringValue : null;
                if (string.IsNullOrWhiteSpace(destination)) destination = "SPS";
                var autoProp=content.FindPropertyRelative("autoModeAvailable");
                var legacyProp=content.FindPropertyRelative("legacyModeAvailable");
                var compat=content.FindPropertyRelative("legacySocketCompatibility");
                var dps=content.FindPropertyRelative("addDpsTipLight");
                if (autoProp==null || legacyProp==null || compat==null || dps==null) { warnings.Add("SPS menu: required native menu capabilities unavailable; native menu retained."); return; }
                var menu=avatar.expressionsMenu;
                if (menu != null && SpsMenuDestination.Exists(menu, destination)) { warnings.Add("SPS menu: avatar already has an SPS menu; native menu retained."); return; }
                bool participant=false;
                foreach (var socket in root.sockets) {
                    if (socket?.socket == null) { warnings.Add("SPS menu: socket capabilities unavailable; native menu retained."); return; }
                    var so=new SerializedObject(socket.socket);
                    var add=so.FindProperty("addMenuItem"); var auto=so.FindProperty("enableAuto");
                    if (add==null || auto==null) { warnings.Add("SPS menu: socket menu capabilities unavailable; native menu retained."); return; }
                    participant |= add.boolValue && auto.boolValue;
                }
                bool autoEnabled=autoProp.boolValue && participant;
                bool legacyEnabled=legacyProp.boolValue || (compat.boolValue && dps.boolValue);
                // Only reorder exact, unique native socket names. Refuse ambiguous names,
                // keeping the native result and all other authoring untouched.
                var socketType=TypeNamed("VF.Component.VRCFuryHapticSocket");
                var socketEditor=TypeNamed("VF.Inspector.VRCFuryHapticSocketEditor");
                var menuName=socketEditor?.GetMethod("GetMenuName",System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (socketType==null || menuName==null) { warnings.Add("SPS menu: socket names cannot be verified; native order retained."); return; }
                var allNames=new List<string>();
                foreach(var socket in avatar.GetComponentsInChildren(socketType,true)) {
                    if (socket.GetComponentInParent<VRCAvatarDescriptor>(true) != avatar) continue;
                    var property=new SerializedObject(socket).FindProperty("addMenuItem");
                    if(property==null) { warnings.Add("SPS menu: socket menu capability unavailable; native order retained."); return; }
                    if(property.boolValue) allNames.Add(menuName.Invoke(null,new object[]{socket}) as string);
                }
                if(allNames.Any(n=>!Segment(n)) || allNames.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=allNames.Count) {
                    warnings.Add("SPS menu: socket names are ambiguous; native order retained."); return;
                }
                var ordered=settings.parts.Where(p=>p!=null && p.included)
                    .OrderBy(p=>LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.IsCasualPart(p)?0:
                      (p.IsFingerRing || p.id=="earLeft" || p.id=="earRight" || p.id=="thighs" ? 2:1));
                var paths=new List<string>();
                foreach(var part in ordered) {
                    var generated=root.sockets.FirstOrDefault(x=>x.id==part.id);
                    if(generated?.socket==null) continue;
                    var add=new SerializedObject(generated.socket).FindProperty("addMenuItem");
                    if(add!=null && add.boolValue) paths.Add(destination+"/"+(string)menuName.Invoke(null,new object[]{generated.socket}));
                }
                if(paths.Count!=allNames.Count) { warnings.Add("SPS menu: external socket ownership is uncertain; native order retained."); return; }
                const string autoName="<b>Auto<\\/b>";
                const string legacyName="<b>Legacy Support<\\/b>\n<size=20>DPS\\/TPS\\/SPS1\\/SPS2\nOne socket at a time";
                desired = new Plan {
                    menu = destination,
                    first = autoEnabled ? autoName : (legacyEnabled ? legacyName : ""),
                    second = autoEnabled && legacyEnabled ? legacyName : "",
                    group = autoEnabled && legacyEnabled ? Sps3MenuLayoutBuild.SettingsLabel(settings.menuLanguage) : "",
                    automatic = true,
                    socketPaths = paths.ToArray()
                };
            } catch (Exception e) { warnings.Add("SPS menu: native authoring unavailable; native menu retained: " + e.Message); return; }
            ApplyPlan(avatar,root.asset,desired,warnings,settingsHost);
        }
        private static void ApplyPlan(VRCAvatarDescriptor avatar,Sps2SetupAsset output,Plan desired,List<string> warnings,GameObject settingsHost) {
            string menu = desired.menu, first = desired.first, second = desired.second, group = desired.group;
            string[] socketPaths = desired.socketPaths;
            bool two = !string.IsNullOrEmpty(second), none = string.IsNullOrEmpty(first);
            if (two && (!Segment(group) || string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
                || string.Equals(first, group, StringComparison.OrdinalIgnoreCase) || string.Equals(second, group, StringComparison.OrdinalIgnoreCase))) {
                warnings.Add(Localization.Sps2Localization.L("設定項目を区別できないため、Fury の標準の並び順を使います。"));
                return;
            }
            string plan = JsonUtility.ToJson(desired);
            var fury=TypeNamed("VF.Model.VRCFury"); var move=TypeNamed("VF.Model.Feature.MoveMenuItem"); var reorder=TypeNamed("VF.Model.Feature.ReorderMenuItem");
            if(fury==null || move==null || reorder==null || !typeof(MonoBehaviour).IsAssignableFrom(fury) || EditorUtility.IsPersistent(avatar)) {
                warnings.Add("Fury メニュー: 標準作者設定を確認できず、標準配置を維持しました。"); return;
            }
            var models=new List<object>();
            try {
            if(two) foreach(var name in new[]{first,second}) {
                var m=Activator.CreateInstance(move,true);
                move.GetField("fromPath")?.SetValue(m,menu+"/"+name); move.GetField("toPath")?.SetValue(m,menu+"/"+group+"/"+name);
                var version=move.GetProperty("Version"); var latest=move.GetMethod("GetLatestVersion");
                if(move.GetField("fromPath")==null || move.GetField("toPath")==null || version==null || latest==null) { warnings.Add("Fury メニュー: 移動モデル非対応のため標準配置を維持しました。"); return; }
                version.SetValue(m,latest.Invoke(m,null)); models.Add(m);
            }
            foreach(var socketPath in socketPaths.Reverse()) {
                var item=Activator.CreateInstance(reorder,true);
                var rp=reorder.GetField("path"); var ri=reorder.GetField("position"); var vv=reorder.GetProperty("Version"); var vl=reorder.GetMethod("GetLatestVersion");
                if(rp==null || ri==null || vv==null || vl==null) { warnings.Add("SPS menu: native reorder capability unavailable; native order retained."); return; }
                rp.SetValue(item,socketPath); ri.SetValue(item,0); vv.SetValue(item,vl.Invoke(item,null)); models.Add(item);
            }
            if(!none) {
            var r=Activator.CreateInstance(reorder,true);
            var path=reorder.GetField("path"); var pos=reorder.GetField("position"); var rv=reorder.GetProperty("Version"); var rl=reorder.GetMethod("GetLatestVersion");
            if(path==null||pos==null||rv==null||rl==null) { warnings.Add("Fury メニュー: 並び替えモデル非対応のため標準配置を維持しました。"); return; }
            path.SetValue(r,menu+"/"+(two?group:first)); pos.SetValue(r,0); rv.SetValue(r,rl.Invoke(r,null)); models.Add(r);
            }
            } catch(Exception e) {
                warnings.Add("Fury メニュー: 作者設定の形式を確認できず、標準配置を維持しました: "+e.Message); return;
            }
            var types = models.Select(m => m.GetType().FullName).ToArray();
            var payloads = models.Select(JsonUtility.ToJson).ToArray();
            var paths = new HashSet<string>(socketPaths);
            if (!none) paths.Add(menu + "/" + (two ? group : first));
            if (two) foreach (var name in new[] { first, second }) {
                paths.Add(menu + "/" + name);
                paths.Add(menu + "/" + group + "/" + name);
            }
            List<RuleSlot> affected;
            GameObject[] hosts;
            GameObject child;
            Transform parent = settingsHost != null ? settingsHost.transform : avatar.transform;
            try {
                var all = avatar.GetComponentsInChildren(fury, true)
                    .Where(component => component.GetComponentInParent<VRCAvatarDescriptor>(true) == avatar)
                    .SelectMany(component => ReadRules(component, move, reorder)).ToList();
                var prior = ReadAutomaticPlan(output);
                affected = all.Where(slot => IsRecorded(slot, output, prior) || Enumerable.Range(0, types.Length)
                    .Any(i => slot.type == types[i] && slot.json == payloads[i])).ToList();
                foreach (var item in affected) {
                    paths.Add(item.source);
                    if (item.destination != null) paths.Add(item.destination);
                }
                if (all.Except(affected).Any(slot => paths.Any(path =>
                        PathsOverlap(path, slot.source) || PathsOverlap(path, slot.destination)))) {
                    warnings.Add(Localization.Sps2Localization.L("既存のメニュー設定を自動配置の対象として確認できないため、そのまま保持しました。"));
                    return;
                }

                // Only reuse a direct native rule host in its current location. Never reparent
                // a manual object or a mixed container, even if it contains an applicable rule.
                hosts = affected.Select(slot => slot.component.gameObject).Distinct().ToArray();
                child = hosts.FirstOrDefault(host => host.transform.parent == parent &&
                    host.transform.childCount == 0 && host.GetComponents<Component>().All(component =>
                        component is Transform || (component != null && component.GetType() == fury &&
                            affected.Any(slot => slot.component == component && slot.index == -1) &&
                            (new SerializedObject(component).FindProperty("config.features")?.arraySize ?? 0) == 0)));
            } catch (Exception) {
                warnings.Add(Localization.Sps2Localization.L("既存のメニュー設定を自動配置の対象として確認できないため、そのまま保持しました。"));
                return;
            }
            bool created = false;
            Undo.IncrementCurrentGroup(); int groupId=Undo.GetCurrentGroup();
            try {
                if (hosts.Length == 1 && (settingsHost == null || hosts[0].transform.parent == parent) &&
                    SameRules(affected, types, payloads)) {
                    Remember(output, plan, types, payloads);
                    Undo.CollapseUndoOperations(groupId);
                    return;
                }
                if (child == null && models.Count > 0) {
                    child = Sps3OwnedGlobalOptions.CreateNativeHost(parent, HostName);
                    created = true;
                }
                var added = new List<Component>();
                foreach(var model in models) {
                    var component=Undo.AddComponent(child,fury);
                    added.Add(component);
                    if(MonoScript.FromMonoBehaviour((MonoBehaviour)component)?.GetClass()!=fury) throw new InvalidOperationException("Native script cannot be saved.");
                    var data=new SerializedObject(component); var prop=data.FindProperty("content");
                    if(prop==null||prop.propertyType!=SerializedPropertyType.ManagedReference) throw new InvalidOperationException("Native managed reference unavailable.");
                    prop.managedReferenceValue=model; data.ApplyModifiedProperties(); data.Update();
                    var read=data.FindProperty("content")?.managedReferenceValue;
                    if(read==null || read.GetType()!=model.GetType() || JsonUtility.ToJson(read)!=JsonUtility.ToJson(model)) throw new InvalidOperationException("Native model readback failed.");
                }
                if (!SameRules(added.SelectMany(component => ReadRules(component, move, reorder)), types, payloads))
                    throw new InvalidOperationException("Native authoring verification failed.");
                foreach (var portion in affected.GroupBy(slot => slot.component)) RemoveRules(portion.Key, portion);
                Remember(output, plan, types, payloads);
                Undo.CollapseUndoOperations(groupId);
            } catch(Exception e) {
                Undo.RevertAllDownToGroup(groupId);
                if (created && child != null) throw new InvalidOperationException("Fury menu rollback could not be verified.", e);
                foreach (var portion in affected.GroupBy(slot => slot.component)) {
                    if (portion.Key == null || !portion.All(before => ReadRules(portion.Key, move, reorder).Any(after =>
                        after.index == before.index && after.id == before.id && after.json == before.json && after.type == before.type)))
                        throw new InvalidOperationException("Fury menu rollback could not be verified.", e);
                }
                warnings.Add("SPS menu: update failed; previous authoring retained: "+e.Message);
            }
        }

        private static void Remember(Sps2SetupAsset output, string plan, string[] types, string[] payloads) {
            if (output.manualMenuPlan == plan && output.manualMenuModelTypes != null && output.manualMenuModelJson != null &&
                output.manualMenuModelTypes.SequenceEqual(types) && output.manualMenuModelJson.SequenceEqual(payloads)) return;
            Undo.RecordObject(output, "Record SPS menu authoring");
            output.manualMenuPlan = plan;
            output.manualMenuModelTypes = types;
            output.manualMenuModelJson = payloads;
            EditorUtility.SetDirty(output);
        }
        [Serializable] private sealed class Plan {
            public string menu,first,second,group;
            public bool automatic;
            public string[] socketPaths;

            internal bool Includes(RuleSlot slot) {
                if (slot.destination != null) {
                    if (string.IsNullOrEmpty(second) || !Segment(group)) return false;
                    return new[] {first, second}.Any(name => slot.source == menu + "/" + name &&
                        slot.destination == menu + "/" + group + "/" + name);
                }
                string setting = string.IsNullOrEmpty(second) ? first : group;
                return (!string.IsNullOrEmpty(setting) && slot.source == menu + "/" + setting) ||
                    (socketPaths != null && socketPaths.Contains(slot.source));
            }
        }
    }
}
