#ifndef GALAXY101_PLANET_RING_INCLUDED
#define GALAXY101_PLANET_RING_INCLUDED
// JS double-precision profile, baked once as premultiplied gamma-space data. Explicit gradients are computed before
// visibility branches so minification follows the projected pixel footprint, including oblique ring sections.
sampler2D _RingProfile;
float4 RingAt(float radius,float2 gradient)
{
    float4 profile=tex2Dgrad(_RingProfile,float2(radius/2.4,.5),float2(gradient.x/2.4,0),float2(gradient.y/2.4,0));
    return float4(profile.a>1e-6?profile.rgb/profile.a:0,profile.a);
}
#endif
