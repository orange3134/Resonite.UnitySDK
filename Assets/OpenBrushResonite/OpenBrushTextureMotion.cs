using UnityEngine;

// Baked shader motion. Frames use the source UV space, so repeating stroke UVs work.
public class OpenBrushTextureMotion : MonoBehaviour
{
    public Material SourceMaterial;
    public Texture2D[] Frames;
    public float FramesPerSecond = 30;
    public bool EmissionOnly;
    public bool SpecularOnly;
    public OpenBrushTimeline Timeline;
    public float[] FrameTimes;
    public Vector2 TextureScale = Vector2.one;
}
