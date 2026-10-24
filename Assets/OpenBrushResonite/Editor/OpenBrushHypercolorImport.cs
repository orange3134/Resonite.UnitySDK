using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// Finite fragment-shader bake synchronized with the drawing timeline. UV charts
// retain interpolated vertex RGBA, including pressure/edge alpha. No short loop
// is substituted for these non-periodic shaders. HDR above the bounded range,
// sub-texel detail and arbitrary overlapping strokes remain approximations.
public static class OpenBrushHypercolorImport {
 public const string Guid="dce872c2-7b49-4684-b59b-c45387949c5c", Single="e8ef32b1-baa8-460a-9c2c-9cf8506794f5";
 public const int Width=128,Height=128,Padding=2,Fps=30;
 public static bool Supports(string guid)=>guid==Guid || guid==Single;
 sealed class Piece { public MeshRenderer renderer;public Mesh mesh;public int[] indices;public float[] copies;public Mesh chart; }
 sealed class Chart {public string key,guid;public List<Piece> pieces=new List<Piece>();public Vector2 min,max;public Vector2 origin;public Texture2D pattern;}
 static string Key(params float[] f)=>string.Join(",",f.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
 static Piece Read(MeshRenderer r) {
  var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;
  if(mesh==null || mesh.subMeshCount!=1 || mesh.GetTopology(0)!=MeshTopology.Triangles || mesh.uv.Length!=mesh.vertexCount || mesh.colors.Length!=mesh.vertexCount)
   throw new InvalidOperationException("Hypercolor bake needs a single triangle UV/RGBA mesh: "+r.name);
  return new Piece{renderer=r,mesh=mesh,indices=mesh.triangles,copies=Enumerable.Repeat(1f,mesh.triangles.Length/3).ToArray()};
 }
 public const float DiffuseRange=32;
 public static void Prepare(GameObject root) { PrepareSamples(root,null,1); }
 // Also used for fixed-time resolution probes. Explicit sample times do not
 // imply a seamless loop; callers that serialize a timeline must cover it.
 public static void PrepareSamples(GameObject root,float[] sampleTimes,int resolutionScale) {
  var targets=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null && Supports(r.sharedMaterial.GetTag("OpenBrushGuid",false,""))).ToArray();
  if(targets.Length==0)return;
  if(root.transform.Find("Hypercolor Shader Timeline")!=null)throw new InvalidOperationException("Hypercolor already prepared");
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || float.IsNaN(timeline.Duration) || float.IsInfinity(timeline.Duration) || timeline.Duration<=0)throw new InvalidOperationException("Hypercolor brushes currently require a finite drawing timeline");
  var shader=Shader.Find("Hidden/OpenBrush/HypercolorBake");if(shader==null || !shader.isSupported)throw new InvalidOperationException("Hypercolor bake requires a graphics-capable Editor");
  var charts=new Dictionary<string,Chart>();
  foreach(var r in targets) {
   var match=Regex.Match(r.name,@"^(.*_stroke_\d+)_part_\d+$");
   if(!match.Success)throw new InvalidOperationException("Hypercolor requires generated stroke/part identity: "+r.name);
   string key=match.Groups[1].Value,guid=r.sharedMaterial.GetTag("OpenBrushGuid",false,"");
   if(!charts.TryGetValue(key,out var chart)){chart=new Chart{key=key,guid=guid};charts.Add(key,chart);}
   if(chart.guid!=guid || r.sharedMaterial.mainTextureScale!=Vector2.one || r.sharedMaterial.mainTextureOffset!=Vector2.zero)throw new InvalidOperationException("Ambiguous Hypercolor chart or unsupported source UV transform");
   chart.pieces.Add(Read(r));
  }
  int columns=Mathf.CeilToInt(Mathf.Sqrt(charts.Count)),rows=Mathf.CeilToInt(charts.Count/(float)columns);
  if(resolutionScale<1 || resolutionScale>8)throw new ArgumentOutOfRangeException(nameof(resolutionScale));
  if(sampleTimes!=null && (sampleTimes.Length<2 || sampleTimes.Any(t=>float.IsNaN(t) || float.IsInfinity(t) || t<0 || t>timeline.Duration) || sampleTimes.Zip(sampleTimes.Skip(1),(a,b)=>b>a).Any(ok=>!ok)))throw new ArgumentException("Expected at least two increasing finite sample times within the timeline");
  // Thirty samples per second for the normal non-audio color evolution.
  int fps=Fps;
  int width=columns*(Width+2*Padding),height=rows*(Height+2*Padding),count=sampleTimes?.Length ?? (Mathf.CeilToInt(timeline.Duration*fps)+1);
  int textureWidth=width*resolutionScale,textureHeight=height*resolutionScale;
  if(textureWidth>SystemInfo.maxTextureSize || textureHeight>SystemInfo.maxTextureSize || (long)textureWidth*textureHeight*(count*2L+1)>134217728)
   throw new InvalidOperationException("Hypercolor bake exceeds 128M frame pixels; split the sketch or shorten the timeline");
  var frames=new Texture2D[count];var specFrames=new Texture2D[count];Texture2D normalAtlas=null;var times=new float[count];var bake=new Material(shader);
  
