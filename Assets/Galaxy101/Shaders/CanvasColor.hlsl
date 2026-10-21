#ifndef GALAXY101_CANVAS_COLOR_INCLUDED
#define GALAXY101_CANVAS_COLOR_INCLUDED
// Canvas 2D/CSS blend byte-space sRGB values. Convert only after compositing.
float3 CanvasToLinear(float3 value)
{
    #ifdef UNITY_COLORSPACE_GAMMA
    return value;
    #else
    return lerp(value/12.92,pow(max((value+.055)/1.055,0),2.4),step(.04045,value));
    #endif
}
float3 CanvasToGamma(float3 value)
{
    #ifdef UNITY_COLORSPACE_GAMMA
    return value;
    #else
    return lerp(value*12.92,1.055*pow(max(value,0),1.0/2.4)-.055,step(.0031308,value));
    #endif
}
float2 CanvasUv(float2 position) {return float2(position.x/1280,1-position.y/720);}
#endif
