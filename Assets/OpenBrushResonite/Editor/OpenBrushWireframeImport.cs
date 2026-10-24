using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Wireframe.shader (Tilt Brush Authors, Apache-2.0): in the non-audio path,
// w = (abs(u-.5)>.45) + (abs(v-.5)>.45), then Blend One One.
// Split the UV threshold regions exactly instead of rasterizing a thin mask.
// Exact coincident faces are summed in source Gamma space. Other overlaps and
// the scene background still blend linearly in Resonite (an approximation).
public static class OpenBrushWireframeImport {
 public const string Guid="2c1a6a63-6552-4d23-86d7-58f6fba8581b";
 struct Vertex {public Vector3 p;public Vector2 uv;public Color c;}
 static List<Vertex> Clip(List<Vertex> polygon,int axis,float boundary,bool above) {
  var result=new List<Vertex>();if(polygon.Count==0)return result;
  var previous=polygon[polygon.Count-1];bool was=above ? previous.uv[axis]>=boundary : previous.uv[axis]<=boundary;
  foreach(var current in polygon) {
   bool inside=above ? current.uv[axis]>=boundary : current.uv[axis]<=boundary;
   if(inside!=was) {
    float t=(boundary-previous.uv[axis])/(current.uv[axis]-previous.uv[axis]);
    var uv=Vector2.LerpUnclamped(previous.uv,current.uv,t);uv[axis]=boundary;
    result.Add(new Vertex{p=Vector3.LerpUnclamped(previous.p,current.p,t),uv=uv,c=Color.LerpUnclamped(previous.c,current.c,t)});
   }
   if(inside)result.Add(current);previous=current;was=inside;
  }
  return result;
 }
 public static Color Emission(Color paint,float gain,float referenceGamma) {
  var result=Color.white;float background=Mathf.GammaToLinearSpace(referenceGamma);
  for(int channel=0;channel<3;channel++)result[channel]=Mathf.LinearToGammaSpace(Mathf.Max(0,Mathf.GammaToLinearSpace(Mathf.Clamp01(referenceGamma+paint[channel]*gain))-background));
  return result;
 }
 // Native linear additive blending cannot reproduce Gamma addition against
 // every destination. .15 models the source's dark neutral environment; zero
 // remains available for a black destination. Neither is exact on all scenes.
 public static Mesh Bake(Mesh source,float referenceGamma=.15f) {
  if(float.IsNaN(referenceGamma)||referenceGamma<0||referenceGamma>=1)throw new ArgumentOutOfRangeException(nameof(referenceGamma));
  var positions=source.vertices;var points=new List<Vector3>();var colors=new List<Color>();var uvs=new List<Vector2>();
  foreach(var f in OpenBrushHighlighterImport.Faces(source)) {
   var triangle=Enumerable.Range(0,3).Select(i=>new Vertex{p=positions[f.indices[i]],uv=f.uv[i],c=f.colors[i]}).ToList();
   for(int u=0;u<3;u++)for(int v=0;v<3;v++) {
    int weight=(u==1?0:1)+(v==1?0:1);if(weight==0)continue;
    var polygon=triangle;
    for(int axis=0;axis<2;axis++) {
     int band=axis==0?u:v;
     if(band>0)polygon=Clip(polygon,axis,band==1?.05f:.95f,true);
     if(band<2)polygon=Clip(polygon,axis,band==0?.05f:.95f,false);
     // A whole face can sit exactly on a threshold. Only the central band owns
     // that boundary, matching the shader's strict comparison without copies.
     if(band!=1 && polygon.Count>0 && polygon.All(p=>p.uv[axis]==(band==0?.05f:.95f)))polygon=new List<Vertex>();
    }
    for(int i=1;i+1<polygon.Count;i++) {
     var a=polygon[0];var b=polygon[i];var c=polygon[i+1];
     if(Vector3.Cross(b.p-a.p,c.p-a.p).sqrMagnitude<=1e-24f)continue;
     foreach(var vertex in new[]{a,b,c}) {
      points.Add(vertex.p);uvs.Add(vertex.uv);float gain=weight*f.copies;
      // Keep output in the bounded source display range, avoiding native HDR
      // bloom. Source alpha does not attenuate RGB with Blend One One.
      colors.Add(Emission(vertex.c,gain,referenceGamma));
     }
    }
   }
  }
  var mesh=new Mesh{name=source.name+"_WireframeEdges",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
  mesh.SetVertices(points);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.triangles=Enumerable.Range(0,points.Count).ToArray();mesh.RecalculateBounds();return mesh;
 }
 public static void Prepare(GameObject root) {
  foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
   if(!renderer.enabled || renderer.sharedMaterial==null || renderer.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=Guid)continue;
   if(renderer.transform.Find("Wireframe UV Edges")!=null)throw new InvalidOperationException("Wireframe already prepared");
   var mesh=Bake(renderer.GetComponent<MeshFilter>().sharedMesh);
   var child=new GameObject("Wireframe UV Edges");child.transform.SetParent(renderer.transform,false);
   child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=renderer.sharedMaterial;
   renderer.sharedMaterial.SetColor("_TintColor",Color.white);renderer.enabled=false;
  }
 }
}
