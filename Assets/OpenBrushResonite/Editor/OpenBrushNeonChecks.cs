using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushNeonChecks {
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){try {
        // Compare to source abs/modulo equation at a positive, integer-shifted phase.
        for(int i=0;i<300;i++) {
            float u=(i%31)*.0517f-.5f,t=(i%41)*.0271f;
            float reference=Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(.2f-(Mathf.Abs(u-.75f*(t+40))%1))*10,5));
            Require(Mathf.Abs(reference-OpenBrushNeonImport.Pulse(u,t))<.0002f,"Source pulse mismatch");
            Require(Mathf.Abs(OpenBrushNeonImport.Pulse(u,t)-OpenBrushNeonImport.Pulse(u,t+4f/3))<.00003f,"Pulse period mismatch");
        }
        var root=new GameObject("NeonCheck");var child=new GameObject("Stroke");child.transform.SetParent(root.transform,false);
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.normals=Enumerable.Repeat(Vector3.forward,3).ToArray();
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up};mesh.colors=Enumerable.Repeat(new Color(.12f,.06f,.18f,1),3).ToArray();mesh.triangles=new[]{0,1,2};
        child.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.AddComponent<MeshRenderer>();
        var source=new Material(Shader.Find("OpenBrush/ExportedBrush"));source.SetOverrideTag("OpenBrushGuid",OpenBrushNeonImport.Guid);renderer.sharedMaterial=source;
        OpenBrushNeonImport.Prepare(root);OpenBrushNeonImport.Prepare(root);
        var motions=root.GetComponentsInChildren<OpenBrushTextureMotion>();Require(motions.Length==1 && motions[0].Frames.Length==40,"Idempotent forty-frame emission");
        Require(renderer.enabled && renderer.sharedMaterial.GetColor("_TintColor")==Color.black,"Opaque black body retained");
        Require(mesh.colors[0].r==.12f && mesh.triangles.Length==3,"Source mesh preserved");
        Require(root.GetComponentsInChildren<MeshRenderer>().Length==2,"One emission overlay");
        var native=new FrooxEngine.FresnelMaterial();OpenBrushMaterialConverter.ConfigureNeon(native);
        Require(native.Exponent==5 && native.NearColor.color.r==1 && native.FarColor.color.r==0,"View-dependent material configured");
        var bodyNative=new FrooxEngine.PBS_VertexColorSpecular();
        OpenBrushMaterialConverter.ConfigureSpecular(renderer.sharedMaterial,OpenBrushProfiles.Find(OpenBrushNeonImport.Guid),bodyNative);
        Require(Mathf.Abs(bodyNative.SpecularColor.color.r-.05f)<.000001f,"Gamma source specular coefficient is preserved as linear BRDF input");
        Debug.Log("OPENBRUSH_NEON_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
