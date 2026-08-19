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

// The material blends One Zero, so a lower alpha would not reveal anything - the hole has to be cut
// with clip(). Returns how close this pixel is to the edge of the hole (1 right at the rim, 0 away
// from it) and discards everything inside of it.
//
// screenUV     0..1 viewport position of this pixel, same convention as Camera.WorldToViewportPoint
// positionWSz  world Z of this pixel - the axis the camera sorts the sprites along
half CutCharacterHole(float2 screenUV, float positionWSz)
{
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

    // Distance to the centre of the hole, normalised so 1 sits on the edge of the ellipse.
    float2 offset = (screenUV - _CodCutoutCenter.xy) / max(_CodCutoutRadius.xy, 1e-4);
    float distanceToCentre = length(offset);

    // 1 in the middle of the hole, 0 outside of it.
    float hole = 1.0 - smoothstep(1.0 - _CutoutSoftness, 1.0, distanceToCentre);

    // Optional hand drawn silhouette, sampled in the space of the hole. Its alpha scales the hole
    // locally, so a ragged texture turns the ellipse into an art directed shape. Default is white,
    // which leaves the ellipse alone. Outside the mask quad there is no hole to shape anyway.
    float2 maskUV = offset * 0.5 + 0.5;
    float insideMask = all(maskUV == saturate(maskUV)) ? 1.0 : 0.0;
    hole *= SAMPLE_TEXTURE2D(_CutoutMask, sampler_CutoutMask, saturate(maskUV)).a * insideMask;

    hole *= inFront;

    // The edge texture breaks the falloff into a ragged dissolve instead of a clean oval. It is
    // sampled in screen space so the pattern sits still while the sprite scrolls past. At strength
    // 0 the threshold is a flat 0.5 and the hole stays a clean ellipse, whatever texture is set.
    float edgeTex = SAMPLE_TEXTURE2D(_CutoutEdgeTex, sampler_CutoutEdgeTex, screenUV * _CutoutEdgeTiling).r;
    float edgeNoise = lerp(0.5, edgeTex, _CutoutEdgeStrength);

    clip(edgeNoise - hole);

    // Everything that survived: 1 right at the cut, falling off outwards, so the hole can be given
    // a drawn border instead of just ending.
    return smoothstep(edgeNoise - _CutoutRimWidth, edgeNoise, hole);
}

#endif
