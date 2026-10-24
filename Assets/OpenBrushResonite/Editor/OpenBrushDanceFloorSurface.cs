using System;
using System.Collections.Generic;
using UnityEngine;

// Bake the complete gamma-space fragment BEFORE conversion to native linear.
// Changing only native tint would change the color curve throughout the pulse.
public static class OpenBrushDanceFloorSurface {
 public const int Padding=8;
 public class Surface {
  public Material material;public int[] palette;public int columns,width,height,tw,th,sourceWidth,sourceHeight;
  public Vector2 Map(int vertex,int phase,Vector2 uv) {
   int tile=palette[vertex]*OpenBrushDanceFloorImport.Rate+phase;
   return new Vector2((tile%columns*tw+Padding+uv.x*sourceWidth)/width,(tile/columns*th+Padding+(1-uv.y)*sourceHeight)/height);
  }
 }
 public static Color Shade(Color tex,Color paint,float pulse) {
  float gain=4*paint.a*tex.a*pulse;
  return new Color(OpenBrushHyperGridSurface.Native(gain*paint.r*tex.r),OpenBrushHyperGridSurface.Native(gain*paint.g*tex.g),OpenBrushHyperGridSurface.Native(gain*paint.b*tex.b),1);
 }
 public static Surface Build(OpenBrushDanceFloorImport.Part p,Material source,ref long budget) {
  var texture=source.mainTexture as Texture2D;
  if(texture==null || !texture.isReadable || source.mainTextureScale!=Vector2.one || source.mainTextureOffset!=Vector2.zero)throw new InvalidOperationException("DanceFloor requires its original readable texture without UV transforms");
  var colors=new List<Color>();var lookup=new Dictionary<Color,int>();var entries=new int[p.colors.Length];
  for(int i=0;i<entries.Length;i++) {
   var c=p.colors[i];if(c!=p.colors[i/4*4] || c.r<0||c.g<0||c.b<0||c.a<0||c.r>1||c.g>1||c.b>1||c.a>1)throw new InvalidOperationException("DanceFloor expects uniform quad RGBA in [0,1]");
   if(!lookup.TryGetValue(c,out int index)){index=colors.Count;lookup.Add(c,index);colors.Add(c);}entries[i]=index;
  }
  int tiles=colors.Count*OpenBrushDanceFloorImport.Rate,columns=Mathf.CeilToInt(Mathf.Sqrt(tiles)),rows=(tiles+columns-1)/columns;
  int tw=texture.width+Padding*2,th=texture.height+Padding*2,w=columns*tw,h=rows*th;
  budget+=(long)w*h*4/3;
  if(w>SystemInfo.maxTextureSize || h>SystemInfo.maxTextureSize || budget>33554432)throw new InvalidOperationException("DanceFloor atlases exceed 32M pixels including mipmaps; split the sketch");
  var pixels=texture.GetPixels();var tile=new Color32[tw*th];
  var atlas=new Texture2D(w,h,TextureFormat.RGBA32,true,false){name="DanceFloor_SourcePulse",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
  // Cache repeated original texels; the source sprite has a small palette.
  var unique=new Dictionary<Color,int>();var indices=new int[pixels.Length];var texels=new List<Color>();
  for(int i=0;i<pixels.Length;i++){if(!unique.TryGetValue(pixels[i],out int index)){index=texels.Count;unique.Add(pixels[i],index);texels.Add(pixels[i]);}indices[i]=index;}
  for(int n=0;n<tiles;n++) {
   float pulse=Mathf.Pow((n%OpenBrushDanceFloorImport.Rate)/(float)OpenBrushDanceFloorImport.Rate,3);
   var shaded=new Color32[texels.Count];
   for(int i=0;i<shaded.Length;i++){var c=Shade(texels[i],colors[n/OpenBrushDanceFloorImport.Rate],pulse);c.r/=4;c.g/=4;c.b/=4;shaded[i]=c.gamma;}
   for(int y=0;y<th;y++)for(int x=0;x<tw;x++){int sx=(x-Padding+texture.width)%texture.width,sy=(y-Padding+texture.height)%texture.height;tile[y*tw+x]=shaded[indices[sy*texture.width+sx]];}
   atlas.SetPixels32(n%columns*tw,n/columns*th,tw,th,tile);
  }
  atlas.Apply(true,false);
  var material=new Material(source){name="DanceFloor - Original Pulse Fragment",mainTexture=atlas};material.SetColor("_TintColor",new Color(4,4,4,1).gamma);material.SetOverrideTag("OpenBrushBoundedFlowAtlas","1");
  Debug.Log("DANCEFLOOR_SURFACE colors="+colors.Count+" atlas="+w+"x"+h+" source="+texture.width+"x"+texture.height);
  return new Surface{material=material,palette=entries,columns=columns,width=w,height=h,tw=tw,th=th,sourceWidth=texture.width,sourceHeight=texture.height};
 }
}

