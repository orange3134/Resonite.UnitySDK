using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniGLTF;
using UniJSON;

// Restore a custom glTF varying ignored by UniGLTF's generated attribute parser.
// Original COLOR_0 stays intact: ChromaticWave uses it for its wave frequency.
public static class OpenBrushSourceBloom {
 public const string Attribute="_OB_BLOOM_V1";
 public static int Restore(ImporterContext importer) {
  var data=importer.Data;var json=JsonParser.Parse(data.Json);
  if(importer.Nodes.Count!=data.GLTF.nodes.Count)throw new InvalidOperationException("Bloom node mapping changed");
  var jsonMeshes=json["meshes"].ArrayItems().ToArray();
  var pending=new List<Tuple<Mesh,Vector4[]>>();var seen=new HashSet<Mesh>();
  for(int i=0;i<data.GLTF.nodes.Count;i++) {
   int index=data.GLTF.nodes[i].mesh;if(index<0)continue;
   var primitives=jsonMeshes[index]["primitives"].ArrayItems().ToArray();
   if(!primitives.Any(p=>p["attributes"].ContainsKey(Attribute)))continue;
   if(primitives.Length!=1)throw new InvalidOperationException("Source bloom requires one primitive per prepared mesh");
   int accessor=primitives[0]["attributes"][Attribute].GetInt32();
   if(accessor<0 || accessor>=data.GLTF.accessors.Count)throw new InvalidOperationException("Invalid source bloom accessor");
   var a=data.GLTF.accessors[accessor];
   if(a.type!="VEC4" || (int)a.componentType!=5126 || a.normalized || a.sparse!=null)throw new InvalidOperationException("Expected dense float4 source bloom");
   var mesh=importer.Nodes[i].GetComponent<MeshFilter>()?.sharedMesh;
   if(mesh==null)throw new InvalidOperationException("Expected prepared static bloom mesh");
   if(!seen.Add(mesh))continue;
   var values=data.GetArrayFromAccessor<Vector4>(accessor).ToArray();
   var positions=data.GetArrayFromAccessor<Vector3>(data.GLTF.meshes[index].primitives[0].attributes.POSITION).ToArray();
   if(values.Length!=mesh.vertexCount || positions.Length!=mesh.vertexCount)throw new InvalidOperationException("Source bloom vertex count mismatch");
   var imported=mesh.vertices;
   for(int v=0;v<values.Length;v++) {
    var p=positions[v];p.z=-p.z;
    if((p-imported[v]).sqrMagnitude>1e-10f)throw new InvalidOperationException("Source bloom vertex order changed");
    for(int c=0;c<4;c++)if(float.IsNaN(values[v][c]) || values[v][c]<0 || values[v][c]>1)throw new InvalidOperationException("Source bloom outside bounded range");
   }
   if(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2))throw new InvalidOperationException("Source bloom UV channel already occupied");
   pending.Add(Tuple.Create(mesh,values));
  }
  foreach(var item in pending)item.Item1.SetUVs(2,item.Item2);
  Debug.Log("Open Brush source bloom restored: "+pending.Count+" meshes.");return pending.Count;
 }

 public static Vector4[] Read(Mesh mesh) {
  var result=new List<Vector4>();mesh.GetUVs(2,result);
  if(result.Count==mesh.vertexCount && mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord2)==4)return result.ToArray();
  if(result.Count!=0)throw new InvalidOperationException("Unexpected source bloom channel");
  // Legacy bundles are exact only when each original triangle has constant
  // RGBA. Reject gradients instead of silently baking a subdivision-dependent
  // result. Regenerating the bundle adds the explicit versioned varying.
  var colors=mesh.colors;var indices=mesh.triangles;
  for(int i=0;i<indices.Length;i+=3)
   if(!colors[indices[i]].Equals(colors[indices[i+1]]) || !colors[indices[i]].Equals(colors[indices[i+2]]))
    throw new InvalidOperationException("Gradient bloom needs original vertex varyings; regenerate this Open Brush bundle.");
  return colors.Select(c=>{
   float m=new Vector3(c.r,c.g,c.b).magnitude*.05f;
   return new Vector4(Mathf.Pow(Mathf.Max(c.r,m),2.2f),Mathf.Pow(Mathf.Max(c.g,m),2.2f),Mathf.Pow(Mathf.Max(c.b,m),2.2f),Mathf.Pow(c.a,2.2f));
  }).ToArray();
 }
}
