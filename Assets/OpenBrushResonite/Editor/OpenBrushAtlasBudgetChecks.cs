using System;
using UnityEditor;
using UnityEngine;
public static class OpenBrushAtlasBudgetChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 public static void Run(){try {
  var small=OpenBrushAtlasBudget.Select(256,128,2,1,1,121,1,8192,true);
  Check(small.TileWidth==256 && small.TileHeight==128,"Short sketches retain full resolution");
  var flow=OpenBrushAtlasBudget.Select(128,128,2,2,2,7801,1,8192,true);
  Check(flow.TileWidth==32 && flow.TileHeight==32 && flow.FramePixels<=OpenBrushAtlasBudget.FramePixelLimit,"Long Flow fits without reducing time samples");
  var wave=OpenBrushAtlasBudget.Select(256,128,2,3,3,7801,1,8192,true);
  Check(wave.TileWidth==32 && wave.TileHeight==16 && wave.Width==108 && wave.Height==60,"Waveform preserves aspect and two-pixel padding");
  Check(wave.FramePixels==(long)108*60*7801,"Pixel count includes full duration");
  var edge=OpenBrushAtlasBudget.Select(16,16,2,1,1,2,1,20,true,800);
  Check(edge.FramePixels==800,"Exact budget is accepted");
  bool rejected=false;try{OpenBrushAtlasBudget.Select(16,16,2,1,1,2,1,20,true,799);}catch(InvalidOperationException){rejected=true;}
  Check(rejected,"Minimum resolution cannot bypass budget");
  rejected=false;try{OpenBrushAtlasBudget.Select(256,128,2,3,3,7801,1,8192,false);}catch(InvalidOperationException){rejected=true;}
  Check(rejected,"Explicit reference sampling does not silently lose resolution");
  var tex=OpenBrushAtlasBudget.Select(128,128,2,2,2,2,1,150,true);
  Check(tex.TextureWidth<=150 && tex.TextureHeight<=150,"Hardware dimension limit respected");
  Debug.Log("OPENBRUSH_ATLAS_BUDGET_CHECKS_PASSED");
  // Existing independent GPU/scalar oracle also verifies unchanged full-resolution mapping.
  OpenBrushWaveformChecks.Run();
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
