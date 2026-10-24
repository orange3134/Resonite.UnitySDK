using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushParticleChecks {
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run(){try{
        var budgetParts=new[]{
            new OpenBrushParticleImport.Part{brushGuid=OpenBrushParticleImport.SnowGuid,particles=new OpenBrushParticleMotion.Particle[2671]},
            new OpenBrushParticleImport.Part{brushGuid=OpenBrushParticleImport.RisingGuid,particles=new OpenBrushParticleMotion.Particle[32]},
            new OpenBrushParticleImport.Part{brushGuid=OpenBrushParticleImport.DotsGuid,particles=new OpenBrushParticleMotion.Particle[1199]}};
        var plan=OpenBrushParticleImport.CompletedFramePlan(budgetParts);
        Require(plan[0]==1 && plan[1]==1800 && plan[2]==2,"Completed Snow uses static points, other small loops unchanged");
        Require(budgetParts.Select((part,i)=>(long)part.particles.Length*plan[i]).Sum()<=1000000,"Aggregate frame-particle budget");
        Require(OpenBrushParticleImport.CompletedFramePlan(new[]{new OpenBrushParticleImport.Part{brushGuid=OpenBrushParticleImport.StarsGuid,particles=new OpenBrushParticleMotion.Particle[1]}})[0]==64,"Sparkle cadence preserved");
        bool rejected=false;try{OpenBrushParticleImport.CompletedFramePlan(budgetParts,100);}catch(InvalidOperationException){rejected=true;}
        Require(rejected,"Minimum cadence still enforces budget");
        var go=new GameObject("ParticleChecks");var p=go.AddComponent<OpenBrushParticleMotion>();
        p.Particles=new[]{new OpenBrushParticleMotion.Particle{center=new Vector3(1,2,3),origin=Vector3.zero,size=.02f,uv=new Vector4(.5f,0,1,.5f),color=new Color(.3f,.6f,.9f,.25f),playBirth=1}};
        p.Bake();Require(p.Frames.Length==64,"Static cycle frame count");var mesh=p.Frames[0];
        Require(mesh.GetTopology(0)==MeshTopology.Points && mesh.vertexCount==1,"One point per source particle");
        Require((mesh.normals[0]-new Vector3(.02f,.02f,0)).magnitude<1e-6,"Native billboard size encoding");
        Require((mesh.uv[0]-new Vector2(.75f,.75f)).magnitude<1e-6,"UV atlas center and Y flip");
        Require(mesh.uv2[0]==new Vector2(.5f,.5f),"UV atlas extent");
        Require((mesh.vertices[0]-p.Particles[0].center).magnitude<1e-6,"Original resting center");
        Require(Mathf.Abs(mesh.colors[0].r-240)<1e-4 && mesh.colors[0].a==1,"Stars preserves source HDR; seed drives phase, not opacity");
        var raw=MeshConverter.ConvertMesh(mesh);
        Require(Mathf.Abs(raw.Colors[0].r-240)<1e-4 && Mathf.Abs(raw.Colors[0].b-720)<1e-4,"SDK raw mesh transport retains floating-point HDR colors");
        Require(OpenBrushParticleMotion.Brightness(p.Period*.5f,.25f)<1e-5,"Independent sparkle phase");
        foreach(var m in p.Frames)UnityEngine.Object.DestroyImmediate(m);
        p.Timeline=go.AddComponent<OpenBrushTimeline>();p.Timeline.Duration=3;p.Bake();
        Require(p.Frames.Length==181,"60 fps bounded growth sequence");
        Require(p.Frames[0].normals[0]==Vector3.zero && p.Frames[59].normals[0]==Vector3.zero,"Initial blank");
        Require(p.Frames[60].normals[0].x==.02f,"Particle appears at birth");
        Require(p.Frames[60].vertices[0]==Vector3.zero,"Particle starts at stroke position");
        Require(Vector3.Distance(p.Frames[180].vertices[0],p.Particles[0].center)<.18f,"Source exponential spread");
        foreach(var m in p.Frames)UnityEngine.Object.DestroyImmediate(m);
        p.Timeline=null;p.Kind=OpenBrushParticleMotion.BrushKind.Snow;p.Bake();
        Require(p.Frames.Length==750 && p.Period==25,"Snow static cadence");
        var expected=p.Particles[0].center+new Vector3(-Mathf.Sin(3)*.01f,.075f,-Mathf.Cos(1.5f)*.01f);
        Require(Vector3.Distance(p.Frames[0].vertices[0],expected)<1e-6,"Snow source displacement and coordinate basis");
        Require(Mathf.Abs(p.Frames[75].vertices[0].y-p.Frames[0].vertices[0].y+.15f)<1e-5,"Snow falls at 6 cm per second");
        Require(Mathf.Abs(p.Frames[0].colors[0].r-Mathf.GammaToLinearSpace(.15f))<1e-6,"Snow alpha envelope and gamma conversion");
        foreach(var m in p.Frames)UnityEngine.Object.DestroyImmediate(m);
        var reference=new Vector3[3];
        p.Sample(0,out var atZero,out _,out _);reference[0]=atZero[0];
        p.Sample(75,out var atStep,out _,out _);reference[1]=atStep[0];
        p.Sample(300,out var atTen,out _,out _);reference[2]=atTen[0];
        p.StaticFrameCountOverride=250;p.Bake();
        Require(p.Frames.Length==250 && p.Period==25 && Mathf.Abs(p.Seconds(25)-2.5f)<1e-6,"Reduced cadence preserves loop and speed");
        Require((p.Frames[0].vertices[0]-reference[0]).magnitude<1e-6 && (p.Frames[25].vertices[0]-reference[1]).magnitude<1e-6 && (p.Frames[100].vertices[0]-reference[2]).magnitude<1e-6,"Reduced sampling follows identical source trajectory");
        Require(p.Frames.All(frame=>frame.vertexCount==1),"No particles discarded");
        foreach(var m in p.Frames)UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("OPENBRUSH_PARTICLE_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}


