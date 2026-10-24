using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// Run only after all brush-specific bakes, before any SDK converters are created.
// Identity is the actual Material instance, never merely its brush GUID/name.
public static class OpenBrushCompletedMeshCombine {
    public const int MaxVertices=65535;
    [Serializable] public sealed class Report {
        public int renderersBefore,renderersAfter,visibleBefore,visibleAfter;
        public int uniqueMeshesBefore,uniqueMeshesAfter,removedHiddenRenderers,mergedRenderers,combinedMeshes;
        public int skippedTransparent,skippedDynamic,skippedUnsupported,skippedSingleton,skippedSharedMesh;
        public long visibleTrianglesBefore,visibleTrianglesAfter;
    }
    sealed class Item {public MeshRenderer renderer;public Mesh mesh;public Matrix4x4 matrix;}

    // ColorEncoding is preparation metadata, not a motion driver. Its decoded
    // colors are already in sharedMesh; keeping these renderers separate would
    // unnecessarily exclude most lit brushes.
    static bool PlainRenderer(MeshRenderer renderer) => renderer.GetComponents<Component>().All(c=>c is Transform || c is MeshFilter || c is MeshRenderer || c is OpenBrushVertexColorEncoding);
    static bool StaticPath(Transform node,Transform root) {
        for(var t=node;t!=null;t=t.parent) {
            if(t.GetComponent<Animator>()!=null || t.GetComponent<Animation>()!=null || t.GetComponent<OpenBrushGrowthPart>()!=null)return false;
            if(t!=root && t.GetComponents<MonoBehaviour>().Any(c=>!(c is OpenBrushTextureMotion) && !(c is OpenBrushVertexColorEncoding)))return false;
            if(t==root)return true;
        }
        return false;
    }
    static bool Opaque(Material material) {
        if(material==null || material.shader==null || material.shader.name!="OpenBrush/ExportedBrush")return false;
        var guid=material.GetTag("OpenBrushGuid",false,"");
        if(string.IsNullOrEmpty(guid))return false;
        var profile=OpenBrushProfiles.Find(guid);
        // Alpha/additive/multiply sorting can change when renderer bounds merge.
        // Special native view-dependent material paths also remain untouched.
        return !profile.Transparent && !profile.Additive && !profile.Multiply &&
            material.renderQueue<=2500 && material.GetFloat("_ZWrite")!=0 &&
            material.GetTag("OpenBrushToonOutline",false,"")!="1" &&
            material.GetTag("OpenBrushDiamondBody",false,"")!="1" &&
            material.GetTag("OpenBrushMylarBody",false,"")!="1";
    }
    static string Key(Item item,Vector3 origin,float cellSize) {
        var r=item.renderer;
        var center=item.matrix.MultiplyPoint3x4(item.mesh.bounds.center);
        var cell=Vector3Int.FloorToInt((center-origin)/cellSize);
        var layout=string.Join(";",item.mesh.GetVertexAttributes().Select(a=>$"{a.attribute}:{a.format}:{a.dimension}:{a.stream}"));
        return $"{r.sharedMaterial.GetInstanceID()}|{r.gameObject.layer}|{r.shadowCastingMode}|{r.receiveShadows}|{r.motionVectorGenerationMode}|{r.sortingLayerID}|{r.sortingOrder}|{r.renderingLayerMask}|{r.lightProbeUsage}|{r.reflectionProbeUsage}|{cell}|{layout}";
    }
    static long Triangles(IEnumerable<MeshRenderer> renderers) {
        long result=0;
        foreach(var r in renderers)if(r.enabled && r.gameObject.activeInHierarchy) {
            var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
            for(int sub=0;sub<mesh.subMeshCount;sub++)if(mesh.GetTopology(sub)==MeshTopology.Triangles)result+=mesh.GetIndexCount(sub)/3;
        }
        return result;
    }
    static int UniqueMeshes(IEnumerable<MeshRenderer> renderers)=>renderers.Select(r=>r.GetComponent<MeshFilter>()?.sharedMesh).Where(m=>m!=null).Distinct().Count();
    static void RemoveRenderer(MeshRenderer renderer,HashSet<GameObject> emptied) {
        var go=renderer.gameObject;var filter=go.GetComponent<MeshFilter>();
        UnityEngine.Object.DestroyImmediate(renderer);
        if(filter!=null)UnityEngine.Object.DestroyImmediate(filter);
        var encoding=go.GetComponent<OpenBrushVertexColorEncoding>();
        if(encoding!=null)UnityEngine.Object.DestroyImmediate(encoding);
        // Do not destroy the mesh asset: motion frames/colliders may share it.
        emptied.Add(go);
    }
    static void Prune(HashSet<GameObject> emptied,Transform root) {
        foreach(var entry in emptied) {
            if(entry==null)continue;
            var t=entry.transform;
            while(t!=null && t!=root && t.childCount==0 && t.GetComponents<Component>().Length==1) {
                var parent=t.parent;UnityEngine.Object.DestroyImmediate(t.gameObject);t=parent;
            }
        }
    }
    public static Report Prepare(GameObject root,bool completed) {
        if(!completed || root.GetComponentsInChildren<OpenBrushGrowthPart>(true).Length!=0)
            throw new InvalidOperationException("Mesh combining requires completed mode without stroke growth.");
        var result=new Report();var original=root.GetComponentsInChildren<MeshRenderer>(true);
        result.renderersBefore=original.Length;result.uniqueMeshesBefore=UniqueMeshes(original);
        result.visibleBefore=original.Count(r=>r.enabled && r.gameObject.activeInHierarchy);
        result.visibleTrianglesBefore=Triangles(original);
        var emptied=new HashSet<GameObject>();var candidates=new List<Item>();
        // Preserve existing mesh instancing: flattening repeated references can
        // increase assets/geometry instead of reducing them.
        var sharedMeshes=new HashSet<Mesh>(original.Select(r=>r.GetComponent<MeshFilter>()?.sharedMesh).Where(m=>m!=null).GroupBy(m=>m).Where(g=>g.Count()>1).Select(g=>g.Key));
        var worldToRoot=root.transform.worldToLocalMatrix;
        foreach(var renderer in original) {
            if(!PlainRenderer(renderer) || !StaticPath(renderer.transform,root.transform)) {result.skippedDynamic++;continue;}
            if(!renderer.enabled) {RemoveRenderer(renderer,emptied);result.removedHiddenRenderers++;continue;}
            if(!renderer.gameObject.activeInHierarchy || renderer.HasPropertyBlock() || renderer.lightmapIndex>=0 || renderer.realtimeLightmapIndex>=0 || renderer.probeAnchor!=null) {result.skippedUnsupported++;continue;}
            var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if(renderer.sharedMaterials.Length!=1 || mesh==null || !mesh.isReadable || mesh.subMeshCount!=1 || mesh.GetTopology(0)!=MeshTopology.Triangles || mesh.blendShapeCount!=0 || mesh.vertexCount>MaxVertices || mesh.bindposes.Length!=0) {result.skippedUnsupported++;continue;}
            if(!Opaque(renderer.sharedMaterial)) {result.skippedTransparent++;continue;}
            if(sharedMeshes.Contains(mesh)) {result.skippedSharedMesh++;continue;}
            var matrix=worldToRoot*renderer.transform.localToWorldMatrix;
            if(!float.IsFinite(matrix.determinant) || matrix.determinant<=1e-10f) {result.skippedUnsupported++;continue;}
            candidates.Add(new Item{renderer=renderer,mesh=mesh,matrix=matrix});
        }
        if(candidates.Count>0) {
            var bounds=new Bounds(candidates[0].matrix.MultiplyPoint3x4(candidates[0].mesh.bounds.center),Vector3.zero);
            foreach(var item in candidates)bounds.Encapsulate(item.matrix.MultiplyPoint3x4(item.mesh.bounds.center));
            // Local spatial cells avoid turning a whole scene into one culling bound.
            float cellSize=Mathf.Max(.001f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))/4);
            foreach(var group in candidates.GroupBy(item=>Key(item,bounds.min,cellSize))) {
                var batch=new List<Item>();int vertices=0;
                foreach(var item in group) {
                    if(vertices+item.mesh.vertexCount>MaxVertices){Combine(root,batch,result,emptied);batch.Clear();vertices=0;}
                    batch.Add(item);vertices+=item.mesh.vertexCount;
                }
                Combine(root,batch,result,emptied);
            }
        }
        Prune(emptied,root.transform);
        var remaining=root.GetComponentsInChildren<MeshRenderer>(true);
        result.renderersAfter=remaining.Length;result.uniqueMeshesAfter=UniqueMeshes(remaining);
        result.visibleAfter=remaining.Count(r=>r.enabled && r.gameObject.activeInHierarchy);
        result.visibleTrianglesAfter=Triangles(remaining);
        if(result.visibleTrianglesBefore!=result.visibleTrianglesAfter)throw new InvalidOperationException("Visible triangle count changed while combining completed meshes");
        Debug.Log("OPENBRUSH_MESH_COMBINE "+JsonUtility.ToJson(result));
        return result;
    }
    static void Combine(GameObject root,List<Item> batch,Report report,HashSet<GameObject> emptied) {
        if(batch.Count<2){report.skippedSingleton+=batch.Count;return;}
        var source=batch[0].renderer;
        var mesh=new Mesh{name="Combined_"+source.sharedMaterial.name+"_"+report.combinedMeshes,indexFormat=IndexFormat.UInt32};
        mesh.CombineMeshes(batch.Select(item=>new CombineInstance{mesh=item.mesh,subMeshIndex=0,transform=item.matrix}).ToArray(),true,true,false);
        mesh.RecalculateBounds();
        var go=new GameObject(mesh.name);go.layer=source.gameObject.layer;go.transform.SetParent(root.transform,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=source.sharedMaterial;
        renderer.shadowCastingMode=source.shadowCastingMode;renderer.receiveShadows=source.receiveShadows;
        renderer.motionVectorGenerationMode=source.motionVectorGenerationMode;
        renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder;
        renderer.renderingLayerMask=source.renderingLayerMask;
        renderer.lightProbeUsage=source.lightProbeUsage;renderer.reflectionProbeUsage=source.reflectionProbeUsage;
        foreach(var item in batch)RemoveRenderer(item.renderer,emptied);
        report.mergedRenderers+=batch.Count;report.combinedMeshes++;
    }
}
