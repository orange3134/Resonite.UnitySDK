using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// Bake audited Digital/Race procedural grids once per stroke, retaining growth.
// Native additive blending and atlas-space antialiasing approximate the source.
public static class OpenBrushDigitalImport {
 public const string Digital="30cb9af6-be41-4872-8f3e-cbff63fe3db8",Race="abfbb2aa-70b4-4a5c-8126-8eedda2b3628";
 // Guard bands keep differently colored charts apart through the first five mips.
 public const int Width=256,Height=128,Padding=32;
 sealed class Piece {public MeshRenderer renderer;public Mesh mesh;public Mesh raster;public int[] uniqueTriangles;public int copies;}
 sealed class Chart {public List<Piece> pieces=new List<Piece>();public Vector2 min,max,origin;public Vector2Int size;}
 // UV frequency, not world length, determines how much texture storage is needed.
 // The old fixed chart undersampled thin/long strokes below one pixel per cell.
 public static Vector2Int Resolution(Vector2 span) {
  if(float.IsNaN(span.x)||float.IsNaN(span.y)||float.IsInfinity(span.x)||float.IsInfinity(span.y)||span.x<1e-6f||span.y<1e-6f)
   throw new InvalidOperationException("Invalid Digital UV span");
  double w=Math.Ceiling(span.x*Width),h=Math.Ceiling(span.y*Height);
  if(w>16384-2*Padding || h>16384-2*Padding)throw new InvalidOperationException("Digital UV chart exceeds supported texture dimensions; split this stroke");
  return new Vector2Int(Math.Max(Width,(int)w),Math.Max(Height,(int)h));
 }
 public static bool Supports(string guid)=>guid==Digital || guid==Race;
 public static void Prepare(GameObject root) {
  var targets=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null && Supports(r.sharedMaterial.GetTag("OpenBrushGuid",false,"")) && r.sharedMaterial.GetTag("OpenBrushDigitalAtlas",false,"")!="1").ToArray();
  foreach(var group in targets.GroupBy(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")))Bake(group.Key,group.ToArray());
 }
 static Vector2 Map(Chart c,Vector2 uv,int w,int h){var q=(uv-c.min)/(c.max-c.min);return new Vector2((c.origin.x+.5f+q.x*(c.size.x-1))/w,(c.origin.y+.5f+q.y*(c.size.y-1))/h);}
 static void Bake(string guid,MeshRenderer[] targets) {
  var shader=Shader.Find("Hidden/OpenBrush/DigitalBake");
  if(shader==null || !shader.isSupported)throw new InvalidOperationException("Digital bake needs a graphics-capable Editor");
  var charts=new Dictionary<string,Chart>();
  foreach(var r in targets) {
   if(!r.enabled)throw new InvalidOperationException("Digital source already prepared: "+r.name);
   var match=Regex.Match(r.name,@"^(.*_stroke_\d+)_part_\d+$");
   if(!match.Success)throw new InvalidOperationException("Digital needs generated stroke/part identity: "+r.name);
   if(!charts.TryGetValue(match.Groups[1].Value,out var c)){c=new Chart();charts.Add(match.Groups[1].Value,c);}
   var m=r.GetComponent<MeshFilter>()?.sharedMesh;
   if(m==null || m.subMeshCount!=1 || m.GetTopology(0)!=MeshTopology.Triangles || m.colors.Length!=m.vertexCount || m.uv.Length!=m.vertexCount)throw new InvalidOperationException("Digital needs triangle UV/RGBA geometry");
   // Combine only exact coincident front/back triangles. The source sums in
   // Gamma space; native addition after decoding otherwise shifts the colors.
   var pos=m.vertices;var uv=m.uv;var rgba=m.colors;var triangles=m.triangles;
   var keys=Enumerable.Range(0,m.vertexCount).Select(i=>string.Join(",",new[]{pos[i].x,pos[i].y,pos[i].z,uv[i].x,uv[i].y,rgba[i].r,rgba[i].g,rgba[i].b,rgba[i].a}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)))).ToArray();
   var groups=new Dictionary<string,List<int[]>>();
   for(int i=0;i<triangles.Length;i+=3){var t=new[]{triangles[i],triangles[i+1],triangles[i+2]};var key=string.Join(";",t.Select(x=>keys[x]).OrderBy(x=>x,StringComparer.Ordinal));if(!groups.TryGetValue(key,out var list)){list=new List<int[]>();groups.Add(key,list);}list.Add(t);}
   var counts=groups.Values.Select(g=>g.Count).Distinct().ToArray();
   if(counts.Length!=1)throw new InvalidOperationException("Digital overlapping faces need multiple coverage charts");
   c.pieces.Add(new Piece{renderer=r,mesh=m,copies=counts[0],uniqueTriangles=groups.Values.SelectMany(g=>g[0]).ToArray()});
  }
  foreach(var chart in charts.Values) {
   var uv=chart.pieces.SelectMany(p=>p.mesh.uv).ToArray();
   chart.min=new Vector2(uv.Min(v=>v.x),uv.Min(v=>v.y));chart.max=new Vector2(uv.Max(v=>v.x),uv.Max(v=>v.y));
   chart.size=Resolution(chart.max-chart.min);
   if(chart.pieces.Select(p=>p.copies).Distinct().Count()!=1)throw new InvalidOperationException("Digital coverage varies within one UV chart");
   // Coincident front/back UVs must carry the same color; never silently merge
   // different surface colors into one texel. Retain all geometric faces.
   var colorsAtUv=new Dictionary<string,Color>();
   foreach(var p in chart.pieces) {
    var u=p.mesh.uv;var rgba=p.mesh.colors;
    for(int i=0;i<u.Length;i++) {
     string key=u[i].x.ToString("F5",CultureInfo.InvariantCulture)+","+u[i].y.ToString("F5",CultureInfo.InvariantCulture);
     if(colorsAtUv.TryGetValue(key,out var prior) && ((Vector4)(prior-rgba[i])).sqrMagnitude>1e-6f)throw new InvalidOperationException("Overlapping Digital UVs carry different colors");
     colorsAtUv[key]=rgba[i];
    }
   }
  }
  // Shelf packing avoids making every short stroke as wide as the longest one.
  var ordered=charts.Values.OrderByDescending(c=>c.size.y).ThenByDescending(c=>c.size.x).ToArray();
  long area=ordered.Sum(c=>(long)(c.size.x+2*Padding)*(c.size.y+2*Padding));
  int limit=SystemInfo.maxTextureSize;
  if(area*4/3>16777216 || ordered.Any(c=>c.size.x+2*Padding>limit || c.size.y+2*Padding>limit))throw new InvalidOperationException("Digital charts exceed texture budget; split this sketch");
  int shelfWidth=Math.Min(limit,Math.Max(ordered.Max(c=>c.size.x+2*Padding),(int)Math.Ceiling(Math.Sqrt(area)*2)));
  int x=0,y=0,rowHeight=0,width=0,height=0;
  foreach(var chart in ordered) {
   int cw=chart.size.x+2*Padding,ch=chart.size.y+2*Padding;
   if(x+cw>shelfWidth){x=0;y+=rowHeight;rowHeight=0;}
   chart.origin=new Vector2(x+Padding,y+Padding);x+=cw;rowHeight=Math.Max(rowHeight,ch);width=Math.Max(width,x);height=Math.Max(height,y+ch);
  }
  if(height>limit || (long)width*height*4/3>16777216)throw new InvalidOperationException("Digital packed atlas exceeds texture budget; split this sketch");
  var bake=new Material(shader);Texture2D atlas=null;
  var rt=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
  var old=RenderTexture.active;bool oldSrgb=GL.sRGBWrite;
  try {
   bake.SetFloat("_Race",guid==Race?1:0);
   rt.Create();RenderTexture.active=rt;GL.sRGBWrite=false;GL.Clear(false,true,Color.clear);
   if(!bake.SetPass(0) || UnityEditor.ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Digital bake shader compilation failed");
   foreach(var chart in charts.Values)foreach(var p in chart.pieces) {
    bake.SetFloat("_Copies",p.copies);bake.SetPass(0);
    var u=p.mesh.uv;var pos=u.Select(v=>{var q=Map(chart,v,width,height);return new Vector3(q.x,q.y,0);}).ToArray();
    p.raster=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=pos,uv=u,colors=p.mesh.colors,triangles=p.uniqueTriangles};
    Graphics.DrawMeshNow(p.raster,Matrix4x4.identity);
   }
   atlas=new Texture2D(width,height,TextureFormat.RGBA32,true,false){name="Digital_"+guid,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=8};
   atlas.ReadPixels(new Rect(0,0,width,height),0,0,false);atlas.Apply(true,false);
  }catch{if(atlas!=null)UnityEngine.Object.DestroyImmediate(atlas);throw;}
  finally{RenderTexture.active=old;GL.sRGBWrite=oldSrgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(bake);foreach(var c in charts.Values)foreach(var p in c.pieces)if(p.raster!=null)UnityEngine.Object.DestroyImmediate(p.raster);}
  var material=new Material(targets[0].sharedMaterial){name="Digital - Source Color Atlas",mainTexture=atlas,mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};
  material.SetColor("_TintColor",new Color(4,4,4,1).gamma);material.SetOverrideTag("OpenBrushBoundedFlowAtlas","1");material.SetOverrideTag("OpenBrushDigitalAtlas","1");
  foreach(var chart in charts.Values)foreach(var p in chart.pieces) {
   var mesh=UnityEngine.Object.Instantiate(p.mesh);mesh.name=p.mesh.name+"_DigitalChart";
   mesh.triangles=p.uniqueTriangles;
   mesh.uv=mesh.uv.Select(v=>Map(chart,v,width,height)).ToArray();mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
   var child=new GameObject("Digital Surface");child.transform.SetParent(p.renderer.transform,false);
   child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;p.renderer.enabled=false;
  }
  Debug.Log("DIGITAL_BAKE guid="+guid+" charts="+charts.Count+" surfaces="+targets.Length+" copies="+string.Join(",",charts.Values.SelectMany(c=>c.pieces).Select(p=>p.copies).Distinct())+" chartWidths="+string.Join(",",charts.Values.Select(c=>c.size.x))+" atlas="+width+"x"+height);
 }
}


