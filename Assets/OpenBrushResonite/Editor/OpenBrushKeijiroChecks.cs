using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushKeijiroChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Keijiro check: "+why);}
 static void Oracle() {
  var shader=Shader.Find("Hidden/OpenBrush/KeijiroReference");Check(shader!=null&&shader.isSupported,"source oracle available");
  var material=new Material(shader);var rt=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
  var read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);var old=RenderTexture.active;bool srgb=GL.sRGBWrite;float error=0;
  try {for(int i=0;i<160;i++) {
   var pos=new Vector3(.3f,1.7f,-.2f);var normal=new Vector3(.3f,.5f,.8f).normalized;
   // Include the narrow smoothstep transition as well as many cycles.
   float time=i*.037f;float phase=i<80 ? Mathf.Asin(.44f+i*.001f) : i*.33f;
   var uv=new Vector3(time*2+phase,.7f,.003f+i*.0005f);
   material.SetVector("_Position",pos);material.SetVector("_Normal",normal);material.SetVector("_UVRadius",uv);material.SetFloat("_Seconds",time);
   GL.sRGBWrite=false;Graphics.Blit(Texture2D.whiteTexture,rt,material);RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,1,1),0,0,false);read.Apply();
   var value=read.GetPixel(0,0);error=Mathf.Max(error,Vector3.Distance(new Vector3(value.r,value.g,value.b),OpenBrushKeijiroImport.Deform(pos,normal,uv,time)));
  }}finally{RenderTexture.active=old;GL.sRGBWrite=srgb;rt.Release();foreach(var o in new UnityEngine.Object[]{rt,read,material})UnityEngine.Object.DestroyImmediate(o);}
  Check(error<.00001f,"CPU/GPU pulse max error="+error);
 }
 public static void Run(){try {
  Oracle();const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-keijiro-v2/";
  var raw=JsonUtility.FromJson<OpenBrushKeijiroImport.Data>(File.ReadAllText(dir+"keijiro.json"));
  var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json")),false);
   OpenBrushGrowthImport.Prepare(root,timing,null);OpenBrushKeijiroImport.Prepare(root,raw,timing);
   var motions=root.GetComponentsInChildren<OpenBrushGeometryMotion>();Check(motions.Length==1,"one shared sequence for five color strokes");var motion=motions[0];
   Check(motion.Frames.Length==Mathf.CeilToInt(timing.duration*60)+1,"60fps covers full timeline");
   Check(motion.Frames[0].triangles.All(i=>i==0),"blank lead in contains no visible triangles");
   Check(motion.Frames.Last().triangles.SequenceEqual(raw.parts[0].triangles),"completed drawing preserves original indices/winding");
   Check(motion.Frames.Last().tangents.SequenceEqual(raw.parts[0].tangents),"original tangent frames retained during contraction");
   Check(motion.SourceMaterial.GetTexture("_BumpMap")!=null,"implicit white source normal restored");
   var colors=raw.parts[0].colors.Select(c=>c.linear).Distinct().ToArray();Check(colors.Length==5,"all five source colors decoded");
   Check(motion.Frames.Last().colors.Distinct().Count()==5,"all five colors survive motion bake");
   Check(motion.Frames[330].vertices.Where((p,i)=>(p-motion.Frames[390].vertices[i]).sqrMagnitude>1e-10f).Any(),"pulse continues after drawing completes");
   Check(motion.Frames[90].triangles.Length>3 && motion.Frames[90].triangles.Length<raw.parts[0].triangles.Length,"partial stroke revealed without future strokes");
   Check(root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<MeshFilter>()!=null&&r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushKeijiroImport.Guid).All(r=>!r.enabled),"original static renderers disabled");
   var profile=OpenBrushProfiles.Find(OpenBrushKeijiroImport.Guid);Check(profile.Specular==0 && profile.SpecularModel && profile.LinearVertexColors,"normal graph uses black specular and decoded paint");
   Check(motion.Frames.All(m=>m.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z))),"all deformed frames finite");
  }
  Debug.Log("OPENBRUSH_KEIJIRO_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
