using System;
using System.Linq;
using UnityEngine;

public class OpenBrushParticleMotion : MonoBehaviour {
    [Serializable] public class Particle {
        public Vector3 center,origin;
        public Vector4 uv;
        public Color color;
        public float size,rotation,sourceStart,sourceEnd,birth,playBirth;
    }
    public enum BrushKind { Stars, Snow, Bubbles, DotsBase, DotsEmission, RisingBubbles }
    public BrushKind Kind;
    public Particle[] Particles;
    public Material SourceMaterial;
    public OpenBrushTimeline Timeline;
    public Mesh[] Frames;
    // Dots changes only at particle births; use exact event times instead of
    // hundreds of identical frames per second of a long held drawing.
    public float[] ExplicitTimes;
    public bool IsDots => Kind==BrushKind.DotsBase || Kind==BrushKind.DotsEmission;
    public Texture2D BakedTexture;
    public Vector4[] BakedUV;
    public Vector4[][] PhaseUV; // Optional per-particle brightness atlas phases.
    public float PeakGain=1;
    public const float SparkleRate=5.3f;
    public const int CycleFrames=64;
    // Rising Bubbles also uses a 60-second static loop. Scroll closes exactly,
    // but its 6pi-second lateral wobble leaves a bounded (<2.2cm per lateral axis) seam.
    // Bubbles bakes 60 seconds of aperiodic curl noise; the static loop has a position seam.
    // Snow has incommensurate 5 s scroll and 2pi s jitter. A 25 s static loop
    // closes the scroll and bounds the remaining jitter seam to 1.4 mm.
    public float Period => IsDots ? 1 : Kind==BrushKind.Stars ? Mathf.PI/SparkleRate : (Kind==BrushKind.Snow ? 25 : 60);
    public int StaticFrames => IsDots ? 1 : Kind==BrushKind.Stars ? CycleFrames : (Kind==BrushKind.Snow ? 750 : 1800);
    public int FrameCount => ExplicitTimes!=null ? ExplicitTimes.Length : Timeline==null ? StaticFrames : Mathf.CeilToInt(Timeline.Duration*60)+1;
    public float Seconds(int frame) => ExplicitTimes!=null ? ExplicitTimes[frame] : Timeline==null ? frame*Period/StaticFrames : Mathf.Min(frame/60f,Timeline.Duration);
    public static float Brightness(float seconds,float seed) => Mathf.Pow(Mathf.Abs(Mathf.Sin(seconds*SparkleRate+seed*2*Mathf.PI)),20);
    public void Sample(int frame,out Vector3[] positions,out Vector2[] sizes,out Color[] colors) {
        float seconds=Seconds(frame);
        positions=new Vector3[Particles.Length];sizes=new Vector2[Particles.Length];colors=new Color[Particles.Length];
        for(int i=0;i<Particles.Length;i++) {
            var p=Particles[i];bool visible=Timeline==null || seconds>=p.playBirth;
            float spread=Timeline==null ? 1 : 1-Mathf.Exp(-(Kind==BrushKind.RisingBubbles ? 1.25f : 1.539f)*Mathf.Max(0,seconds-p.playBirth));
            positions[i]=IsDots ? p.center : Vector3.Lerp(p.origin,p.center,spread);
            sizes[i]=Vector2.one*(visible ? p.size : 0);
            Color hdr;
            if(IsDots) {
                // Source c=.4*paintRGBA*spriteRGBA, c.rgb += c.rgb*c.a*300.
                // Two additive layers factor this exactly: .4*RGB*spriteRGB
                // plus 48*RGB*paintAlpha*spriteRGB*spriteAlpha. Do not clamp
                // the HDR vertex channels or use alpha as a sparkle phase.
                hdr=p.color*(Kind==BrushKind.DotsBase ? .4f : 48*p.color.a);
            } else if(Kind==BrushKind.RisingBubbles) {
                var center=positions[i];
                var sourceCenter=new Vector3(-center.x,center.y,-center.z)*10;
                var displacement=OpenBrushRisingAppearance.Displacement(sourceCenter,p.color.a,seconds);
                positions[i]+=spread*new Vector3(-displacement.x,displacement.y,-displacement.z);
                hdr=Color.white; // Time-varying paint + unchanged white highlight are in the atlas.
            } else if(Kind==BrushKind.Bubbles) {
                var center=positions[i];
                var seed=new Vector3(-center.x,center.y,-center.z)*10;
                var displacement=OpenBrushBubbleMath.Displacement(seed,seconds);
                positions[i]+=spread*new Vector3(-displacement.x,displacement.y,-displacement.z);
                hdr=Color.white; // Paint RGB and independent white highlights are in BakedTexture.
            } else if(Kind==BrushKind.Snow) {
                float phase=Mathf.Repeat(seconds*.2f+p.color.a,1);
                // Source canvas displacement is decimeters. Legacy GLB plus UniGLTF
                // reflect X and Z; the send root restores that basis with Y=180.
                positions[i]+=spread*.1f*new Vector3(-Mathf.Sin(phase*12+seconds)*.1f,
                    (phase-.5f)*-3,-Mathf.Cos(phase*6+seconds)*.1f);
                float fade=Mathf.Pow(1-Mathf.Abs(2*(phase-.5f)),3);
                // Snow: 2*RGB*Tint times 2*alpha*Tint.a, with normal Tint=(1,1,1,1).
                hdr=p.color*(4*fade);
            } else hdr=p.color*(800*Brightness(seconds,p.color.a));
            // Stars multiplies its HDR vertex RGB by sprite RGB and alpha in
            // the fragment. Clamping before the sprite loses colored edges.
            // MeshConverter preserves float colors, so retain that range.
            // Native linear composition still differs in the subunit range
            // from Open Brush's gamma framebuffer and postprocessing.
            colors[i]=Kind==BrushKind.Stars || IsDots ? new Color(hdr.r,hdr.g,hdr.b,1) :
                new Color(Mathf.Clamp01(hdr.r),Mathf.Clamp01(hdr.g),Mathf.Clamp01(hdr.b),1).linear;
        }
    }
    public void Bake() {
        if((long)FrameCount*Particles.Length>1000000)throw new InvalidOperationException("Particle bake exceeds 1,000,000 frame-particles; split the sketch.");
        Frames=new Mesh[FrameCount];
        var indices=System.Linq.Enumerable.Range(0,Particles.Length).ToArray();
        var uv0=new Vector2[Particles.Length];var uv1=new Vector2[Particles.Length];
        for(int i=0;i<Particles.Length;i++) {
            var uv=Particles[i].uv;
            // Native PointMesh stores UV center in UV0 and full UV extent in UV1.
            uv0[i]=new Vector2((uv.x+uv.z)*.5f,1-(uv.y+uv.w)*.5f);uv1[i]=new Vector2(uv.z-uv.x,uv.w-uv.y);
            if(BakedUV!=null) {
                uv=BakedUV[i];uv0[i]=new Vector2((uv.x+uv.z)*.5f,(uv.y+uv.w)*.5f);
                uv1[i]=new Vector2(uv.z-uv.x,uv.w-uv.y);
            }
        }
        for(int f=0;f<Frames.Length;f++) {
            Sample(f,out var positions,out var sizes,out var colors);
            if(PhaseUV!=null)for(int i=0;i<Particles.Length;i++) {
                var uv=PhaseUV[i][OpenBrushRisingAppearance.PhaseIndex(Seconds(f),Particles[i].color.a)];
                uv0[i]=new Vector2((uv.x+uv.z)*.5f,(uv.y+uv.w)*.5f);
                uv1[i]=new Vector2(uv.z-uv.x,uv.w-uv.y);
            }
            var normals=new Vector3[Particles.Length];
            for(int i=0;i<normals.Length;i++)normals[i]=new Vector3(sizes[i].x,sizes[i].y,Particles[i].rotation);
            var mesh=new Mesh{name=Kind+"Points_"+f,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=positions;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv0;mesh.uv2=uv1;
            mesh.SetIndices(indices,MeshTopology.Points,0);mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(System.Linq.Enumerable.Max(Particles,p=>p.size));mesh.bounds=bounds;
            Frames[f]=mesh;
        }
    }
}




