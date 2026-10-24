// Vertex equation from Open Brush KeijiroTube.hlsl (Tilt Brush Authors, Apache-2.0).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class OpenBrushKeijiroImport {
 public const string Guid="f8ba3d18-01fc-4d7b-b2d9-b99d10b8e7cf";
 public const int Fps=60;
 [Serializable] public class Data {public int schemaVersion;public string brushGuid,coordinateSystem;public Part[] parts;}
 [Serializable] public class Part:OpenBrushDiscoImport.Part {public Vector4[] tangents;}
 public static Vector3 Deform(Vector3 position,Vector3 normal,Vector3 uvRadius,float time) {
  float wave=Mathf.Clamp01(Mathf.Sin(uvRadius.x-time*2));
  float u=Mathf.Clamp01((wave-.45f)/.05f);float pulse=u*u*(3-2*u);
  return position-normal*(pulse*uvRadius.z);
 }
 public static Mesh Frame(Part p,float[] birth,float time) {
  var positions=p.positions.Select((v,i)=>Deform(v,p.normals[i],p.uvRadius[i],time)).ToList();
  var normals=p.normals.ToList();var tangents=p.tangents.ToList();var colors=p.colors.Select(c=>{float a=c.a;var v=c.linear;v.a=a;return v;}).ToList();
  var uv=p.uvRadius.Select(v=>new Vector2(v.x,1-v.y)).ToList();var triangles=new List<int>();
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
  var mesh=new Mesh{name="KeijiroPulse_"+time,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
  mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
  return mesh;
 }
 public static void Prepare(GameObject root,Data data,OpenBrushGrowthImport.Data timing,float[] samples=null) {
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || timing?.strokes==null || data==null || data.schemaVersion!=1 || data.brushGuid!=Guid || data.coordinateSystem!="UnityInvertZ" || data.parts==null || data.parts.Length==0)
   throw new InvalidOperationException("KeijiroTube needs original radius data and drawing timing");
  var times=samples ?? Enumerable.Range(0,Mathf.CeilToInt(timeline.Duration*Fps)+1).Select(i=>Mathf.Min(i/(float)Fps,timeline.Duration)).ToArray();
  if(times.Length<2 || times.Any(t=>!float.IsFinite(t)||t<0||t>timeline.Duration) || times.Zip(times.Skip(1),(a,b)=>a<b).Any(v=>!v))throw new InvalidOperationException("Invalid Keijiro sample times");
  if(data.parts.Select(p=>p.nodeName).Distinct().Count()!=data.parts.Length)throw new InvalidOperationException("Repeated Keijiro source node");
  var transforms=root.GetComponentsInChildren<Transform>(true);double budget=0;
  var mapped=new Dictionary<Part,float[]>();
  foreach(var p in data.parts) {
   int n=p.positions?.Length ?? 0;
   if(n==0 || p.normals?.Length!=n || p.tangents?.Length!=n || p.uvRadius?.Length!=n || p.colors?.Length!=n || p.triangles==null || p.triangles.Length%3!=0 || p.triangles.Any(i=>i<0||i>=n))throw new InvalidOperationException("Invalid Keijiro channels");
   if(p.tangents.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)||Mathf.Abs(Mathf.Abs(v.w)-1)>1e-5f))throw new InvalidOperationException("Invalid Keijiro tangents");
   if(p.positions.Concat(p.normals).Concat(p.uvRadius).Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)) || p.uvRadius.Any(v=>v.z<0) || p.colors.Any(c=>!float.IsFinite(c.r)||!float.IsFinite(c.g)||!float.IsFinite(c.b)||!float.IsFinite(c.a)))throw new InvalidOperationException("Invalid Keijiro values");
   var parent=transforms.Where(t=>t.name==p.nodeName).ToArray();
   if(parent.Length!=1 || parent[0].GetComponentInChildren<OpenBrushGeometryMotion>(true)!=null)throw new InvalidOperationException("Ambiguous or prepared Keijiro parent");
   var original=parent[0].GetComponentsInChildren<MeshRenderer>(true);
   if(original.Length==0 || original.Any(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=Guid))throw new InvalidOperationException("Keijiro material mismatch");
   var rawBounds=new Bounds(p.positions[0],Vector3.zero);foreach(var v in p.positions)rawBounds.Encapsulate(v);
   var points=original.SelectMany(r=>r.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v=>parent[0].InverseTransformPoint(r.transform.TransformPoint(v)))).ToArray();
   var imported=new Bounds(points[0],Vector3.zero);foreach(var v in points)imported.Encapsulate(v);
   if((rawBounds.min-imported.min).magnitude>1e-4 || (rawBounds.max-imported.max).magnitude>1e-4)throw new InvalidOperationException("Keijiro raw and prepared coordinates differ");
   var strokes=timing.strokes.Where(s=>s.name==p.nodeName).ToArray();var groups=OpenBrushDiscoGrowth.MatchStrokes(p,strokes,timeline.Duration);
   mapped.Add(p,Enumerable.Range(0,n).Select(i=>{var s=strokes[groups[i]];return OpenBrushGrowthImport.Birth(timing,s,p.times[i]);}).ToArray());
   // Indexed frames retain vertices; only cut triangles add intersections.
   budget+=(double)(n+p.triangles.Length/3*2)*times.Length;
   if(budget>20000000)throw new InvalidOperationException("Keijiro exceeds 20M frame vertices; split sketch");
  }
  foreach(var p in data.parts) {
   var parent=transforms.Single(t=>t.name==p.nodeName);var original=parent.GetComponentsInChildren<MeshRenderer>(true);
   var frames=times.Select(t=>Frame(p,mapped[p],t)).ToArray();
   var child=new GameObject("KeijiroTube - Traveling Contraction");child.transform.SetParent(parent,false);
   var motion=child.AddComponent<OpenBrushGeometryMotion>();motion.SourceMaterial=original[0].sharedMaterial;motion.Frames=frames;motion.FrameTimes=times;motion.Timeline=timeline;
   foreach(var r in original)r.enabled=false;
   Debug.Log("KEIJIRO_BAKE frames="+frames.Length+" vertices="+p.positions.Length+" finalTriangles="+frames.Last().triangles.Length/3);
  }
 }
}
