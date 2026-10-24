using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Original normal-material DanceFloor, without audio input. Preserve loaded-sketch
// birth phase independently of the timestamp that reveals each original quad.
public static class OpenBrushDanceFloorImport {
 public const string Guid="6a1cf9f9-032c-45ec-311e-a6680bee32e9";
 public const int Rate=60;
 [Serializable] public class Data {public int schemaVersion;public string brushGuid,coordinateSystem;public float appUnitsPerMeter;public Part[] parts;}
 [Serializable] public class Part : OpenBrushHyperGridImport.Part {public Vector3[] normals;}
 public static int Phase(float time,float birth) => Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(time-birth,1)*Rate+.0001f),0,Rate-1);
 public static Vector3 Position(Vector3 p,Vector3 normal,float pulse) {
  var original=new Vector3(-p.x,p.y,-p.z)*10;
  return new Vector3(-Mathf.Ceil(original.x*5),Mathf.Ceil(original.y*5),-Mathf.Ceil(original.z*5))/50+normal*(pulse*.01f);
 }
 public static Mesh Frame(Part p,float[] births,float phaseOrigin,float time,OpenBrushDanceFloorSurface.Surface surface) {
  var vertices=new Vector3[p.positions.Length];var uv=new Vector2[vertices.Length];
  for(int i=0;i<vertices.Length;i++) {
   int phase=Phase(time,p.sourceBirths[i/4]-phaseOrigin);float pulse=Mathf.Pow(phase/(float)Rate,3);
   vertices[i]=Position(p.positions[i],p.normals[i],pulse);uv[i]=surface.Map(i,phase,p.uv[i]);
  }
  var triangles=new List<int>();
  for(int i=0;i<p.triangles.Length;i+=3)if(time>=births[p.triangles[i]/4]){triangles.Add(p.triangles[i]);triangles.Add(p.triangles[i+1]);triangles.Add(p.triangles[i+2]);}
  if(triangles.Count==0)triangles.AddRange(new[]{0,0,0});
  var mesh=new Mesh{name="DanceFloor_"+time,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices,uv=uv,normals=p.normals,colors=Enumerable.Repeat(Color.white,vertices.Length).ToArray()};
  mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
 }
 public static void Prepare(GameObject root,Data data,OpenBrushGrowthImport.Data timing) {
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || timing?.strokes==null || data==null || data.schemaVersion!=1 || data.brushGuid!=Guid || data.coordinateSystem!="UnityInvertZ" || data.appUnitsPerMeter!=10 || data.parts==null || data.parts.Length==0)throw new InvalidOperationException("DanceFloor requires original normals, birth phases and drawing timestamps");
  var transforms=root.GetComponentsInChildren<Transform>(true);var mapped=new Dictionary<Part,float[]>();var samples=new Dictionary<Part,float[]>();double budget=0;
  if(data.parts.Select(p=>p.nodeName).Distinct().Count()!=data.parts.Length)throw new InvalidOperationException("Duplicate DanceFloor node");
  foreach(var p in data.parts) {
   int n=p.positions?.Length ?? 0,q=n/4;
   if(n==0 || n%4!=0 || p.normals?.Length!=n || p.uv?.Length!=n || p.colors?.Length!=n || p.births?.Length!=q || p.sourceBirths?.Length!=q || p.strokeStarts?.Length!=q || p.strokeEnds?.Length!=q || p.triangles?.Length!=q*6 || p.triangles.Any(v=>v<0||v>=n))throw new InvalidOperationException("Invalid DanceFloor channels");
   if(p.positions.Concat(p.normals).Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)) || p.normals.Any(v=>Mathf.Abs(v.magnitude-1)>.001f) || p.uv.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||v.x<0||v.x>1||v.y<0||v.y>1) || p.colors.Any(c=>!float.IsFinite(c.r)||!float.IsFinite(c.g)||!float.IsFinite(c.b)||!float.IsFinite(c.a)) || p.sourceBirths.Any(b=>!float.IsFinite(b)))throw new InvalidOperationException("Invalid DanceFloor values");
   for(int i=0;i<p.triangles.Length;i+=3)if(p.triangles[i]/4!=p.triangles[i+1]/4 || p.triangles[i]/4!=p.triangles[i+2]/4)throw new InvalidOperationException("DanceFloor triangle crosses quads");
   var parents=transforms.Where(t=>t.name==p.nodeName).ToArray();
   if(parents.Length!=1 || parents[0].GetComponentInChildren<OpenBrushGeometryMotion>(true)!=null)throw new InvalidOperationException("Missing or prepared DanceFloor parent");
   var renderers=parents[0].GetComponentsInChildren<MeshRenderer>(true);
   if(renderers.Length==0 || renderers.Any(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=Guid))throw new InvalidOperationException("DanceFloor material mismatch");
   var births=new float[q];
   for(int i=0;i<q;i++) {
    var matches=timing.strokes.Where(s=>s.name==p.nodeName && Mathf.Abs(s.sourceStart-p.strokeStarts[i])<.0001f && Mathf.Abs(s.sourceEnd-p.strokeEnds[i])<.0001f).ToArray();
    if(matches.Length!=1 || !float.IsFinite(p.births[i]) || p.births[i]<p.strokeStarts[i]-.0001f || p.births[i]>p.strokeEnds[i]+.0001f)throw new InvalidOperationException("Ambiguous DanceFloor drawing timestamp");
    var s=matches[0];births[i]=OpenBrushGrowthImport.Birth(timing,s,p.births[i]);
   }
   // Include exact birth and reset events as well as 60 Hz samples. Avoid a
   // delayed appearance or sampling across the discontinuous one-second reset.
   float origin=data.parts.Min(part=>part.sourceBirths.Min());
   var times=new SortedSet<float>{0,timeline.Duration};foreach(var b in births)times.Add(b);
   foreach(var phase in p.sourceBirths.Select(b=>Mathf.Repeat(b-origin,1)).Distinct())
    for(int k=0;k<=Mathf.CeilToInt(timeline.Duration*Rate);k++){float t=phase+k/(float)Rate;if(t<=timeline.Duration)times.Add(t);}
   budget+=(double)n*times.Count;if(budget>20000000)throw new InvalidOperationException("DanceFloor exceeds 20M frame vertices; split the sketch");
   mapped.Add(p,births);samples.Add(p,times.ToArray());
  }
  float phaseOrigin=data.parts.Min(p=>p.sourceBirths.Min());long surfaceBudget=0;
  foreach(var p in data.parts) {
   var parent=transforms.Single(t=>t.name==p.nodeName);var renderers=parent.GetComponentsInChildren<MeshRenderer>(true);
   var surface=OpenBrushDanceFloorSurface.Build(p,renderers[0].sharedMaterial,ref surfaceBudget);
   var child=new GameObject("DanceFloor - Timed Grid Pulse");child.transform.SetParent(parent,false);
   var motion=child.AddComponent<OpenBrushGeometryMotion>();motion.SourceMaterial=surface.material;motion.Timeline=timeline;motion.FrameTimes=samples[p];motion.Frames=motion.FrameTimes.Select(t=>Frame(p,mapped[p],phaseOrigin,t,surface)).ToArray();
   foreach(var r in renderers)r.enabled=false;
   Debug.Log("DANCEFLOOR_BAKE quads="+mapped[p].Length+" frames="+motion.Frames.Length+" vertices="+p.positions.Length);
  }
 }
}
