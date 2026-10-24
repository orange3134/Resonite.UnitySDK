using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushBubbleWandChecks {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Oracle() {
  var shader=Shader.Find("Hidden/OpenBrush/BubbleWandReference");Check(shader!=null&&shader.isSupported,"BubbleWand shader oracle unavailable");
  var mat=new Material(shader);var rt=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
  var read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);var old=RenderTexture.active;bool srgb=GL.sRGBWrite;float error=0;
  try {for(int i=0;i<80;i++) {
   var p=new Vector3(.3f+i*.01f,1.7f,-.2f);var n=new Vector3(.3f,.5f,.8f).normalized;
   var uv=new Vector3(i/79f,.7f,.003f+i*.002f);float seconds=i*.137f;
   OpenBrushBubbleWandImport.Sample(p,n,uv,seconds,out var point,out var normal);
   mat.SetVector("_Position",p);mat.SetVector("_Normal",n);mat.SetVector("_UVRadius",uv);mat.SetFloat("_Seconds",seconds);
   for(int channel=0;channel<2;channel++) {
    mat.SetFloat("_OutputNormal",channel);GL.sRGBWrite=false;Graphics.Blit(Texture2D.whiteTexture,rt,mat);RenderTexture.active=rt;
    read.ReadPixels(new Rect(0,0,1,1),0,0,false);read.Apply();var c=read.GetPixel(0,0);
    error=Mathf.Max(error,Vector3.Distance(new Vector3(c.r,c.g,c.b),channel==0 ? point : normal));
   }
  }} finally {RenderTexture.active=old;GL.sRGBWrite=srgb;rt.Release();foreach(var o in new UnityEngine.Object[]{mat,rt,read})UnityEngine.Object.DestroyImmediate(o);}
  Check(error<.0001f,"CPU/GPU BubbleWand position/normal mismatch: "+error);Debug.Log("BUBBLEWAND_GPU_ERROR "+error);
 }
 static void Frames() {
  var p=new OpenBrushBubbleWandImport.Part{positions=new[]{Vector3.zero,Vector3.right,Vector3.up},normals=Enumerable.Repeat(Vector3.forward,3).ToArray(),uvRadius=Enumerable.Repeat(new Vector3(.5f,0,.1f),3).ToArray(),colors=Enumerable.Repeat(Color.red,3).ToArray(),tangents=Enumerable.Repeat(new Vector4(1,0,0,1),3).ToArray(),triangles=new[]{0,1,2}};
  var birth=new[]{1f,2f,2f};var blank=OpenBrushBubbleWandImport.Frame(p,birth,0);var partial=OpenBrushBubbleWandImport.Frame(p,birth,1.5f);var full=OpenBrushBubbleWandImport.Frame(p,birth,3);var later=OpenBrushBubbleWandImport.Frame(p,birth,4);
  Check(blank.triangles.All(v=>v==0),"Blank lead is retained");Check(partial.vertexCount>3,"Stroke frontier interpolates original vertices");
  var completed=OpenBrushBubbleWandImport.Frame(p,new float[3],0);
  Check(completed.vertexCount==p.positions.Length && completed.triangles.SequenceEqual(p.triangles),"Completed first frame needs no frontier vertices");
  Check(full.triangles.SequenceEqual(p.triangles),"Completed frame preserves topology");Check(full.vertices[0]!=later.vertices[0],"Curl motion survives completed mode");
  Check(Mathf.Abs(full.vertices[0].z-.1f)<.002f,"Radius is meters, not a tenfold bulge");Check(full.normals.All(n=>Mathf.Abs(n.magnitude-1)<1e-5),"Normals stay normalized");
  var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,true);tex.SetPixels(new[]{Color.red,Color.green,Color.blue,Color.white});tex.Apply();
  var a=OpenBrushBubbleWandAppearance.Shade(tex,Color.white,.5f,0);var b=OpenBrushBubbleWandAppearance.Shade(tex,Color.white,.5f,20);
  Check((new Vector3(a.r-b.r,a.g-b.g,a.b-b.b)).magnitude<1e-5,"Film closes its 20-second texture loop");
 }
 static void DiscoAtlas() {
  int faces=SystemInfo.maxTextureSize+3;
  var part=new OpenBrushDiscoImport.Part{triangles=Enumerable.Range(0,faces*3).ToArray(),colors=Enumerable.Range(0,faces*3).Select(i=>i/3%2==0 ? Color.red : Color.blue).ToArray()};
  var tex=OpenBrushDiscoImport.SpecularAtlas(part);var size=OpenBrushDiscoImport.AtlasSize(faces);
  Check(tex.width<=SystemInfo.maxTextureSize && tex.height>1,"Large Disco atlas packs into rows");
  foreach(int face in new[]{0,size.x-1,size.x,faces-1}) {
   var uv=OpenBrushDiscoImport.FaceUV(face,size);var actual=tex.GetPixel((int)(uv.x*tex.width),(int)(uv.y*tex.height));
   Check(actual==OpenBrushDiscoImport.SpecularColor(part.colors[face*3]),"Face color remains mapped across atlas rows");
  }
  Check(OpenBrushDiscoImport.FaceUV(2,OpenBrushDiscoImport.AtlasSize(3))==new Vector2(2.5f/3,.5f),"Small atlas UV compatibility");
  UnityEngine.Object.DestroyImmediate(tex);
 }
 static void FlatFlow() {
  var root=new GameObject("FlatUVCheck");root.AddComponent<OpenBrushTimeline>().Duration=1;
  var child=new GameObject("brush_Sparks_stroke_0000_part_000");child.transform.SetParent(root.transform,false);
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},normals=Enumerable.Repeat(Vector3.forward,3).ToArray(),colors=new[]{Color.red,Color.green,Color.blue},uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(0,.5f)},triangles=new[]{0,1,2}};
  child.AddComponent<MeshFilter>().sharedMesh=mesh;
  var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=new Material(Shader.Find("Standard"));renderer.sharedMaterial.SetOverrideTag("OpenBrushGuid",OpenBrushFlowImport.Sparks);
  OpenBrushFlowImport.PrepareSamples(root,new[]{0f,1f},1);
  var result=child.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
  Check(!renderer.enabled && result.vertices.SequenceEqual(mesh.vertices) && result.triangles.SequenceEqual(mesh.triangles),"Zero-width UV retains visible geometry");
  Check(result.uv.Distinct().Count()==3,"Zero-width UV has a nondegenerate bake domain");
  Check(mesh.uv.All(v=>v.x==0) && mesh.colors[1]==Color.green,"Original shader UV and paint preserved");
  var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();Check(motion.Frames.Length==2,"Flow timing preserved");
  UnityEngine.Object.DestroyImmediate(root);
 }
 static void HueBudget() {
  var small=OpenBrushAtlasBudget.Select(64,64,2,2,2,1,1,8192,true,16777216);
  Check(small.TileWidth==64,"Small face atlases retain full detail");
  var large=OpenBrushAtlasBudget.Select(64,64,2,71,71,1,1,8192,true,16777216);
  Check(large.TileWidth==32 && large.FramePixels<=16777216,"Large face atlas fits without dropping geometry");
 }
 static void OverlappingHue() {
  var root=new GameObject("HueOverlapCheck");var child=new GameObject("brush_Lofted_stroke_0000_part_000");child.transform.SetParent(root.transform,false);
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.forward,Vector3.forward+Vector3.right,Vector3.forward+Vector3.up},normals=Enumerable.Repeat(Vector3.forward,6).ToArray(),colors=new[]{Color.red,Color.red,Color.red,Color.blue,Color.blue,Color.blue},uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.up},triangles=Enumerable.Range(0,6).ToArray()};
  child.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=new Material(Shader.Find("Standard"));renderer.sharedMaterial.SetOverrideTag("OpenBrushGuid",OpenBrushHueShiftImport.Lofted);
  OpenBrushHueShiftImport.Prepare(root);
  Check(child.transform.childCount==2 && !renderer.enabled,"Overlapping paint retains both geometric faces");
  var a=child.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh;var b=child.transform.GetChild(1).GetComponent<MeshFilter>().sharedMesh;
  Check(a.vertices.SequenceEqual(mesh.vertices.Take(3)) && b.vertices.SequenceEqual(mesh.vertices.Skip(3)),"Face split preserves positions and winding");
  Check(a.uv[0]!=b.uv[0],"Different paint receives separate atlas cells");
  var tex=(Texture2D)child.transform.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
  Vector2 auv=(a.uv[0]+a.uv[1]+a.uv[2])/3,buv=(b.uv[0]+b.uv[1]+b.uv[2])/3;
  Check(tex.GetPixelBilinear(auv.x,auv.y)!=tex.GetPixelBilinear(buv.x,buv.y),"Different face paint survives GPU bake");
  UnityEngine.Object.DestroyImmediate(root);
 }
 public static void Run(){try{Oracle();Frames();DiscoAtlas();FlatFlow();HueBudget();OverlappingHue();Debug.Log("OPENBRUSH_BUBBLEWAND_CHECKS_PASSED");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
