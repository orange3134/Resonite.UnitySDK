using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushExperimentalPaint2Checks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Paint2: "+message);}
 public static void Run(){try {
  const string directory="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-experimental-paint2-v1";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(Path.Combine(directory,"materials.json")));
  using(var data=new AutoGltfFileParser(Path.Combine(directory,"drawing.glb")).Parse())
  using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();Check(OpenBrushSourceTangents.Restore(importer)==245,"source tangent frames retained on all 245 sections");
   var root=instance.gameObject;var filters=root.GetComponentsInChildren<MeshFilter>(true);var before=filters.Select(f=>f.sharedMesh).ToArray();
   var renderers=root.GetComponentsInChildren<MeshRenderer>(true);var textures=renderers.Select(r=>r.sharedMaterial.mainTexture).ToArray();
   Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==245,"35 strokes and 245 drawing sections imported");
   for(int i=0;i<filters.Length;i++) {
    var mesh=filters[i].sharedMesh;
    Check(before[i].vertices.SequenceEqual(mesh.vertices) && before[i].normals.SequenceEqual(mesh.normals) && before[i].tangents.SequenceEqual(mesh.tangents) && before[i].uv.SequenceEqual(mesh.uv) && before[i].triangles.SequenceEqual(mesh.triangles),"geometry, normal/tangent frames and texture coordinates retained");
    Check(before[i].colors.Zip(mesh.colors,(a,b)=>a.linear==new Color(b.r,b.g,b.b,a.a) && a.a==b.a).All(x=>x),"source RGB decoded exactly once, alpha preserved");
    Check(renderers[i].sharedMaterial.mainTexture==textures[i],"exported albedo input retained");
   }
   var materials=renderers.Select(r=>r.sharedMaterial).Distinct().ToArray();Check(materials.Length==7,"seven brush profiles");
   foreach(var material in materials) {
    var profile=OpenBrushProfiles.Find(material.GetTag("OpenBrushGuid",false,""));
    var native=new FrooxEngine.PBS_VertexColorSpecular();OpenBrushMaterialConverter.ConfigureSpecular(material,profile,native);
    bool tape=profile.Name=="DuctTapeGeometry" || profile.Name=="ThickGeometry";
    float cutoff=tape?.2f:.5f;
    float smooth=tape?.414f:profile.Name=="InkGeometry"?.4f:profile.Name=="Leaves2"?.395f:profile.Name=="Wire (Lit)"?.078125f:profile.Name=="TaperedWire"?.1f:0;
    float spec=tape?.5372549f:profile.Name=="Wire (Lit)"?.5019608f:profile.Name=="TaperedWire"?.09411765f:0;
    Check(native.Culling==FrooxEngine.Culling.Back && native.AlphaHandling==FrooxEngine.AlphaHandling.AlphaClip && native.AlphaClip==cutoff,profile.Name+" original one-sided cutout");
    Check(Mathf.Abs(native.SpecularColor.color.r-.5f*Mathf.GammaToLinearSpace(spec))<1e-6f && Mathf.Abs(native.SpecularColor.color.a-smooth)<1e-6f,profile.Name+" source specular half multiplier and shininess");
    Check(Mathf.Abs(material.GetColor("_TintColor").r-(tape?.7058824f:1))<1e-6f,profile.Name+" source base tint");
    Check(material.GetTexture("_BumpMap")!=null,profile.Name+" explicit or source-default normal input");
   }
   var prepared=filters.Select(f=>f.sharedMesh).ToArray();OpenBrushMaterialImport.Prepare(root,manifest,false);
   Check(prepared.SequenceEqual(filters.Select(f=>f.sharedMesh)),"repeated preparation does not double-decode vertex color");
   UnityEngine.Object.DestroyImmediate(root);
  }
  Debug.Log("OPENBRUSH_EXPERIMENTAL_PAINT2_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
