using UnityEngine;
// Finite source vertex deformation and drawing reveal share one native clock.
public class OpenBrushGeometryMotion : MonoBehaviour {
 public OpenBrushTimeline Timeline;
 public float[] FrameTimes;
 public Mesh[] Frames;
 public Material SourceMaterial;
}
