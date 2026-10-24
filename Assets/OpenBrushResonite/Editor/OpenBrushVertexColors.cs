using System;
using UnityEditor;
using UnityEngine;

public static class OpenBrushVertexColors {
    public static void Prepare(MeshFilter filter, bool saveAssets) {
        var state=filter.GetComponent<OpenBrushVertexColorEncoding>();
        if(state==null)state=Undo.AddComponent<OpenBrushVertexColorEncoding>(filter.gameObject);
        if(state.LinearMesh==null || filter.sharedMesh!=state.LinearMesh) {
            var source=filter.sharedMesh;
            if(source==null || source.colors.Length!=source.vertexCount)
                throw new InvalidOperationException("Missing Open Brush vertex colors");
            var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+"_LinearVertexRGB";
            var colors=mesh.colors;
            for(int i=0;i<colors.Length;i++) {
                // Alpha holds coverage/pressure, not sRGB data.
                float alpha=colors[i].a;colors[i]=colors[i].linear;colors[i].a=alpha;
            }
            mesh.colors=colors;
            state.SourceMesh=source;state.LinearMesh=mesh;
            Undo.RecordObject(filter,"Prepare Open Brush vertex RGB");filter.sharedMesh=mesh;
        }
        if(saveAssets && !EditorUtility.IsPersistent(state.LinearMesh))
            AssetDatabase.CreateAsset(state.LinearMesh,AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/"+state.LinearMesh.name+".asset"));
        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        PrefabUtility.RecordPrefabInstancePropertyModifications(state);
    }
}
