using UnityEngine;
public class OpenBrushTimeline : MonoBehaviour { public AudioClip DurationClip; public float Duration; public bool AutoPlay; }
public class OpenBrushGrowthPart : MonoBehaviour { public OpenBrushTimeline Timeline; public float Start,End; }
public class OpenBrushPlaybackButton : MonoBehaviour {public OpenBrushTimeline Timeline;public FrooxEngine.PlaybackAction Action;}
