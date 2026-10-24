// MylarTube normal ShaderGraph equations, Open Brush Unity Tools (Apache-2.0).
// Source N dot V uses true per-fragment view; native Matcap approximates it
// using camera-forward normals. Bundle preparation squeezes the legacy export
// before time-band clipping; the material-import guard requires that bake.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OpenBrushMylarImport {
 public const string Guid="8e58ceea-7830-49b4-aba9-6215104ab52a";
 public const int Resolution=128,FrameCount=120;
 public const float Period=20;
 public static bool Supports(string guid)=>guid==Guid;
 public static float Rim(float cosine) {
  float edge=1-Mathf.Clamp01(Mathf.Abs(cosine));return edge*(1-Mathf.Pow(edge,5));
 }
 public static Color Shade(Texture2D pattern,Color paint,float cosine,float seconds) {
  float rim=Rim(cosine);
  // Graph Normal(Tangent).G is zero: both UV components start at rim.
  // BrushTime output0 is seconds; the source adds time*.05 only to V.
  var t=OpenBrushTextureSampling.SampleRaw(pattern,rim,rim+seconds/Period);
  return OpenBrushFireImport.DisplayEmission(new Color(
   rim*t.r*(.25f*rim+.75f*paint.r),rim*t.g*(.25f*rim+.75f*paint.g),rim*t.b*(.25f*rim+.75f*paint.b),1));
 }
 public static Texture2D[] Bake(Texture2D pattern,Color paint) {
  if(pattern==null||!pattern.isReadable)throw new InvalidOperationException("Mylar needs its readable source pattern");
  var frames=new Texture2D[FrameCount];
  for(int f=0;f<FrameCount;f++) {
   var pixels=new Color[Resolution*Resolution];
   for(int y=0;y<Resolution;y++)for(int x=0;x<Resolution;x++) {
    float nx=2*(x+.5f)/Resolution-1,ny=2*(y+.5f)/Resolution-1;
    float cosine=Mathf.Sqrt(Mathf.Max(0,1-nx*nx-ny*ny));
    
    pixels[y*Resolution+x]=Shade(pattern,paint,cosine,f*Period/FrameCount);
   }
   var texture=new Texture2D(Resolution,Resolution,TextureFormat.RGBA32,true,true){name="MylarView_"+paint+"_"+f,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
   texture.SetPixels(pixels);texture.Apply(true,false);frames[f]=texture;
  }
  return frames;
 }
 static IEnumerable<(Color color,int[] indices)> Colors(Mesh mesh) {
  var colors=mesh.colors;var indices=mesh.triangles;var groups=new Dictionary<Color,List<int>>();
  if(colors.Length!=mesh.vertexCount)throw new InvalidOperationException("Mylar requires source paint colors");
  for(int i=0;i<indices.Length;i+=3) {
   Color c=colors[indices[i]];
   if(c!=colors[indices[i+1]]||c!=colors[indices[i+2]])throw new InvalidOperationException("Mylar gradient triangles need a fragment-color bake");
   if(!groups.TryGetValue(c,out var list)){list=new List<int>();groups.Add(c,list);}
   list.Add(indices[i]);list.Add(indices[i+1]);list.Add(indices[i+2]);
  }
  return groups.Select(g=>(g.Key,g.Value.ToArray()));
 }
 public static void Prepare(GameObject root) {
  var inputs=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.sharedMaterial!=null&&Supports(r.sharedMaterial.GetTag("OpenBrushGuid",false,""))&&r.sharedMaterial.GetTag("OpenBrushMylarPrepared",false,"")!="1")
   .Select(r=>new{renderer=r,mesh=r.GetComponent<MeshFilter>().sharedMesh,groups=Colors(r.GetComponent<MeshFilter>().sharedMesh).ToArray()}).ToArray();
  var unique=inputs.SelectMany(i=>i.groups.Select(g=>(i.renderer.sharedMaterial,g.color))).Distinct().Count();
  if((long)unique*Resolution*Resolution*FrameCount>33554432)throw new InvalidOperationException("Mylar film exceeds 32M frame pixels; split the sketch");
  var cache=new Dictionary<(Material,Color),(Material body,Material emission)>();
  var frameCache=new Dictionary<(Texture2D,Color),Texture2D[]>();
  foreach(var input in inputs) {
   var source=input.renderer.sharedMaterial;
   foreach(var group in input.groups) {
    var key=(source,group.color);
    if(!cache.TryGetValue(key,out var materials)) {
     var frameKey=(source.mainTexture as Texture2D,group.color);
     if(!frameCache.TryGetValue(frameKey,out var frames)){frames=Bake(frameKey.Item1,group.color);frameCache.Add(frameKey,frames);}
     var body=new Material(source){name="Mylar - Reflective Surface",mainTexture=null};
     body.SetColor("_TintColor",group.color);
     body.SetOverrideTag("OpenBrushMylarBody","1");body.SetOverrideTag("OpenBrushMylarPrepared","1");
     var emission=new Material(source){name="Mylar - Animated View Pattern",mainTexture=frames[0]};
     emission.SetOverrideTag("OpenBrushViewEmission","1");emission.SetOverrideTag("OpenBrushMylarPrepared","1");
     var clock=new GameObject("Mylar - Pattern Motion");clock.transform.SetParent(root.transform,false);
     var motion=clock.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=emission;motion.Frames=frames;motion.FramesPerSecond=FrameCount/Period;motion.TextureScale=Vector2.one;
     materials=(body,emission);cache.Add(key,materials);
    }
    // Each original face and winding retained, without adding reverse or duplicate faces.
    var mesh=UnityEngine.Object.Instantiate(input.mesh);mesh.name=input.mesh.name+"_MylarFilm";mesh.triangles=group.indices;mesh.RecalculateBounds();
    var child=new GameObject("Mylar Film");child.transform.SetParent(input.renderer.transform,false);
    child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=materials.emission;
    var bodyMesh=UnityEngine.Object.Instantiate(mesh);
    var bodyChild=new GameObject("Mylar Surface");bodyChild.transform.SetParent(input.renderer.transform,false);
    bodyChild.AddComponent<MeshFilter>().sharedMesh=bodyMesh;bodyChild.AddComponent<MeshRenderer>().sharedMaterial=materials.body;
   }
   input.renderer.enabled=false;
  }
  if(inputs.Length>0)Debug.Log("OPENBRUSH_MYLAR_PATTERN_PREPARED "+inputs.Length+" segments, "+cache.Count+" color sequences; view-normal approximation");
 }
}




