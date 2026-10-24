using FrooxEngine;
public class OpenBrushTimelineConverter : ResoniteComponentConverter<OpenBrushTimeline> {
    public ResRef<AudioClipPlayer> Player;
    protected override void UpdateConversion(OpenBrushTimeline source,IConversionContext context) {
        var player=EnsureComponent(ref Player);player.Enabled=true;player.persistent=true;
        // A silent clip supplies durable native playback timing; no AudioOutput is created.
        player.Clip=context.GetAudioClip(source.DurationClip);
        player.playback=new PlaybackState{Playing=source.AutoPlay,Loop=true,Position=0,Speed=1};
    }
    protected override void Cleanup(){if(Player!=null)DestroyImmediate(Player);}
}
public class OpenBrushGrowthPartConverter : ResoniteComponentConverter<OpenBrushGrowthPart> {
    public ResRef<OpenBrushScaleGradient> Gradient;public ResRef<PlaybackPositionDriver> Position;
    protected override void UpdateConversion(OpenBrushGrowthPart source,IConversionContext context) {
        var g=EnsureComponent(ref Gradient);g.Enabled=true;g.persistent=true;g.Interpolate=true;g.DrivenTransform=source.transform;g.Points.Clear();
        if(source.Start>0)Add(g,0,UnityEngine.Vector3.zero);Add(g,source.Start,UnityEngine.Vector3.zero);
        float ramp=UnityEngine.Mathf.Min(.0001f,(source.End-source.Start)*.01f);
        Add(g,source.Start+ramp,new UnityEngine.Vector3(1,1,ramp/(source.End-source.Start)));Add(g,source.End,UnityEngine.Vector3.one);if(source.Timeline.Duration>source.End)Add(g,source.Timeline.Duration,UnityEngine.Vector3.one);
        OpenBrushTimelineLink.Position(EnsureComponent(ref Position),source.Timeline,g.Progress_Element.Member);
    }
    static void Add(OpenBrushScaleGradient g,float time,UnityEngine.Vector3 value){var p=g.Points.Add();p.Position=time;p.Value=value;}
    protected override void Cleanup(){if(Gradient!=null)DestroyImmediate(Gradient);if(Position!=null)DestroyImmediate(Position);}
}
public class OpenBrushPlaybackButtonConverter : ResoniteComponentConverter<OpenBrushPlaybackButton> {
    public ResRef<TouchButton> Button;public ResRef<ButtonPlaybackAction> Action;
    protected override void UpdateConversion(OpenBrushPlaybackButton source,IConversionContext context) {
        var button=EnsureComponent(ref Button);button.Enabled=true;button.persistent=true;button.AcceptRemoteTouch=true;button.AcceptPhysicalTouch=true;button.ActiveUserFilter=ActiveUserHandling.Disabled;
        var action=EnsureComponent(ref Action);action.Enabled=true;action.persistent=true;action.Playback=OpenBrushTimelineLink.Player(source.Timeline);action.OnPress=source.Action;
        action.OnHover=action.OnLeave=action.OnRelease=PlaybackAction.None;
    }
    protected override void Cleanup(){if(Button!=null)DestroyImmediate(Button);if(Action!=null)DestroyImmediate(Action);}
}
