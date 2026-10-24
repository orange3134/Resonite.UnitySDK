using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Experimental finite self-shadow bake for FlatLit. Only the audited opaque
// Printing material is a caster; transparent/custom shader meshes are not guessed.
public static class OpenBrushPrintingShadowImport {
 public struct Face { public Vector3 A,B,C; public Bounds Bounds; public Vector3 Center; }
 public sealed class Rays {
  sealed class Node { public Bounds Bounds; public Node Left,Right; public int Start,Count; }
  readonly Face[] faces; readonly Node root;
  public Rays(IEnumerable<Face> source) {faces=source.ToArray();root=Build(0,faces.Length);}
  Node Build(int start,int count) {
   if(count==0)return null;
   var node=new Node{Start=start,Count=count,Bounds=faces[start].Bounds};
   for(int i=start+1;i<start+count;i++)node.Bounds.Encapsulate(faces[i].Bounds);
   if(count<=8)return node;
   var size=node.Bounds.size;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
   Array.Sort(faces,start,count,Comparer<Face>.Create((a,b)=>a.Center[axis].CompareTo(b.Center[axis])));
   int half=count/2;node.Left=Build(start,half);node.Right=Build(start+half,count-half);node.Count=0;return node;
  }
  public bool Blocked(Vector3 point,Vector3 direction) => Hit(root,new Ray(point,direction));
  bool Hit(Node n,Ray ray) {
   if(n==null || !n.Bounds.IntersectRay(ray))return false;
   if(n.Count==0)return Hit(n.Left,ray)||Hit(n.Right,ray);
   for(int i=n.Start;i<n.Start+n.Count;i++) {
    var f=faces[i];var e1=f.B-f.A;var e2=f.C-f.A;var p=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,p);
    // The source shadow map culls back faces seen FROM the light. Our ray goes
    // TO the light, so the accepted determinant has the opposite sign.
    if(det>=-1e-12f)continue;
    float inv=1/det;var t=ray.origin-f.A;float u=Vector3.Dot(t,p)*inv;if(u<0||u>1)continue;
    var q=Vector3.Cross(t,e1);float v=Vector3.Dot(ray.direction,q)*inv;if(v<0||u+v>1)continue;
    if(Vector3.Dot(e2,q)*inv>1e-6f)return true;
   }
   return false;
  }
 }
 public static Face Triangle(Vector3 a,Vector3 b,Vector3 c) {
  var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);bounds.Expand(.000002f);
  return new Face{A=a,B=b,C=c,Bounds=bounds,Center=(a+b+c)/3};
 }
 public static float Growth(float time,float start,float end) => Mathf.Clamp01((time-start)/(end-start));
 public static Matrix4x4 Canvas(Transform part,Transform root) {
  // Compose local transforms directly: subtracting large world translations
  // loses enough precision to produce shadow acne on adjacent growth slices.
  var relative=Matrix4x4.identity;var current=part;
  for(;current!=root && current!=null;current=current.parent)relative=Matrix4x4.TRS(current.localPosition,current.localRotation,current.localScale)*relative;
  if(current!=root)throw new InvalidOperationException("Shadow part is outside the sketch");
  return Matrix4x4.Scale(new Vector3(-1,1,-1))*relative;
 }
 sealed class Part {
  public MeshRenderer Renderer; public OpenBrushGrowthPart Growth; public Matrix4x4 Canvas;
  public Mesh Mesh; public Vector3[] Points,Normals,Caster; public Color[] Paint;
  public List<Mesh> Frames=new List<Mesh>(); public List<float> Times=new List<float>();public byte[] Previous;
 }
 static Part Receiver(MeshRenderer renderer,Transform root) {
  var growth=renderer.transform.parent.GetComponent<OpenBrushGrowthPart>();
  if(growth==null)throw new InvalidOperationException("Printing shadow bake needs drawing timing");
  var original=growth.GetComponent<OpenBrushVertexColorEncoding>()?.SourceMesh;
  if(original==null)throw new InvalidOperationException("Printing shadow bake needs original Gamma paint");
  var canvas=Canvas(growth.transform,root);
  var input=renderer.GetComponent<MeshFilter>().sharedMesh;var points=input.vertices;
  var ids=growth.GetComponent<MeshFilter>().sharedMesh.triangles;var rgba=original.colors;var paint=ids.Select(i=>rgba[i]).ToArray();
  if(points.Length!=paint.Length)throw new InvalidOperationException("Printing shadow source correspondence changed");
  var vertices=new List<Vector3>();var colors=new List<Color>();var normals=new List<Vector3>();var lightingNormals=new List<Vector3>();
  for(int i=0;i<points.Length;i+=3) {
   var a=points[i];var b=points[i+1];var c=points[i+2];var normal=OpenBrushTriangleImport.UnitNormal(b-a,c-a);
   var lightingNormal=OpenBrushTriangleImport.UnitNormal(canvas.MultiplyVector(b-a),canvas.MultiplyVector(c-a));
   float edge=Mathf.Max(canvas.MultiplyVector(b-a).magnitude,canvas.MultiplyVector(c-a).magnitude,canvas.MultiplyVector(c-b).magnitude);
   int n=Mathf.Clamp(Mathf.CeilToInt(edge/.004f),1,4);
   Action<int,int> add=(u,v)=>{float x=u/(float)n,y=v/(float)n;vertices.Add(a*(1-x-y)+b*x+c*y);colors.Add(paint[i]*(1-x-y)+paint[i+1]*x+paint[i+2]*y);normals.Add(normal);lightingNormals.Add(lightingNormal);};
   for(int u=0;u<n;u++)for(int v=0;v<n-u;v++) {
    add(u,v);add(u+1,v);add(u,v+1);
    if(u+v<n-1){add(u+1,v);add(u+1,v+1);add(u,v+1);}
   }
  }
  var mesh=new Mesh{name=input.name+"_SelfShadow",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices.ToArray(),normals=normals.ToArray()};
  mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateBounds();
  return new Part{Renderer=renderer,Growth=growth,Canvas=canvas,Mesh=mesh,Points=vertices.ToArray(),Normals=lightingNormals.ToArray(),Caster=points,Paint=colors.ToArray()};
 }
 public static void Prepare(GameObject root,OpenBrushTriangleImport.Lighting light) {
  var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.sharedMaterial.GetTag("OpenBrushSourceFlatLighting",false,"")=="1").ToArray();
  if(renderers.Length==0)return;
  var timeline=root.GetComponent<OpenBrushTimeline>();if(timeline==null)throw new InvalidOperationException("Printing shadow bake needs a shared timeline");
  var parts=renderers.Select(r=>Receiver(r,root.transform)).ToArray();
  // Include exact growth boundaries and <=1/32 second samples. A step sequence
  // uses the PREVIOUS sample, never a future occluder. The final hold is constant.
  var times=new SortedSet<float>{0};
  foreach(var p in parts) {int n=Mathf.CeilToInt((p.Growth.End-p.Growth.Start)*32);for(int i=0;i<=n;i++)times.Add(Mathf.Lerp(p.Growth.Start,p.Growth.End,i/(float)n));}
  long vertices=parts.Sum(p=>(long)p.Points.Length);
  if(vertices>250000 || vertices*times.Count>40000000)throw new InvalidOperationException("Experimental Printing shadow sampling budget exceeded: vertices="+vertices+" samples="+times.Count);
  var direction=Quaternion.Euler(light.mainRotation[0],light.mainRotation[1],light.mainRotation[2])*Vector3.back;direction.x=-direction.x;
  long stored=0;int shadowed=0;
  foreach(float time in times) {
   var matrices=parts.Select(p=>p.Canvas*Matrix4x4.Scale(new Vector3(1,1,Growth(time,p.Growth.Start,p.Growth.End)))).ToArray();
   var faces=new List<Face>();
   for(int k=0;k<parts.Length;k++) {
    var p=parts[k];if(time<=p.Growth.Start)continue;
    for(int i=0;i<p.Caster.Length;i+=3)faces.Add(Triangle(matrices[k].MultiplyPoint3x4(p.Caster[i]),matrices[k].MultiplyPoint3x4(p.Caster[i+1]),matrices[k].MultiplyPoint3x4(p.Caster[i+2])));
   }
   var rays=new Rays(faces);
   for(int k=0;k<parts.Length;k++) {
    var p=parts[k];var visibility=new byte[p.Points.Length];var colors=new Color[p.Points.Length];
    for(int i=0;i<p.Points.Length;i+=3) {
     // Preserve the original flat face direction through subdivision. Computing
     // it again from tiny generated triangles can change or erase the lighting.
     float ndl=Mathf.Max(0,Vector3.Dot(p.Normals[i],direction));
     for(int j=0;j<3;j++) {
      int v=i+j;var position=matrices[k].MultiplyPoint3x4(p.Points[v]);
      bool blocked=time>p.Growth.Start && ndl>0 && rays.Blocked(position+direction*.0001f,direction);
      visibility[v]=(byte)(blocked?0:1);var color=p.Paint[v];
      for(int ch=0;ch<3;ch++)color[ch]*=light.ambient[ch]*.5f+(blocked?0:light.mainColor[ch]*ndl);
      colors[v]=color;if(blocked)shadowed++;
     }
    }
    if(p.Previous!=null && p.Previous.SequenceEqual(visibility))continue;
    stored+=p.Points.Length;if(stored>4000000)throw new InvalidOperationException("Experimental Printing shadow retained mesh budget exceeded");
    var frame=UnityEngine.Object.Instantiate(p.Mesh);frame.name=p.Mesh.name+"_"+p.Frames.Count;frame.colors=colors;
    p.Frames.Add(frame);p.Times.Add(time);p.Previous=visibility;
   }
  }
  foreach(var p in parts) {
   if(p.Frames.Count==1)p.Renderer.GetComponent<MeshFilter>().sharedMesh=p.Frames[0];
   else {
    var motion=p.Renderer.gameObject.AddComponent<OpenBrushGeometryMotion>();motion.Timeline=timeline;motion.FrameTimes=p.Times.ToArray();motion.Frames=p.Frames.ToArray();motion.SourceMaterial=p.Renderer.sharedMaterial;p.Renderer.enabled=false;
   }
   UnityEngine.Object.DestroyImmediate(p.Mesh);
  }
  Debug.Log("PRINTING_SELF_SHADOW parts="+parts.Length+" samples="+times.Count+" frames="+parts.Sum(p=>p.Frames.Count)+" storedVertices="+stored+" shadowedSamples="+shadowed);
 }
}
