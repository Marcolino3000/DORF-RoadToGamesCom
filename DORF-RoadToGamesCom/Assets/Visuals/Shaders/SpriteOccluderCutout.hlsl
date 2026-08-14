#ifndef COD_SPRITE_OCCLUDER_CUTOUT_INCLUDED
#define COD_SPRITE_OCCLUDER_CUTOUT_INCLUDED

#include "SurfaceInput.hlsl"

// Own copy of UnlitInput.hlsl's buffer: the SRP batcher needs every material property of this
// shader in one UnityPerMaterial block, and UnlitInput.hlsl is shared with UnlitSprite.shader.
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
half _Cutoff;
half4 _CutoutRimColor;
float _CutoutSoftness;
float _CutoutMinAlpha;
float _CutoutRimWidth;
float _CutoutDepthFade;
float _CutoutEdgeTiling;
float _CutoutEdgeStrength;
float _CutoutMaxDepthSlope;
CBUFFER_END

// Written every frame by CharacterCutout.cs. Globals, so they stay OUTSIDE of UnityPerMaterial -
// putting them in would break SRP batcher compatibility.
float4 _CodCutoutCenter; // xy = viewport position of the character, z = her world Z, w = 1 while active
float4 _CodCutoutRadius; // xy = half size of the hole in viewport units

TEXTURE2D(_CutoutMask);     SAMPLER(sampler_CutoutMask);
TEXTURE2D(_CutoutEdgeTex);  SAMPLER(sampler_CutoutEdgeTex);

// How far this pixel is inside the hole: 0 leaves the sprite alone, 1 is the fully open middle,
// everything between is the blurred edge. The caller turns this into alpha, which only works
// because the material alpha blends - see the note on the blend mode in the shader.
//
// screenUV     0..1 viewport position of this pixel, same convention as Camera.WorldToViewportPoint
// positionWSz  world Z of this pixel - the axis the camera sorts the sprites along
// rim          out: band around the middle of the fade, for drawing a border on the hole
half CharacterHole(float2 screenUV, float positionWSz, out half rim)
{
    rim = 0;

    // Standing sprites have one single Z, sprites lying on the ground (Wiese, Weg, the house
    // planes) run away from the camera and change Z from pixel to pixel. Only standing sprites may
    // be cut, otherwise the lawn in front of her would get a hole in it. Has to be measured before
    // any branch, derivatives need uniform control flow.
    float depthSlope = fwidth(positionWSz);

    if (_CodCutoutCenter.w < 0.5 || depthSlope > _CutoutMaxDepthSlope)
        return 0;

    // Only sprites standing in front of the character may open up. Ramp it over _CutoutDepthFade
    // world units so nothing pops while she walks past an object.
    float inFront = 1.0 - smoothstep(_CodCutoutCenter.z - _CutoutDepthFade, _CodCutoutCenter.z, positionWSz);
    if (inFront <= 0.0)
        return 0;

    // Distance to the centre of the hole, normalised so 1 sits on its edge. CharacterCutout.cs
    // already divides the horizontal radius by the aspect, so this comes out round on screen.
    float2 offset = (screenUV - _CodCutoutCenter.xy) / max(_CodCutoutRadius.xy, 1e-4);
    float distanceToCentre = length(offset);

    // This is the blur. _CutoutSoftness is how much of the radius the fade eats: small values keep
    // a tight edge, 1 fades all the way from the centre outwards.
    float hole = 1.0 - smoothstep(1.0 - _CutoutSoftness, 1.0, distanceToCentre);

    // Optional hand drawn silhouette, sampled in the space of the hole. Its alpha scales the hole
    // locally, so a ragged texture turns the circle into an art directed shape. Default is white,
    // which leaves the circle alone. Outside the mask quad there is no hole to shape anyway.
    float2 maskUV = offset * 0.5 + 0.5;
    float insideMask = all(maskUV == saturate(maskUV)) ? 1.0 : 0.0;
    hole *= SAMPLE_TEXTURE2D(_CutoutMask, sampler_CutoutMask, saturate(maskUV)).a * insideMask;

    hole *= inFront;

    // The edge texture pushes the fade around locally, so the border goes ragged instead of being a
    // perfect gradient. Sampled in screen space so the pattern sits still while the sprite scrolls
    // past, aspect corrected so the grain stays square. At strength 0 nothing happens.
    float2 edgeUV = screenUV * float2(_ScreenParams.x / _ScreenParams.y, 1.0) * _CutoutEdgeTiling;
    float edgeTex = SAMPLE_TEXTURE2D(_CutoutEdgeTex, sampler_CutoutEdgeTex, edgeUV).r;
    hole = saturate(hole + (edgeTex - 0.5) * _CutoutEdgeStrength);

    // A band peaking halfway through the fade, so the hole can be given a drawn border instead of
    // just petering out. _CutoutRimWidth 1 spreads it over the whole fade, small values keep it tight.
    rim = saturate(1.0 - abs(hole - 0.5) / max(_CutoutRimWidth * 0.5, 1e-4));
    rim = smoothstep(0.0, 1.0, rim);

    return hole;
}

#endif
