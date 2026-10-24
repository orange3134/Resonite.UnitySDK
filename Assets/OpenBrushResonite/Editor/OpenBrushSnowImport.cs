using System;
using System.Linq;
using UnityEngine;
public static class OpenBrushSnowImport {
 public static Mesh PointMesh(OpenBrushParticleMotion.Particle[] particles) {
  var mesh=new Mesh{name="Snow_StaticPoints",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
  mesh.vertices=particles.Select(p=>p.center).ToArray();
  mesh.normals=particles.Select(p=>new Vector3(p.size,p.size,p.rotation)).ToArray();
  mesh.colors=particles.Select(p=>Color.white).ToArray();
  mesh.uv=particles.Select(p=>new Vector2((p.uv.x+p.uv.z)*.5f,1-(p.uv.y+p.uv.w)*.5f)).ToArray();
  mesh.uv2=particles.Select(p=>new Vector2(p.uv.z-p.uv.x,p.uv.w-p.uv.y)).ToArray();
  mesh.SetIndices(Enumerable.Range(0,particles.Length).ToArray(),MeshTopology.Points,0);mesh.RecalculateBounds();
  var bounds=mesh.bounds;bounds.Expand(particles.Max(p=>p.size));mesh.bounds=bounds;return mesh;
 }
 public static void Prepare(GameObject root,OpenBrushParticleImport.Part[] parts) {
  if(parts.Length==0)return;
  var transforms=root.GetComponentsInChildren<Transform>(true);
  var sources=parts.Select(p=>new{part=p,parent=transforms.Single(t=>t.name==p.nodeName)}).ToArray();
  // Merge export batches only when material identity and local-to-world basis
  // match exactly. Keep separate parents when transforms differ.
  var sets=sources.GroupBy(s=>(s.parent.localToWorldMatrix,s.parent.GetComponentInChildren<MeshRenderer>(true).sharedMaterial)).ToArray();
  long particles=parts.Sum(p=>(long)p.particles.Length),groups=sets.Sum(set=>(long)set.SelectMany(s=>s.part.particles).Select(p=>p.color).Distinct().Count());
  long bytes=particles*64+groups*OpenBrushSnowGroup.Frames*32;
  if(bytes>134217728)throw new InvalidOperationException("Compact Snow curves exceed 128 MiB; split the sketch");
  var clip=OpenBrushGrowthImport.CreateDurationClip(OpenBrushSnowGroup.Period,"CompletedSnowTimeline");
  foreach(var set in sets) {
   var holder=new GameObject("Snow - Shared Motion");holder.transform.SetParent(set.First().parent,false);
   var timeline=holder.AddComponent<OpenBrushTimeline>();timeline.Duration=OpenBrushSnowGroup.Period;timeline.DurationClip=clip;timeline.AutoPlay=true;
   foreach(var group in set.SelectMany(s=>s.part.particles).GroupBy(p=>p.color)) {
    var child=new GameObject("Snow - Phase Group");child.transform.SetParent(holder.transform,false);
    var motion=child.AddComponent<OpenBrushSnowGroup>();motion.Paint=group.Key;motion.Timeline=timeline;motion.SourceMaterial=set.Key.Item2;motion.Mesh=PointMesh(group.ToArray());
   }
   foreach(var s in set)foreach(var renderer in s.parent.GetComponentsInChildren<MeshRenderer>(true))renderer.enabled=false;
  }
  Debug.Log("SNOW_COMPACT particles="+particles+" groups="+groups+" samples="+(groups*OpenBrushSnowGroup.Frames)+" estimatedBytes="+bytes+" fps=30 period=25");
 }
}
