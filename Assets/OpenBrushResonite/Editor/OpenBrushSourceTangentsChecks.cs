using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushSourceTangentsChecks {
 public static void Run(){try {
  const string folder="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/";
  foreach(var path in new[]{"bundle-experimental-paint-v2/drawing.glb","oriented-growth.glb","bundle-mixed-v2/drawing.glb"}) {
   using(var data=new AutoGltfFileParser(Path.Combine(folder,path)).Parse())
   using(var importer=new ImporterContext(data)) {
    var instance=importer.Load();
    var meshes=instance.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Distinct().ToArray();
    var positions=meshes.Select(m=>m.vertices).ToArray();var normals=meshes.Select(m=>m.normals).ToArray();
    var uv=meshes.Select(m=>m.uv).ToArray();var triangles=meshes.Select(m=>m.triangles).ToArray();
    int restored=OpenBrushSourceTangents.Restore(importer);
    var first=meshes.Select(m=>m.tangents).ToArray();
    if(OpenBrushSourceTangents.Restore(importer)!=restored)throw new Exception("Repeated restore changes coverage");
    for(int i=0;i<meshes.Length;i++) {
     if(!positions[i].SequenceEqual(meshes[i].vertices) || !normals[i].SequenceEqual(meshes[i].normals) ||
        !uv[i].SequenceEqual(meshes[i].uv) || !triangles[i].SequenceEqual(meshes[i].triangles) || !first[i].SequenceEqual(meshes[i].tangents))
      throw new Exception("Tangent restoration changed geometry/UVs or was not idempotent");
    }
    Debug.Log("TANGENT_REGRESSION "+path+": "+restored+" / "+meshes.Length+" meshes; geometry, UVs and repeated preparation preserved");
    UnityEngine.Object.DestroyImmediate(instance.gameObject);
   }
  }
  Debug.Log("OPENBRUSH_SOURCE_TANGENT_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
