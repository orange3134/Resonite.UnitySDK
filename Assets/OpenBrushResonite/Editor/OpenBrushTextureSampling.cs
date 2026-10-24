using UnityEngine;

// CPU GetPixelBilinear places integer texel centers at i / size; GPU tex2D places
// them at (i + 0.5) / size. Bake source shader samples at GPU coordinates. Returns
// raw stored channels at mip 0, without a color-space conversion. Callers retain
// the source Gamma workflow or explicitly decode their complete emission product.
public static class OpenBrushTextureSampling {
    public static Color SampleRaw(Texture2D texture,float u,float v) =>
        texture.GetPixelBilinear(u-.5f/texture.width,v-.5f/texture.height);
}
