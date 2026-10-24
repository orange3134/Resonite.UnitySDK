using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ResoniteLink;

// An explicit measurement fixture. Flat geometry with controlled unit normals,
// over an opaque black plate, isolates the native view-response from lighting.
public static class OpenBrushMatcapToonProbe {
    [Serializable] public class Report { public string rootId; public string[] materialIds; public bool completed; public string error; }
    static Mesh Quad(Vector3 normal,float width,float height) {
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0),new Vector3(width/2,height/2,0),new Vector3(-width/2,height/2,0)};
        mesh.normals=Enumerable.Repeat(normal,4).ToArray();mesh.colors=Enumerable.Repeat(Color.white,4).ToArray();
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();return mesh;
    }
    static Texture2D Bake(int row) {
        const int size=256;var pixels=new Color[size*size];
        var bloom=OpenBrushFireImport.BloomColor(row==1 ? new Color(.8f,.06f,.03f,1) : new Color(.12f,.06f,.18f,1));
        for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
            float u=2*(x+.5f)/size-1,v=2*(y+.5f)/size-1;
            float facing=Mathf.Pow(Mathf.Max(0,1-u*u-v*v),2.5f);
            pixels[y*size+x]=row==0 ? new Color(.1f*facing,.1f*facing,.1f*facing,1)
                : OpenBrushFireImport.DisplayEmission(new Color(bloom.r*facing,bloom.g*facing,bloom.b*facing,1));
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBAFloat,true,true){name="MatcapProbe"+row,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        texture.SetPixels(pixels);texture.Apply(true,false);return texture;
    }
    public static void Run() {
        string output=Path.GetFullPath(".local/matcap-toon-probe-v1.json");LinkInterface link=null;var report=new Report();
        try {
            if(File.Exists(output))throw new InvalidOperationException("Inspect existing probe report before retry");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Open Brush Toon Matcap Measurement v1");root.transform.position=new Vector3(112.4f,1.8f,2);
            var black=new Material(Shader.Find("OpenBrush/ExportedBrush")){name="Probe Black Backing"};black.SetOverrideTag("OpenBrushGuid","a8fea537-da7c-4d4b-817f-24f074725d6d");black.SetColor("_TintColor",Color.black);
            var plate=new GameObject("Black Backing");plate.transform.SetParent(root.transform,false);plate.transform.localPosition=new Vector3(0,0,.01f);
            plate.AddComponent<MeshFilter>().sharedMesh=Quad(Vector3.back,1.1f,.7f);plate.AddComponent<MeshRenderer>().sharedMaterial=black;
            float[] dots={-1,0,.25f,.5f,.75f,1};
            for(int row=0;row<3;row++) {
                var material=new Material(Shader.Find("OpenBrush/ExportedBrush")){name="MatcapProbeRow"+row,mainTexture=Bake(row)};
                material.SetOverrideTag("OpenBrushGuid",OpenBrushNeonImport.Guid);material.SetOverrideTag("OpenBrushViewEmissionToon","1");
                for(int column=0;column<dots.Length;column++) {
                    var local=new Vector3((column-2.5f)*.155f,(1-row)*.18f,0);
                    var view=(new Vector3(0,0,-.8f)-local).normalized;
                    var perpendicular=Vector3.Cross(view,Vector3.up).normalized;
                    var normal=view*dots[column]+perpendicular*Mathf.Sqrt(1-dots[column]*dots[column]);
                    var part=new GameObject("Row"+row+" Dot"+dots[column]);part.transform.SetParent(root.transform,false);part.transform.localPosition=local;
                    part.AddComponent<MeshFilter>().sharedMesh=Quad(normal,.11f,.11f);part.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
            }
            link=new LinkInterface();var session=Task.Run(async()=>{await link.Connect(new Uri("ws://localhost:47840"),CancellationToken.None);return await link.GetSessionData();}).GetAwaiter().GetResult();
            if(!session.Success)throw new InvalidOperationException(session.ErrorInfo);
            var window=ScriptableObject.CreateInstance<ResoniteLinkWindow>();window.ConvertSkybox=false;
            typeof(ResoniteLinkWindow).GetField("_linkInterface",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,link);
            typeof(ResoniteLinkWindow).GetField("_uniqueSessionId",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,session.UniqueSessionId);
            var converter=new SceneConverter();converter.EnsureInitialized(window);converter.EnsureAssetConverter();report.rootId=converter.GetTransformSlotId(root.transform);
            File.WriteAllText(output,JsonUtility.ToJson(report,true));converter.ConvertScene();
            report.materialIds=UnityEngine.Object.FindObjectsOfType<OpenBrushMaterialConverter>().Where(c=>c.ViewEmissionToon!=null).OrderBy(c=>c.Source.name).Select(c=>converter.GetId(c.ViewEmissionToon.Binding)).ToArray();
            report.completed=!converter.IsCorrupted;File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Debug.Log("OPENBRUSH_TOON_MATCAP_PROBE_SENT "+JsonUtility.ToJson(report));link.Dispose();EditorApplication.Exit(report.completed?0:1);
        }catch(Exception e){report.error=e.ToString();if(!File.Exists(output)||report.rootId!=null)File.WriteAllText(output,JsonUtility.ToJson(report,true));Debug.LogException(e);link?.Dispose();EditorApplication.Exit(1);}
    }
}


