using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushLitStandardChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("LitStandard: "+message);}
 public static void Run(){try {
  const string directory="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-lit-standard-v1";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(Path.Combine(directory,"materials.json")));
  using(var data=new AutoGltfFileParser(Path.Combine(directory,"drawing.glb")).Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();
   Check(OpenBrushSourceTangents.Restore(importer)==105,"source tangent frames restored on three textured brushes / 105 parts");
   var root=instance.gameObject;var filters=root.GetComponentsInChildren<MeshFilter>(true);
   var before=filters.Select(f=>f.sharedMesh).ToArray();
   var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
   var textures=renderers.Select(r=>r.sharedMaterial.mainTexture).ToArray();
   Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==140,"20 strokes / 140 drawing parts imported");
   for(int i=0;i<filters.Length;i++) {
    var mesh=filters[i].sharedMesh;
    Check(before[i].vertices.SequenceEqual(mesh.vertices) && before[i].normals.SequenceEqual(mesh.normals) && before[i].tangents.SequenceEqual(mesh.tangents) && before[i].uv.SequenceEqual(mesh.uv) && before[i].triangles.SequenceEqual(mesh.triangles),"geometry and source tangent frames retained");
    Check(before[i].colors.Zip(mesh.colors,(a,b)=>a.linear==new Color(b.r,b.g,b.b,a.a) && a.a==b.a).All(x=>x),"source RGB decoded exactly once, alpha unchanged");
    Check(renderers[i].sharedMaterial.mainTexture==textures[i],"source albedo retained");
   }
   var materials=renderers.Select(r=>r.sharedMaterial).Distinct().ToArray();Check(materials.Length==4,"four material families");
   foreach(var material in materials) {
    var profile=OpenBrushProfiles.Find(material.GetTag("OpenBrushGuid",false,""));
    var native=new FrooxEngine.PBS_VertexColorSpecular();OpenBrushMaterialConverter.ConfigureSpecular(material,profile,native);
    bool guts=profile.Name=="Guts",muscle=profile.Name=="Muscle",gouache=profile.Name=="Gouache";
    var expected=guts?new Color(.23921569f,.25882354f,.26666668f,1):muscle?new Color(.08235294f,.08235294f,.08235294f,1):gouache?Color.black:new Color(.29411766f,.29411766f,.29411766f,1);
    float specularGain=guts?1.5f:muscle||gouache?1:2;
    expected*=specularGain;
    var actual=native.SpecularColor.color;
    Check(Mathf.Abs(actual.r-expected.r)<1e-6 && Mathf.Abs(actual.g-expected.g)<1e-6 && Mathf.Abs(actual.b-expected.b)<1e-6,profile.Name+" source RGB specular with measured native lighting calibration");
    Check(Mathf.Abs(native.AlbedoColor.color.r-(guts?1.3f:1.5f))<1e-6 && native.AlbedoColor.color.a==1,profile.Name+" measured diffuse gain with unchanged alpha");
    Check(Mathf.Abs(actual.a-(guts?.743f:muscle?.57f:gouache?.01f:.574f))<1e-6,profile.Name+" connected shininess retained");
    Check(native.Culling==FrooxEngine.Culling.Off && native.AlphaHandling==FrooxEngine.AlphaHandling.AlphaClip && native.AlphaClip==(guts||muscle?.3f:.5f),profile.Name+" normal graph two-sided alpha clip");
    Check(Mathf.Abs(native.TextureScale.x-(guts?.15f:muscle?.25f:1))<1e-6 && native.TextureScale.y==1,profile.Name+" graph UV tiling restored");
    Check((material.GetTexture("_BumpMap")!=null)==(guts||muscle||gouache),profile.Name+" explicit normal map or flat source default retained");
   }
   var prepared=filters.Select(f=>f.sharedMesh).ToArray();OpenBrushMaterialImport.Prepare(root,manifest,false);
   Check(prepared.SequenceEqual(filters.Select(f=>f.sharedMesh)),"preparation is idempotent for vertex color");
   UnityEngine.Object.DestroyImmediate(root);
  }
  Debug.Log("OPENBRUSH_LIT_STANDARD_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
