using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Normal HyperGrid: canvas-local ceil quantization, .1s birth release, no audio.
public static class OpenBrushHyperGridImport {
 public const string Guid="6a1cf9f9-032c-45ec-9b6e-a6680bee32e9";
 [Serializable] public class Data {public int schemaVersion;public string brushGuid,coordinateSystem;public float appUnitsPerMeter;public Part[] parts;}
 [Serializable] public class Part {public string nodeName;public Vector3[] positions,offsets;public Vector2[] uv;public Color[] colors;public float[] births,sourceBirths,strokeStarts,strokeEnds;public int[] triangles;}
 public static Vector3 Quantize(Vector3 position,Vector3 offset,float age) {
  // Export reflects X; UniGLTF reflects Z. Quantize in the ORIGINAL canvas
  // basis, before either reflection and before the destination root transform.
  var p=new Vector3(-position.x,position.y,-position.z)*10;
  float size=offset.magnitude*10;
  if(!float.IsFinite(size) || size<1e-6f)throw new InvalidOperationException("Invalid HyperGrid offset size");
  float q=.5f/size+5*Mathf.Clamp01(1-10*Mathf.Max(0,age));
  return new Vector3(-Mathf.Ceil(p.x*q),Mathf.Ceil(p.y*q),-Mathf.Ceil(p.z*q))/(q*10);
 }
 public static float[] SampleTimes(float[] births,float duration)=>births.SelectMany(b=>Enumerable.Range(0,7).Select(i=>Mathf.Min(b+i/60f,duration))).Concat(new[]{0f,duration}).Distinct().OrderBy(t=>t).ToArray();
 public static Mesh Frame(Part p,float[] births,float time,Vector2[] surfaceUv=null) {
  var vertices=new Vector3[p.positions.Length];var colors=new Color[vertices.Length];
  for(int i=0;i<vertices.Length;i++) {
   float age=time>=births[i/4]+.1f ? .1f : time-births[i/4];
   vertices[i]=Quantize(p.positions[i],p.offsets[i],age);
   // Source doubles RGB and alpha, then multiplies them in the fragment.
   // Fold the constant quad alpha into RGB so native opacity handling cannot
   // clamp the source alpha2 before that multiplication.
   var c=p.colors[i];colors[i]=surfaceUv!=null ? Color.white : new Color(4*c.r*c.a,4*c.g*c.a,4*c.b*c.a,1);
  }
  var triangles=new List<int>();
  for(int i=0;i<p.triangles.Length;i+=3)if(births[p.triangles[i]/4]<=time){triangles.Add(p.triangles[i]);triangles.Add(p.triangles[i+1]);triangles.Add(p.triangles[i+2]);}
  if(triangles.Count==0)triangles.AddRange(new[]{0,0,0});
  var mesh=new Mesh{name="HyperGridBirth_"+time,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices,colors=colors,uv=surfaceUv ?? p.uv.Select(v=>new Vector2(v.x,1-v.y)).ToArray()};
  mesh.normals=Enumerable.Repeat(Vector3.forward,vertices.Length).ToArray();mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
 }
 public static void Prepare(GameObject root,Data data,OpenBrushGrowthImport.Data timing) {
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || timing?.strokes==null || data==null || data.schemaVersion!=1 || data.brushGuid!=Guid || data.coordinateSystem!="UnityInvertZ" || data.appUnitsPerMeter!=10 || data.parts==null || data.parts.Length==0)throw new InvalidOperationException("HyperGrid requires original canvas offsets and drawing timestamps");
  if(data.parts.Select(p=>p.nodeName).Distinct().Count()!=data.parts.Length)throw new InvalidOperationException("Duplicate HyperGrid node");
  var transforms=root.GetComponentsInChildren<Transform>(true);double budget=0;
  var mapped=new Dictionary<Part,float[]>();var samples=new Dictionary<Part,float[]>();
  foreach(var p in data.parts) {
   int n=p.positions?.Length ?? 0,q=n/4;
   if(n==0 || n%4!=0 || p.offsets?.Length!=n || p.uv?.Length!=n || p.colors?.Length!=n || p.births?.Length!=q || p.strokeStarts?.Length!=q || p.strokeEnds?.Length!=q || p.sourceBirths?.Length!=q || p.triangles?.Length!=q*6 || p.triangles.Any(v=>v<0||v>=n))throw new InvalidOperationException("Invalid HyperGrid channels");
   if(p.positions.Concat(p.offsets).Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)) || p.offsets.Any(v=>v.sqrMagnitude<1e-12) || p.uv.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)) || p.colors.Any(c=>!float.IsFinite(c.r)||!float.IsFinite(c.g)||!float.IsFinite(c.b)||!float.IsFinite(c.a)))throw new InvalidOperationException("Nonfinite HyperGrid input");
   for(int i=0;i<p.triangles.Length;i+=3)if(p.triangles[i]/4!=p.triangles[i+1]/4 || p.triangles[i]/4!=p.triangles[i+2]/4)throw new InvalidOperationException("HyperGrid triangle crosses quads");
   var parents=transforms.Where(t=>t.name==p.nodeName).ToArray();
   if(parents.Length!=1 || parents[0].GetComponentInChildren<OpenBrushGeometryMotion>(true)!=null)throw new InvalidOperationException("Missing or prepared HyperGrid parent");
   var renderers=parents[0].GetComponentsInChildren<MeshRenderer>(true);
   if(renderers.Length==0 || renderers.Any(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=Guid))throw new InvalidOperationException("HyperGrid material mismatch");
   var births=new float[q];
   for(int i=0;i<q;i++) {
    var matches=timing.strokes.Where(s=>s.name==p.nodeName && Mathf.Abs(s.sourceStart-p.strokeStarts[i])<.0001f && Mathf.Abs(s.sourceEnd-p.strokeEnds[i])<.0001f).ToArray();
    if(matches.Length!=1 || !float.IsFinite(p.births[i]) || p.births[i]<p.strokeStarts[i]-.0001f || p.births[i]>p.strokeEnds[i]+.0001f)throw new InvalidOperationException("Ambiguous HyperGrid quad timestamp");
    var s=matches[0];births[i]=OpenBrushGrowthImport.Birth(timing,s,p.births[i]);
   }
   if(timing.completed)for(int i=0;i<births.Length;i++)births[i]=-.1f;
   var times=timing.completed ? new[]{0f,timeline.Duration} : SampleTimes(births,timeline.Duration);budget+=(double)n*times.Length;
   if(budget>20000000)throw new InvalidOperationException("HyperGrid exceeds 20M frame vertices; split the sketch");
   mapped.Add(p,births);samples.Add(p,times);
  }
  foreach(var p in data.parts) {
   var parent=transforms.Single(t=>t.name==p.nodeName);var renderers=parent.GetComponentsInChildren<MeshRenderer>(true);
   var material=OpenBrushHyperGridSurface.Build(p,renderers[0].sharedMaterial,out var surfaceUv);
   var child=new GameObject("HyperGrid - Canvas Quantization");child.transform.SetParent(parent,false);
   var motion=child.AddComponent<OpenBrushGeometryMotion>();motion.SourceMaterial=material;motion.Timeline=timeline;motion.FrameTimes=samples[p];motion.Frames=motion.FrameTimes.Select(t=>Frame(p,mapped[p],t,surfaceUv)).ToArray();
   foreach(var r in renderers)r.enabled=false;
   Debug.Log("HYPERGRID_BAKE quads="+mapped[p].Length+" frames="+motion.Frames.Length+" vertices="+p.positions.Length);
  }
 }
}


