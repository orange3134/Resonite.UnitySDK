using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Two source geometry/fragment shader families require independent triangle data.
// Keep each growth parent intact so the original drawing clock still reveals it.
public static class OpenBrushTriangleImport {
 // Vector3.normalized discards cross products below 1e-5. Small valid brush
 // triangles still have a direction; normalize without a world-area cutoff.
 public static Vector3 UnitNormal(Vector3 a,Vector3 b) {
  double x=(double)a.y*b.z-(double)a.z*b.y,y=(double)a.z*b.x-(double)a.x*b.z,z=(double)a.x*b.y-(double)a.y*b.x;
  double length=Math.Sqrt(x*x+y*y+z*z);return length>0?new Vector3((float)(x/length),(float)(y/length),(float)(z/length)):Vector3.zero;
 }
 [Serializable] public class Lighting {public int schemaVersion;public string mode;public float[] ambient,mainColor,mainRotation;}
 public const string Printing="d3f3b18a-da03-f694-b838-28ba8e749a98",Faceted="4391ffaa-df73-4396-9e33-31e4e4930b27";
 public static Mesh Build(Mesh source,Matrix4x4 originalCanvas,bool axisColors) {
  if(source==null || source.subMeshCount!=1 || source.GetTopology(0)!=MeshTopology.Triangles || source.colors.Length!=source.vertexCount)throw new InvalidOperationException("Triangle brush requires one colored triangle primitive");
  var ids=source.triangles;var points=source.vertices;var rgba=source.colors;
  var vertices=ids.Select(i=>points[i]).ToArray();var colors=ids.Select(i=>rgba[i]).ToArray();var normals=new Vector3[ids.Length];
  if(ids.Length==0 || ids.Length%3!=0 || vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new InvalidOperationException("Invalid triangle brush positions");
  for(int i=0;i<ids.Length;i+=3) {
   var normal=UnitNormal(vertices[i+1]-vertices[i],vertices[i+2]-vertices[i]);
   for(int j=0;j<3;j++)normals[i+j]=normal;
   if(axisColors) {
    var a=originalCanvas.MultiplyPoint3x4(vertices[i]);var b=originalCanvas.MultiplyPoint3x4(vertices[i+1]);var c=originalCanvas.MultiplyPoint3x4(vertices[i+2]);
    var n=UnitNormal(b-a,c-a);
    // Normal material _ColorX/Y/Z are the positive RGB basis. Negative fragment
    // channels are clipped by the source framebuffer. Paint RGB is not sampled.
    for(int j=0;j<3;j++)colors[i+j]=new Color(Mathf.Max(0,n.x),Mathf.Max(0,n.y),Mathf.Max(0,n.z),rgba[ids[i+j]].a);
   }
  }
  var mesh=new Mesh{name=source.name+(axisColors?"_AxisFacets":"_FlatTriangles"),indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices,colors=colors,normals=normals};
  var tangents=source.tangents;if(tangents.Length==source.vertexCount)mesh.tangents=ids.Select(i=>tangents[i]).ToArray();
  for(int channel=0;channel<8;channel++){var uv=new List<Vector4>();source.GetUVs(channel,uv);if(uv.Count==source.vertexCount)mesh.SetUVs(channel,ids.Select(i=>uv[i]).ToList());}
  mesh.SetTriangles(Enumerable.Range(0,ids.Length).ToArray(),0);mesh.RecalculateBounds();return mesh;
 }
 public static void BakeLighting(Mesh mesh,Matrix4x4 originalCanvas,Color[] gammaColors,Lighting light) {
  if(light==null || light.schemaVersion!=1 || light.mode!="source-gamma-main-and-ambient" ||
   new[]{light.ambient,light.mainColor,light.mainRotation}.Any(v=>v==null || v.Length!=3 || v.Any(x=>!float.IsFinite(x))) ||
   light.ambient.Concat(light.mainColor).Any(v=>v<0) || gammaColors.Length!=mesh.vertexCount)throw new InvalidOperationException("Missing or invalid source FlatLit lighting");
  var points=mesh.vertices;var colors=new Color[points.Length];
  // ExportCollector changes light rotation to glTF's reflected X basis.
  var direction=Quaternion.Euler(light.mainRotation[0],light.mainRotation[1],light.mainRotation[2])*Vector3.back;direction.x=-direction.x;
  for(int i=0;i<points.Length;i+=3) {
   var a=originalCanvas.MultiplyPoint3x4(points[i]);var b=originalCanvas.MultiplyPoint3x4(points[i+1]);var c=originalCanvas.MultiplyPoint3x4(points[i+2]);
   var n=UnitNormal(b-a,c-a);float ndl=Mathf.Max(0,Vector3.Dot(n,direction));
   for(int j=0;j<3;j++) {
    var paint=gammaColors[i+j];var result=paint;
    for(int ch=0;ch<3;ch++)result[ch]=paint[ch]*(light.mainColor[ch]*ndl+light.ambient[ch]*.5f);
    colors[i+j]=result;
   }
  }
  // FlatLit's normal material has Metallic=0 and white texture: lighting is
  // view-independent. Preserve the exported lighting when relocating the model.
  // Source shadow-map visibility is not available in the GLB and remains a gap.
  mesh.colors=colors;
 }
 public static void Prepare(GameObject root,Lighting lighting=null) {
  int count=0;
  var litMaterials=new Dictionary<Material,Material>();
  foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)) {
   string guid=r.sharedMaterial?.GetTag("OpenBrushGuid",false,"");
   if(guid!=Printing && guid!=Faceted || !r.enabled || r.name=="Original Triangle Surface")continue;
   var filter=r.GetComponent<MeshFilter>();
   if(filter==null)throw new InvalidOperationException("Missing triangle brush mesh");
   // Undo the export X reflection and importer Z reflection in source-canvas
   // coordinates. Destination position/rotation must not recolor a saved sketch.
   var canvas=Matrix4x4.Scale(new Vector3(-1,1,-1))*root.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
   var mesh=Build(filter.sharedMesh,canvas,guid==Faceted);
   var material=r.sharedMaterial;
   if(guid==Printing && lighting!=null) {
    var original=filter.GetComponent<OpenBrushVertexColorEncoding>()?.SourceMesh;
    if(original==null || !original.vertices.SequenceEqual(filter.sharedMesh.vertices))throw new InvalidOperationException("Source Gamma paint colors are unavailable");
    var gamma=original.colors;BakeLighting(mesh,canvas,filter.sharedMesh.triangles.Select(i=>gamma[i]).ToArray(),lighting);
    if(!litMaterials.TryGetValue(r.sharedMaterial,out material)) {
     material=new Material(r.sharedMaterial){name=r.sharedMaterial.name+"_SourceLighting"};material.SetColor("_TintColor",Color.white);material.SetOverrideTag("OpenBrushSourceFlatLighting","1");
     litMaterials.Add(r.sharedMaterial,material);
    }
   }
   var child=new GameObject("Original Triangle Surface");child.transform.SetParent(r.transform,false);
   child.AddComponent<MeshFilter>().sharedMesh=mesh;var surface=child.AddComponent<MeshRenderer>();surface.sharedMaterial=material;
   surface.shadowCastingMode=guid==Faceted?UnityEngine.Rendering.ShadowCastingMode.Off:r.shadowCastingMode;
   surface.receiveShadows=r.receiveShadows;r.enabled=false;count++;
  }
  if(count>0)Debug.Log("TRIANGLE_BRUSH_BAKE surfaces="+count);
 }
}
