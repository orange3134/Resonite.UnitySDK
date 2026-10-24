using System;
using System.Collections.Generic;

// Profiles explicitly match exported brush GUIDs; unverified GUIDs are rejected.
// Source: each BrushDescriptor.m_Material (never m_TestingMaterial).
// Native materials approximate custom shaders. Gains are calibrated for Resonite bloom;
// tone mapping, nonlinear pressure response, refraction and procedural effects differ.
// LightWire body/emission split and Disco/DiamondHull reflectance are approximation profiles.
public static class OpenBrushProfiles
{
    public sealed class Profile
    {
        public string Guid, Name;
        public bool Unlit, Additive, Multiply, Cutout, Transparent, EmissiveWire, SpecularModel, LinearVertexColors, SingleSided, IgnoreAlbedoTexture, SpecularIsLinear;
        public float Gain = 1, Smoothness, Metallic, Cutoff = .5f, Tint = 1, Specular, SpecularScale = 1, DiffuseGain = 1;
        public UnityEngine.Color? SpecularColor;
        public UnityEngine.Vector2? TextureScale;
        public Profile(string guid, string name) { Guid = guid; Name = name; }
    }
    public static readonly Profile[] All = {
        // Normal graph uses _Specular (black), not legacy _SpecColor.
        new Profile("f8ba3d18-01fc-4d7b-b2d9-b99d10b8e7cf","KeijiroTube") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, Smoothness=.432f, IgnoreAlbedoTexture=true, DiffuseGain=1.5f },
        new Profile("8d1510b2-1fc5-4c90-8db7-4c07eecfd849","Slice") { Unlit=true, Transparent=true, IgnoreAlbedoTexture=true },
        // Legacy fallback; the opt-in DiamondFilm preparation restores additive
        // compositing and animated/view-dependent thin-film emission.
        new Profile("4f070b71-da25-48c4-9322-e9216d921562","DiamondMembrane") { Transparent=true, Smoothness=.95f },
        new Profile("c86c058d-1bda-2e94-08db-f3d6a96ac4a1","Marbled Rainbow") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, Cutout=true, Cutoff=.5f, Smoothness=.8f, Specular=.21960784f, SpecularIsLinear=true, DiffuseGain=1.25f },
        // Normal Lacewing graph: texture * paint, alpha clip .9, tangent normal.
        // _SpecularColor is black in the shipping material, so its animated
        // specular branch contributes zero (legacy _SpecColor is not consumed).
        new Profile("1cf94f63-f57a-4a1a-ad14-295af4f5ab5c","Lacewing") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, Cutout=true, Cutoff=.9f, Smoothness=.8f, DiffuseGain=1.25f },
        // Five-color front/rear calibration against the normal URP material.
        // Native linear lighting remains an approximation of the source gamma scene.
        new Profile("ddda8745-4bb5-ac54-88b6-d1480370583e","LeakyPen") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, Cutout=true, Cutoff=.5f, DiffuseGain=1.25f },
        new Profile("2c1a6a63-6552-4d23-86d7-58f6fba8581b","Wireframe") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        new Profile("61820308-bae0-48d4-8288-a7d4e6734d2f","Dark") { Unlit=true, Multiply=true, IgnoreAlbedoTexture=true },
        // Normal materials: SoftHighlighter texture (Feather), Additive and
        // Drafting. Drafting currently captures the visible source state.
        new Profile("a555b809-2017-46cb-ac26-e63173d8f45e","Feather") { Unlit=true, Additive=true },
        new Profile("c1c9b26d-673a-4dc6-b373-51715654ab96","TubeAdditive") { Unlit=true, Additive=true },
        new Profile("492b36ff-b337-436a-ba5f-1e87ee86747e","Drafting") { Unlit=true, Additive=true },
        // Normal Digital/Race procedural grids use a static fragment atlas.
        new Profile("30cb9af6-be41-4872-8f3e-cbff63fe3db8","Digital") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        new Profile("abfbb2aa-70b4-4a5c-8126-8eedda2b3628","Race") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        // Normal LitStandardAlphaCut graph: __SpecColor, _shininess, _Tiling.
        // Its UV tiling property is not retained by the legacy GLB exporter.
        // Source color/roughness are retained; numeric specular and diffuse gains
        // approximate Gamma-source lighting in native PBS. Calibrated on five
        // paint colors and both sides, not an exact cross-renderer BRDF mapping.
        new Profile("1b897b7e-9b76-425a-b031-a867c48df409","Gouache") { LinearVertexColors=true, SpecularModel=true, Cutout=true, Cutoff=.5f, Smoothness=.01f, DiffuseGain=1.5f },
        new Profile("99aafe96-1645-44cd-99bd-979bc6ef37c5","Guts") { LinearVertexColors=true, SpecularModel=true, Cutout=true, Cutoff=.3f, Smoothness=.743f, SpecularColor=new UnityEngine.Color(.23921569f,.25882354f,.26666668f,1), TextureScale=new UnityEngine.Vector2(.15f,1), SpecularIsLinear=true, SpecularScale=1.5f, DiffuseGain=1.3f },
        new Profile("f28c395c-a57d-464b-8f0b-558c59478fa3","Muscle") { LinearVertexColors=true, SpecularModel=true, Cutout=true, Cutoff=.3f, Smoothness=.57f, Specular=.08235294f, TextureScale=new UnityEngine.Vector2(.25f,1), SpecularIsLinear=true, DiffuseGain=1.5f },
        new Profile("355b3579-bf1d-4ff5-a200-704437fe684b","SmoothHull") { LinearVertexColors=true, SpecularModel=true, Cutout=true, Cutoff=.5f, Smoothness=.574f, Specular=.29411766f, SpecularIsLinear=true, SpecularScale=2, DiffuseGain=1.5f },
        // Fragment hue transforms are baked before native material conversion.
        // Keep source RGBA here; the baker needs the original red channel.
        new Profile("3d9755da-56c7-7294-9b1d-5ec349975f52","TaperedHueShift") { Unlit=true, Cutout=true, Cutoff=.5f, IgnoreAlbedoTexture=true },
        // Diffuse gain is an empirical five-color/front-and-rear lighting match,
        // not an exact equivalence of the Gamma source and native linear BRDFs.
        new Profile("c5da2e70-a6e4-63a4-898c-5cfedef09c97","LoftedHueShift") { SpecularModel=true, SpecularIsLinear=true, Specular=.13333334f, Smoothness=.4f, SingleSided=true, IgnoreAlbedoTexture=true, DiffuseGain=1.5f },
        new Profile("53d753ef-083c-45e1-98e7-4459b4471219","Fire2") { Unlit=true, Additive=true },
        new Profile("50e99447-3861-05f4-697d-a1b96e771b98","Sparks") { Unlit=true, Additive=true },
        new Profile("7136a729-1aab-bd24-f8b2-ca88b6adfb67","Wind") { Unlit=true, Additive=true },
        new Profile("03a529e1-f519-3dd4-582d-2d5cd92c3f4f","Rain") { Unlit=true, Additive=true },
        // Additional normal StandardSingleSided materials; absent _Bump defaults
        // are restored in GLB preparation, and source tangents in the SDK sender.
        // The normal graph multiplies the Specular color property by 0.5.
        new Profile("84d5bbb2-6634-8434-f8a7-681b576b4664","DuctTapeGeometry") { LinearVertexColors=true, SpecularModel=true, SpecularScale=.5f, SingleSided=true, Cutout=true, Cutoff=0.2f, Smoothness=0.414f, Specular=0.5372549f, Tint=0.7058824f },
        new Profile("7c972c27-d3c2-8af4-7bf8-5d9db8f0b7bb","InkGeometry") { LinearVertexColors=true, SpecularModel=true, SpecularScale=.5f, SingleSided=true, Cutout=true, Cutoff=0.5f, Smoothness=0.4f, Specular=0f, Tint=1f },
        new Profile("62fef968-e842-3224-4a0e-1fdb7cfb745c","Wire (Lit)") { LinearVertexColors=true, SpecularModel=true, SpecularScale=.5f, SingleSided=true, Cutout=true, Cutoff=0.5f, Smoothness=0.078125f, Specular=0.5019608f, Tint=1f },
        new Profile("f0a2298a-be80-432c-9fee-a86dcc06f4f9","SingleSided") { LinearVertexColors=true, SpecularModel=true, SpecularScale=.5f, SingleSided=true, Cutout=true, Cutoff=0.5f, Smoothness=0f, Specular=0f, Tint=1f },
        new Profile("9568870f-8594-60f4-1b20-dfbc8a5eac0e","TaperedWire") { LinearVertexColors=true, SpecularModel=true, SpecularScale=.5f, SingleSided=true, Cutout=true, Cutoff=0.5f, Smoothness=0.1f, Specular=0.09411765f, Tint=1f },
        new Profile("7259cce5-41c1-ec74-c885-78af28a31d95","Leaves2") { LinearVertexColors=true, SpecularModel=true, SpecularScale=.5f, SingleSided=true, Cutout=true, Cutoff=0.5f, Smoothness=0.395f, Specular=0f, Tint=1f },
        new Profile("39ee7377-7a9e-47a7-a0f8-0c77712f75d3","ThickGeometry") { LinearVertexColors=true, SpecularModel=true, SpecularScale=.5f, SingleSided=true, Cutout=true, Cutoff=0.2f, Smoothness=0.414f, Specular=0.5372549f, Tint=0.7058824f },

        // Normal StandardSingleSided graph: specular workflow, source alpha clip,
        // front-face culling. Exported backface geometry carries its own color.
        new Profile("fde6e778-0f7a-e584-38d6-89d44cee59f6","Charcoal") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, Cutout=true, Cutoff=.5f, Smoothness=.01f },
        new Profile("725f4c6a-6427-6524-29ab-da371924adab","DryBrush") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, Cutout=true, Cutoff=.2f, Smoothness=.05f },
        new Profile("2e03b1bf-3ebd-4609-9d7e-f4cafadc4dfa","SquarePaper") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, Cutout=true, Cutoff=.16f, Smoothness=.145f },
        new Profile("f4a0550c-332a-4e1a-9793-b71508f4a454","DoubleFlat") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, Cutout=true, Cutoff=.5f },
        // Original geometry normals and axis-color fragments are prepared per triangle.
        new Profile("d3f3b18a-da03-f694-b838-28ba8e749a98","3D Printing Brush") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, IgnoreAlbedoTexture=true },
        new Profile("4391ffaa-df73-4396-9e33-31e4e4930b27","FacetedTube") { Unlit=true, SingleSided=true, IgnoreAlbedoTexture=true },
        new Profile("7ae1f880-a517-44a0-99f9-1cab654498c6","ConcaveHull") { LinearVertexColors=true, SpecularModel=true, IgnoreAlbedoTexture=true, DiffuseGain=1.5f },
        new Profile("528fc9e8-5b95-48f3-b3f0-67ac5236f010","MatteMembrane") { LinearVertexColors=true, SpecularModel=true, IgnoreAlbedoTexture=true, DiffuseGain=1.3f },
        new Profile("63b09da0-a6a6-4f86-a48a-28cc53fd64de","ShinyMembrane") { LinearVertexColors=true, SpecularModel=true, Smoothness=.743f, Specular=.2f, SpecularIsLinear=true, IgnoreAlbedoTexture=true, DiffuseGain=1.3f },
        new Profile("6a1cf9f9-032c-45ec-311e-a6680bee32e9","DanceFloor") { Unlit=true },
        new Profile("6a1cf9f9-032c-45ec-9b6e-a6680bee32e9","HyperGrid") { Unlit=true, Additive=true },
        new Profile("6a1cf9f9-032c-45ec-9b1d-a6680bee30f7","Dots") { Unlit=true, Additive=true },
        new Profile("10201aa3-ebc2-42d8-84b7-2e63f6eeb8ab","Waveform") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        new Profile("dce872c2-7b49-4684-b59b-c45387949c5c","Hypercolor") { SpecularModel=true, Cutout=true, Cutoff=.5f, IgnoreAlbedoTexture=true },
        new Profile("e8ef32b1-baa8-460a-9c2c-9cf8506794f5","HypercolorSingleSided") { SpecularModel=true, Cutout=true, Cutoff=.5f, IgnoreAlbedoTexture=true },
        new Profile("f6e85de3-6dcc-4e7f-87fd-cee8c3d25d51","Electricity") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        new Profile("ad1ad437-76e2-450d-a23a-e17f8310b960","Rainbow") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        // Normal shader fragment bakes; currently require a finite drawing timeline.
        new Profile("0f0ff7b2-a677-45eb-a7d6-0cd7206f4816","ChromaticWave") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        new Profile("c33714d1-b2f9-412e-bd50-1884c9d46336","Plasma") { Unlit=true, Additive=true },
        new Profile("44bb800a-fbc3-4592-8426-94ecb05ddec3","Streamers") { Unlit=true, Additive=true },
        new Profile("b2ffef01-eaaa-4ab5-aa64-95a2c4f5dbc6","NeonPulse") { SpecularModel=true, SpecularIsLinear=true, Specular=.05f, Smoothness=.8f, IgnoreAlbedoTexture=true },
        new Profile("1caa6d7d-f015-3f54-3a4b-8b5354d39f81","Comet") { Unlit=true, Additive=true },
        new Profile("4391385a-df73-4396-9e33-31e4e4930b27","Toon") { Unlit=true, SingleSided=true, IgnoreAlbedoTexture=true },
        new Profile("9871385a-df73-4396-9e33-31e4e4930b27","TubeToonInverted") { Unlit=true, SingleSided=true, IgnoreAlbedoTexture=true },
        new Profile("cf019139-d41c-4eb0-a1d0-5cf54b0a42f3","Highlighter") { Unlit=true, Additive=true, Cutout=true, Cutoff=.12f },
        new Profile("accb32f5-4509-454f-93f8-1df3fd31df1b","SoftHighlighter") { Unlit=true, Additive=true },
        new Profile("d229d335-c334-495a-a801-660ac8a87360","VelvetInk") { Unlit=true, Additive=true },
        new Profile("d9cc5e99-ace1-4d12-96e0-4a7c18c99cfc","Fairy") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        new Profile("bdf65db2-1fb7-4202-b5e0-c6b5e3ea851e","Space") { Unlit=true, Additive=true, IgnoreAlbedoTexture=true },
        new Profile("a8147ce1-005e-abe4-88e8-09a1eaadcc89","Rising Bubbles") { Unlit=true, Additive=true },
        new Profile("89d104cd-d012-426b-b5b3-bbaee63ac43c","Bubbles") { Unlit=true, Additive=true },
        new Profile("d902ed8b-d0d1-476c-a8de-878a79e3a34c","Snow") { Unlit=true, Additive=true },
        new Profile("0eb4db27-3f82-408d-b5a1-19ebd7d5b711","Stars") { Unlit=true, Additive=true, Gain=2 },
        new Profile("2241cd32-8ba2-48a5-9ee7-2caef7e9ed62","Light") { Unlit=true, Additive=true, Gain=1.5f },
        new Profile("cb92b597-94ca-4255-b017-0e3f42f12f9e","Fire") { Unlit=true, Additive=true, Gain=1.5f },
        new Profile("02ffb866-7fb2-4d15-b761-1012cefb1360","Embers") { Unlit=true, Additive=true, Gain=2 },
        new Profile("70d79cca-b159-4f35-990c-f02193947fe8","Smoke") { Unlit=true, Additive=true },
        new Profile("d0262945-853c-4481-9cbd-88586bed93cb","DuctTape") { LinearVertexColors=true, Cutout=true, Cutoff=.2f, Smoothness=.414f, Tint=.7019608f },
        new Profile("8dc4a70c-d558-4efd-a5ed-d4e860f40dc3","Splatter") { Cutout=true, Cutoff=.2f },
        new Profile("5347acf0-a8e2-47b6-8346-30c70719d763","WigglyGraphite") { Cutout=true, LinearVertexColors=true, SpecularModel=true },
        new Profile("700f3aa8-9a7c-2384-8b8a-ea028905dd8c","CelVinyl") { Unlit=true, Cutout=true, Cutoff=.554f },
        new Profile("4391aaaa-df81-4396-9e33-31e4e4930b27","LightWire") { Smoothness=.81f, EmissiveWire=true, Gain=2 },
        new Profile("4391aaaa-df73-4396-9e33-31e4e4930b27","Disco") { Metallic=1, Smoothness=.65f },
        new Profile("79348357-432d-4746-8e29-0e25c112e3aa","MatteHull"),
        new Profile("c8313697-2563-47fc-832e-290f4c04b901","DiamondHull") { Transparent=true, Smoothness=.95f },
        new Profile("e0abbc80-0f80-e854-4970-8924a0863dcc","Petal"),
        new Profile("d381e0f5-3def-4a0d-8853-31e9200bcbda","Lofted"),
        // Normal materials for taper, single-sided paint, and hull brushes.
        // Double taper is baked from the original shader offsets before mesh import.
        new Profile("0d3889f3-3ede-470c-8af4-de4813306126","DoubleTaperedMarker") { Unlit=true },
        new Profile("0d3889f3-3ede-470c-8af4-f44813306126","DoubleTaperedFlat") { LinearVertexColors=true, SpecularModel=true },
        new Profile("2f212815-f4d3-c1a4-681a-feeaf9c6dc37","Icing") { LinearVertexColors=true, SpecularModel=true, Smoothness=.15f, SingleSided=true },
        new Profile("ea19de07-d0c0-4484-9198-18489a3c1487","Leaves") { LinearVertexColors=true, SpecularModel=true, Cutout=true, Cutoff=.5f, SingleSided=true },
        new Profile("faaa4d44-fcfb-4177-96be-753ac0421ba3","ShinyHull") { LinearVertexColors=true, SpecularModel=true, Smoothness=.743f, Specular=.2f },
        new Profile("a8fea537-da7c-4d4b-817f-24f074725d6d","UnlitHull") { Unlit=true },
        // Quill's shader uses vertex RGBA, not the stale serialized Marker texture.
        // Native alpha blending approximates its MSAA alpha-to-coverage.
        new Profile("c4f8b3e2-9d1a-5e7f-4c3b-8a6d2f9e1c7b","QuillRibbon") { Unlit=true, Transparent=true, IgnoreAlbedoTexture=true },
        new Profile("b3e7f8c2-4d5a-1e9b-6c8f-3a7d2f1e9c4b","QuillCube") { Unlit=true, Transparent=true, IgnoreAlbedoTexture=true },
        new Profile("f1c4e3e7-2a9f-4b5d-8c3e-7d9a1f8e6b4c","QuillCylinder") { Unlit=true, Transparent=true, IgnoreAlbedoTexture=true },
        new Profile("a2d5f6b8-9c1e-4f3a-7b8d-2e6c9f4a1d5b","QuillEllipse") { Unlit=true, Transparent=true, IgnoreAlbedoTexture=true },
        new Profile("55303bc4-c749-4a72-98d9-d23e68e76e18","FlatDeprecated") { LinearVertexColors=true, SpecularModel=true },
        new Profile("1a26b8c0-8a07-4f8a-9fac-d2ef36e0cad0","TaperedMarker_Flat") { LinearVertexColors=true, SpecularModel=true, SingleSided=true, IgnoreAlbedoTexture=true },
        new Profile("8e58ceea-7830-49b4-aba9-6215104ab52a","MylarTube") { SpecularModel=true, SingleSided=true, Specular=.7490196f, SpecularIsLinear=true, Smoothness=.68f },
        // Additional normal-material families, exercised with multi-color charts.
        // Spikes still differs in lit brightness; this is an approximation, not a parity guarantee.
        new Profile("d1d991f2-e7a0-4cf1-b328-f57e915e6260","DotMarker") { Unlit=true, Cutout=true, Cutoff=.5f },
        new Profile("0077f88c-d93a-42f3-b59b-b31c50cdb414","Taffy") { Unlit=true, Cutout=true, Cutoff=.5f },
        new Profile("1161af82-50cf-47db-9706-0c3576d43c43","CoarseBristles") { LinearVertexColors=true, SpecularModel=true, Cutout=true, Cutoff=.25f },
        new Profile("b468c1fb-f254-41ed-8ec9-57030bc5660c","TaperedFlat") { LinearVertexColors=true, SpecularModel=true, Cutout=true, Cutoff=.067f },
        new Profile("f1114e2e-eb8d-4fde-915a-6e653b54e9f5","Paper") { LinearVertexColors=true, Cutout=true, Cutoff=.16f, Smoothness=.145f },
        new Profile("75b32cf0-fdd6-4d89-a64b-e2a00b247b0f","ThickPaint") { LinearVertexColors=true, Cutout=true, Cutoff=.5f, Smoothness=.4f },
        new Profile("b67c0e81-ce6d-40a8-aeb0-ef036b081aa3","WetPaint") { LinearVertexColors=true, Cutout=true, Cutoff=.3f, Smoothness=.85f },
        new Profile("cf7f0059-7aeb-53a4-2b67-c83d863a9ffa","Spikes") { LinearVertexColors=true, SpecularModel=true },
        // Normal m_Material: Unlit.shader, LitVertexColor, LitStandardAlphaCut.
        new Profile("429ed64a-4e97-4466-84d3-145a861ef684","Marker") { Unlit=true, Cutout=true, Cutoff=.067f },
        new Profile("4391385a-cf83-4396-9e33-31e4e4930b27","Wire") { Unlit=true, Cutout=true, Cutoff=.067f },
        new Profile("d90c6ad8-af0f-4b54-b422-e0f92abe1b3c","TaperedMarker") { Unlit=true, Cutout=true, Cutoff=.067f },
        new Profile("2d35bcf0-e4d8-452c-97b1-3311be063130","Flat") { LinearVertexColors=true, SpecularModel=true },
        new Profile("f5c336cf-5108-4b40-ade9-c687504385ab","Ink") { LinearVertexColors=true, Cutout=true, Cutoff=.5f, Smoothness=.263f },
        new Profile("f72ec0e7-a844-4e38-82e3-140c44772699","OilPaint") { LinearVertexColors=true, Cutout=true, Cutoff=.5f, Smoothness=.4f }
    };
    public static Profile Find(string guid)
    {
        foreach (var profile in All) if (profile.Guid == guid) return profile;
        throw new NotSupportedException("No audited Open Brush profile for " + guid);
    }
}