  var rt=new RenderTexture(textureWidth,textureHeight,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){filterMode=FilterMode.Bilinear};
  var pattern=Load("Hypercolor_BaseMap");var normal=Load("Hypercolor_Normal");
  bake.SetTexture("_MainTex",pattern);bake.SetTexture("_Bump",normal);
  var old=RenderTexture.active;bool oldSrgb=GL.sRGBWrite;int chartIndex=0;
  try {
   rt.Create();
   foreach(var chart in charts.Values) {
    var uvs=chart.pieces.SelectMany(p=>p.mesh.uv).ToArray();chart.min=new Vector2(uvs.Min(u=>u.x),uvs.Min(u=>u.y));chart.max=new Vector2(uvs.Max(u=>u.x),uvs.Max(u=>u.y));
    if(chart.max.x-chart.min.x<1e-6 || chart.max.y-chart.min.y<1e-6)throw new InvalidOperationException("Degenerate Hypercolor UV chart");
    chart.origin=new Vector2((chartIndex%columns)*(Width+2*Padding)+Padding,(chartIndex/columns)*(Height+2*Padding)+Padding);chartIndex++;
    foreach(var piece in chart.pieces) {
     var uv=piece.mesh.uv;var colors=piece.mesh.colors;var pos=new Vector3[piece.indices.Length];var srcUv=new Vector2[pos.Length];var copies=new Vector2[pos.Length];var rgba=new Color[pos.Length];
     for(int i=0;i<pos.Length;i++) {int v=piece.indices[i];var q=AtlasUV(chart,uv[v],width,height);pos[i]=new Vector3(q.x,q.y,0);srcUv[i]=uv[v];rgba[i]=colors[v];copies[i]=new Vector2(piece.copies[i/3],0);}
     piece.chart=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=pos,uv=srcUv,uv2=copies,colors=rgba,triangles=Enumerable.Range(0,pos.Length).ToArray()};
    }
   }
   for(int channel=0;channel<3;channel++)for(int f=0;f<(channel==2?1:count);f++) {
    bake.SetFloat("_Output",channel);
    times[f]=sampleTimes==null?Mathf.Min(f/(float)fps,timeline.Duration):sampleTimes[f];RenderTexture.active=rt;GL.sRGBWrite=false;GL.Clear(false,true,channel==0?Color.clear:channel==1?new Color(0,0,0,.4f):new Color(.5f,.5f,1,1));
    bake.SetFloat("_Seconds",times[f]);
    foreach(var chart in charts.Values) {
     if(!bake.SetPass(0) || UnityEditor.ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Hypercolor shader compilation failed");
     foreach(var piece in chart.pieces)Graphics.DrawMeshNow(piece.chart,Matrix4x4.identity);
    }
    var tex=new Texture2D(textureWidth,textureHeight,TextureFormat.RGBA32,false,channel==2){name="Hypercolor_"+channel+"_"+f,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
    tex.ReadPixels(new Rect(0,0,textureWidth,textureHeight),0,0,false);tex.Apply(false,false);if(channel==0)frames[f]=tex;else if(channel==1)specFrames[f]=tex;else normalAtlas=tex;
   }
  } catch {foreach(var frame in frames.Concat(specFrames).Concat(new[]{normalAtlas}))if(frame!=null)UnityEngine.Object.DestroyImmediate(frame);throw;}
  finally {UnityEngine.Object.DestroyImmediate(pattern);UnityEngine.Object.DestroyImmediate(normal);RenderTexture.active=old;GL.sRGBWrite=oldSrgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(bake);foreach(var chart in charts.Values){if(chart.pattern!=null)UnityEngine.Object.DestroyImmediate(chart.pattern);foreach(var piece in chart.pieces)if(piece.chart!=null)UnityEngine.Object.DestroyImmediate(piece.chart);}}
  // Diffuse and specular share the drawing clock, with a static normal atlas.
  var material=new Material(targets[0].sharedMaterial){name="Hypercolor - Timed Fragment Atlas",mainTexture=frames[0],mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};material.SetColor("_TintColor",Color.white);
  material.SetOverrideTag("OpenBrushHypercolorAtlas","1");material.SetTexture("_MaskTex",specFrames[0]);material.SetTexture("_BumpMap",normalAtlas);
  var driver=new GameObject("Hypercolor Shader Timeline");driver.transform.SetParent(root.transform,false);
  var motion=driver.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FramesPerSecond=fps;motion.Timeline=timeline;motion.FrameTimes=times;
  var specDriver=new GameObject("Hypercolor Specular Timeline");specDriver.transform.SetParent(root.transform,false);
  var spec=specDriver.AddComponent<OpenBrushTextureMotion>();spec.SourceMaterial=material;spec.Frames=specFrames;spec.FramesPerSecond=fps;spec.Timeline=timeline;spec.FrameTimes=times;spec.SpecularOnly=true;
  foreach(var chart in charts.Values)foreach(var piece in chart.pieces) {
   var mesh=UnityEngine.Object.Instantiate(piece.mesh);mesh.name=piece.mesh.name+"_HypercolorChart";mesh.triangles=piece.indices;
   mesh.uv=mesh.uv.Select(u=>AtlasUV(chart,u,width,height)).ToArray();mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();if(mesh.tangents.Length!=mesh.vertexCount)mesh.RecalculateTangents();
   var child=new GameObject("Timed Hypercolor Surface");child.transform.SetParent(piece.renderer.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;piece.renderer.enabled=false;
  }
  Debug.Log("HYPERCOLOR_BAKE charts="+charts.Count+" frames="+count+" atlas="+textureWidth+"x"+textureHeight+" duration="+timeline.Duration);
 }
 static Texture2D Load(string name){var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};if(!t.LoadImage(System.IO.File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/"+name+".png")))throw new InvalidOperationException("Missing normal Hypercolor texture");return t;}
 static Vector2 AtlasUV(Chart c,Vector2 uv,int width,int height) {var t=(uv-c.min)/(c.max-c.min);return new Vector2((c.origin.x+.5f+t.x*(Width-1))/width,(c.origin.y+.5f+t.y*(Height-1))/height);}
}




