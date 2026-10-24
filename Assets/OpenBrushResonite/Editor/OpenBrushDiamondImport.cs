// Thin-film equations adapted from Open Brush Unity Tools DiamondHullGetDiffraction.hlsl,
// Apache-2.0, see SourceTextures/LICENSE. Native matcap evaluates camera-forward
// normals: it approximates perspective N dot V and world-normal pattern anchoring.
// Keep opt-in until multi-view comparisons justify replacing the old profile.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OpenBrushDiamondImport {
 public const string Hull="c8313697-2563-47fc-832e-290f4c04b901",Membrane="4f070b71-da25-48c4-9322-e9216d921562";
 public const int Resolution=128,FrameCount=96;
 public const float Period=6/.152f;
 public static bool Supports(string guid)=>guid==Hull||guid==Membrane;
 public static float Reflectance(float cosine,float wavelength,float thickness) {
  // Equal incident/exit media (n0=n2=1) simplify the source equations exactly.
  double c=Math.Max(.000001,Math.Min(1,Math.Abs(cosine))),n=1.3;
  double cf=Math.Sqrt(1-(1-c*c)/(n*n));
  double rs=(c-n*cf)/(c+n*cf),rp=(n*c-cf)/(n*c+cf);
  double phase=4*Math.PI*n*thickness*cf/wavelength;
  double S(double r){double a=r*r,b=1-r*r;return 1-b*b/(a*a-2*a*Math.Cos(phase)+1);}
  return (float)Math.Max(0,Math.Min(1,(S(rs)+S(rp))*.5));
 }
 public static float Rim(float cosine) {
  float c=Mathf.Clamp01(cosine),f=Mathf.Pow(1-c,5);
  return Mathf.Lerp(10*(1-f)*f+c*(1-c),150,1-Mathf.Clamp01(Mathf.Abs(cosine)/.15f));
 }
 public static Color Film(Texture2D pattern,float nx,float ny,float cosine,float seconds) {
  float rim=Rim(cosine);
  var t=OpenBrushTextureSampling.SampleRaw(pattern,nx+.21f*rim+seconds/Period,ny+.21f*rim);
  float thickness=250+150*(t.r+t.g+t.b)/3;
  return new Color(Reflectance(cosine,650,thickness),Reflectance(cosine,510,thickness),Reflectance(cosine,475,thickness),1);
 }
 public static Texture2D[] Bake(Texture2D pattern,Color paint) {
  if(pattern==null||!pattern.isReadable)throw new InvalidOperationException("Diamond film needs readable source thickness pattern");
  var frames=new Texture2D[FrameCount];
  for(int f=0;f<FrameCount;f++) {
   var pixels=new Color[Resolution*Resolution];
   for(int y=0;y<Resolution;y++)for(int x=0;x<Resolution;x++) {
    float nx=2*(x+.5f)/Resolution-1,ny=2*(y+.5f)/Resolution-1;
    float cosine=Mathf.Sqrt(Mathf.Max(0,1-nx*nx-ny*ny)),rim=Rim(cosine);
    var film=Film(pattern,nx,ny,cosine,f*Period/FrameCount);
    pixels[y*Resolution+x]=OpenBrushFireImport.DisplayEmission(new Color((paint.r+.25f)*film.r*rim,(paint.g+.25f)*film.g*rim,(paint.b+.25f)*film.b*rim,1));
   }
   var texture=new Texture2D(Resolution,Resolution,TextureFormat.RGBA32,true,true){name="DiamondFilm_"+paint+"_"+f,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
   texture.SetPixels(pixels);texture.Apply(true,false);frames[f]=texture;
  }
  return frames;
 }
 static IEnumerable<(Color color,int[] indices)> Colors(Mesh mesh) {
  var colors=mesh.colors;var indices=mesh.triangles;var groups=new Dictionary<Color,List<int>>();
  if(colors.Length!=mesh.vertexCount)throw new InvalidOperationException("Diamond requires source paint colors");
  for(int i=0;i<indices.Length;i+=3) {
   Color c=colors[indices[i]];
   if(c!=colors[indices[i+1]]||c!=colors[indices[i+2]])throw new InvalidOperationException("Diamond gradient triangles need a fragment-color bake");
   if(!groups.TryGetValue(c,out var list)){list=new List<int>();groups.Add(c,list);}
   list.Add(indices[i]);list.Add(indices[i+1]);list.Add(indices[i+2]);
  }
  return groups.Select(g=>(g.Key,g.Value.ToArray()));
 }
 public static void Prepare(GameObject root) {
  var inputs=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.sharedMaterial!=null&&Supports(r.sharedMaterial.GetTag("OpenBrushGuid",false,""))&&r.sharedMaterial.GetTag("OpenBrushDiamondPrepared",false,"")!="1")
   .Select(r=>new{renderer=r,mesh=r.GetComponent<MeshFilter>().sharedMesh,groups=Colors(r.GetComponent<MeshFilter>().sharedMesh).ToArray()}).ToArray();
  var unique=inputs.SelectMany(i=>i.groups.Select(g=>(i.renderer.sharedMaterial,g.color))).Distinct().Count();
  if((long)unique*Resolution*Resolution*FrameCount>33554432)throw new InvalidOperationException("Diamond film exceeds 32M frame pixels; split the sketch");
  var cache=new Dictionary<(Material,Color),(Material body,Material emission)>();
  var frameCache=new Dictionary<(Texture2D,Color),Texture2D[]>();
  foreach(var input in inputs) {
   var source=input.renderer.sharedMaterial;
   foreach(var group in input.groups) {
    var key=(source,group.color);
    if(!cache.TryGetValue(key,out var materials)) {
     var frameKey=(source.mainTexture as Texture2D,group.color);
     if(!frameCache.TryGetValue(frameKey,out var frames)){frames=Bake(frameKey.Item1,group.color);frameCache.Add(frameKey,frames);}
     var body=new Material(source){name="Diamond - Additive Surface",mainTexture=null};
     body.SetColor("_TintColor",new Color(.2f*group.color.r,.2f*group.color.g,.2f*group.color.b,1));
     body.SetOverrideTag("OpenBrushDiamondBody","1");body.SetOverrideTag("OpenBrushDiamondPrepared","1");
     var emission=new Material(source){name="Diamond - Animated Thin Film",mainTexture=frames[0]};
     emission.SetOverrideTag("OpenBrushViewEmission","1");emission.SetOverrideTag("OpenBrushDiamondPrepared","1");
     var clock=new GameObject("Diamond - Thin Film Motion");clock.transform.SetParent(root.transform,false);
     var motion=clock.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=emission;motion.Frames=frames;motion.FramesPerSecond=FrameCount/Period;motion.TextureScale=Vector2.one;
     materials=(body,emission);cache.Add(key,materials);
    }
    // Each original face and winding retained, including explicit membrane backs.
    var mesh=UnityEngine.Object.Instantiate(input.mesh);mesh.name=input.mesh.name+"_DiamondFilm";mesh.triangles=group.indices;mesh.RecalculateBounds();
    var child=new GameObject("Diamond Film");child.transform.SetParent(input.renderer.transform,false);
    child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=materials.emission;
    // Native PBS_Specular culls backs. Add reverse windings to emulate source Cull Off.
    var bodyMesh=UnityEngine.Object.Instantiate(mesh);var both=new int[group.indices.Length*2];group.indices.CopyTo(both,0);
    for(int j=0;j<group.indices.Length;j+=3){int k=j+group.indices.Length;both[k]=group.indices[j];both[k+1]=group.indices[j+2];both[k+2]=group.indices[j+1];}
    bodyMesh.triangles=both;
    var bodyChild=new GameObject("Diamond Surface");bodyChild.transform.SetParent(input.renderer.transform,false);
    bodyChild.AddComponent<MeshFilter>().sharedMesh=bodyMesh;bodyChild.AddComponent<MeshRenderer>().sharedMaterial=materials.body;
   }
   input.renderer.enabled=false;
  }
  if(inputs.Length>0)Debug.Log("OPENBRUSH_DIAMOND_FILM_PREPARED "+inputs.Length+" segments, "+cache.Count+" color sequences; view-normal approximation");
 }
}
