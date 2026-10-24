using System;
using UnityEngine;
using FrooxEngine;
public class OpenBrushGeometryMotionConverter : ResoniteComponentConverter<OpenBrushGeometryMotion> {
 public ResRef<FrooxEngine.MeshRenderer> Renderer;
 public ResRef<OpenBrushMeshMultiplexer> Sequence;
 public ResRef<OpenBrushIndexGradient> Index;
 public ResRef<PlaybackPositionDriver> Position;
 protected override void UpdateConversion(OpenBrushGeometryMotion source,IConversionContext context){
  if(source.Timeline==null || source.Frames==null || source.Frames.Length<2 || source.FrameTimes==null || source.FrameTimes.Length!=source.Frames.Length)throw new InvalidOperationException("Incomplete timed geometry sequence");
  for(int i=0;i<source.Frames.Length;i++)if(source.Frames[i]==null || !float.IsFinite(source.FrameTimes[i]) || source.FrameTimes[i]<0 || source.FrameTimes[i]>source.Timeline.Duration || (i>0 && source.FrameTimes[i]<=source.FrameTimes[i-1]))throw new InvalidOperationException("Invalid geometry frame or time");
  var renderer=EnsureComponent(ref Renderer);renderer.Enabled=source.enabled;renderer.persistent=true;renderer.ShadowCastMode=Renderite.Shared.ShadowCastMode.Off;
  var material=context.GetMaterial(source.SourceMaterial);renderer.Materials.Clear();renderer.Materials.Add(material);renderer.Mesh=context.GetMesh(source.Frames[0]);
  var sequence=EnsureComponent(ref Sequence);sequence.Enabled=source.enabled;sequence.persistent=true;sequence.Target=renderer.Mesh_Element.Member;
  while(sequence.Assets.Count>source.Frames.Length)sequence.Assets.RemoveAt(sequence.Assets.Count-1);
  for(int i=0;i<source.Frames.Length;i++){var mesh=context.GetMesh(source.Frames[i]);if(i>=sequence.Assets.Count)sequence.Assets.Add(mesh);else sequence.Assets[i]=mesh;}
  OpenBrushTimelineLink.Index(EnsureComponent(ref Index),source.FrameTimes,sequence.Index_Element.Member);
  OpenBrushTimelineLink.Position(EnsureComponent(ref Position),source.Timeline,Index.Binding.Progress_Element.Member);
 }
 protected override void Cleanup(){if(Position!=null)DestroyImmediate(Position);if(Index!=null)DestroyImmediate(Index);if(Sequence!=null)DestroyImmediate(Sequence);if(Renderer!=null)DestroyImmediate(Renderer);}
}
