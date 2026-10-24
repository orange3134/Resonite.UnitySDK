using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushExperimentalPaintChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Experimental paint: "+message);}
 public static void Run(){try {
  string directory="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-experimental-paint-v2";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(Path.Combine(directory,"materials.json")));
  using(var data=new AutoGltfFileParser(Path.Combine(directory,"drawing.glb")).Parse())
  using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();Check(OpenBrushSourceTangents.Restore(importer)==105,"source tangent frames restored on three ribbon families");instance.ShowMeshes();var root=instance.gameObject;
   var filters=root.GetComponentsInChildren<MeshFilter>();
   var before=filters.Select(f=>f.sharedMesh).ToArray();
   var renderers=root.GetComponentsInChildren<MeshRenderer>();
   var textures=renderers.Select(r=>r.sharedMaterial.mainTexture).ToArray();
   Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==140,"all twenty strokes and 140 drawing sections imported");
   for(int i=0;i<filters.Length;i++) {
    var mesh=filters[i].sharedMesh;
    Check(before[i].vertices.SequenceEqual(mesh.vertices) && before[i].triangles.SequenceEqual(mesh.triangles) && before[i].normals.SequenceEqual(mesh.normals) && before[i].tangents.SequenceEqual(mesh.tangents),"source geometry/winding/normals preserved");
    Check(before[i].colors.Zip(mesh.colors,(a,b)=>a.linear==new Color(b.r,b.g,b.b,a.a) && a.a==b.a).All(x=>x),"source RGB decoded once including distinct backface colors, alpha preserved");
    Check(renderers[i].sharedMaterial.mainTexture==textures[i],"original exported texture retained");
   }
   foreach(var material in renderers.Select(r=>r.sharedMaterial).Distinct()) {
    var profile=OpenBrushProfiles.Find(material.GetTag("OpenBrushGuid",false,""));
    var native=new FrooxEngine.PBS_VertexColorSpecular();OpenBrushMaterialConverter.ConfigureSpecular(material,profile,native);
    float cutoff=profile.Name=="DryBrush"?.2f:profile.Name=="SquarePaper"?.16f:.5f;
    float smooth=profile.Name=="DryBrush"?.05f:profile.Name=="SquarePaper"?.145f:profile.Name=="Charcoal"?.01f:0;
    Check(native.Culling==FrooxEngine.Culling.Back && native.AlphaHandling==FrooxEngine.AlphaHandling.AlphaClip && native.AlphaClip==cutoff,profile.Name+" uses normal graph front face and cutout");
    var spec=native.SpecularColor.color;
    Check(spec.r==0 && spec.g==0 && spec.b==0 && Mathf.Abs(spec.a-smooth)<1e-6,profile.Name+" normal graph black specular and shininess");
    Check(material.GetTexture("_BumpMap")!=null,profile.Name+" source normal input retained including the implicit DoubleFlat default");
   }
   foreach(var renderer in renderers.Where(r=>OpenBrushProfiles.Find(r.sharedMaterial.GetTag("OpenBrushGuid",false,"")).Name=="DoubleFlat")) {
    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
    Check(mesh.tangents.Length==mesh.vertexCount,"DoubleFlat source tangents retained for default normal input");
    Check(MeshConverter.ConvertMesh(mesh).HasTangents,"DoubleFlat tangent stream included in native upload");
   }
   var prepared=filters.Select(f=>f.sharedMesh).ToArray();OpenBrushMaterialImport.Prepare(root,manifest,false);
   Check(prepared.SequenceEqual(filters.Select(f=>f.sharedMesh)),"repeated preparation does not double-decode paint colors");
   UnityEngine.Object.DestroyImmediate(root);
  }
  Debug.Log("OPENBRUSH_EXPERIMENTAL_PAINT_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}


