using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OpenBrushElectricityImport {
 public const string Guid="f6e85de3-6dcc-4e7f-87fd-cee8c3d25d51";
 public const int Fps=60,Tile=128,Padding=2;
 [Serializable] public class Data {public int schemaVersion;public string brushGuid,coordinateSystem;public Part[] parts;}
 [Serializable] public class Part {public string nodeName;public Vector3[] positions,offsets;public Vector2[] uv;public Color[] colors;public float[] times,strokeStarts,strokeEnds;public int[] triangles;}
 struct Vertex {public Vector3 p;public Vector2 uv;public float t;public static Vertex Lerp(Vertex a,Vertex b,float k)=>new Vertex{p=Vector3.Lerp(a.p,b.p,k),uv=Vector2.Lerp(a.uv,b.uv,k),t=Mathf.Lerp(a.t,b.t,k)};}
 public sealed class Deformer:IDisposable {
  readonly Material material;readonly Texture2D positions,offsets,readback;readonly RenderTexture target;readonly int count;
  public Deformer(Part part){
   var shader=Shader.Find("Hidden/OpenBrush/ElectricityDeform");if(shader==null || !shader.isSupported)throw new InvalidOperationException("Electricity requires a graphics-capable Editor");
   material=new Material(shader);count=part.positions.Length;int width=Mathf.Min(4096,Mathf.NextPowerOfTwo(count)),height=Mathf.CeilToInt(count/(float)width);
   if(height>SystemInfo.maxTextureSize)throw new InvalidOperationException("Electricity vertex input too large");
   positions=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point};offsets=UnityEngine.Object.Instantiate(positions);
   var p=new Color[width*height];var o=new Color[p.Length];
   for(int i=0;i<count;i++){var v=part.positions[i];var d=part.offsets[i];p[i]=new Color(v.x,v.y,v.z,part.uv[i].x);o[i]=new Color(d.x,d.y,d.z,0);}
   positions.SetPixels(p);positions.Apply(false,false);offsets.SetPixels(o);offsets.Apply(false,false);material.SetTexture("_Offsets",offsets);
   target=new RenderTexture(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();readback=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true);
  }
  public Vector3[] Evaluate(float seconds,float mod,float displacement=2){
   var old=RenderTexture.active;bool srgb=GL.sRGBWrite;
   try{material.SetFloat("_Seconds",seconds);material.SetFloat("_Mod",mod);material.SetFloat("_Displacement",displacement);GL.sRGBWrite=false;Graphics.Blit(positions,target,material);
    if(ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Electricity deformation shader failed");
    RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);readback.Apply(false,false);
    return readback.GetPixels().Take(count).Select(c=>new Vector3(c.r,c.g,c.b)).ToArray();
   }finally{RenderTexture.active=old;GL.sRGBWrite=srgb;}
  }
  public void Dispose(){target.Release();foreach(var obj in new UnityEngine.Object[]{material,positions,offsets,readback,target})UnityEngine.Object.DestroyImmediate(obj);}
 }
 static int[] Groups(Part p,OpenBrushGrowthImport.Stroke[] strokes,float duration){
  // Reuse the validated multi-stroke timestamp identity check; no Disco geometry math.
  return OpenBrushDiscoGrowth.MatchStrokes(new OpenBrushDiscoImport.Part{positions=p.positions,times=p.times,strokeStarts=p.strokeStarts,strokeEnds=p.strokeEnds,triangles=p.triangles},strokes,duration);
 }
 public static void Prepare(GameObject root,Data data,OpenBrushGrowthImport.Data timing,float[] samples=null){
  var timeline=root.GetComponent<OpenBrushTimeline>();
  if(timeline==null || timing?.strokes==null || data==null || data.schemaVersion!=1 || data.brushGuid!=Guid || data.coordinateSystem!="UnityInvertZ" || data.parts==null || data.parts.Length==0)throw new InvalidOperationException("Electricity requires original offsets and a finite drawing timeline");
  if(data.parts.Select(p=>p.nodeName).Distinct().Count()!=data.parts.Length)throw new InvalidOperationException("Duplicate Electricity batch");
  int count=samples?.Length ?? Mathf.CeilToInt(timeline.Duration*Fps)+1;
  if(samples!=null && (samples.Length<2 || samples.Any(t=>!float.IsFinite(t)||t<0||t>timeline.Duration) || samples.Zip(samples.Skip(1),(a,b)=>b>a).Any(ok=>!ok)))throw new InvalidOperationException("Invalid Electricity sample times");
  // A partially revealed triangle can clip into a quad (two triangles).
  if(data.parts.Any(p=>p.triangles==null) || data.parts.Sum(p=>(double)p.triangles.Length*3*2*count)>20000000)throw new InvalidOperationException("Electricity exceeds 20M frame vertices; split the sketch");
  var transforms=root.GetComponentsInChildren<Transform>(true);
  foreach(var p in data.parts){
   int n=p.positions?.Length ?? 0;
   if(n==0 || p.offsets?.Length!=n || p.uv?.Length!=n || p.colors?.Length!=n || p.triangles.Length%3!=0 || p.triangles.Any(i=>i<0||i>=n))throw new InvalidOperationException("Invalid Electricity source geometry");
   if(p.positions.Concat(p.offsets).Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)) || p.uv.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||v.x<-.0001f||v.x>1.0001f||v.y<-.0001f||v.y>1.0001f) || p.colors.Any(c=>!float.IsFinite(c.r)||!float.IsFinite(c.g)||!float.IsFinite(c.b)))throw new InvalidOperationException("Nonfinite or unsupported Electricity channels");
   Groups(p,timing.strokes.Where(s=>s.name==p.nodeName).ToArray(),timeline.Duration);
   var parents=transforms.Where(t=>t.name==p.nodeName).ToArray();if(parents.Length!=1 || parents[0].GetComponentInChildren<OpenBrushGeometryMotion>()!=null)throw new InvalidOperationException("Missing or already prepared Electricity parent");
   var renderers=parents[0].GetComponentsInChildren<MeshRenderer>(true);
   if(renderers.Length==0 || renderers.Any(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=Guid))throw new InvalidOperationException("Electricity material mismatch");
   var expected=new Bounds(p.positions[0],Vector3.zero);foreach(var v in p.positions)expected.Encapsulate(v);
   var actual=new Bounds();bool first=true;foreach(var r in renderers)foreach(var v in r.GetComponent<MeshFilter>().sharedMesh.vertices){var q=parents[0].InverseTransformPoint(r.transform.TransformPoint(v));if(first){actual=new Bounds(q,Vector3.zero);first=false;}else actual.Encapsulate(q);}
   if((expected.min-actual.min).magnitude>1e-4 || (expected.max-actual.max).magnitude>1e-4)throw new InvalidOperationException("Raw Electricity and imported model coordinates differ");
  }
  foreach(var p in data.parts){
   var parent=transforms.Single(t=>t.name==p.nodeName);var original=parent.GetComponentsInChildren<MeshRenderer>(true);
   var strokes=timing.strokes.Where(s=>s.name==p.nodeName).ToArray();var groups=Groups(p,strokes,timeline.Duration);
   int columns=Mathf.CeilToInt(Mathf.Sqrt(strokes.Length)),rows=Mathf.CeilToInt(strokes.Length/(float)columns),width=columns*(Tile+2*Padding),height=rows*(Tile+2*Padding);
   if(width>SystemInfo.maxTextureSize || height>SystemInfo.maxTextureSize)throw new InvalidOperationException("Electricity surface atlas too large");
   var atlasUv=new Vector2[p.positions.Length];var playTimes=new float[p.positions.Length];
   for(int i=0;i<atlasUv.Length;i++){int s=groups[i];atlasUv[i]=new Vector2(((s%columns)*(Tile+2*Padding)+Padding+.5f+p.uv[i].x*(Tile-1))/width,((s/columns)*(Tile+2*Padding)+Padding+.5f+p.uv[i].y*(Tile-1))/height);playTimes[i]=OpenBrushGrowthImport.Birth(timing,strokes[s],p.times[i]);}
   var material=new Material(original[0].sharedMaterial){name="Electricity - Original Curl Strands"};material.SetColor("_TintColor",new Color(4,4,4,1).gamma);material.SetOverrideTag("OpenBrushBoundedFlowAtlas","1");material.mainTexture=Surface(p,atlasUv,width,height);
   var meshes=new Mesh[count];var times=new float[count];
   using(var deformer=new Deformer(p))for(int f=0;f<count;f++){
    times[f]=samples==null?Mathf.Min(f/(float)Fps,timeline.Duration):samples[f];
    var positions=new List<Vector3>();var uv=new List<Vector2>();
    foreach(float mod in new[]{1f,1.333f,1.77f}){
     var deformed=deformer.Evaluate(times[f],mod);
     for(int tri=0;tri<p.triangles.Length;tri+=3){
      var vertices=new Vertex[3];for(int j=0;j<3;j++){int v=p.triangles[tri+j];vertices[j]=new Vertex{p=deformed[v],uv=atlasUv[v],t=playTimes[v]};}
      var polygon=new List<Vertex>();for(int j=0;j<3;j++){var a=vertices[j];var b=vertices[(j+1)%3];bool inside=a.t<=times[f],other=b.t<=times[f];if(inside)polygon.Add(a);if(inside!=other)polygon.Add(Vertex.Lerp(a,b,(times[f]-a.t)/(b.t-a.t)));}
      for(int j=1;j+1<polygon.Count;j++)foreach(var v in new[]{polygon[0],polygon[j],polygon[j+1]}){positions.Add(v.p);uv.Add(v.uv);}
     }
    }
    if(positions.Count==0)for(int j=0;j<3;j++){positions.Add(Vector3.zero);uv.Add(Vector2.zero);}
    var mesh=new Mesh{name="ElectricityGrowth_"+f,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(positions);mesh.SetUVs(0,uv);mesh.SetColors(Enumerable.Repeat(Color.white,positions.Count).ToArray());mesh.SetNormals(Enumerable.Repeat(Vector3.forward,positions.Count).ToArray());mesh.SetTriangles(Enumerable.Range(0,positions.Count).ToArray(),0);mesh.RecalculateBounds();meshes[f]=mesh;
   }
   var child=new GameObject("Electricity - Three Curl Strands");child.transform.SetParent(parent,false);var motion=child.AddComponent<OpenBrushGeometryMotion>();motion.SourceMaterial=material;motion.Frames=meshes;motion.FrameTimes=times;motion.Timeline=timeline;
   foreach(var r in original)r.enabled=false;
   Debug.Log("ELECTRICITY_BAKE strokes="+strokes.Length+" frames="+count+" originalVertices="+p.positions.Length+" finalVertices="+meshes.Last().vertexCount);
  }
 }
 static Texture2D Surface(Part p,Vector2[] atlasUv,int width,int height){
  var shader=Shader.Find("Hidden/OpenBrush/ElectricitySurface");if(shader==null || !shader.isSupported)throw new InvalidOperationException("Electricity surface shader unavailable");
  var bake=new Material(shader);var chart=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=atlasUv.Select(u=>new Vector3(u.x,u.y,0)).ToArray(),uv=p.uv,colors=p.colors,triangles=p.triangles};
  var rt=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var old=RenderTexture.active;bool srgb=GL.sRGBWrite;
  try{rt.Create();RenderTexture.active=rt;GL.sRGBWrite=false;GL.Clear(false,true,new Color(0,0,0,1));if(!bake.SetPass(0)||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Electricity surface bake failed");Graphics.DrawMeshNow(chart,Matrix4x4.identity);
   var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,false){name="ElectricitySpectralAtlas",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};texture.ReadPixels(new Rect(0,0,width,height),0,0,false);texture.Apply(false,false);return texture;
  }finally{RenderTexture.active=old;GL.sRGBWrite=srgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(chart);UnityEngine.Object.DestroyImmediate(bake);}
 }
}
