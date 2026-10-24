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
public static class OpenBrushFlowImport {
 public const string Fairy="d9cc5e99-ace1-4d12-96e0-4a7c18c99cfc", Space="bdf65db2-1fb7-4202-b5e0-c6b5e3ea851e";
 public const string Plasma="c33714d1-b2f9-412e-bd50-1884c9d46336", Streamers="44bb800a-fbc3-4592-8426-94ecb05ddec3";
 public const string ChromaticWave="0f0ff7b2-a677-45eb-a7d6-0cd7206f4816";
 public const string Rainbow="ad1ad437-76e2-450d-a23a-e17f8310b960";
 public const string Wind="7136a729-1aab-bd24-f8b2-ca88b6adfb67", Rain="03a529e1-f519-3dd4-582d-2d5cd92c3f4f";
 public const string Fire2="53d753ef-083c-45e1-98e7-4459b4471219", Sparks="50e99447-3861-05f4-697d-a1b96e771b98";
 public const int Width=128,Height=128,Padding=2,Fps=30;
 public static bool Supports(string guid)=>guid==Plasma || guid==Streamers || guid==ChromaticWave || guid==Rainbow || guid==Wind || guid==Rain || guid==Fire2 || guid==Sparks || guid==Fairy || guid==Space;
 static int Range(string guid)=>guid==Fairy || guid==Space?2:guid==Fire2 || guid==Sparks?1:0;
 sealed class Piece { public MeshRenderer renderer;public Mesh mesh;public int[] indices;public float[] copies;public Vector4[] bloom;public Mesh chart; }
 sealed class Chart {public string key,guid;public List<Piece> pieces=new List<Piece>();public Vector2 min,max;public Vector2 origin,size;public Texture2D pattern,displacement;}
 static string Key(params float[] f)=>string.Join(",",f.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
 static Piece Read(MeshRenderer r) {
  var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;
  if(mesh==null || mesh.subMeshCount!=1 || mesh.GetTopology(0)!=MeshTopology.Triangles || mesh.uv.Length!=mesh.vertexCount || mesh.colors.Length!=mesh.vertexCount)
   throw new InvalidOperationException("Flow bake needs a single triangle UV/RGBA mesh: "+r.name);
  var p=mesh.vertices;var u=mesh.uv;var c=mesh.colors;
  string guid=r.sharedMaterial.GetTag("OpenBrushGuid",false,"");
  var bloom=guid==ChromaticWave || guid==Fire2?OpenBrushSourceBloom.Read(mesh):new Vector4[mesh.vertexCount];
  var keys=Enumerable.Range(0,p.Length).Select(i=>Key(p[i].x,p[i].y,p[i].z,u[i].x,u[i].y,c[i].r,c[i].g,c[i].b,c[i].a,bloom[i].x,bloom[i].y,bloom[i].z,bloom[i].w)).ToArray();
  var unique=new Dictionary<string,List<int[]>>();var tri=mesh.triangles;
  for(int i=0;i<tri.Length;i+=3) {
   var t=new[]{tri[i],tri[i+1],tri[i+2]};var key=string.Join(";",t.Select(v=>keys[v]).OrderBy(v=>v,StringComparer.Ordinal));
   if(!unique.TryGetValue(key,out var list)){list=new List<int[]>();unique.Add(key,list);}list.Add(t);
  }
  return new Piece{renderer=r,mesh=mesh,bloom=bloom,indices=unique.Values.SelectMany(v=>v[0]).ToArray(),copies=unique.Values.Select(v=>(float)v.Count).ToArray()};
 }
 public const float StreamersCeiling=4;
 // Resonite bloom spreads raw source HDR excessively. This display ceiling
 // retains the original procedural colors below 2; raw probes can opt out.
 public const float CelestialCeiling=2;
 public static void Prepare(GameObject root) {
  // Separate ranges keep mixed sketches from changing existing brush gains.
  PrepareSamples(root,null,1,StreamersCeiling,0);
  PrepareSamples(root,null,1,16,1);
  PrepareSamples(root,null,1,1,2);
 }
 // Also used for fixed-time resolution probes. Explicit sample times do not
 // imply a seamless loop; callers that serialize a timeline must cover it.
 public static void PrepareSamples(GameObject root,float[] sampleTimes,int resolutionScale,float streamersCeiling=1,float celestialCeiling=CelestialCeiling) {
  for(int group=0;group<3;group++)PrepareSamples(root,sampleTimes,resolutionScale,group==2?1:streamersCeiling,group,celestialCeiling);
 }
 static void PrepareSamples(GameObject root,float[] sampleTimes,int resolutionScale,float streamersCeiling,int group,float celestialCeiling=CelestialCeiling) {
  var targets=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>{
   if(r.sharedMaterial==null)return false;
   string g=r.sharedMaterial.GetTag("OpenBrushGuid",false,"");
   return Supports(g) && Range(g)==group;
  }).ToArray();
  if(targets.Length==0)return;
  if(!float.IsFinite(celestialCeiling) || celestialCeiling<1 || celestialCeiling>65504)throw new ArgumentOutOfRangeException(nameof(celestialCeiling));
  bool hdr=group==2;
  string driverName=hdr?"Fairy and Space Shader Timeline":group==1?"Fire and Sparks Shader Timeline":"Flow Shader Timeline";
  if(root.transform.Find(driverName)!=null)throw new InvalidOperationException("Flow already prepared");
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || float.IsNaN(timeline.Duration) || float.IsInfinity(timeline.Duration) || timeline.Duration<=0)throw new InvalidOperationException("Flow brushes currently require a finite drawing timeline");
  var shader=Shader.Find("Hidden/OpenBrush/FlowBake");if(shader==null || !shader.isSupported)throw new InvalidOperationException("Flow bake requires a graphics-capable Editor");
  var charts=new Dictionary<string,Chart>();
  foreach(var r in targets) {
   var match=Regex.Match(r.name,@"^(.*_stroke_\d+)_part_\d+$");
   if(!match.Success)throw new InvalidOperationException("Flow requires generated stroke/part identity: "+r.name);
   string key=match.Groups[1].Value,guid=r.sharedMaterial.GetTag("OpenBrushGuid",false,"");
   if(!charts.TryGetValue(key,out var chart)){chart=new Chart{key=key,guid=guid};charts.Add(key,chart);}
   if(chart.guid!=guid || r.sharedMaterial.mainTextureScale!=Vector2.one || r.sharedMaterial.mainTextureOffset!=Vector2.zero)throw new InvalidOperationException("Ambiguous flow chart or unsupported source UV transform");
   chart.pieces.Add(Read(r));
  }
  int columns=Mathf.CeilToInt(Mathf.Sqrt(charts.Count)),rows=Mathf.CeilToInt(charts.Count/(float)columns);
  if(resolutionScale<1 || resolutionScale>8)throw new ArgumentOutOfRangeException(nameof(resolutionScale));
  if(float.IsNaN(streamersCeiling) || streamersCeiling<1 || streamersCeiling>16)throw new ArgumentOutOfRangeException(nameof(streamersCeiling));
  if(sampleTimes!=null && (sampleTimes.Length<2 || sampleTimes.Any(t=>float.IsNaN(t) || float.IsInfinity(t) || t<0 || t>timeline.Duration) || sampleTimes.Zip(sampleTimes.Skip(1),(a,b)=>b>a).Any(ok=>!ok)))throw new ArgumentException("Expected at least two increasing finite sample times within the timeline");
  // ChromaticWave blue can exceed 15 Hz; 60 fps reduces temporal aliasing.
  int fps=charts.Values.Any(c=>c.guid==ChromaticWave)?60:Fps;
  int count=sampleTimes?.Length ?? (Mathf.CeilToInt(timeline.Duration*fps)+1);
  var layout=OpenBrushAtlasBudget.Select(Width,Height,Padding,columns,rows,count,resolutionScale,SystemInfo.maxTextureSize,sampleTimes==null && resolutionScale==1);
  OpenBrushAtlasBudget.Record("Flow",layout,Width,Height,count);
  int width=layout.Width,height=layout.Height,textureWidth=layout.TextureWidth,textureHeight=layout.TextureHeight;
  var frames=new Texture2D[count];var times=new float[count];var bake=new Material(shader);
  bake.SetFloat("_TextureGain",streamersCeiling);
  bake.SetFloat("_CelestialCeiling",celestialCeiling);
  var rt=new RenderTexture(textureWidth,textureHeight,0,(hdr?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32),RenderTextureReadWrite.Linear){filterMode=FilterMode.Bilinear};
  var old=RenderTexture.active;bool oldSrgb=GL.sRGBWrite;int chartIndex=0;
  try {
   rt.Create();
   foreach(var chart in charts.Values) {
    var uvs=chart.pieces.SelectMany(p=>p.mesh.uv).ToArray();chart.min=new Vector2(uvs.Min(u=>u.x),uvs.Min(u=>u.y));chart.max=new Vector2(uvs.Max(u=>u.x),uvs.Max(u=>u.y));
    if(chart.max.x-chart.min.x<1e-6 || chart.max.y-chart.min.y<1e-6)throw new InvalidOperationException("Degenerate flow UV chart");
    chart.size=new Vector2(layout.TileWidth,layout.TileHeight);chart.origin=new Vector2((chartIndex%columns)*(layout.TileWidth+2*Padding)+Padding,(chartIndex/columns)*(layout.TileHeight+2*Padding)+Padding);chartIndex++;
    chart.pattern=new Texture2D(2,2,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
    if(chart.guid!=Fairy && chart.guid!=Space && chart.guid!=ChromaticWave && chart.guid!=Rainbow && !chart.pattern.LoadImage(System.IO.File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/"+(chart.guid==Plasma?"Plasma":chart.guid==Wind?"Wind":chart.guid==Rain?"Rain":chart.guid==Fire2?"Fire2":chart.guid==Sparks?"Sparks":"Streamers")+".png")))throw new InvalidOperationException("Missing normal flow pattern");
    if(chart.guid==Fire2) {
     chart.displacement=new Texture2D(2,2,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
     if(!chart.displacement.LoadImage(System.IO.File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/Fire2Displacement.png")))throw new InvalidOperationException("Missing Fire2 displacement texture");
    }
    foreach(var piece in chart.pieces) {
     var uv=piece.mesh.uv;var colors=piece.mesh.colors;var pos=new Vector3[piece.indices.Length];var srcUv=new Vector2[pos.Length];var copies=new Vector2[pos.Length];var rgba=new Color[pos.Length];var bloom=new Vector4[pos.Length];
     for(int i=0;i<pos.Length;i++) {int v=piece.indices[i];var q=AtlasUV(chart,uv[v],width,height);pos[i]=new Vector3(q.x,q.y,0);srcUv[i]=uv[v];rgba[i]=colors[v];copies[i]=new Vector2(piece.copies[i/3],0);bloom[i]=piece.bloom[v];}
     piece.chart=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=pos,uv=srcUv,uv2=copies,colors=rgba,triangles=Enumerable.Range(0,pos.Length).ToArray()};
     piece.chart.SetUVs(2,bloom);
    }
   }
   for(int f=0;f<count;f++) {
    times[f]=sampleTimes==null?Mathf.Min(f/(float)fps,timeline.Duration):sampleTimes[f];RenderTexture.active=rt;GL.sRGBWrite=false;GL.Clear(false,true,new Color(0,0,0,1));
    bake.SetFloat("_Seconds",times[f]);
    foreach(var chart in charts.Values) {
     bake.SetTexture("_MainTex",chart.pattern);bake.SetTexture("_DisplaceTex",chart.displacement);bake.SetFloat("_Kind",chart.guid==Plasma?0:chart.guid==Streamers?1:chart.guid==ChromaticWave?2:chart.guid==Rainbow?3:chart.guid==Wind?4:chart.guid==Rain?5:chart.guid==Fire2?6:chart.guid==Sparks?7:chart.guid==Fairy?8:9);
     if(!bake.SetPass(0) || UnityEditor.ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Flow shader compilation failed");
     foreach(var piece in chart.pieces)Graphics.DrawMeshNow(piece.chart,Matrix4x4.identity);
    }
    var tex=new Texture2D(textureWidth,textureHeight,(hdr?TextureFormat.RGBAHalf:TextureFormat.RGBA32),false,hdr){name="FlowTimeline_"+f,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
    tex.ReadPixels(new Rect(0,0,textureWidth,textureHeight),0,0,false);tex.Apply(false,false);frames[f]=tex;
   }
  } catch {foreach(var frame in frames)if(frame!=null)UnityEngine.Object.DestroyImmediate(frame);throw;}
  finally {RenderTexture.active=old;GL.sRGBWrite=oldSrgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(bake);foreach(var chart in charts.Values){if(chart.pattern!=null)UnityEngine.Object.DestroyImmediate(chart.pattern);if(chart.displacement!=null)UnityEngine.Object.DestroyImmediate(chart.displacement);foreach(var piece in chart.pieces)if(piece.chart!=null)UnityEngine.Object.DestroyImmediate(piece.chart);}}
  // One atlas sequence and one native clock for all strokes and colors.
  var material=new Material(targets[0].sharedMaterial){name="Flow - Timed Fragment Atlas",mainTexture=frames[0],mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};material.SetColor("_TintColor",new Color(streamersCeiling,streamersCeiling,streamersCeiling,1).gamma);
  material.SetOverrideTag("OpenBrushBoundedFlowAtlas","1");
  var driver=new GameObject(driverName);driver.transform.SetParent(root.transform,false);
  var motion=driver.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FramesPerSecond=fps;motion.Timeline=timeline;motion.FrameTimes=times;
  foreach(var chart in charts.Values)foreach(var piece in chart.pieces) {
   var mesh=UnityEngine.Object.Instantiate(piece.mesh);mesh.name=piece.mesh.name+"_FlowChart";mesh.triangles=piece.indices;
   mesh.uv=mesh.uv.Select(u=>AtlasUV(chart,u,width,height)).ToArray();mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
   var child=new GameObject("Timed Flow Fragments");child.transform.SetParent(piece.renderer.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;piece.renderer.enabled=false;
  }
  Debug.Log("FLOW_BAKE charts="+charts.Count+" frames="+count+" atlas="+textureWidth+"x"+textureHeight+" duration="+timeline.Duration);
 }
 static Vector2 AtlasUV(Chart c,Vector2 uv,int width,int height) {var t=(uv-c.min)/(c.max-c.min);return new Vector2((c.origin.x+.5f+t.x*(c.size.x-1))/width,(c.origin.y+.5f+t.y*(c.size.y-1))/height);}
}
