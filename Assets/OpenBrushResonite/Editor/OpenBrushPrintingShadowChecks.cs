using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushPrintingShadowChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Printing shadow check: "+why);}
 static void CheckFutureCaster() {
  var root=new GameObject("Shadow causality fixture");var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=4;
  var material=new Material(Shader.Find("Unlit/Color"));material.SetOverrideTag("OpenBrushSourceFlatLighting","1");
  for(int i=0;i<2;i++) {
   var parent=new GameObject(i==0?"Receiver":"Future caster");parent.transform.SetParent(root.transform,false);parent.transform.localPosition=new Vector3(0,0,-i*.01f);
   var growth=parent.AddComponent<OpenBrushGrowthPart>();growth.Timeline=timeline;growth.Start=i*2;growth.End=i*2+1;
   var mesh=new Mesh{vertices=new[]{new Vector3(-.002f,-.002f,0),new Vector3(0,.002f,0),new Vector3(.002f,-.002f,0)},triangles=new[]{0,1,2},colors=new[]{Color.white,Color.white,Color.white}};
   parent.AddComponent<MeshFilter>().sharedMesh=mesh;parent.AddComponent<OpenBrushVertexColorEncoding>().SourceMesh=mesh;
   var surface=new GameObject("Surface");surface.transform.SetParent(parent.transform,false);surface.AddComponent<MeshFilter>().sharedMesh=mesh;surface.AddComponent<MeshRenderer>().sharedMaterial=material;
  }
  var transform=root.transform.Find("Receiver");var before=OpenBrushPrintingShadowImport.Canvas(transform,root.transform);root.transform.SetPositionAndRotation(new Vector3(10000,20000,30000),Quaternion.Euler(23,47,11));
  Check(OpenBrushPrintingShadowImport.Canvas(transform,root.transform)==before,"shadow coordinates are invariant to remote world placement");
  OpenBrushPrintingShadowImport.Prepare(root,new OpenBrushTriangleImport.Lighting{schemaVersion=1,mode="source-gamma-main-and-ambient",ambient=new[]{.2f,.2f,.2f},mainColor=new[]{1f,1f,1f},mainRotation=new[]{0f,180f,0f}});
  var motion=root.transform.Find("Receiver/Surface").GetComponent<OpenBrushGeometryMotion>();
  Check(motion!=null && motion.FrameTimes.Skip(1).All(t=>t>2),"later stroke cannot cast an early shadow on an already drawn stroke");
  Check(motion.Frames[0].colors.All(c=>Mathf.Abs(c.r-1.1f)<.0001f) && motion.Frames.Last().colors.Any(c=>Mathf.Abs(c.r-.1f)<.0001f),"later caster changes only the direct light, leaving ambient intact");
  Check(motion.Frames[0].vertexCount>3,"small illuminated face was actually subdivided");
  UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(material);
 }
 public static void Run(){try {
  Check(OpenBrushTriangleImport.UnitNormal(Vector3.right*.0001f,Vector3.up*.0001f)==Vector3.forward,"sub-millimeter valid face retains unit normal");
  Check(OpenBrushTriangleImport.UnitNormal(Vector3.right,Vector3.right)==Vector3.zero,"zero-area face remains degenerate");
  var face=OpenBrushPrintingShadowImport.Triangle(new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(0,1,1));
  var rays=new OpenBrushPrintingShadowImport.Rays(new[]{face});
  Check(rays.Blocked(Vector3.zero,Vector3.forward),"light-facing caster blocks a ray toward light");
  Check(!rays.Blocked(new Vector3(2,0,0),Vector3.forward),"ray outside triangle remains lit");
  Check(!rays.Blocked(new Vector3(0,0,2),Vector3.back),"back-face caster is culled like source shadow pass");
  Check(!rays.Blocked(new Vector3(0,0,1.00002f),Vector3.forward),"surface bias does not shadow itself");
  Check(OpenBrushPrintingShadowImport.Growth(1,2,3)==0 && OpenBrushPrintingShadowImport.Growth(2.5f,2,3)==.5f && OpenBrushPrintingShadowImport.Growth(4,2,3)==1,"occluder scale follows exact growth interval");
  CheckFutureCaster();
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-printing-lighting02/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
  var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   root.transform.SetPositionAndRotation(new Vector3(318,0,2),Quaternion.Euler(0,180,0));
   OpenBrushMaterialImport.Prepare(root,manifest,false);var timeline=OpenBrushGrowthImport.Prepare(root,timing,null);OpenBrushTriangleImport.Prepare(root,manifest.flatLighting);
   OpenBrushPrintingShadowImport.Prepare(root,manifest.flatLighting);
   var motions=root.GetComponentsInChildren<OpenBrushGeometryMotion>();
   Check(motions.Length>0,"self-shadow changes over drawing time");
   foreach(var motion in motions) {
    Check(motion.Timeline==timeline && motion.FrameTimes[0]==0 && motion.FrameTimes.SequenceEqual(motion.FrameTimes.OrderBy(t=>t).Distinct()),"shared clock and strictly ordered frames");
    var growth=motion.transform.parent.GetComponent<OpenBrushGrowthPart>();
    Check(motion.FrameTimes.Skip(1).All(t=>t>growth.Start),"no shadow on a receiver before it is drawn");
    var first=motion.Frames[0];Check(motion.Frames.All(m=>m.vertices.SequenceEqual(first.vertices) && m.triangles.SequenceEqual(first.triangles)),"shadow frames preserve drawing geometry");
    Check(motion.Frames.All(m=>m.colors.All(c=>float.IsFinite(c.r)&&float.IsFinite(c.g)&&float.IsFinite(c.b)&&c.a>=0&&c.a<=1.000001f)),"finite Gamma paint and original opacity");
   }
   Check(root.GetComponentsInChildren<OpenBrushGrowthPart>().Length==35,"all 35 source growth parents retained");
  }
  Debug.Log("OPENBRUSH_PRINTING_SHADOW_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
