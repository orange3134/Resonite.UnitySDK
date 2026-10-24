using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// One UV chart per stroke preserves interpolated paint colors, and keeps the
// existing growth-part transforms. Only emission changes over the 7/3s cycle.
public static class OpenBrushLightWireImport {
 public const int Width=2048,Height=16,Padding=2,Fps=30,FrameCount=70;
 public static float EmissionGain=>2*Mathf.Exp(7);
 sealed class Piece {public MeshRenderer renderer;public Mesh mesh,chart;}
 sealed class Chart {public List<Piece> pieces=new List<Piece>();public Vector2 min,max,origin;}
 public static void Prepare(GameObject root) {
  var targets=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled && r.sharedMaterial!=null && r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushProceduralImport.WireGuid).ToArray();
  if(targets.Length==0)return;
  if(root.transform.Find("LightWire Surface Chase")!=null)throw new InvalidOperationException("LightWire already prepared");
  var shader=Shader.Find("Hidden/OpenBrush/LightWireBake");
  if(shader==null || !shader.isSupported)throw new InvalidOperationException("LightWire bake requires a graphics-capable Editor");
  var charts=new Dictionary<string,Chart>();
  foreach(var r in targets) {
   var match=Regex.Match(r.name,@"^(.*_stroke_\d+)_part_\d+$");
   string key=match.Success?match.Groups[1].Value:r.GetInstanceID().ToString();
   if(!charts.TryGetValue(key,out var c)){c=new Chart();charts.Add(key,c);}
   var m=r.GetComponent<MeshFilter>()?.sharedMesh;
   if(m==null || m.subMeshCount!=1 || m.GetTopology(0)!=MeshTopology.Triangles || m.uv.Length!=m.vertexCount || m.colors.Length!=m.vertexCount)
    throw new InvalidOperationException("LightWire requires a triangle UV/RGBA mesh: "+r.name);
   if(r.sharedMaterial.mainTextureScale!=Vector2.one || r.sharedMaterial.mainTextureOffset!=Vector2.zero)throw new InvalidOperationException("Unsupported LightWire source UV transform");
   if(m.colors.Any(v=>v.r<0 || v.g<0 || v.b<0 || v.r>1 || v.g>1 || v.b>1))throw new InvalidOperationException("LightWire atlas expects normalized paint RGB");
   c.pieces.Add(new Piece{renderer=r,mesh=m});
  }
  int columns=Mathf.Min(4,charts.Count),rows=Mathf.CeilToInt(charts.Count/(float)columns);
  int width=columns*(Width+2*Padding),height=rows*(Height+2*Padding);
  if(width>SystemInfo.maxTextureSize || height>SystemInfo.maxTextureSize || (long)width*height*(FrameCount+3)>134217728)
   throw new InvalidOperationException("LightWire bake exceeds 128M frame pixels; split the sketch");
  var frames=new Texture2D[FrameCount];var maps=new Texture2D[3];var bake=new Material(shader);
  var rt=new RenderTexture(width,height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
  var old=RenderTexture.active;bool srgb=GL.sRGBWrite;int ci=0;
  try {
   rt.Create();
   foreach(var c in charts.Values) {
    var uvs=c.pieces.SelectMany(p=>p.mesh.uv).ToArray();c.min=new Vector2(uvs.Min(u=>u.x),uvs.Min(u=>u.y));c.max=new Vector2(uvs.Max(u=>u.x),uvs.Max(u=>u.y));
    if(c.max.x-c.min.x<1e-6 || c.max.y-c.min.y<1e-6)throw new InvalidOperationException("Degenerate LightWire UV chart");
    c.origin=new Vector2((ci%columns)*(Width+2*Padding)+Padding,(ci/columns)*(Height+2*Padding)+Padding);ci++;
    foreach(var p in c.pieces) {
     var uv=p.mesh.uv;var colors=p.mesh.colors;var positions=uv.Select(u=>{var q=AtlasUV(c,u,width,height);return new Vector3(q.x,q.y,0);}).ToArray();
     p.chart=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=positions,uv=uv,colors=colors,triangles=p.mesh.triangles};
    }
   }
   for(int channel=0;channel<4;channel++)for(int f=0;f<(channel==3?FrameCount:1);f++) {
    bake.SetFloat("_Output",channel);bake.SetFloat("_Seconds",f/(float)Fps);RenderTexture.active=rt;GL.sRGBWrite=false;GL.Clear(false,true,Color.black);
    if(!bake.SetPass(0) || UnityEditor.ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("LightWire bake shader compilation failed");
    foreach(var c in charts.Values)foreach(var p in c.pieces)Graphics.DrawMeshNow(p.chart,Matrix4x4.identity);
    var tex=new Texture2D(width,height,channel==3?TextureFormat.RGBAHalf:TextureFormat.RGBA32,false,channel>=2){name="LightWireSurface_"+channel+"_"+f,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
    tex.ReadPixels(new Rect(0,0,width,height),0,0,false);tex.Apply(false,false);if(channel==3)frames[f]=tex;else maps[channel]=tex;
   }
  }catch{foreach(var t in frames.Concat(maps))if(t!=null)UnityEngine.Object.DestroyImmediate(t);throw;}
  finally{RenderTexture.active=old;GL.sRGBWrite=srgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(bake);foreach(var c in charts.Values)foreach(var p in c.pieces)if(p.chart!=null)UnityEngine.Object.DestroyImmediate(p.chart);}
  var material=new Material(targets[0].sharedMaterial){name="LightWire - Colored Surface and Chase",mainTexture=maps[0],mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};
  material.SetOverrideTag("OpenBrushLightWireAtlas","1");material.SetTexture("_MaskTex",maps[1]);material.SetTexture("_BumpMap",maps[2]);
  // Replace the earlier generic, white-paint-only emission sequence.
  var oldMaterials=targets.Select(r=>r.sharedMaterial).Distinct().ToArray();
  foreach(var m in root.GetComponentsInChildren<OpenBrushTextureMotion>(true).Where(m=>oldMaterials.Contains(m.SourceMaterial)).ToArray())UnityEngine.Object.DestroyImmediate(m.gameObject);
  var driver=new GameObject("LightWire Surface Chase");driver.transform.SetParent(root.transform,false);
  var motion=driver.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FramesPerSecond=Fps;motion.EmissionOnly=true;
  foreach(var c in charts.Values)foreach(var p in c.pieces) {
   var mesh=UnityEngine.Object.Instantiate(p.mesh);mesh.name=p.mesh.name+"_LightWireChart";mesh.uv=mesh.uv.Select(u=>AtlasUV(c,u,width,height)).ToArray();mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
   var child=new GameObject("Colored LightWire Surface");child.transform.SetParent(p.renderer.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;p.renderer.enabled=false;
  }
  Debug.Log("LIGHTWIRE_SURFACE_BAKE charts="+charts.Count+" frames="+FrameCount+" atlas="+width+"x"+height);
 }
 static Vector2 AtlasUV(Chart c,Vector2 uv,int width,int height){var t=(uv-c.min)/(c.max-c.min);return new Vector2((c.origin.x+.5f+t.x*(Width-1))/width,(c.origin.y+.5f+t.y*(Height-1))/height);}
}
