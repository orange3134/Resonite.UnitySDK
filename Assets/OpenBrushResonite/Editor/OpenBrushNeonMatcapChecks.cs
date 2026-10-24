using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushNeonMatcapChecks {
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){try {
        for(int i=0;i<500;i++) {
            float pulse=Mathf.Pow(10,-6*i/499f);
            float restored=OpenBrushNeonMatcapImport.PulseValue(OpenBrushNeonMatcapImport.PulseKey(pulse));
            Require(Mathf.Abs(restored-pulse)<=pulse*.011f,"Pulse quantization exceeds 1.1 percent");
        }
        var color=new Color(.12f,.06f,.18f,1);var bloom=OpenBrushFireImport.BloomColor(color);
        for(int i=0;i<=100;i++) {
            float n=i/100f;var actual=OpenBrushNeonMatcapImport.Shade(color,.43f,n);
            float reference=Mathf.GammaToLinearSpace(Mathf.Clamp01(bloom.b*.43f*Mathf.Pow(n,5)));
            Require(Mathf.Abs(actual.b-reference)<.000001f,"Source HDR/view multiplication must precede display clipping");
        }
        var root=new GameObject("NeonMatcapCheck");var child=new GameObject("Stroke");child.transform.SetParent(root.transform,false);
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.normals=Enumerable.Repeat(Vector3.back,3).ToArray();
        mesh.uv=new[]{Vector2.zero,new Vector2(.02f,0),new Vector2(.02f,1)};mesh.colors=Enumerable.Repeat(color,3).ToArray();mesh.triangles=new[]{0,1,2};
        child.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.AddComponent<MeshRenderer>();
        var source=new Material(Shader.Find("OpenBrush/ExportedBrush"));source.SetOverrideTag("OpenBrushGuid",OpenBrushNeonImport.Guid);renderer.sharedMaterial=source;
        OpenBrushNeonMatcapImport.Prepare(root);
        int rendererCount=root.GetComponentsInChildren<MeshRenderer>().Length;
        OpenBrushNeonMatcapImport.Prepare(root);Require(root.GetComponentsInChildren<MeshRenderer>().Length==rendererCount,"Idempotence");
        Require(renderer.sharedMaterial.GetColor("_TintColor")==Color.black && renderer.enabled,"Gloss body retained");
        var native=new FrooxEngine.PBS_VertexColorSpecular();
        OpenBrushMaterialConverter.ConfigureSpecular(renderer.sharedMaterial,OpenBrushProfiles.Find(OpenBrushNeonImport.Guid),native);
        Require(native.OffsetUnits==1,"Body depth bias keeps coincident view emission visible");
        Require(mesh.vertexCount==3 && mesh.colors[0]==color,"Input mesh untouched");
        float area=0;
        foreach(var filter in child.GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject!=child)) {
            var part=filter.sharedMesh;var p=part.vertices;var uv=part.uv;
            for(int t=0;t<p.Length;t+=3) {
                area+=Vector3.Cross(p[t+1]-p[t],p[t+2]-p[t]).magnitude*.5f;
                float lo=Mathf.Min(uv[t].x,Mathf.Min(uv[t+1].x,uv[t+2].x));
                float hi=Mathf.Max(uv[t].x,Mathf.Max(uv[t+1].x,uv[t+2].x));
                Require(hi-lo<=1f/512+.000001f,"UV phase span bounded");
            }
        }
        Require(Mathf.Abs(area-.5f)<.00001f,"Subdivision preserves source surface area");
        foreach(var motion in root.GetComponentsInChildren<OpenBrushTextureMotion>())
            Require(motion.Frames.Length==80 && motion.FramesPerSecond==60 && motion.Frames.All(t=>t!=null),"Complete 60fps pulse sequence");
        child.transform.localScale=Vector3.zero;
        Require(child.GetComponentsInChildren<MeshRenderer>().All(r=>r.transform.lossyScale==Vector3.zero),"Emission follows parent drawing visibility");
        Debug.Log("OPENBRUSH_NEON_MATCAP_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
