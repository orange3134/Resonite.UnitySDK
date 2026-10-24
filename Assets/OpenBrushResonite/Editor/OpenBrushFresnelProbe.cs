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
public static class OpenBrushFresnelProbe {
    [Serializable] public class Report { public string rootId; public string[] materialIds; public bool completed; public string error; }
    static Mesh Quad(Vector3 normal,float width,float height) {
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0),new Vector3(width/2,height/2,0),new Vector3(-width/2,height/2,0)};
        mesh.normals=Enumerable.Repeat(normal,4).ToArray();mesh.colors=Enumerable.Repeat(Color.white,4).ToArray();
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();return mesh;
    }
    public static void Run() {
        string output=Path.GetFullPath(".local/fresnel-probe-v1.json");LinkInterface link=null;var report=new Report();
        try {
            if(File.Exists(output))throw new InvalidOperationException("Inspect existing probe report before retry");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Open Brush Fresnel Measurement v1");root.transform.position=new Vector3(106,1.8f,2);
            var black=new Material(Shader.Find("OpenBrush/ExportedBrush")){name="Probe Black Backing"};black.SetOverrideTag("OpenBrushGuid","a8fea537-da7c-4d4b-817f-24f074725d6d");black.SetColor("_TintColor",Color.black);
            var plate=new GameObject("Black Backing");plate.transform.SetParent(root.transform,false);plate.transform.localPosition=new Vector3(0,0,.01f);
            plate.AddComponent<MeshFilter>().sharedMesh=Quad(Vector3.back,1.1f,.7f);plate.AddComponent<MeshRenderer>().sharedMaterial=black;
            float[] dots={-1,0,.25f,.5f,.75f,1};
            for(int row=0;row<3;row++) {
                var material=new Material(Shader.Find("OpenBrush/ExportedBrush")){name="FresnelProbeRow"+row,mainTexture=Texture2D.whiteTexture};
                material.SetOverrideTag("OpenBrushGuid",OpenBrushNeonImport.Guid);material.SetOverrideTag("OpenBrushNeonEmission","1");
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
            report.materialIds=UnityEngine.Object.FindObjectsOfType<OpenBrushMaterialConverter>().Where(c=>c.Neon!=null).OrderBy(c=>c.Source.name).Select(c=>converter.GetId(c.Neon.Binding)).ToArray();
            report.completed=!converter.IsCorrupted;File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Debug.Log("OPENBRUSH_FRESNEL_PROBE_SENT "+JsonUtility.ToJson(report));link.Dispose();EditorApplication.Exit(report.completed?0:1);
        }catch(Exception e){report.error=e.ToString();if(!File.Exists(output)||report.rootId!=null)File.WriteAllText(output,JsonUtility.ToJson(report,true));Debug.LogException(e);link?.Dispose();EditorApplication.Exit(1);}
    }
}
