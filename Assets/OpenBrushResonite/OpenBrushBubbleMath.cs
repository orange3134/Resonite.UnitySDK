// Derived from Open Brush Bubbles.shader (Apache-2.0, Tilt Brush Authors 2020)
// and Ashima Arts Noise.cginc (MIT, Ian McEwan 2011).
using UnityEngine;

public static class OpenBrushBubbleMath {
    static float Frac(float v)=>v-Mathf.Floor(v);
    static float Mod289(float v)=>v-Mathf.Floor(v/289f)*289f;
    static float Permute(float v)=>Mod289((v*34+1)*v);
    public static float Noise(float vx,float vy) {
        const float c=.211324865405187f,d=.366025403784439f,e=-.577350269189626f;
        float ix=Mathf.Floor(vx+(vx+vy)*d),iy=Mathf.Floor(vy+(vx+vy)*d);
        float x=vx-ix+(ix+iy)*c,y=vy-iy+(ix+iy)*c;
        float ax=x>y ? 1:0,ay=x>y ? 0:1;
        ix=Mod289(ix);iy=Mod289(iy);
        return 130*(Corner(x,y,Permute(Permute(iy)+ix))+
            Corner(x-ax+c,y-ay+c,Permute(Permute(iy+ay)+ix+ax))+
            Corner(x+e,y+e,Permute(Permute(iy+1)+ix+1)));
    }
    static float Corner(float x,float y,float p) {
        float m=Mathf.Max(.5f-x*x-y*y,0);m*=m;m*=m;
        float g=2*Frac(p*.024390243902439f)-1,h=Mathf.Abs(g)-.5f;
        float a=g-Mathf.Floor(g+.5f);
        m*=1.79284291400159f-.85373472095314f*(a*a+h*h);
        return m*(a*x+h*y);
    }
    // Position in original Open Brush canvas units (decimeters), not export meters.
    public static Vector3 Displacement(Vector3 seed,float seconds) {
        float t=seconds*.5f+1;
        var jitter=new Vector3(Mathf.Sin(t+seconds+seed.z*.2f),
            Mathf.Cos(t*1.2f+seconds+seed.x*.2f),Mathf.Cos(t+seconds+seed.x*.2f))*.02f;
        var v=(seed+jitter)*.1f+Vector3.one*(seconds*.25f);
        // In snoise3D=(N(x,y),N(y,z),N(z,x)), each first curl term is
        // independent of its differentiation axis and cancels exactly.
        const float d=30;
        var curl=-new Vector3(Noise(v.y,v.z+d)-Noise(v.y,v.z-d),
            Noise(v.z,v.x+d)-Noise(v.z,v.x-d),Noise(v.x,v.y+d)-Noise(v.x,v.y-d))/(2*d);
        return (jitter+curl*10)*.1f;
    }
}
