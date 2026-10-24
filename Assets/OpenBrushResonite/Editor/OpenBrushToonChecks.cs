using System;
using UnityEngine;
using UnityEditor;
public static class OpenBrushToonChecks {
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){try {
        var root=new GameObject("Toon Check");var stroke=new GameObject("ToonSample");stroke.transform.SetParent(root.transform,false);
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.normals=new[]{Vector3.back,Vector3.back,Vector3.back};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up};mesh.colors=new[]{Color.red,Color.red,Color.red};mesh.triangles=new[]{0,1,2};
        stroke.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=stroke.AddComponent<MeshRenderer>();renderer.sharedMaterial=new Material(Shader.Find("Unlit/Color"));var original=renderer.sharedMaterial;
        var identity=new OpenBrushSmokeImport.Identity{brushGuid=OpenBrushToonImport.Guid};
        var manifest=new OpenBrushSmokeImport.Manifest{schemaVersion=1,materials=new[]{new OpenBrushSmokeImport.Entry{openBrush=identity,targets=new[]{new OpenBrushSmokeImport.Target{nodeName="ToonSample"}}}}};
        bool rejected=false;try{OpenBrushMaterialImport.Prepare(root,manifest,false);}catch(InvalidOperationException e){rejected=e.Message.Contains("source vertex shading");}
        Require(rejected && renderer.sharedMaterial==original && stroke.transform.childCount==0,"Unprepared Toon fails before mutation");
        identity.vertexBake="toon-world-y-v1";
        Require(OpenBrushMaterialImport.Prepare(root,manifest,false)==1,"Prepared Toon accepts its manifest");
        OpenBrushToonImport.Prepare(root);
        var child=stroke.transform.Find("Toon - Native Outline");
        Require(child!=null && child.GetComponent<MeshFilter>().sharedMesh==mesh,"Outline follows the same source/growth transform and geometry");
        Require(child.GetComponent<MeshRenderer>().sharedMaterial.GetTag("OpenBrushToonOutline",false,"")=="1","Outline uses its dedicated native conversion");
        Debug.Log("OPENBRUSH_TOON_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
