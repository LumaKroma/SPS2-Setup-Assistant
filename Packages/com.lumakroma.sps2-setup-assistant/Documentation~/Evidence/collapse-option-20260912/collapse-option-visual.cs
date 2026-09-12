var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true)).Single(a=>a.name=="MANUKA_lilToon");
var avatar=UnityEngine.Object.Instantiate(original.gameObject);avatar.name="Issue121 placement correction probe";
try {
var descriptor=avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
var settings=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root;string message;
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message))return "failed: "+message;
if(root.testPlug!=null)root.testPlug.SetActive(false);if(root.longTestPlug!=null)root.longTestPlug.SetActive(false);
var socket=root.sockets.Single(s=>s.id=="mouth");var data=new UnityEditor.SerializedObject(socket.socket);var points=new System.Collections.Generic.List<UnityEngine.Vector3>();
var start=socket.pose;
for(int segment=0;segment<2;segment++){
 var end=socket.pathStops[segment];var stop=data.FindProperty("guidedPathStops").GetArrayElementAtIndex(segment);float d=UnityEngine.Vector3.Distance(start.position,end.position)*.5f;
 var p0=start.position;var p3=end.position;var p1=p0+start.rotation*(stop.FindPropertyRelative("customizeTangentOut").boolValue?stop.FindPropertyRelative("tangentOut").vector3Value:UnityEngine.Vector3.back*d);var p2=p3+end.rotation*(stop.FindPropertyRelative("customizeTangentIn").boolValue?stop.FindPropertyRelative("tangentIn").vector3Value:UnityEngine.Vector3.forward*d);
 for(int i=0;i<=40;i++){float t=i/40f,u=1-t;points.Add(u*u*u*p0+3*u*u*t*p1+3*u*t*t*p2+t*t*t*p3);}start=end;
}
var verts=new System.Collections.Generic.List<UnityEngine.Vector3>();var tris=new System.Collections.Generic.List<int>();const int sides=24;
for(int i=0;i<points.Count;i++){var tangent=(points[Math.Min(i+1,points.Count-1)]-points[Math.Max(i-1,0)]).normalized;var x=UnityEngine.Vector3.right;var y=UnityEngine.Vector3.Cross(tangent,x).normalized;for(int s=0;s<sides;s++){float angle=2*UnityEngine.Mathf.PI*s/sides;verts.Add(avatar.transform.InverseTransformPoint(points[i]+.025f*(x*UnityEngine.Mathf.Cos(angle)+y*UnityEngine.Mathf.Sin(angle))));if(i<points.Count-1){int a=i*sides+s,b=i*sides+(s+1)%sides,c=a+sides,d=b+sides;tris.AddRange(new[]{a,b,c,b,d,c});}}}
var tube=new UnityEngine.GameObject("Native cubic geometry preview (not runtime)");tube.transform.SetParent(avatar.transform,false);var tubeMesh=new UnityEngine.Mesh();tubeMesh.SetVertices(verts);tubeMesh.SetTriangles(tris,0);tubeMesh.RecalculateNormals();tube.AddComponent<UnityEngine.MeshFilter>().sharedMesh=tubeMesh;var tubeMat=new UnityEngine.Material(UnityEngine.Shader.Find("Standard"));tubeMat.color=UnityEngine.Color.cyan;tube.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial=tubeMat;
foreach(var t in avatar.GetComponentsInChildren<UnityEngine.Transform>(true))t.gameObject.layer=31;
var mat=new UnityEngine.Material(UnityEngine.Shader.Find("Unlit/Color")); mat.color=UnityEngine.Color.cyan;
foreach(var markerSocket in root.sockets){var dot=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);dot.transform.SetParent(avatar.transform);dot.transform.position=markerSocket.pose.position;dot.transform.localScale=UnityEngine.Vector3.one*.009f;dot.layer=31;dot.GetComponent<UnityEngine.Renderer>().sharedMaterial=mat;var axis=new UnityEngine.GameObject("preview axis");axis.transform.SetParent(avatar.transform);axis.layer=31;var line=axis.AddComponent<UnityEngine.LineRenderer>();line.sharedMaterial=mat;line.positionCount=2;line.startWidth=line.endWidth=.002f;line.SetPosition(0,markerSocket.pose.position);line.SetPosition(1,markerSocket.pose.position+markerSocket.pose.forward*.06f);}
var camObj=new UnityEngine.GameObject("preview camera");camObj.transform.SetParent(avatar.transform);var cam=camObj.AddComponent<UnityEngine.Camera>();cam.cullingMask=1<<31;cam.orthographic=true;cam.orthographicSize=.18f;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;cam.backgroundColor=new UnityEngine.Color(.17f,.18f,.2f);cam.nearClipPlane=.01f;cam.farClipPlane=10;
var rt=new UnityEngine.RenderTexture(1024,1280,24);cam.targetTexture=rt;var tex=new UnityEngine.Texture2D(1024,1280,UnityEngine.TextureFormat.RGB24,false);var prior=UnityEngine.RenderTexture.active;
try{foreach(var side in new[]{true}){camObj.transform.position=avatar.transform.position+new UnityEngine.Vector3(2,1.065f,.015f);camObj.transform.LookAt(avatar.transform.position+new UnityEngine.Vector3(0,1.065f,.015f));cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1024,1280),0,0);tex.Apply();var path=System.IO.Path.GetFullPath(UnityEngine.Application.dataPath+"/../../../../issue121-definition/collapse-option-"+(side?"side":"front")+".png");System.IO.File.WriteAllBytes(path,tex.EncodeToPNG());}}
finally{UnityEngine.RenderTexture.active=prior;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(mat);UnityEngine.Object.DestroyImmediate(tubeMat);UnityEngine.Object.DestroyImmediate(tubeMesh);}
return "Rendered front and side placement previews";
}finally{UnityEngine.Object.DestroyImmediate(avatar);}

