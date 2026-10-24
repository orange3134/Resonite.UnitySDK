// Equations adapted from Open Brush Disco.shadergraph / bakeDisco.compute.
// Tilt Brush Authors, Apache-2.0. Glint gain is calibrated for Resonite bloom.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class OpenBrushDiscoImport
{
    public const string Guid="4391aaaa-df73-4396-9e33-31e4e4930b27";
    public const int FrameCount=48;
    public const float Period=Mathf.PI/2;
    [Serializable] public class Data { public int schemaVersion;public string brushGuid,coordinateSystem;public Part[] parts; }
    [Serializable] public class Part { public string nodeName;public float[] times,strokeStarts,strokeEnds;public Vector3[] positions,normals,uvRadius;public Color[] colors;public int[] triangles; }
    [MenuItem("Resonite SDK/Open Brush/Add Disco Motion to Prepared Model")]
    public static void PrepareSelected() {
        var root=Selection.activeGameObject;
        if(root==null || EditorUtility.IsPersistent(root))throw new InvalidOperationException("Select the prepared model root");
        var path=EditorUtility.OpenFilePanel("Disco raw motion JSON","","json");if(string.IsNullOrEmpty(path))return;
        Prepare(root,JsonUtility.FromJson<Data>(File.ReadAllText(path)),true);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
    }
    public static Vector3 Deform(Vector3 position,Vector3 normal,Vector3 uvRadius,float seconds) {
        float wave=Mathf.Sin(seconds*2+uvRadius.x*10+(uvRadius.y%1)*10);
        return position+normal*(wave*wave*.6f*uvRadius.z);
    }
    public static void Prepare(GameObject root,Data data,bool saveAssets=false) {
        if(data==null || data.schemaVersion!=1 || data.brushGuid!=Guid || data.coordinateSystem!="UnityInvertZ" || data.parts==null || data.parts.Length==0)
            throw new InvalidOperationException("Expected verified Disco raw geometry");
        if(data.parts.Select(p=>p.nodeName).Distinct().Count()!=data.parts.Length)throw new InvalidOperationException("Duplicate Disco source nodes");
        // Preflight identities and the coordinate conversion before hiding any existing chunks.
        foreach(var part in data.parts) {
            var parents=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name==part.nodeName).ToArray();
            if(parents.Length!=1)throw new InvalidOperationException("Ambiguous Disco source node: "+part.nodeName);
            var parent=parents[0];if(parent.GetComponentInChildren<OpenBrushMeshMotion>(true)!=null)continue;
            if(part.positions.Length!=part.normals.Length || part.positions.Length!=part.colors.Length || part.positions.Length!=part.uvRadius.Length || part.triangles.Length%3!=0 || part.triangles.Any(i=>i<0 || i>=part.positions.Length))throw new InvalidOperationException("Invalid raw geometry");
            var renderers=parent.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length==0 || renderers.Any(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=Guid))throw new InvalidOperationException("Disco material identity mismatch");
            var bounds=new Bounds(part.positions[0],Vector3.zero);foreach(var p in part.positions)bounds.Encapsulate(p);
            var imported=new Bounds();bool first=true;
            foreach(var r in renderers)foreach(var p in r.GetComponent<MeshFilter>().sharedMesh.vertices) {
                var q=parent.InverseTransformPoint(r.transform.TransformPoint(p));
                if(first){imported=new Bounds(q,Vector3.zero);first=false;}else imported.Encapsulate(q);
            }
            if((bounds.min-imported.min).magnitude>1e-4 || (bounds.max-imported.max).magnitude>1e-4)
                throw new InvalidOperationException("Raw Disco and prepared model coordinates do not match");
        }
        foreach(var part in data.parts) {
            var parent=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==part.nodeName);
            if(parent.GetComponentInChildren<OpenBrushMeshMotion>(true)!=null)continue;
            var renderers=parent.GetComponentsInChildren<MeshRenderer>(true);
            var child=new GameObject("Disco - Facet Wave Motion");child.transform.SetParent(parent,false);
            var motion=child.AddComponent<OpenBrushMeshMotion>();// Each batch drives a different glint atlas, even if its source material is shared.
            motion.SourceMaterial=new Material(renderers[0].sharedMaterial){name=renderers[0].sharedMaterial.name+" - "+part.nodeName};
            motion.SourceMaterial.SetOverrideTag("OpenBrushDiscoSurface","1");
            motion.SourceMaterial.mainTexture=SpecularAtlas(part);
            Bake(part,parent,out motion.Frames,out motion.GlintFrames);motion.FramesPerSecond=FrameCount/Period;
            if(saveAssets) {
                if(!AssetDatabase.IsValidFolder("Assets/OpenBrushGenerated"))AssetDatabase.CreateFolder("Assets","OpenBrushGenerated");
                AssetDatabase.CreateAsset(motion.SourceMaterial.mainTexture,AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/DiscoSpecular.asset"));
                AssetDatabase.CreateAsset(motion.SourceMaterial,AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/DiscoMotion.mat"));
                foreach(var mesh in motion.Frames)AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/"+mesh.name+".asset"));
                foreach(var texture in motion.GlintFrames)AssetDatabase.CreateAsset(texture,AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/"+texture.name+".asset"));
            }
            foreach(var renderer in renderers)renderer.enabled=false;
        }
        if(saveAssets)AssetDatabase.SaveAssets();
    }
    // Normal URP material: black diffuse, __SpecColor=.5137255, AO=0.
    // Bake the source Gamma color product before decoding for native Linear PBS.
    // With _SPECGLOSSMAP, native PBS reads smoothness from the map alpha;
    // SpecularColor alpha (_Glossiness) applies only when the map is absent.
    public static Color SpecularColor(Color color) {
        const float gain=.5137255f;
        return new Color(Mathf.GammaToLinearSpace(color.r*gain),Mathf.GammaToLinearSpace(color.g*gain),Mathf.GammaToLinearSpace(color.b*gain),.65f);
    }
    public static Color EmissionColor(Color color,float normalY) {
        float glint=200*Mathf.Pow(Mathf.Abs(normalY),100);
        return new Color(Mathf.GammaToLinearSpace(Mathf.Clamp01(color.r*glint)),Mathf.GammaToLinearSpace(Mathf.Clamp01(color.g*glint)),Mathf.GammaToLinearSpace(Mathf.Clamp01(color.b*glint)),1);
    }
    static Texture2D SpecularAtlas(Part part) {
        int faces=part.triangles.Length/3;
        if(faces>SystemInfo.maxTextureSize)throw new InvalidOperationException("Disco face atlas exceeds texture width; split source batch");
        var texture=new Texture2D(faces,1,TextureFormat.RGBAFloat,false,true){name="DiscoSpecular",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var colors=new Color[faces];
        for(int i=0;i<faces;i++) {
            var a=part.colors[part.triangles[i*3]];var b=part.colors[part.triangles[i*3+1]];var c=part.colors[part.triangles[i*3+2]];
            if(a!=b || a!=c)throw new InvalidOperationException("Disco per-face specular requires uniform vertex color");
            colors[i]=SpecularColor(a);
        }
        texture.SetPixels(colors);texture.Apply(false,false);return texture;
    }
    public static void Bake(Part part,Transform parent,out Mesh[] frames,out Texture2D[] glints) {
        frames=new Mesh[FrameCount];glints=new Texture2D[FrameCount];
        int count=part.triangles.Length,faces=count/3;
        var indices=Enumerable.Range(0,count).ToArray();
        for(int frame=0;frame<FrameCount;frame++) {
            var positions=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var uv=new Vector2[count];var light=new Color[faces];
            for(int i=0;i<count;i++){int v=part.triangles[i];positions[i]=Deform(part.positions[v],part.normals[v],part.uvRadius[v],frame*Period/FrameCount);colors[i]=part.colors[v];uv[i]=new Vector2((i/3+.5f)/faces,.5f);}
            // Source shader uses screen derivatives: split each face so its moving normal stays flat.
            for(int face=0;face<faces;face++) {
                int i=face*3;var normal=Vector3.Cross(positions[i+1]-positions[i],positions[i+2]-positions[i]).normalized;
                normals[i]=normals[i+1]=normals[i+2]=normal;
                float normalY=parent.TransformDirection(normal).normalized.y;
                var color=(colors[i]+colors[i+1]+colors[i+2])/3;light[face]=EmissionColor(color,normalY);
            }
            var mesh=new Mesh {name="DiscoWave_"+frame.ToString("D2"),indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=positions;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();frames[frame]=mesh;
            var texture=new Texture2D(faces,1,TextureFormat.RGBAFloat,false,true){name="DiscoGlints_"+frame.ToString("D2"),filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(light);texture.Apply(false,false);glints[frame]=texture;
        }
    }
}
