using UnityEngine;

// Remembers the prepared mesh so repeated preparation never gamma-decodes it twice.
// Mesh copies isolate Open Brush color conversion from shared imported assets.
[AddComponentMenu("")]
public sealed class OpenBrushVertexColorEncoding : MonoBehaviour {
    [HideInInspector] public Mesh SourceMesh;
    [HideInInspector] public Mesh LinearMesh;
}
