using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class OpenBrushCompletedMeshCombineChecks {
    const string Marker="429ed64a-4e97-4466-84d3-145a861ef684";
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static Material Material(string guid=Marker) {
        var m=new Material(Shader.Find("OpenBrush/ExportedBrush"));m.SetOverrideTag("OpenBrushGuid",guid);
        m.SetFloat("_ZWrite",1);m.renderQueue=2000;return m;
    }
    static Mesh Triangle() {
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,1)};
        mesh.normals=Enumerable.Repeat(new Vector3(0,-1,1).normalized,3).ToArray();
        mesh.tangents=Enumerable.Repeat(new Vector4(1,0,0,1),3).ToArray();
        mesh.colors=new[]{new Color(.2f,.1f,.8f,.25f),new Color(.5f,.3f,.1f,.75f),new Color(.4f,.7f,.2f,1)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one};
        mesh.SetUVs(3,new List<Vector4>{new Vector4(1,2,3,4),new Vector4(5,6,7,8),new Vector4(9,10,11,12)});
        mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();return mesh;
    }
    static MeshRenderer Add(GameObject root,Material material,Vector3 position,Mesh mesh=null) {
        var go=new GameObject("stroke");go.transform.SetParent(root.transform,false);go.transform.localPosition=position;
        go.AddComponent<MeshFilter>().sharedMesh=mesh ?? Triangle();var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;return r;
    }
    sealed class Vertex {public Vector3 p,n,t;public float w;public Color color;public Vector2 uv;public Vector4 uv3;}
    static Vertex[] Read(GameObject root) {
        var vertices=new List<Vertex>();
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled)) {
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;var uv3=new List<Vector4>();mesh.GetUVs(3,uv3);
            var matrix=r.transform.localToWorldMatrix;var normal=matrix.inverse.transpose;
            for(int i=0;i<mesh.vertexCount;i++)vertices.Add(new Vertex{p=matrix.MultiplyPoint3x4(mesh.vertices[i]),n=normal.MultiplyVector(mesh.normals[i]).normalized,t=matrix.MultiplyVector(mesh.tangents[i]).normalized,w=mesh.tangents[i].w,color=mesh.colors[i],uv=mesh.uv[i],uv3=uv3[i]});
        }
        return vertices.ToArray();
    }
    static void Geometry() {
        var root=new GameObject("geometry");root.transform.position=new Vector3(1,2,3);root.transform.rotation=Quaternion.Euler(11,27,-9);root.transform.localScale=new Vector3(1.3f,.7f,1.8f);
        var mat=Material();var a=Add(root,mat,Vector3.zero);var b=Add(root,mat,new Vector3(.2f,.1f,0));Add(root,mat,new Vector3(20,0,0));
        a.transform.localScale=new Vector3(2,.7f,1.5f);a.transform.localRotation=Quaternion.Euler(15,-7,33);
        b.transform.localScale=new Vector3(.6f,1.2f,.8f);b.transform.localRotation=Quaternion.Euler(-13,28,40);
        // Real lit brushes carry preparation metadata. Keep decoded RGB/alpha,
        // and do not run gamma conversion a second time while combining.
        OpenBrushVertexColors.Prepare(a.GetComponent<MeshFilter>(),false);
        OpenBrushVertexColors.Prepare(b.GetComponent<MeshFilter>(),false);
        var before=Read(root);var report=OpenBrushCompletedMeshCombine.Prepare(root,true);var after=Read(root);
        Require(report.combinedMeshes==1 && report.mergedRenderers==2,"Nearby renderers combine; distant renderer stays separate");
        Require(before.Length==after.Length && report.visibleTrianglesBefore==report.visibleTrianglesAfter,"Topology counts retained");
        foreach(var vertex in before) {
            var match=after.OrderBy(v=>(v.p-vertex.p).sqrMagnitude).First();
            Require(Vector3.Distance(vertex.p,match.p)<.00001f,"World position retained");
            Require(Vector3.Distance(vertex.n,match.n)<.00001f,"Inverse-transpose normals retained under nonuniform scales");
            Require(Vector3.Distance(vertex.t,match.t)<.00001f && vertex.w==match.w,"Tangent orientation/handedness retained");
            Require(vertex.color==match.color && vertex.uv==match.uv && vertex.uv3==match.uv3,"RGBA and all UV dimensions retained");
        }
        Require(root.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial==mat),"Same material instance retained");
        UnityEngine.Object.DestroyImmediate(root);
    }
    static void Eligibility() {
        var root=new GameObject("eligibility");var mat=Material();
        var transparent=Material(OpenBrushSmokeImport.SmokeGuid);transparent.renderQueue=3000;
        var ta=Add(root,transparent,Vector3.zero);Add(root,transparent,Vector3.zero);
        var uniqueA=Add(root,mat,Vector3.zero);var uniqueB=Add(root,new Material(mat),Vector3.zero);
        var mirrored=Add(root,mat,Vector3.zero);mirrored.transform.localScale=new Vector3(-1,1,1);
        var block=Add(root,mat,Vector3.zero);var properties=new MaterialPropertyBlock();properties.SetFloat("_Gain",.5f);block.SetPropertyBlock(properties);
        var differentState=Add(root,mat,Vector3.zero);differentState.shadowCastingMode=ShadowCastingMode.Off;
        var moving=new GameObject("dynamic");moving.transform.SetParent(root.transform);moving.AddComponent<OpenBrushGeometryMotion>();var dr=Add(moving,mat,Vector3.zero);
        var report=OpenBrushCompletedMeshCombine.Prepare(root,true);
        Require(report.combinedMeshes==0,"Different material identity, render state and dynamic/unsupported inputs stay separate");
        Require(ta!=null && mirrored!=null && block!=null && dr!=null && uniqueA!=null && uniqueB!=null,"Excluded renderers survive");
        Require(report.skippedTransparent==2 && report.skippedDynamic==1,"Exclusion reasons recorded");
        bool rejected=false;try{OpenBrushCompletedMeshCombine.Prepare(root,false);}catch(InvalidOperationException){rejected=true;}
        Require(rejected,"Animated mode rejected before mutations");UnityEngine.Object.DestroyImmediate(root);
    }
    static void HiddenAndMotion() {
        var root=new GameObject("hidden");var mat=Material();var shared=Triangle();var hidden=Add(root,mat,Vector3.zero,shared);hidden.enabled=false;
        hidden.gameObject.AddComponent<OpenBrushVertexColorEncoding>().SourceMesh=shared;
        var motion=new GameObject("frames");motion.transform.SetParent(hidden.transform,false);
        var driver=motion.AddComponent<OpenBrushGeometryMotion>();driver.Frames=new[]{shared};driver.SourceMaterial=mat;
        var report=OpenBrushCompletedMeshCombine.Prepare(root,true);
        Require(report.removedHiddenRenderers==1 && root.GetComponentsInChildren<MeshRenderer>().Length==0,"Disabled source renderer no longer uploaded");
        Require(driver!=null && driver.Frames[0]==shared && shared.vertexCount==3,"Motion driver, transform and shared mesh survive pruning");
        UnityEngine.Object.DestroyImmediate(root);
    }
    static void ChunkLimit() {
        var root=new GameObject("chunks");var mat=Material();var mesh=new Mesh();
        mesh.vertices=new Vector3[10000];mesh.triangles=new[]{0,1,2};
        for(int i=0;i<7;i++)Add(root,mat,Vector3.zero,UnityEngine.Object.Instantiate(mesh));
        var report=OpenBrushCompletedMeshCombine.Prepare(root,true);
        Require(report.renderersAfter==2 && report.combinedMeshes==1,"Vertex budget splits large groups");
        Require(root.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.vertexCount<=OpenBrushCompletedMeshCombine.MaxVertices),"No oversized combined mesh");
        UnityEngine.Object.DestroyImmediate(root);
    }
    static void SharedMesh() {
        var root=new GameObject("instances");var mat=Material();var mesh=Triangle();
        for(int i=0;i<10;i++)Add(root,mat,new Vector3(i,0,0),mesh);
        var report=OpenBrushCompletedMeshCombine.Prepare(root,true);
        Require(report.combinedMeshes==0 && report.uniqueMeshesAfter==1 && report.skippedSharedMesh==10,"Already instanced geometry is not expanded into more assets");
        UnityEngine.Object.DestroyImmediate(root);
    }
    public static void Run() {try {
        Geometry();Eligibility();HiddenAndMotion();ChunkLimit();SharedMesh();
        Debug.Log("OPENBRUSH_COMPLETED_MESH_COMBINE_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}}
}
