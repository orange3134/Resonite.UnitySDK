using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniGLTF;
using UniJSON;

// Restore a custom glTF varying ignored by UniGLTF's generated attribute parser.
// Original Slice UVW is independent of paint color and the UV importer flip.
public static class OpenBrushSourceSlice {
 public const string Attribute="_OB_SLICE_UVW_V1";
 public static int Restore(ImporterContext importer) {
  var data=importer.Data;var json=JsonParser.Parse(data.Json);
  if(importer.Nodes.Count!=data.GLTF.nodes.Count)throw new InvalidOperationException("Slice node mapping changed");
  var jsonMeshes=json["meshes"].ArrayItems().ToArray();
  var pending=new List<Tuple<Mesh,Vector3[]>>();var seen=new HashSet<Mesh>();
  for(int i=0;i<data.GLTF.nodes.Count;i++) {
   int index=data.GLTF.nodes[i].mesh;if(index<0)continue;
   var primitives=jsonMeshes[index]["primitives"].ArrayItems().ToArray();
   if(!primitives.Any(p=>p["attributes"].ContainsKey(Attribute)))continue;
   if(primitives.Length!=1)throw new InvalidOperationException("Source Slice requires one primitive per prepared mesh");
   int accessor=primitives[0]["attributes"][Attribute].GetInt32();
   if(accessor<0 || accessor>=data.GLTF.accessors.Count)throw new InvalidOperationException("Invalid source Slice accessor");
   var a=data.GLTF.accessors[accessor];
   if(a.type!="VEC3" || (int)a.componentType!=5126 || a.normalized || a.sparse!=null)throw new InvalidOperationException("Expected dense float3 source Slice");
   var mesh=importer.Nodes[i].GetComponent<MeshFilter>()?.sharedMesh;
   if(mesh==null)throw new InvalidOperationException("Expected prepared static Slice mesh");
   if(!seen.Add(mesh))continue;
   var values=data.GetArrayFromAccessor<Vector3>(accessor).ToArray();
   var positions=data.GetArrayFromAccessor<Vector3>(data.GLTF.meshes[index].primitives[0].attributes.POSITION).ToArray();
   if(values.Length!=mesh.vertexCount || positions.Length!=mesh.vertexCount)throw new InvalidOperationException("Source Slice vertex count mismatch");
   var imported=mesh.vertices;
   for(int v=0;v<values.Length;v++) {
    var p=positions[v];p.z=-p.z;
    if((p-imported[v]).sqrMagnitude>1e-10f)throw new InvalidOperationException("Source Slice vertex order changed");
    for(int c=0;c<3;c++)if(!float.IsFinite(values[v][c]) || values[v][c]<0 || (c<2 && values[v][c]>1))throw new InvalidOperationException("Source Slice outside bounded range");
   }
   if(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2))throw new InvalidOperationException("Source Slice UV channel already occupied");
   pending.Add(Tuple.Create(mesh,values));
  }
  foreach(var item in pending)item.Item1.SetUVs(2,item.Item2);
  Debug.Log("Open Brush source Slice restored: "+pending.Count+" meshes.");return pending.Count;
 }

}
