using FrooxEngine;
using UnityEngine;

// The marker is assigned only after validating the GLB's explicit GUID manifest.
// Legacy Open Brush GLB retains TB sRGB vertex RGB; alpha remains linear.
// Unlit uses sRGB interpolation. Audited PBS profiles decode mesh RGB before upload;
// their interpolation setting alone did not correct the native rendered colors.
[MaterialConverter(false, "OpenBrush/ExportedBrush")]
public class OpenBrushMaterialConverter : ResoniteMaterialConverter
{
    public ResRef<UnlitMaterial> Unlit;
    public ResRef<PBS_Specular> Disco;
    public ResRef<PBS_Specular> Hypercolor;
    public ResRef<PBS_Specular> LightWire;
    public ResRef<PBS_Specular> Diamond;
    public ResRef<PBS_Specular> Mylar;
    public ResRef<FresnelMaterial> Neon;
    public ResRef<MatcapMaterial> ViewEmission;
    public ResRef<XiexeToonMaterial> ViewEmissionToon;
    public ResRef<XiexeToonMaterial> ToonOutline;
    public ResRef<PBS_VertexColorMetallic> Surface;
    public ResRef<PBS_VertexColorSpecular> Specular;
    public OpenBrushProfiles.Profile Profile => OpenBrushProfiles.Find(Source.GetTag("OpenBrushGuid", false, ""));

