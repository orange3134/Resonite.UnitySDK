using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// Finite fragment-shader bake synchronized with the drawing timeline. UV charts
// retain interpolated vertex RGBA, including pressure/edge alpha. No short loop
// is substituted for this non-periodic shader. Bounded native bloom, texture quantization,
// sub-texel detail and arbitrary overlapping strokes remain approximations.
public static class OpenBrushWaveformImport {
 public const string Guid="10201aa3-ebc2-42d8-84b7-2e63f6eeb8ab";
 // Calibrated native Unlit bloom ceiling; source HDR can be much higher.
 public const float Ceiling=32;
 public const int Width=256,Height=128,Padding=2,Fps=60;
 public static bool Supports(string guid)=>guid==Guid;
 sealed class Piece { public MeshRenderer renderer;public Mesh mesh;public int[] indices;public float[] copies;public Mesh chart; }
 sealed class Chart {public string key,guid;public List<Piece> pieces=new List<Piece>();public Vector2 min,max;public Vector2 origin,size;}
 static string Key(params float[] f)=>string.Join(",",f.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
 static Piece Read(MeshRenderer r) {
  var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;
  if(mesh==null || mesh.subMeshCount!=1 || mesh.GetTopology(0)!=MeshTopology.Triangles || mesh.uv.Length!=mesh.vertexCount || mesh.colors.Length!=mesh.vertexCount)
   throw new InvalidOperationException("Waveform bake needs a single triangle UV/RGBA mesh: "+r.name);
  var p=mesh.vertices;var u=mesh.uv;var c=mesh.colors;
  var keys=Enumerable.Range(0,p.Length).Select(i=>Key(p[i].x,p[i].y,p[i].z,u[i].x,u[i].y,c[i].r,c[i].g,c[i].b,c[i].a)).ToArray();
  var unique=new Dictionary<string,List<int[]>>();var tri=mesh.triangles;
  for(int i=0;i<tri.Length;i+=3) {
   var t=new[]{tri[i],tri[i+1],tri[i+2]};var key=string.Join(";",t.Select(v=>keys[v]).OrderBy(v=>v,StringComparer.Ordinal));
   if(!unique.TryGetValue(key,out var list)){list=new List<int[]>();unique.Add(key,list);}list.Add(t);
  }
  return new Piece{renderer=r,mesh=mesh,indices=unique.Values.SelectMany(v=>v[0]).ToArray(),copies=unique.Values.Select(v=>(float)v.Count).ToArray()};
 }
 public static void Prepare(GameObject root) { PrepareSamples(root,null,1); }
 // Also used for fixed-time resolution probes. Explicit sample times do not
 // imply a seamless loop; callers that serialize a timeline must cover it.
 public static void PrepareSamples(GameObject root,float[] sampleTimes,int resolutionScale) {
  var targets=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null && Supports(r.sharedMaterial.GetTag("OpenBrushGuid",false,""))).ToArray();
  if(targets.Length==0)return;
  if(root.transform.Find("Waveform Shader Timeline")!=null)throw new InvalidOperationException("Waveform already prepared");
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || float.IsNaN(timeline.Duration) || float.IsInfinity(timeline.Duration) || timeline.Duration<=0)throw new InvalidOperationException("Waveform brushes currently require a finite drawing timeline");
  var shader=Shader.Find("Hidden/OpenBrush/WaveformBake");if(shader==null || !shader.isSupported)throw new InvalidOperationException("Waveform bake requires a graphics-capable Editor");
  var charts=new Dictionary<string,Chart>();
  foreach(var r in targets) {
   var match=Regex.Match(r.name,@"^(.*_stroke_\d+)_part_\d+$");
   if(!match.Success)throw new InvalidOperationException("Waveform requires generated stroke/part identity: "+r.name);
   string key=match.Groups[1].Value,guid=r.sharedMaterial.GetTag("OpenBrushGuid",false,"");
   if(!charts.TryGetValue(key,out var chart)){chart=new Chart{key=key,guid=guid};charts.Add(key,chart);}
   if(chart.guid!=guid || r.sharedMaterial.mainTextureScale!=Vector2.one || r.sharedMaterial.mainTextureOffset!=Vector2.zero)throw new InvalidOperationException("Ambiguous waveform chart or unsupported source UV transform");
   chart.pieces.Add(Read(r));
  }
  int columns=Mathf.CeilToInt(Mathf.Sqrt(charts.Count)),rows=Mathf.CeilToInt(charts.Count/(float)columns);
  if(resolutionScale<1 || resolutionScale>8)throw new ArgumentOutOfRangeException(nameof(resolutionScale));
  if(sampleTimes!=null && (sampleTimes.Length<2 || sampleTimes.Any(t=>float.IsNaN(t) || float.IsInfinity(t) || t<0 || t>timeline.Duration) || sampleTimes.Zip(sampleTimes.Skip(1),(a,b)=>b>a).Any(ok=>!ok)))throw new ArgumentException("Expected at least two increasing finite sample times within the timeline");
  // Source color-dependent blue oscillation can exceed 20 Hz; use 60 fps.
  int fps=Fps;
  int count=sampleTimes?.Length ?? (Mathf.CeilToInt(timeline.Duration*fps)+1);
  var layout=OpenBrushAtlasBudget.Select(Width,Height,Padding,columns,rows,count,resolutionScale,SystemInfo.maxTextureSize,sampleTimes==null && resolutionScale==1);
  OpenBrushAtlasBudget.Record("Waveform",layout,Width,Height,count);
  int width=layout.Width,height=layout.Height,textureWidth=layout.TextureWidth,textureHeight=layout.TextureHeight;
  var frames=new Texture2D[count];var times=new float[count];var bake=new Material(shader);
  bake.SetFloat("_Ceiling",Ceiling);
  var rt=new RenderTexture(textureWidth,textureHeight,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){filterMode=FilterMode.Bilinear};
  var old=RenderTexture.active;bool oldSrgb=GL.sRGBWrite;int chartIndex=0;
  try {
   rt.Create();
   foreach(var chart in charts.Values) {
    var uvs=chart.pieces.SelectMany(p=>p.mesh.uv).ToArray();chart.min=new Vector2(uvs.Min(u=>u.x),uvs.Min(u=>u.y));chart.max=new Vector2(uvs.Max(u=>u.x),uvs.Max(u=>u.y));
    if(chart.max.x-chart.min.x<1e-6 || chart.max.y-chart.min.y<1e-6)throw new InvalidOperationException("Degenerate waveform UV chart");
    chart.size=new Vector2(layout.TileWidth,layout.TileHeight);chart.origin=new Vector2((chartIndex%columns)*(layout.TileWidth+2*Padding)+Padding,(chartIndex/columns)*(layout.TileHeight+2*Padding)+Padding);chartIndex++;

    foreach(var piece in chart.pieces) {
     var uv=piece.mesh.uv;var colors=piece.mesh.colors;var pos=new Vector3[piece.indices.Length];var srcUv=new Vector2[pos.Length];var copies=new Vector2[pos.Length];var rgba=new Color[pos.Length];
     for(int i=0;i<pos.Length;i++) {int v=piece.indices[i];var q=AtlasUV(chart,uv[v],width,height);pos[i]=new Vector3(q.x,q.y,0);srcUv[i]=uv[v];rgba[i]=colors[v];copies[i]=new Vector2(piece.copies[i/3],0);}
     piece.chart=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=pos,uv=srcUv,uv2=copies,colors=rgba,triangles=Enumerable.Range(0,pos.Length).ToArray()};
    }
   }
   for(int f=0;f<count;f++) {
    times[f]=sampleTimes==null?Mathf.Min(f/(float)fps,timeline.Duration):sampleTimes[f];RenderTexture.active=rt;GL.sRGBWrite=false;GL.Clear(false,true,new Color(0,0,0,1));
    bake.SetFloat("_Seconds",times[f]);
    foreach(var chart in charts.Values) {
     
     if(!bake.SetPass(0) || UnityEditor.ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Waveform shader compilation failed");
     foreach(var piece in chart.pieces)Graphics.DrawMeshNow(piece.chart,Matrix4x4.identity);
    }
    var tex=new Texture2D(textureWidth,textureHeight,TextureFormat.RGBA32,false,false){name="WaveformTimeline_"+f,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
    tex.ReadPixels(new Rect(0,0,textureWidth,textureHeight),0,0,false);tex.Apply(false,false);frames[f]=tex;
   }
  } catch {foreach(var frame in frames)if(frame!=null)UnityEngine.Object.DestroyImmediate(frame);throw;}
  finally {RenderTexture.active=old;GL.sRGBWrite=oldSrgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(bake);foreach(var chart in charts.Values){foreach(var piece in chart.pieces)if(piece.chart!=null)UnityEngine.Object.DestroyImmediate(piece.chart);}}
  // One atlas sequence and one native clock for all strokes and colors.
  var material=new Material(targets[0].sharedMaterial){name="Waveform - Timed Fragment Atlas",mainTexture=frames[0],mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};material.SetColor("_TintColor",new Color(Ceiling,Ceiling,Ceiling,1).gamma);
  material.SetOverrideTag("OpenBrushBoundedWaveformAtlas","1");
  var driver=new GameObject("Waveform Shader Timeline");driver.transform.SetParent(root.transform,false);
  var motion=driver.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FramesPerSecond=fps;motion.Timeline=timeline;motion.FrameTimes=times;
  foreach(var chart in charts.Values)foreach(var piece in chart.pieces) {
   var mesh=UnityEngine.Object.Instantiate(piece.mesh);mesh.name=piece.mesh.name+"_WaveformChart";mesh.triangles=piece.indices;
   mesh.uv=mesh.uv.Select(u=>AtlasUV(chart,u,width,height)).ToArray();mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
   var child=new GameObject("Timed Waveform Fragments");child.transform.SetParent(piece.renderer.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;piece.renderer.enabled=false;
  }
  Debug.Log("WAVEFORM_BAKE charts="+charts.Count+" frames="+count+" atlas="+textureWidth+"x"+textureHeight+" duration="+timeline.Duration);
 }
 static Vector2 AtlasUV(Chart c,Vector2 uv,int width,int height) {var t=(uv-c.min)/(c.max-c.min);return new Vector2((c.origin.x+.5f+t.x*(c.size.x-1))/width,(c.origin.y+.5f+t.y*(c.size.y-1))/height);}
}

