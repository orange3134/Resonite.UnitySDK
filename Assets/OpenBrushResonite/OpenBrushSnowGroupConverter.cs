using System;
using System.Collections.Generic;
using System.Reflection;
using FrooxEngine;
using UnityEngine;
[Serializable,ResoniteTypeName("[FrooxEngine]FrooxEngine.ValueGradientDriver<[Elements.Core]Elements.Core.float3>")]
public class OpenBrushPositionGradient : ValueGradientDriver<Vector3> {
 public Transform DrivenTransform;
 public override void CollectMembers(Dictionary<string,ResoniteLink.Member> members,IConversionContext context) {
  base.CollectMembers(members,context);
  var method=context.GetType().GetMethod("GetLinkSlot",BindingFlags.NonPublic|BindingFlags.Instance);
  if(method==null)throw new InvalidOperationException("SDK Slot field API changed");
  var slot=(ResoniteLink.Slot)method.Invoke(context,new object[]{DrivenTransform});
  ((ResoniteLink.Reference)members["Target"]).TargetID=slot.Position.ID;
 }
}
[Serializable,ResoniteTypeName("[FrooxEngine]FrooxEngine.ValueGradientDriver<[Elements.Core]Elements.Core.colorX>")]
public class OpenBrushColorGradient : ValueGradientDriver<ColorX> { }
public class OpenBrushSnowGroupConverter : ResoniteComponentConverter<OpenBrushSnowGroup> {
 public ResRef<FrooxEngine.MeshRenderer> Renderer;
 public ResRef<UnlitMaterial> Material;
 public ResRef<OpenBrushPositionGradient> Translation;
 public ResRef<OpenBrushColorGradient> Tint;
 public ResRef<PlaybackPositionDriver> TranslationClock,TintClock;
 protected override void UpdateConversion(OpenBrushSnowGroup source,IConversionContext context) {
  if(source.Mesh==null || source.Mesh.vertexCount==0 || source.Timeline==null)throw new InvalidOperationException("Incomplete compact Snow group");
  var material=EnsureComponent(ref Material);material.Enabled=true;material.persistent=true;
  material.BlendMode=BlendMode.Additive;material.ZWrite=ZWrite.Off;material.Sidedness=Sidedness.Double;
  material.UseVertexColors=true;material.VertexColorInterpolationSpace=Renderite.Shared.ColorProfile.Linear;
  material.UseBillboardGeometry=true;material.UsePerBillboardScale=true;material.UsePerBillboardRotation=true;material.UsePerBillboardUV=true;
  material.BillboardSize=Vector2.one*.5f;material.Texture=context.GetITexture2D(source.SourceMaterial.mainTexture);
  material.TextureScale=Vector2.one;material.RenderQueue=3000;
  OpenBrushSnowGroup.Sample(source.Paint,0,out _,out var initial);material.TintColor=initial.ToColorX_Linear();
  var renderer=EnsureComponent(ref Renderer);renderer.Enabled=source.enabled;renderer.persistent=true;renderer.ShadowCastMode=Renderite.Shared.ShadowCastMode.Off;
  renderer.Materials.Clear();renderer.Materials.Add(material);renderer.Mesh=context.GetMesh(source.Mesh);
  var translation=EnsureComponent(ref Translation);translation.Enabled=true;translation.persistent=true;translation.DrivenTransform=source.transform;translation.Interpolate=false;translation.Points.Clear();
  var tint=EnsureComponent(ref Tint);tint.Enabled=true;tint.persistent=true;tint.Interpolate=false;tint.Target=material.TintColor_Element.Member;tint.Points.Clear();
  for(int f=0;f<OpenBrushSnowGroup.Frames;f++) {
   float time=f/OpenBrushSnowGroup.Fps;OpenBrushSnowGroup.Sample(source.Paint,time,out var offset,out var color);
   var p=translation.Points.Add();p.Position=time;p.Value=offset;
   var c=tint.Points.Add();c.Position=time;c.Value=color.ToColorX_Linear();
  }
  OpenBrushTimelineLink.Position(EnsureComponent(ref TranslationClock),source.Timeline,translation.Progress_Element.Member);
  OpenBrushTimelineLink.Position(EnsureComponent(ref TintClock),source.Timeline,tint.Progress_Element.Member);
 }
 protected override void Cleanup(){if(TranslationClock!=null)DestroyImmediate(TranslationClock);if(TintClock!=null)DestroyImmediate(TintClock);if(Translation!=null)DestroyImmediate(Translation);if(Tint!=null)DestroyImmediate(Tint);if(Renderer!=null)DestroyImmediate(Renderer);if(Material!=null)DestroyImmediate(Material);}
}
