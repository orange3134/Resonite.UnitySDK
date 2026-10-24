using System;
using System.Collections.Generic;
using System.Reflection;
using FrooxEngine;
using UnityEngine;
[Serializable,ResoniteTypeName("[FrooxEngine]FrooxEngine.ValueGradientDriver<[Elements.Core]Elements.Core.float3>")]
public class OpenBrushScaleGradient : ValueGradientDriver<Vector3> {
    public Transform DrivenTransform;
    public override void CollectMembers(Dictionary<string,ResoniteLink.Member> members,IConversionContext context) {
        base.CollectMembers(members,context);
        // SDK does not expose Slot fields; resolve its own assigned field ID, never infer an ID suffix.
        var method=context.GetType().GetMethod("GetLinkSlot",BindingFlags.NonPublic|BindingFlags.Instance);
        if(method==null)throw new InvalidOperationException("SDK Slot field API changed");
        var slot=(ResoniteLink.Slot)method.Invoke(context,new object[]{DrivenTransform});
        ((ResoniteLink.Reference)members["Target"]).TargetID=slot.Scale.ID;
    }
}
[Serializable,ResoniteTypeName("[FrooxEngine]FrooxEngine.ValueGradientDriver<int>")]
public class OpenBrushIndexGradient : ValueGradientDriver<int> { }
[Serializable,ResoniteTypeName("[FrooxEngine]FrooxEngine.ValueGradientDriver<float>")]
public class OpenBrushFloatGradient : ValueGradientDriver<float> { }
[Serializable,ResoniteTypeName("[FrooxEngine]FrooxEngine.ValueGradientDriver<bool>")]
public class OpenBrushBoolGradient : ValueGradientDriver<bool> { }
public static class OpenBrushTimelineLink {
    public static void Index(OpenBrushIndexGradient gradient,float[] times,FrooxEngine.IField<int> target) {
        gradient.Enabled=true;gradient.persistent=true;gradient.Interpolate=false;gradient.Target=target;gradient.Points.Clear();
        for(int i=0;i<times.Length;i++){var point=gradient.Points.Add();point.Position=times[i];point.Value=i;}
    }

    public static AudioClipPlayer Player(OpenBrushTimeline source) {
        var converter=source.GetComponent<OpenBrushTimelineConverter>();
        if(converter?.Player==null)throw new InvalidOperationException("Timeline must be converted before its children");
        return converter.Player.Binding;
    }
    public static void Position(PlaybackPositionDriver driver,OpenBrushTimeline timeline,FrooxEngine.IField<float> target) {
        driver.Enabled=true;driver.persistent=true;driver.Source=Player(timeline);driver.Target=target;driver.UseNormalizedPosition=false;driver.WriteBack=false;
    }
}
