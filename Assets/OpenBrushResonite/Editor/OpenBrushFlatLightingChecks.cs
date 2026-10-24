using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushFlatLightingChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Flat lighting check: "+why);}
 public static void Run(){try {
  var source=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2},colors=new[]{new Color(.5f,.25f,.75f,.6f),Color.white,Color.black}};
  var mesh=OpenBrushTriangleImport.Build(source,Matrix4x4.identity,false);
  var light=new OpenBrushTriangleImport.Lighting{schemaVersion=1,mode="source-gamma-main-and-ambient",ambient=new[]{.4f,.2f,.6f},mainColor=new[]{.8f,.6f,.4f},mainRotation=new[]{0f,0f,0f}};
  OpenBrushTriangleImport.BakeLighting(mesh,Matrix4x4.identity,source.colors,light);
  Check(Vector3.Distance((Vector4)mesh.colors[0],new Vector3(.1f,.025f,.225f))<1e-6 && Math.Abs(mesh.colors[0].a-.6f)<1e-6,"back-to-light face has source half ambient; gamma paint and alpha retained");
  OpenBrushTriangleImport.BakeLighting(mesh,Matrix4x4.Rotate(Quaternion.Euler(0,180,0)),source.colors,light);
  Check(Vector3.Distance((Vector4)mesh.colors[0],new Vector3(.5f,.175f,.525f))<1e-6,"original-canvas normal changes main-light term without native fill");
  Check(source.colors[0]==new Color(.5f,.25f,.75f,.6f),"source paint never overwritten");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-solid-extra-v3/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
  var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   root.transform.SetPositionAndRotation(new Vector3(310,0,2),Quaternion.Euler(0,180,0));
   OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushGrowthImport.Prepare(root,timing,null);OpenBrushTriangleImport.Prepare(root,manifest.flatLighting);
   var printed=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.sharedMaterial.GetTag("OpenBrushSourceFlatLighting",false,"")=="1").ToArray();
   Check(printed.Length==35 && printed.Select(r=>r.sharedMaterial).Distinct().Count()==1,"five printing strokes keep35growthparts and share one material");
   Check(printed.All(r=>r.transform.parent.GetComponent<OpenBrushGrowthPart>()!=null),"source lighting follows drawing reveal");
   var mat=printed[0].sharedMaterial;var native=new FrooxEngine.UnlitMaterial();OpenBrushMaterialConverter.ConfigureUnlit(mat,OpenBrushProfiles.Find(OpenBrushTriangleImport.Printing),native);
   Check(native.UseVertexColors && !native.UseBillboardGeometry && native.BlendMode==FrooxEngine.BlendMode.Opaque && native.Sidedness==FrooxEngine.Sidedness.Front,"baked light is native opaque vertex color, no second world lighting");
   var before=printed.Select(r=>r.GetComponent<MeshFilter>().sharedMesh.colors).ToArray();root.transform.rotation=Quaternion.Euler(20,73,15);OpenBrushTriangleImport.Prepare(root,manifest.flatLighting);
   Check(printed.Select((r,i)=>r.GetComponent<MeshFilter>().sharedMesh.colors.SequenceEqual(before[i])).All(x=>x),"relocation and repeat preparation preserve source lighting");
  }
  Debug.Log("OPENBRUSH_FLAT_LIGHTING_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
