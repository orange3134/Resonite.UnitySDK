using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Non-audio curl motion. Preserve triangle orientation/size (not billboards).
// Mesh frames are finite and repeated; audio/phase at export is not recorded.
public static class OpenBrushWaveformParticleImport {
 public const string Guid="6a1cf9f9-032c-45ec-9b6e-a6680bee30f7";
 public const int Fps=30;
 public static void Prepare(GameObject root,OpenBrushElectricityImport.Data data,OpenBrushGrowthImport.Data timing) {
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || timing?.strokes==null || data==null || data.schemaVersion!=1 || data.brushGuid!=Guid || data.coordinateSystem!="UnityInvertZ" || data.parts==null || data.parts.Length==0)throw new InvalidOperationException("WaveformParticles needs original offsets and timing");
  int count=Mathf.CeilToInt(timeline.Duration*Fps)+1;
  if(data.parts.Sum(p=>(double)p.positions.Length*count)>10000000)throw new InvalidOperationException("WaveformParticles exceeds 10M frame vertices; split the sketch");
  var transforms=root.GetComponentsInChildren<Transform>(true);
  foreach(var p in data.parts) {
   int n=p.positions.Length;
   if(n==0 || p.offsets?.Length!=n || p.colors?.Length!=n || p.uv?.Length!=n || p.times?.Length!=n || p.triangles==null || p.triangles.Length%3!=0 || p.triangles.Any(i=>i<0||i>=n))throw new InvalidOperationException("Invalid WaveformParticles data");
   var parent=transforms.Single(t=>t.name==p.nodeName);var originals=parent.GetComponentsInChildren<MeshRenderer>(true);
   if(originals.Length==0 || originals.Any(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=Guid))throw new InvalidOperationException("WaveformParticles binding mismatch");
   var strokes=timing.strokes.Where(s=>s.name==p.nodeName).ToArray();
   var groups=OpenBrushDiscoGrowth.MatchStrokes(new OpenBrushDiscoImport.Part{positions=p.positions,times=p.times,strokeStarts=p.strokeStarts,strokeEnds=p.strokeEnds,triangles=p.triangles},strokes,timeline.Duration);
   var births=Enumerable.Range(0,n).Select(i=>timing.completed?-10:OpenBrushGrowthImport.Birth(timing,strokes[groups[i]],p.times[i])).ToArray();
   var colors=p.colors.Distinct().ToArray();var source=originals[0].sharedMaterial.mainTexture as Texture2D;
   if(source==null || !source.isReadable || (long)source.width*source.height*colors.Length>16777216)throw new InvalidOperationException("Missing or oversized particle sprite atlas");
   var tiles=colors.Select(c=>OpenBrushAdditiveImport.Bake(source,c,1)).ToArray();
   var atlas=new Texture2D(2,2,TextureFormat.RGBA32,true,true){name="WaveformParticles_GammaSprites",wrapMode=TextureWrapMode.Clamp};
   var rects=atlas.PackTextures(tiles,4,4096,false);
   var uv=p.uv.Select((u,i)=>{var rect=rects[Array.IndexOf(colors,p.colors[i])];return new Vector2(Mathf.Lerp(rect.xMin+.5f/atlas.width,rect.xMax-.5f/atlas.width,u.x),Mathf.Lerp(rect.yMin+.5f/atlas.height,rect.yMax-.5f/atlas.height,u.y));}).ToArray();
   foreach(var tile in tiles)UnityEngine.Object.DestroyImmediate(tile);
   var material=new Material(originals[0].sharedMaterial){name="WaveformParticles - Original Curl",mainTexture=atlas};material.SetColor("_TintColor",Color.white);
   var frames=new Mesh[count];var times=new float[count];
   using(var deformer=new Deformer(p,births))for(int f=0;f<count;f++) {
    times[f]=Mathf.Min(f/(float)Fps,timeline.Duration);var positions=deformer.Evaluate(times[f]);
    var triangles=new List<int>();for(int j=0;j<p.triangles.Length;j+=3)if(births[p.triangles[j]]<=times[f]){triangles.Add(p.triangles[j]);triangles.Add(p.triangles[j+1]);triangles.Add(p.triangles[j+2]);}
    if(triangles.Count==0)triangles.AddRange(new[]{0,0,0});
    var mesh=new Mesh{name="WaveformParticles_"+f,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=positions,uv=uv,colors=Enumerable.Repeat(Color.white,n).ToArray(),normals=Enumerable.Repeat(Vector3.forward,n).ToArray(),triangles=triangles.ToArray()};mesh.RecalculateBounds();frames[f]=mesh;
   }
   var child=new GameObject("WaveformParticles - Curl Motion");child.transform.SetParent(parent,false);var motion=child.AddComponent<OpenBrushGeometryMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FrameTimes=times;motion.Timeline=timeline;
   foreach(var renderer in originals)renderer.enabled=false;
   Debug.Log("WAVEFORM_PARTICLES_BAKE frames="+count+" vertices="+n+" colors="+colors.Length);
  }
 }
 public sealed class Deformer:IDisposable {
  readonly Material material;readonly Texture2D positions,offsets,paint,readback;readonly RenderTexture target;readonly int count;
  public Deformer(OpenBrushElectricityImport.Part part,float[] births) {
   var shader=Shader.Find("Hidden/OpenBrush/WaveformParticleDeform");if(shader==null || !shader.isSupported)throw new InvalidOperationException("WaveformParticles requires graphics");
   material=new Material(shader);count=part.positions.Length;int width=Mathf.Min(4096,Mathf.NextPowerOfTwo(count)),height=Mathf.CeilToInt(count/(float)width);
   positions=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point};offsets=UnityEngine.Object.Instantiate(positions);paint=UnityEngine.Object.Instantiate(positions);
   var p=new Color[width*height];var o=new Color[p.Length];var c=new Color[p.Length];for(int i=0;i<count;i++){var v=part.positions[i];var d=part.offsets[i];p[i]=new Color(v.x,v.y,v.z,births[i]);o[i]=new Color(d.x,d.y,d.z,0);c[i]=part.colors[i];}
   positions.SetPixels(p);positions.Apply();offsets.SetPixels(o);offsets.Apply();paint.SetPixels(c);paint.Apply();material.SetTexture("_Offsets",offsets);material.SetTexture("_Paint",paint);
   target=new RenderTexture(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();readback=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true);
  }
  public Vector3[] Evaluate(float seconds) {
   var old=RenderTexture.active;bool srgb=GL.sRGBWrite;
   try {material.SetFloat("_Seconds",seconds);GL.sRGBWrite=false;Graphics.Blit(positions,target,material);if(ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Waveform particle shader failed");RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);readback.Apply();return readback.GetPixels().Take(count).Select(c=>new Vector3(c.r,c.g,c.b)).ToArray();}
   finally {RenderTexture.active=old;GL.sRGBWrite=srgb;}
  }
  public void Dispose(){target.Release();foreach(var obj in new UnityEngine.Object[]{material,positions,offsets,paint,readback,target})UnityEngine.Object.DestroyImmediate(obj);}
 }
}
