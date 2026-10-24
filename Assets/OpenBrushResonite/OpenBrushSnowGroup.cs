using UnityEngine;
// Completed Snow: particles with identical source RGBA share translation and tint.
// Seed alpha remains exact; there is no quantization or particle thinning.
public class OpenBrushSnowGroup : MonoBehaviour {
 public Mesh Mesh;
 public Material SourceMaterial;
 public Color Paint;
 public OpenBrushTimeline Timeline;
 public const int Frames=750;
 public const float Period=25, Fps=30;
 public static void Sample(Color paint,float seconds,out Vector3 offset,out Color tint) {
  float phase=Mathf.Repeat(seconds*.2f+paint.a,1);
  offset=.1f*new Vector3(-Mathf.Sin(phase*12+seconds)*.1f,(phase-.5f)*-3,-Mathf.Cos(phase*6+seconds)*.1f);
  float fade=Mathf.Pow(1-Mathf.Abs(2*(phase-.5f)),3);
  var hdr=paint*(4*fade);
  tint=new Color(Mathf.Clamp01(hdr.r),Mathf.Clamp01(hdr.g),Mathf.Clamp01(hdr.b),1).linear;
 }
}
