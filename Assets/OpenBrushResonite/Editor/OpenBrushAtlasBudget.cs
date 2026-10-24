using System;
using System.Collections.Generic;
using UnityEngine;

// Spatial fallback only: never alter shader sample times or the drawing clock.
public static class OpenBrushAtlasBudget {
 public const long FramePixelLimit=134217728;
 public static readonly List<string> Adjustments=new List<string>();
 public struct Layout {
  public int TileWidth,TileHeight,Width,Height,TextureWidth,TextureHeight;
  public long FramePixels;
 }
 public static Layout Select(int tileWidth,int tileHeight,int padding,int columns,int rows,int frames,int scale,int maxTextureSize,bool adaptive,long limit=FramePixelLimit) {
  if(tileWidth<16 || tileHeight<16 || padding<0 || columns<1 || rows<1 || frames<1 || scale<1 || maxTextureSize<1 || limit<1)throw new ArgumentOutOfRangeException("Invalid atlas budget parameters");
  while(true) {
   long width=(long)columns*(tileWidth+2L*padding),height=(long)rows*(tileHeight+2L*padding);
   double pixels=(double)width*height*scale*scale*frames;
   if(width*scale<=maxTextureSize && height*scale<=maxTextureSize && pixels<=limit)
    return new Layout{TileWidth=tileWidth,TileHeight=tileHeight,Width=(int)width,Height=(int)height,TextureWidth=(int)(width*scale),TextureHeight=(int)(height*scale),FramePixels=(long)pixels};
   // Keep the original aspect ratio and at least 16 samples on the shorter axis.
   if(!adaptive || tileWidth/2<16 || tileHeight/2<16)
    throw new InvalidOperationException("Animated atlas cannot fit its frame-pixel budget even at the allowed resolution; split the sketch or shorten the timeline. Required pixels="+pixels+", limit="+limit);
   tileWidth/=2;tileHeight/=2;
  }
 }
 public static void Record(string brush,Layout layout,int originalWidth,int originalHeight,int frames) {
  if(layout.TileWidth==originalWidth && layout.TileHeight==originalHeight)return;
  string message=brush+" atlas detail reduced per stroke from "+originalWidth+"x"+originalHeight+" to "+layout.TileWidth+"x"+layout.TileHeight+" to fit memory budget; "+frames+" frames and original timing retained. Fine details may be softer.";
  Adjustments.Add(message);Debug.LogWarning(message);
 }
}
