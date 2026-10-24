using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// Static fragment color bake. Lighting remains native for Lofted; growth parents
// retain their animation. No time sequence is needed for these normal materials.
public static class OpenBrushHueShiftImport {
 public const string Tapered="3d9755da-56c7-7294-9b1d-5ec349975f52",Lofted="c5da2e70-a6e4-63a4-898c-5cfedef09c97";
 public const int Width=512,Height=256,Padding=2;
 sealed class Piece {public MeshRenderer renderer;public Mesh mesh;public Mesh raster;public Vector2[] chartUV;public bool ownsMesh;}
 sealed class Chart {public List<Piece> pieces=new List<Piece>();public Vector2 min,max,origin;public int width=Width,height=Height;}
 public static bool Supports(string guid)=>guid==Tapered || guid==Lofted;
 public static void Prepare(GameObject root) {
  var targets=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null && Supports(r.sharedMaterial.GetTag("OpenBrushGuid",false,"")) && r.sharedMaterial.GetTag("OpenBrushHueShiftAtlas",false,"")!="1").ToArray();
  foreach(var group in targets.GroupBy(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")))Bake(group.Key,group.ToArray());
 }
 static Vector2 Map(Chart c,Vector2 uv,int w,int h){var q=(uv-c.min)/(c.max-c.min);return new Vector2((c.origin.x+.5f+q.x*(c.width-1))/w,(c.origin.y+.5f+q.y*(c.height-1))/h);}
 // Separate faces when source UVs fold over different paint values. Bake
 // with original interpolated UV/RGBA; only the destination chart changes.
 static bool NeedsFaceCharts(Chart chart) {
  var seen=new Dictionary<Vector2,Color>();var all=chart.pieces.SelectMany(p=>p.mesh.uv).ToArray();
  if(all.Max(v=>v.x)-all.Min(v=>v.x)<1e-6f || all.Max(v=>v.y)-all.Min(v=>v.y)<1e-6f)return true;
  foreach(var p in chart.pieces) {
   var uv=p.mesh.uv;var color=p.mesh.colors;
   for(int i=0;i<uv.Length;i++) {
    if(seen.TryGetValue(uv[i],out var prior) && ((Vector4)(prior-color[i])).sqrMagnitude>1e-6f)return true;
    seen[uv[i]]=color[i];
   }
  }
  return false;
 }
 static void Bake(string guid,MeshRenderer[] targets,bool faceMode=false) {
  var shader=Shader.Find("Hidden/OpenBrush/HueShiftBake");
  if(shader==null || !shader.isSupported)throw new InvalidOperationException("HueShift bake needs a graphics-capable Editor");
  var charts=new Dictionary<string,Chart>();
  foreach(var r in targets) {
   if(!r.enabled)throw new InvalidOperationException("HueShift source already prepared: "+r.name);
   var match=Regex.Match(r.name,@"^(.*_stroke_\d+)_part_\d+$");
   if(!match.Success)throw new InvalidOperationException("HueShift needs generated stroke/part identity: "+r.name);
   if(!charts.TryGetValue(match.Groups[1].Value,out var c)){c=new Chart();charts.Add(match.Groups[1].Value,c);}
   var m=r.GetComponent<MeshFilter>()?.sharedMesh;
   if(m==null || m.subMeshCount!=1 || m.GetTopology(0)!=MeshTopology.Triangles || m.colors.Length!=m.vertexCount || m.uv.Length!=m.vertexCount)throw new InvalidOperationException("HueShift needs triangle UV/RGBA geometry");
   c.pieces.Add(new Piece{renderer=r,mesh=m});
  }
  if(!faceMode) {
   var bad=charts.Values.Where(NeedsFaceCharts).ToArray();
   if(bad.Length!=0) {
    var fallback=bad.SelectMany(c=>c.pieces).Select(p=>p.renderer).ToArray();
    var normal=targets.Except(fallback).ToArray();
    if(normal.Length>0)Bake(guid,normal);
    Bake(guid,fallback,true);return;
   }
  } else {
   var originals=charts.Values.ToArray();charts.Clear();int face=0;
   foreach(var c in originals)foreach(var p in c.pieces) {
    var pos=p.mesh.vertices;var normal=p.mesh.normals;var tangent=p.mesh.tangents;var uv=p.mesh.uv;var color=p.mesh.colors;var tri=p.mesh.triangles;
    for(int i=0;i<tri.Length;i+=3) {
     var ids=new[]{tri[i],tri[i+1],tri[i+2]};
     var mesh=new Mesh{name=p.mesh.name+"_Face",vertices=ids.Select(v=>pos[v]).ToArray(),uv=ids.Select(v=>uv[v]).ToArray(),colors=ids.Select(v=>color[v]).ToArray(),triangles=new[]{0,1,2}};
     if(normal.Length==pos.Length)mesh.normals=ids.Select(v=>normal[v]).ToArray();
     if(tangent.Length==pos.Length)mesh.tangents=ids.Select(v=>tangent[v]).ToArray();
     charts.Add("face_"+face++,new Chart{width=64,height=64,pieces=new List<Piece>{new Piece{renderer=p.renderer,mesh=mesh,ownsMesh=true,chartUV=new[]{Vector2.zero,Vector2.right,Vector2.up}}}});
    }
   }
  }
  int tileWidth=faceMode?64:Width,tileHeight=faceMode?64:Height;
  int columns=Mathf.CeilToInt(Mathf.Sqrt(charts.Count)),rows=Mathf.CeilToInt(charts.Count/(float)columns);
  int width=columns*(tileWidth+2*Padding),height=rows*(tileHeight+2*Padding),chartIndex=0;
  if(width>SystemInfo.maxTextureSize || height>SystemInfo.maxTextureSize || (long)width*height>16777216)throw new InvalidOperationException("HueShift atlas exceeds 16M pixels; split this sketch");
  foreach(var chart in charts.Values) {
   var uv=chart.pieces.SelectMany(p=>p.chartUV ?? p.mesh.uv).ToArray();
   chart.min=new Vector2(uv.Min(v=>v.x),uv.Min(v=>v.y));chart.max=new Vector2(uv.Max(v=>v.x),uv.Max(v=>v.y));
   if(chart.max.x-chart.min.x<1e-6 || chart.max.y-chart.min.y<1e-6)throw new InvalidOperationException("Degenerate HueShift UV chart");
   chart.origin=new Vector2(chartIndex%columns*(tileWidth+2*Padding)+Padding,chartIndex/columns*(tileHeight+2*Padding)+Padding);chartIndex++;

  }
  var pattern=new Texture2D(2,2,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
  var bake=new Material(shader);Texture2D atlas=null;
  var rt=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
  var old=RenderTexture.active;bool oldSrgb=GL.sRGBWrite;
  try {
   if(guid==Tapered && !pattern.LoadImage(System.IO.File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/TaperedHueShift.png")))throw new InvalidOperationException("Missing normal TaperedHueShift texture");
   bake.SetTexture("_MainTex",pattern);bake.SetFloat("_Lofted",guid==Lofted?1:0);
   rt.Create();RenderTexture.active=rt;GL.sRGBWrite=false;GL.Clear(false,true,Color.clear);
   if(!bake.SetPass(0) || UnityEditor.ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("HueShift bake shader compilation failed");
   foreach(var chart in charts.Values)foreach(var p in chart.pieces) {
    var u=p.mesh.uv;var pos=(p.chartUV ?? u).Select(v=>{var q=Map(chart,v,width,height);return new Vector3(q.x,q.y,0);}).ToArray();
    p.raster=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=pos,uv=u,colors=p.mesh.colors,triangles=p.mesh.triangles};
    Graphics.DrawMeshNow(p.raster,Matrix4x4.identity);
   }
   atlas=new Texture2D(width,height,TextureFormat.RGBA32,false,false){name="HueShift_"+guid,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
   atlas.ReadPixels(new Rect(0,0,width,height),0,0,false);atlas.Apply(false,false);
  }catch{if(atlas!=null)UnityEngine.Object.DestroyImmediate(atlas);throw;}
  finally{RenderTexture.active=old;GL.sRGBWrite=oldSrgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(bake);UnityEngine.Object.DestroyImmediate(pattern);foreach(var c in charts.Values)foreach(var p in c.pieces)if(p.raster!=null)UnityEngine.Object.DestroyImmediate(p.raster);}
  var material=new Material(targets[0].sharedMaterial){name="HueShift - Source Color Atlas",mainTexture=atlas,mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};
  material.SetColor("_TintColor",Color.white);material.SetOverrideTag("OpenBrushHueShiftAtlas","1");
  foreach(var chart in charts.Values)foreach(var p in chart.pieces) {
   var mesh=UnityEngine.Object.Instantiate(p.mesh);mesh.name=p.mesh.name+"_HueShiftChart";
   mesh.uv=(p.chartUV ?? mesh.uv).Select(v=>Map(chart,v,width,height)).ToArray();mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
   var child=new GameObject("HueShift Surface");child.transform.SetParent(p.renderer.transform,false);
   child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;p.renderer.enabled=false;
   if(p.ownsMesh)UnityEngine.Object.DestroyImmediate(p.mesh);
  }
  Debug.Log("HUESHIFT_BAKE guid="+guid+" charts="+charts.Count+" surfaces="+targets.Length+" atlas="+width+"x"+height);
 }
}
