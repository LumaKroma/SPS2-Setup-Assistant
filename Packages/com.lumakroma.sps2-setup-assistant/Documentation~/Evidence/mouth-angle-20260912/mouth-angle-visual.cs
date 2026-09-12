var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true)).Single(a=>a.name=="MANUKA_lilToon");
var avatar=UnityEngine.Object.Instantiate(original.gameObject);avatar.name="Issue121 placement correction probe";
try {
var descriptor=avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
var settings=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root;string message;
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message))return "failed: "+message;
if(root.testPlug!=null)root.testPlug.SetActive(false);if(root.longTestPlug!=null)root.longTestPlug.SetActive(false);
foreach(var t in avatar.GetComponentsInChildren<UnityEngine.Transform>(true))t.gameObject.layer=31;
var mat=new UnityEngine.Material(UnityEngine.Shader.Find("Unlit/Color")); mat.color=UnityEngine.Color.cyan;
foreach(var markerSocket in root.sockets.Where(s=>s.id=="mouth")){var dot=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);dot.transform.SetParent(avatar.transform);dot.transform.position=markerSocket.pose.position;dot.transform.localScale=UnityEngine.Vector3.one*.009f;dot.layer=31;dot.GetComponent<UnityEngine.Renderer>().sharedMaterial=mat;var axis=new UnityEngine.GameObject("preview axis");axis.transform.SetParent(avatar.transform);axis.layer=31;var line=axis.AddComponent<UnityEngine.LineRenderer>();line.sharedMaterial=mat;line.positionCount=2;line.startWidth=line.endWidth=.002f;line.SetPosition(0,markerSocket.pose.position);line.SetPosition(1,markerSocket.pose.position+markerSocket.pose.forward*.06f);}
var camObj=new UnityEngine.GameObject("preview camera");camObj.transform.SetParent(avatar.transform);var cam=camObj.AddComponent<UnityEngine.Camera>();cam.cullingMask=1<<31;cam.orthographic=true;cam.orthographicSize=.18f;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;cam.backgroundColor=new UnityEngine.Color(.17f,.18f,.2f);cam.nearClipPlane=.01f;cam.farClipPlane=10;
var rt=new UnityEngine.RenderTexture(1024,1280,24);cam.targetTexture=rt;var tex=new UnityEngine.Texture2D(1024,1280,UnityEngine.TextureFormat.RGB24,false);var prior=UnityEngine.RenderTexture.active;
try{foreach(var side in new[]{true}){camObj.transform.position=avatar.transform.position+new UnityEngine.Vector3(2,1.065f,.015f);camObj.transform.LookAt(avatar.transform.position+new UnityEngine.Vector3(0,1.065f,.015f));cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1024,1280),0,0);tex.Apply();var path=System.IO.Path.GetFullPath(UnityEngine.Application.dataPath+"/../../../../issue121-definition/mouth-angle-"+(side?"side":"front")+".png");System.IO.File.WriteAllBytes(path,tex.EncodeToPNG());}}
finally{UnityEngine.RenderTexture.active=prior;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(mat);}
return "Rendered front and side placement previews";
}finally{UnityEngine.Object.DestroyImmediate(avatar);}


