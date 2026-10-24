using FrooxEngine;
using UnityEngine;

// Exact matches only: ordinary transparent materials must retain their converters.
[MaterialConverter(false, "Brush/Particle/Smoke", "OpenBrush/ExportedSmoke")]
public class OpenBrushSmokeConverter : ResoniteMaterialConverter
{
    public ResRef<FrooxEngine.UnlitMaterial> Unlit;

    public override IAssetProvider<FrooxEngine.Material> UpdateConversion(UnityEngine.Material material, IConversionContext context)
    {
        if (Unlit == null)
            Unlit = gameObject.AddResoniteComponent<FrooxEngine.UnlitMaterial>();
        var data = Unlit.Binding;
        Configure(material, data);
        data.Texture = context.GetITexture2D(material.mainTexture);
        return data;
    }

    public static void Configure(UnityEngine.Material source, FrooxEngine.UnlitMaterial target)
    {
        // Smoke hard-codes SrcAlpha/One. Serialized _SrcBlend/_DstBlend can be stale.
        target.BlendMode = BlendMode.Additive;
        target.Sidedness = Sidedness.Double;
        target.ZWrite = ZWrite.Off;
        target.UseVertexColors = true;
        target.UseBillboardGeometry = false; // Exported triangles are not native billboard geometry.
        target.TextureScale = source.mainTextureScale;
        target.TextureOffset = source.mainTextureOffset;
        target.RenderQueue = source.renderQueue;
        var tint = source.HasProperty("_TintColor") ? source.GetColor("_TintColor") : Color.white;
        if (source.HasProperty("_Opacity"))
            tint.a *= source.GetFloat("_Opacity");
        target.TintColor = tint.ToColorX_sRGB();
    }
}
