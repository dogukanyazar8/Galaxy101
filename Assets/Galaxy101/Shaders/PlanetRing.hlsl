#ifndef GALAXY101_PLANET_RING_INCLUDED
#define GALAXY101_PLANET_RING_INCLUDED
// Ring profile and both ray-shadow tests ported from the supplied canvas renderer.
float RingSmooth(float a, float b, float x) { float t=saturate((x-a)/(b-a)); return t*t*(3-2*t); }
float RingHash(float i) { return frac(sin(i*127.1+311.7)*43758.5453); }
float RingNoise(float x) { float i=floor(x),f=frac(x); return lerp(RingHash(i),RingHash(i+1),f*f*(3-2*f)); }
float4 RingAt(float r)
{
    float a=0; float3 c=0;
    if(r>1.24 && r<1.53) { a=.1+.12*RingSmooth(1.24,1.53,r);c=float3(150,140,124); }
    else if(r>=1.53 && r<1.95) {float t=(r-1.53)/.42,s=sin(t*3.14159265359);a=.78+.18*s;c=float3(214+22*s,194+18*s,152+10*t);}
    else if(r>=1.95 && r<2.03) {a=.05;c=float3(120,112,100);}
    else if(r>=2.03 && r<2.27) {a=(r>2.205 && r<2.218) ? .05 : .62;c=float3(196,182,156);}
    else if(r>=2.31 && r<2.33) {a=.35;c=float3(210,200,180);}
    if(a>0) {c*=.82+.26*RingNoise(r*140)+.08*RingNoise(r*380);a*=.75+.35*RingNoise(r*210+7);}
    return float4(c/255,saturate(a));
}
#endif
