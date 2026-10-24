using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniGLTF;

// UniGLTF MeshUploader always recalculates tangents. Open Brush supplies its own
// smoothed tangent frames; recalculating separately for each growth section adds
// visible shading seams. Restore explicit source frames after the default Z-flip
// importer has loaded the prepared model, before the SDK clones/uploads meshes.
public static class OpenBrushSourceTangents {
 public static int Restore(ImporterContext importer) {
  var data=importer.Data;
  if(importer.Nodes.Count!=data.GLTF.nodes.Count)throw new InvalidOperationException("GLB node mapping changed");
  var pending=new List<Tuple<Mesh,Vector4[]>>();
  var seen=new HashSet<Mesh>();
  for(int i=0;i<data.GLTF.nodes.Count;i++) {
   int meshIndex=data.GLTF.nodes[i].mesh;
   if(meshIndex<0)continue;
   var primitives=data.GLTF.meshes[meshIndex].primitives;
   if(!primitives.Any(p=>p.attributes.TANGENT>=0))continue;
   if(primitives.Count!=1)throw new InvalidOperationException("Source tangent restoration requires one primitive per prepared mesh");
   var primitive=primitives[0];
   // RuntimeGltfInstance takes mesh ownership during Load(), emptying Meshes;
   // Nodes retains the glTF index mapping and references the owned meshes.
   var mesh=importer.Nodes[i].GetComponent<MeshFilter>()?.sharedMesh;
   if(mesh==null)throw new InvalidOperationException("Expected a prepared static mesh renderer");
   if(!seen.Add(mesh))continue;
   var positions=data.GetArrayFromAccessor<Vector3>(primitive.attributes.POSITION).ToArray();
   var tangents=data.GetArrayFromAccessor<Vector4>(primitive.attributes.TANGENT).ToArray();
   if(tangents.Length!=mesh.vertexCount || positions.Length!=mesh.vertexCount)throw new InvalidOperationException("Source tangent vertex count mismatch");
   var importedPositions=mesh.vertices;
   for(int v=0;v<tangents.Length;v++) {
    var p=positions[v];p.z=-p.z;
    if((p-importedPositions[v]).sqrMagnitude>1e-10f)throw new InvalidOperationException("Source vertex order or importer axis changed");
    var t=tangents[v];
    if(float.IsNaN(t.x+t.y+t.z+t.w) || float.IsInfinity(t.x+t.y+t.z+t.w) ||
       new Vector3(t.x,t.y,t.z).sqrMagnitude<1e-10f || Mathf.Abs(Mathf.Abs(t.w)-1)>1e-5f)
     throw new InvalidOperationException("Invalid source tangent frame");
    // Reflecting Z changes handedness; UniGLTF also reverses V, changing it
    // back. Therefore XYZ.z flips, while tangent.w remains the glTF value.
    t.z=-t.z;tangents[v]=t;
   }
   pending.Add(Tuple.Create(mesh,tangents));
  }
  foreach(var item in pending)item.Item1.tangents=item.Item2;
  Debug.Log("Open Brush source tangent frames restored: "+pending.Count+" meshes.");
  return pending.Count;
 }
}
