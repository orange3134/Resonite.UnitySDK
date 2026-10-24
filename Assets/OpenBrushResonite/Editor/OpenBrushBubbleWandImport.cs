// Vertex equation from Open Brush BubbleWandVertex.hlsl (Tilt Brush Authors, Apache-2.0).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class OpenBrushBubbleWandImport {
 public const string Guid="eba3f993-f9a1-4d35-b84e-bb08f48981a4";
 public const int Fps=30;
 [Serializable] public class Data {public int schemaVersion;public string brushGuid,coordinateSystem;public Part[] parts;}
 [Serializable] public class Part:OpenBrushDiscoImport.Part {public Vector4[] tangents;}
 // Equations from normal BubbleWandVertex.hlsl (_IS_TILT_MESH), Apache-2.0.
 // The combined legacy GLB/UniGLTF basis flips X/Z; source units are decimeters.
 static Vector3 Mirror(Vector3 v)=>new Vector3(-v.x,v.y,-v.z);
 public static void Sample(Vector3 position,Vector3 normal,Vector3 uvRadius,float seconds,out Vector3 point,out Vector3 direction) {
  var n=Mirror(normal);var wave=n*(uvRadius.z*10*Mathf.Sin(uvRadius.x*3.14159f));
  var pos=Mirror(position)*10+wave;var sample=pos;float t=seconds*-.54f;
  sample.x+=Mathf.Sin(t+seconds+sample.z)*3;
  sample.z+=Mathf.Cos(t+seconds+sample.x)*3;
  sample.y+=Mathf.Cos(t*1.2f+seconds+sample.x)*3;
  var v=sample*.1f+Vector3.one*(seconds*.05f);const float d=30;
  var curl=-new Vector3(OpenBrushBubbleMath.Noise(v.y,v.z+d)-OpenBrushBubbleMath.Noise(v.y,v.z-d),
   OpenBrushBubbleMath.Noise(v.z,v.x+d)-OpenBrushBubbleMath.Noise(v.z,v.x-d),
   OpenBrushBubbleMath.Noise(v.x,v.y+d)-OpenBrushBubbleMath.Noise(v.x,v.y-d))/(2*d)*.3f;
  point=Mirror(pos+curl)*.1f;direction=Mirror((n+curl*2.5f+wave*2.5f).normalized);
 }
 public static Vector3 Deform(Vector3 p,Vector3 n,Vector3 uv,float seconds){Sample(p,n,uv,seconds,out var point,out _);return point;}
 public static Mesh Frame(Part p,float[] birth,float time) {
  var positions=p.positions.ToList();
  var normals=p.normals.ToList();var tangents=p.tangents.ToList();var colors=p.colors.Select(c=>{float a=c.a;var v=c.linear;v.a=a;return v;}).ToList();
  var uv=p.uvRadius.Select(v=>new Vector2(v.x,1-v.y)).ToList();var triangles=new List<int>();
  for(int k=0;k<p.positions.Length;k++){Sample(p.positions[k],p.normals[k],p.uvRadius[k],time,out var point,out var normal);positions[k]=point;normals[k]=normal;}
  // Deform ORIGINAL vertices first. Only then interpolate intersections at
  // the drawing frontier; sin/smoothstep after subdivision gives a different shape.
  for(int i=0;i<p.triangles.Length;i+=3) {
   var polygon=new List<int>();
   for(int j=0;j<3;j++) {
    int a=p.triangles[i+j],b=p.triangles[i+(j+1)%3];bool inside=birth[a]<=time,other=birth[b]<=time;
    if(inside)polygon.Add(a);
    if(inside!=other) {
     // Canonical edge direction gives identical shared-edge intersections.
     if(birth[a]>birth[b]){int swap=a;a=b;b=swap;}
     float k=(time-birth[a])/(birth[b]-birth[a]);polygon.Add(positions.Count);
     positions.Add(Vector3.Lerp(positions[a],positions[b],k));normals.Add(Vector3.Lerp(normals[a],normals[b],k));
     colors.Add(Color.Lerp(colors[a],colors[b],k));uv.Add(Vector2.Lerp(uv[a],uv[b],k));
     tangents.Add(Vector4.Lerp(tangents[a],tangents[b],k));
    }
   }
   for(int j=1;j+1<polygon.Count;j++){triangles.Add(polygon[0]);triangles.Add(polygon[j]);triangles.Add(polygon[j+1]);}
  }
  if(triangles.Count==0)triangles.AddRange(new[]{0,0,0});
  var mesh=new Mesh{name="BubbleWandPulse_"+time,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
  mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
  return mesh;
 }
 static IEnumerable<(Color color,int[] triangles)> ColorGroups(Part p) {
  var groups=new Dictionary<Color,List<int>>();
  for(int i=0;i<p.triangles.Length;i+=3) {
   var c=p.colors[p.triangles[i]];
   if(c!=p.colors[p.triangles[i+1]] || c!=p.colors[p.triangles[i+2]])throw new InvalidOperationException("BubbleWand per-triangle paint gradients need a fragment bake");
   if(!groups.TryGetValue(c,out var list)){list=new List<int>();groups.Add(c,list);}
   list.Add(p.triangles[i]);list.Add(p.triangles[i+1]);list.Add(p.triangles[i+2]);
  }
  return groups.Select(g=>(g.Key,g.Value.ToArray()));
 }
 public static void Prepare(GameObject root,Data data,OpenBrushGrowthImport.Data timing,float[] samples=null) {
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || timing?.strokes==null || data==null || data.schemaVersion!=1 || data.brushGuid!=Guid || data.coordinateSystem!="UnityInvertZ" || data.parts==null || data.parts.Length==0)
   throw new InvalidOperationException("BubbleWand needs original radius data and drawing timing");
  var times=samples ?? Enumerable.Range(0,Mathf.CeilToInt(timeline.Duration*Fps)+1).Select(i=>Mathf.Min(i/(float)Fps,timeline.Duration)).ToArray();
  if(times.Length<2 || times.Any(t=>!float.IsFinite(t)||t<0||t>timeline.Duration) || times.Zip(times.Skip(1),(a,b)=>a<b).Any(v=>!v))throw new InvalidOperationException("Invalid BubbleWand sample times");
  if(data.parts.Select(p=>p.nodeName).Distinct().Count()!=data.parts.Length)throw new InvalidOperationException("Repeated BubbleWand source node");
  var transforms=root.GetComponentsInChildren<Transform>(true);double budget=0;var materials=new Dictionary<(Material,Color),(Material body,Material film)>();
  var mapped=new Dictionary<Part,float[]>();
  foreach(var p in data.parts) {
   int n=p.positions?.Length ?? 0;
   if(n==0 || p.normals?.Length!=n || p.tangents?.Length!=n || p.uvRadius?.Length!=n || p.colors?.Length!=n || p.triangles==null || p.triangles.Length%3!=0 || p.triangles.Any(i=>i<0||i>=n))throw new InvalidOperationException("Invalid BubbleWand channels");
   if(p.tangents.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)||Mathf.Abs(Mathf.Abs(v.w)-1)>1e-5f))throw new InvalidOperationException("Invalid BubbleWand tangents");
   if(p.positions.Concat(p.normals).Concat(p.uvRadius).Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)) || p.uvRadius.Any(v=>v.z<0) || p.colors.Any(c=>!float.IsFinite(c.r)||!float.IsFinite(c.g)||!float.IsFinite(c.b)||!float.IsFinite(c.a)))throw new InvalidOperationException("Invalid BubbleWand values");
   var parent=transforms.Where(t=>t.name==p.nodeName).ToArray();
   if(parent.Length!=1 || parent[0].GetComponentInChildren<OpenBrushGeometryMotion>(true)!=null)throw new InvalidOperationException("Ambiguous or prepared BubbleWand parent");
   var original=parent[0].GetComponentsInChildren<MeshRenderer>(true);
   if(original.Length==0 || original.Any(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=Guid))throw new InvalidOperationException("BubbleWand material mismatch");
   var rawBounds=new Bounds(p.positions[0],Vector3.zero);foreach(var v in p.positions)rawBounds.Encapsulate(v);
   var points=original.SelectMany(r=>r.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v=>parent[0].InverseTransformPoint(r.transform.TransformPoint(v)))).ToArray();
   var imported=new Bounds(points[0],Vector3.zero);foreach(var v in points)imported.Encapsulate(v);
   if((rawBounds.min-imported.min).magnitude>1e-4 || (rawBounds.max-imported.max).magnitude>1e-4)throw new InvalidOperationException("BubbleWand raw and prepared coordinates differ");
   var strokes=timing.strokes.Where(s=>s.name==p.nodeName).ToArray();var groups=OpenBrushDiscoGrowth.MatchStrokes(p,strokes,timeline.Duration);
   mapped.Add(p,Enumerable.Range(0,n).Select(i=>{var s=strokes[groups[i]];return OpenBrushGrowthImport.Birth(timing,s,p.times[i]);}).ToArray());
   // Indexed frames retain vertices; only cut triangles add intersections.
   budget+=(double)(n+p.triangles.Length/3*2)*times.Length*ColorGroups(p).Count();
   if(budget>20000000)throw new InvalidOperationException("BubbleWand exceeds 20M frame vertices; split sketch");
  }
  foreach(var p in data.parts) {
   var parent=transforms.Single(t=>t.name==p.nodeName);var original=parent.GetComponentsInChildren<MeshRenderer>(true);
   foreach(var group in ColorGroups(p)) {
    var key=(original[0].sharedMaterial,group.color);
    if(!materials.TryGetValue(key,out var pair)) {
     if(materials.Count>=16)throw new InvalidOperationException("BubbleWand film exceeds 16 paint colors; split sketch");
     pair=OpenBrushBubbleWandAppearance.Prepare(root,key.Item1,key.Item2);materials.Add(key,pair);
    }
    var part=new Part{positions=p.positions,normals=p.normals,uvRadius=p.uvRadius,tangents=p.tangents,colors=p.colors,triangles=group.triangles};
    var frames=times.Select(t=>Frame(part,mapped[p],t)).ToArray();
    foreach(var material in new[]{pair.body,pair.film}) {
     var child=new GameObject("BubbleWand - "+material.name);child.transform.SetParent(parent,false);
     var motion=child.AddComponent<OpenBrushGeometryMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FrameTimes=times;motion.Timeline=timeline;
    }
   }
   foreach(var r in original)r.enabled=false;
   Debug.Log("BUBBLEWAND_BAKE frames="+times.Length+" vertices="+p.positions.Length);
  }
 }
}
