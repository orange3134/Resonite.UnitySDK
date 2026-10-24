using UnityEngine;
// Completed-stroke shader motion. This does not replace the growth GLB animation.
public class OpenBrushMeshMotion : MonoBehaviour
{
    public OpenBrushTimeline Timeline;public float[] FrameTimes;
    public Material SourceMaterial;
    public Mesh[] Frames;
    public Texture2D[] GlintFrames;
    public float FramesPerSecond;
}
