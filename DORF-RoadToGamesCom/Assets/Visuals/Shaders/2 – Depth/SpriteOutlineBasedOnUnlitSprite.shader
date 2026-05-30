// Based on UnlitSprite.shader from this project.
// Keeps the same render-order settings (opaque tags, ZWrite On, same pass layout)
// while adding a simple sprite outline.
Shader "Universal Render Pipeline/Unlit Sprite Outline From Default Shader"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutout", Range(0.0, 1.0)) = 0.5
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineThickness("Outline Width (Texels)", Range(0.0, 100.0)) = 1.0

        // BlendMode
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("Src", Float) = 1.0
        [HideInInspector] _DstBlend("Dst", Float) = 0.0

        // Editmode props
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _SampleGI("SampleGI", float) = 0.0
    }

    HLSLINCLUDE
    #include "UnlitInput.hlsl"

    half4 _OutlineColor;
    float _OutlineThickness;
    float4 _MainTex_TexelSize;

    struct OutlineAttributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
        float4 color : COLOR;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct OutlineVaryings
    {
        float4 color : COLOR;
        float2 uv : TEXCOORD0;
        float fogCoord : TEXCOORD1;
        float4 vertex : SV_POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };

    struct OutlineDepthAttributes
    {
        float4 position : POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct OutlineDepthVaryings
    {
        float2 uv : TEXCOORD0;
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };

    OutlineVaryings OutlineVert(OutlineAttributes input)
    {
        OutlineVaryings output = (OutlineVaryings)0;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
        output.color = input.color;
        output.vertex = vertexInput.positionCS;
        output.uv = TRANSFORM_TEX(input.uv, _MainTex);
        output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);

        return output;
    }

    OutlineDepthVaryings OutlineDepthOnlyVertex(OutlineDepthAttributes input)
    {
        OutlineDepthVaryings output = (OutlineDepthVaryings)0;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
        output.positionCS = TransformObjectToHClip(input.position.xyz);
        return output;
    }

    half SampleSpriteAlpha(float2 uv, half vertexAlpha)
    {
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * vertexAlpha;
    }

    half ComputeOutlineMask(float2 uv, half vertexAlpha, half baseAlpha)
    {
        float2 texel = _MainTex_TexelSize.xy * _OutlineThickness * abs(_MainTex_ST.xy);

        half n = 0.0h;
        n = max(n, SampleSpriteAlpha(uv + float2( texel.x, 0.0), vertexAlpha));
        n = max(n, SampleSpriteAlpha(uv + float2(-texel.x, 0.0), vertexAlpha));
        n = max(n, SampleSpriteAlpha(uv + float2(0.0,  texel.y), vertexAlpha));
        n = max(n, SampleSpriteAlpha(uv + float2(0.0, -texel.y), vertexAlpha));
        n = max(n, SampleSpriteAlpha(uv + float2( texel.x,  texel.y), vertexAlpha));
        n = max(n, SampleSpriteAlpha(uv + float2( texel.x, -texel.y), vertexAlpha));
        n = max(n, SampleSpriteAlpha(uv + float2(-texel.x,  texel.y), vertexAlpha));
        n = max(n, SampleSpriteAlpha(uv + float2(-texel.x, -texel.y), vertexAlpha));

        half hasBase = step(_Cutoff, baseAlpha);
        half hasNeighbor = step(_Cutoff, n);
        return (1.0h - hasBase) * hasNeighbor;
    }

    half4 OutlineFrag(OutlineVaryings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        half2 uv = input.uv;
        half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
        half3 baseColor = texColor.rgb * input.color.rgb;
        half baseAlpha = texColor.a * input.color.a;

        half outlineMask = ComputeOutlineMask(uv, input.color.a, baseAlpha);
        half outlineAlpha = outlineMask * _OutlineColor.a * input.color.a;

        half3 outlineRgb = _OutlineColor.rgb * input.color.rgb;
        half3 color = lerp(baseColor, outlineRgb, outlineMask);
        half alpha = max(baseAlpha, outlineAlpha);
        clip(alpha - _Cutoff);

        #ifdef _ALPHAPREMULTIPLY_ON
        color *= alpha;
        #endif

        color = MixFog(color, input.fogCoord);
        return half4(color, alpha);
    }

    half4 OutlineDepthOnlyFragment(OutlineDepthVaryings input) : SV_TARGET
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        half baseAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
        half outlineMask = ComputeOutlineMask(input.uv, 1.0h, baseAlpha);
        half alpha = max(baseAlpha, outlineMask * _OutlineColor.a);

        Alpha(alpha, half4(1.0h, 1.0h, 1.0h, 1.0h), _Cutoff);
        return 0;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "ShaderModel" = "4.5"
        }
        LOD 100

        Blend [_SrcBlend][_DstBlend]
        ZWrite On
        Cull Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex OutlineDepthOnlyVertex
            #pragma fragment OutlineDepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            ENDHLSL
        }

        // This pass is used during lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            Cull Off

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaUnlit

            #include "UnlitMetaPass.hlsl"
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "ShaderModel" = "2.0"
        }
        LOD 100

        Blend [_SrcBlend][_DstBlend]
        ZWrite On
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma only_renderers gles gles3 glcore
            #pragma target 2.0

            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma only_renderers gles gles3 glcore
            #pragma target 2.0

            #pragma vertex OutlineDepthOnlyVertex
            #pragma fragment OutlineDepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            ENDHLSL
        }

        // This pass is used during lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            Cull Off

            HLSLPROGRAM
            #pragma only_renderers gles gles3 glcore
            #pragma target 2.0

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaUnlit

            #include "UnlitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
