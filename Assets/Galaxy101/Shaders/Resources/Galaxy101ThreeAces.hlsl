#ifndef GALAXY101_THREE_ACES_INCLUDED
#define GALAXY101_THREE_ACES_INCLUDED

// three.js ACESFilmicToneMapping (tonemapping_pars_fragment, r150+), the design's renderer (table3d.js: exposure 1.05):
// colour *= exposure / 0.6, the RRT+ODT fit between the ACES input and output matrices, saturate. Linear in, linear out.
// HLSL rows here are the GLSL column-major constructors' columns.

static const float3x3 G101_ACES_IN = float3x3(
    0.59719, 0.35458, 0.04823,
    0.07600, 0.90834, 0.01566,
    0.02840, 0.13383, 0.83777);
static const float3x3 G101_ACES_OUT = float3x3(
     1.60475, -0.53108, -0.07367,
    -0.10208,  1.10813, -0.00605,
    -0.00327, -0.07276,  1.07602);
static const float3x3 G101_ACES_IN_INV = float3x3(
     1.7647410, -0.6757777, -0.0889633,
    -0.1470279,  1.1602515, -0.0132237,
    -0.0363368, -0.1624364,  1.1987733);
static const float3x3 G101_ACES_OUT_INV = float3x3(
    0.6430382, 0.3111868, 0.0457755,
    0.0592687, 0.9314365, 0.0092949,
    0.0059619, 0.0639290, 0.9301184);

float3 G101_RRTAndODTFit(float3 v)
{
    float3 a = v * (v + 0.0245786) - 0.000090537;
    float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
    return a / b;
}

float3 G101_ThreeAces(float3 color, float exposure)
{
    color *= exposure / 0.6;
    color = mul(G101_ACES_IN, color);
    color = G101_RRTAndODTFit(color);
    color = mul(G101_ACES_OUT, color);
    return saturate(color);
}

// The colour that G101_ThreeAces maps to `target` — for materials three.js draws with toneMapped:false, which share the
// tone-mapped camera buffer in Unity. Solves the fit's quadratic per channel.
float3 G101_ThreeAcesInverse(float3 target, float exposure)
{
    float3 v = min(mul(G101_ACES_OUT_INV, saturate(target)), 0.9999);
    float3 A = 1.0 - v * 0.983729;
    float3 B = 0.0245786 - v * 0.4329510;
    float3 C = -(0.000090537 + v * 0.238081);
    float3 x = (-B + sqrt(max(B * B - 4.0 * A * C, 0.0))) / (2.0 * A);
    return max(mul(G101_ACES_IN_INV, max(x, 0.0)) * (0.6 / exposure), 0.0);
}

#endif