    public override IAssetProvider<FrooxEngine.Material> UpdateConversion(UnityEngine.Material source, IConversionContext context)
    {
        var p = OpenBrushProfiles.Find(source.GetTag("OpenBrushGuid", false, ""));
        if(source.GetTag("OpenBrushMylarBody",false,"")=="1") {
            if(Mylar==null)Mylar=gameObject.AddResoniteComponent<PBS_Specular>();
            var body=Mylar.Binding;body.Enabled=true;body.persistent=true;
            body.BlendMode=BlendMode.Opaque;body.RenderQueue=2000;
            // Keep the coincident additive pattern visible above this opaque
            // depth-writing body. Native Matcap has no depth-offset control.
            body.OffsetUnits=1;
            var paint=source.GetColor("_TintColor");
            body.AlbedoColor=Color.black.ToColorX_Linear();
            body.SpecularColor=new Color(paint.r*.7490196f,paint.g*.7490196f,paint.b*.7490196f,.68f).ToColorX_Linear();
            body.EmissiveColor=Color.black.ToColorX_Linear();
            body.AlbedoTexture=null;body.SpecularMap=null;
            return body;
        }
        if(source.GetTag("OpenBrushDiamondBody",false,"")=="1") {
            if(Diamond==null)Diamond=gameObject.AddResoniteComponent<PBS_Specular>();
            var body=Diamond.Binding;body.Enabled=true;body.persistent=true;
            body.BlendMode=BlendMode.Additive;body.RenderQueue=3000;
            var color=source.GetColor("_TintColor");
            // Source gamma-additive overlaps are brighter than linear blending.
            // Five-color front/rear calibration; retain the optical specular
            // estimate independently from this empirical diffuse compensation.
            float gain=p.Name=="DiamondMembrane" ? 3 : 4;
            body.AlbedoColor=new Color(color.r*gain,color.g*gain,color.b*gain,1).ToColorX_sRGB();
            // Specular is a numeric reflectance, not an sRGB display color.
            // Keep the source gamma-shader parameter as linear data, just as
            // the audited Marbled profile does. Thin-film variation is still approximated.
            var spec=new Color(color.r*.25f,color.g*.25f,color.b*.25f,.8f);
            body.SpecularColor=spec.ToColorX_Linear();body.EmissiveColor=Color.black.ToColorX_Linear();
            body.AlbedoTexture=null;body.SpecularMap=null;
            return body;
        }
        if(source.GetTag("OpenBrushLightWireAtlas",false,"")=="1") {
            if(LightWire==null)LightWire=gameObject.AddResoniteComponent<PBS_Specular>();
            var lit=LightWire.Binding;lit.Enabled=true;lit.persistent=true;
            lit.BlendMode=BlendMode.Opaque;lit.RenderQueue=2000;
            lit.AlbedoColor=Color.white.ToColorX_Linear();lit.AlbedoTexture=context.GetITexture2D(source.mainTexture);
            lit.SpecularColor=Color.white.ToColorX_Linear();lit.SpecularMap=context.GetITexture2D(source.GetTexture("_MaskTex"));
            lit.OcclusionMap=context.GetITexture2D(source.GetTexture("_BumpMap"));
            lit.EmissiveColor=Color.white.ToColorX_Linear();
            lit.TextureScale=Vector2.one;lit.TextureOffset=Vector2.zero;
            return lit;
        }
        if(source.GetTag("OpenBrushHypercolorAtlas",false,"")=="1") {
            if(Hypercolor==null)Hypercolor=gameObject.AddResoniteComponent<PBS_Specular>();
            var lit=Hypercolor.Binding;lit.Enabled=true;lit.persistent=true;
            lit.BlendMode=BlendMode.Cutout;lit.AlphaCutoff=.5f;
            lit.AlbedoColor=new Color(32,32,32,1).gamma.ToColorX_sRGB();
            lit.AlbedoTexture=context.GetITexture2D(source.mainTexture);
            lit.SpecularColor=new Color(1,1,1,.4f).ToColorX_Linear();
            lit.SpecularMap=context.GetITexture2D(source.GetTexture("_MaskTex"));
            lit.NormalMap=context.GetITexture2D(source.GetTexture("_BumpMap"));lit.NormalScale=1;
            lit.EmissiveColor=Color.black.ToColorX_Linear();lit.RenderQueue=2450;
            lit.TextureScale=Vector2.one;lit.TextureOffset=Vector2.zero;
            return lit;
        }
        if(source.GetTag("OpenBrushDiscoSurface",false,"")=="1") {
            if(Disco==null)Disco=gameObject.AddResoniteComponent<PBS_Specular>();
            ConfigureDisco(Disco.Binding);
            Disco.Binding.SpecularMap=context.GetITexture2D(source.mainTexture);
            Disco.Binding.OcclusionMap=context.GetITexture2D(UnityEngine.Texture2D.blackTexture);
            return Disco.Binding;
        }
        if (source.GetTag("OpenBrushToonOutline", false, "") == "1") {
            if (ToonOutline == null) ToonOutline = gameObject.AddResoniteComponent<XiexeToonMaterial>();
            var outline=ToonOutline.Binding;outline.Enabled=true;outline.persistent=true;
            outline.UseVertexColors=false;outline.Color=Color.black.ToColorX_Linear();
            outline.EmissionColor=Color.black.ToColorX_Linear();outline.RimIntensity=0;
            outline.SpecularIntensity=0;outline.Reflectivity=0;outline.Metallic=0;
            outline.Outline=XiexeToonMaterial.OutlineStyle.Emissive;
            outline.OutlineColor=Color.black.ToColorX_Linear();outline.OutlineAlbedoTint=false;
            outline.OutlineWidth=.35f;outline.OutlineMask=context.GetITexture2D(source.GetTexture("_MaskTex"));
            outline.Culling=Culling.Back;outline.ColorMask=ColorMask.RGBA;outline.BlendMode=BlendMode.Opaque;outline.ZWrite=ZWrite.On;
            outline.MainTextureScale=Vector2.one;outline.MainTextureOffset=Vector2.zero;outline.RenderQueue=2000;
            return outline;
        }
        if(source.GetTag("OpenBrushViewEmissionToon",false,"")=="1") {
            if(ViewEmissionToon==null)ViewEmissionToon=gameObject.AddResoniteComponent<XiexeToonMaterial>();
            var view=ViewEmissionToon.Binding;view.Enabled=true;view.persistent=true;
            view.Color=Color.black.ToColorX_Linear();view.UseVertexColors=false;view.Saturation=1;
            view.EmissionColor=Color.black.ToColorX_Linear();view.RimIntensity=0;view.SpecularIntensity=0;
            view.MatcapTint=Color.white.ToColorX_Linear();view.Matcap=context.GetITexture2D(source.mainTexture);
            view.Metallic=0;view.Glossiness=1;view.Reflectivity=1;view.Outline=XiexeToonMaterial.OutlineStyle.None;
            view.BlendMode=BlendMode.Additive;view.ZWrite=ZWrite.Off;view.Culling=Culling.Off;
            view.ColorMask=ColorMask.RGBA;view.RenderQueue=3000;view.OffsetUnits=-1;
            view.MainTextureScale=Vector2.one;view.OcclusionMapScale=Vector2.one;
            view.OcclusionColor=Color.white.ToColorX_Linear();
            return view;
        }
        if(source.GetTag("OpenBrushViewEmission",false,"")=="1") {
            if(ViewEmission==null)ViewEmission=gameObject.AddResoniteComponent<MatcapMaterial>();
            var view=ViewEmission.Binding;view.Enabled=true;view.persistent=true;
            view.BlendMode=BlendMode.Additive;view.RenderQueue=3000;
            view.Matcap=context.GetITexture2D(source.mainTexture);
            return view;
        }
        if(source.GetTag("OpenBrushNeonEmission",false,"")=="1") {
            if(Neon==null)Neon=gameObject.AddResoniteComponent<FresnelMaterial>();
            ConfigureNeon(Neon.Binding);Neon.Binding.NearTexture=context.GetITexture2D(source.mainTexture);
            return Neon.Binding;
        }
        if (p.Unlit || source.GetTag("OpenBrushSourceFlatLighting",false,"")=="1")
        {
            if (Unlit == null) Unlit = gameObject.AddResoniteComponent<UnlitMaterial>();
            ConfigureUnlit(source, p, Unlit.Binding);
            Unlit.Binding.Texture = context.GetITexture2D(source.mainTexture);
            Unlit.Binding.MaskTexture = p.Additive && p.Cutout ? context.GetITexture2D(source.GetTexture("_MaskTex")) : null;
            return Unlit.Binding;
        }
        if (p.SpecularModel)
        {
            if (Specular == null) Specular = gameObject.AddResoniteComponent<PBS_VertexColorSpecular>();
            ConfigureSpecular(source, p, Specular.Binding);
            Specular.Binding.AlbedoTexture = context.GetITexture2D(source.mainTexture);
            Specular.Binding.NormalMap = context.GetITexture2D(source.GetTexture("_BumpMap"));
            Specular.Binding.SpecularMap = source.GetTag("OpenBrushSpecularMap",false,"")=="1" ? context.GetITexture2D(source.GetTexture("_MaskTex")) : null;
            return Specular.Binding;
        }
        if (Surface == null) Surface = gameObject.AddResoniteComponent<PBS_VertexColorMetallic>();
        ConfigureSurface(source, p, Surface.Binding);
        Surface.Binding.AlbedoTexture = p.EmissiveWire ? null : context.GetITexture2D(source.mainTexture);
        Surface.Binding.EmissiveMap = p.EmissiveWire ? context.GetITexture2D(source.mainTexture) : null;
        Surface.Binding.NormalMap = context.GetITexture2D(source.GetTexture("_BumpMap"));
        return Surface.Binding;
    }
    public static void ConfigureDisco(PBS_Specular target) {
        target.Enabled=true;target.persistent=true;target.BlendMode=BlendMode.Opaque;
        target.AlbedoColor=Color.black.ToColorX_Linear();target.AlbedoTexture=null;
        target.SpecularColor=new Color(1,1,1,.65f).ToColorX_Linear();
        target.EmissiveColor=Color.white.ToColorX_Linear();
        target.TextureScale=Vector2.one;target.TextureOffset=Vector2.zero;
        target.RenderQueue=2000;
    }
    public static void ConfigureNeon(FresnelMaterial target) {
        target.Enabled=true;target.persistent=true;target.Exponent=5;target.GammaCurve=1;
        target.NearColor=Color.white.ToColorX_Linear();target.FarColor=Color.black.ToColorX_Linear();
        target.UseVertexColors=false;target.BlendMode=BlendMode.Additive;target.Sidedness=Sidedness.Double;
        target.ZWrite=ZWrite.Off;target.OffsetUnits=-1;target.RenderQueue=3000;
        target.NearTextureScale=Vector2.one;target.NearTextureOffset=Vector2.zero;
        target.FarTextureScale=Vector2.one;target.FarTextureOffset=Vector2.zero;
    }
    public static void ConfigureUnlit(UnityEngine.Material source, OpenBrushProfiles.Profile p, UnlitMaterial target)
    {
        target.Enabled = true;
        target.persistent = true;
        target.BlendMode = p.Multiply ? BlendMode.Multiply : p.Additive ? BlendMode.Additive : p.Transparent ? BlendMode.Alpha : p.Cutout ? BlendMode.Cutout : BlendMode.Opaque;
        target.AlphaCutoff = p.Cutoff;
        target.MaskMode = MaskTextureMode.AlphaClip;
        target.MaskScale = source.mainTextureScale; target.MaskOffset = source.mainTextureOffset;
        target.Sidedness = p.SingleSided ? Sidedness.Front : Sidedness.Double;
        target.OffsetUnits = p.Name == "Toon" ? -2 : 0;
        target.ZWrite = p.Additive || p.Transparent || p.Multiply ? ZWrite.Off : ZWrite.On;
        target.UseVertexColors = true;
        target.VertexColorInterpolationSpace = Renderite.Shared.ColorProfile.sRGB;
        target.UseBillboardGeometry = false;
        var tint = source.GetColor("_TintColor").linear;
        float gain=source.GetTag("OpenBrushFireEmission",false,"")=="1" ? 1 : p.Gain;
        tint.r *= gain; tint.g *= gain; tint.b *= gain;
        // Native ColorProfileHelper leaves values >=1 unchanged. For this
        // bounded atlas, preserve the gamma-encoded multiplier supplied by the
        // baker; decoding it here would apply excessive HDR gain in Unlit.
        target.TintColor = (source.GetTag("OpenBrushBoundedFlowAtlas",false,"")=="1" || source.GetTag("OpenBrushBoundedWaveformAtlas",false,"")=="1")
            ? source.GetColor("_TintColor").ToColorX_sRGB() : tint.ToColorX_Linear();
        target.TextureScale = source.mainTextureScale;
        target.TextureOffset = source.mainTextureOffset;
        target.RenderQueue = p.Name == "Toon" ? 2001 : p.Name == "DanceFloor" || p.Additive || p.Transparent || p.Multiply ? 3000 : p.Cutout ? 2450 : 2000;
    }
    public static void ConfigureSpecular(UnityEngine.Material source, OpenBrushProfiles.Profile p, PBS_VertexColorSpecular target)
    {
        // Native Matcap has no configurable depth bias. Move only the paired body
        // one depth unit back so the coincident emission surface passes its test.
        target.OffsetUnits=source.GetTag("OpenBrushNeonViewBody",false,"")=="1" ? 1 : 0;
        // Normal LitVertexColor URP graph uses specular workflow with a black specular input.
        // Metallic workflow's fixed dielectric reflectance makes saturated diffuse brushes pale.
        target.Enabled=true; target.persistent=true;
        target.VertexColorInterpolationSpace=Renderite.Shared.ColorProfile.sRGB;
        target.VertexColorTarget=PBS_VertexColorSpecular.ColorTarget.Albedo;
        target.Culling=p.SingleSided ? Culling.Back : Culling.Off; target.AlphaHandling=p.Cutout ? AlphaHandling.AlphaClip : AlphaHandling.Opaque;
        target.AlphaClip=p.Cutoff; target.AlbedoColor=source.GetColor("_TintColor").ToColorX_sRGB();
        if(p.DiffuseGain!=1) {
            var diffuse=source.GetColor("_TintColor").linear;
            diffuse.r*=p.DiffuseGain;diffuse.g*=p.DiffuseGain;diffuse.b*=p.DiffuseGain;
            target.AlbedoColor=diffuse.ToColorX_Linear();
        }
        var specular=p.SpecularColor ?? new Color(p.Specular,p.Specular,p.Specular,1);
        if(!p.SpecularIsLinear)specular=specular.linear;
        specular.r*=p.SpecularScale;specular.g*=p.SpecularScale;specular.b*=p.SpecularScale;
        specular.a=p.Smoothness;
        target.SpecularColor=specular.ToColorX_Linear();
        target.EmissiveColor=new Color(0,0,0,1).ToColorX_Linear();
        target.NormalScale=source.GetFloat("_BumpScale");
        target.TextureScale=Vector2.Scale(source.mainTextureScale,p.TextureScale ?? Vector2.one); target.TextureOffset=source.mainTextureOffset;
        target.RenderQueue=p.Cutout ? 2450 : 2000;
    }
    public static void ConfigureSurface(UnityEngine.Material source, OpenBrushProfiles.Profile p, PBS_VertexColorMetallic target)
    {
        target.Enabled = true;
        target.persistent = true;
        target.VertexColorInterpolationSpace = Renderite.Shared.ColorProfile.sRGB;
        target.VertexColorTarget = p.EmissiveWire ? PBS_VertexColorMetallic.ColorTarget.Emissive : PBS_VertexColorMetallic.ColorTarget.Albedo;
        target.Culling = p.SingleSided ? Culling.Back : Culling.Off;
        target.AlphaHandling = p.Transparent ? AlphaHandling.AlphaBlend : p.Cutout ? AlphaHandling.AlphaClip : AlphaHandling.Opaque;
        target.AlphaClip = p.Cutoff;
        target.AlbedoColor = (p.EmissiveWire ? new Color(.12f,.12f,.12f,1) : source.GetColor("_TintColor")).ToColorX_sRGB();
        target.EmissiveColor = (p.EmissiveWire ? new Color(p.Gain,p.Gain,p.Gain,1) : new Color(0,0,0,1)).ToColorX_Linear();
        target.Metallic = p.Metallic;
        target.Smoothness = p.Smoothness;
        target.NormalScale = source.GetFloat("_BumpScale");
        target.TextureScale = source.mainTextureScale;
        target.TextureOffset = source.mainTextureOffset;
        target.RenderQueue = p.Transparent ? 3000 : p.Cutout ? 2450 : 2000;
    }
}










