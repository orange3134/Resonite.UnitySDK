using UnityEngine;

// A native PhotonDust approximation of the Embers shader; no Unity runtime is required.
public class OpenBrushEmbersMotion : MonoBehaviour
{
    public OpenBrushTimeline Timeline;public Mesh[] EmissionFrames;public float[] FrameTimes;public float Start;
    public Mesh EmissionMesh;
    public Texture2D Sprite;
    public Texture2D LifetimeTexture;
    public float Lifetime = 1f / .6f;
    public float Rate = 30f;
    public float Size = .025f;
    // Source _ScrollDistance = (2,6,0) decimeters per lifetime.
    public Vector3 Travel = new Vector3(.2f, .6f, 0);
}
