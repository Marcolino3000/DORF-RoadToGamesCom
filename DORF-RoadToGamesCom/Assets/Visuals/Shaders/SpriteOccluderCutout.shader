// Same as UnlitSprite.shader, plus a hole around the character so foreground sprites never hide
// her completely. Driven by CharacterCutout.cs.
//
// Unlike UnlitSprite.shader this one ALPHA BLENDS and does not write depth. It has to: with the
// One Zero blend the hole could only ever be a hard clip(), and the blurred edge needs real
// transparency. The sprite body itself is kept fully solid below so it still looks the same.
Shader "Coming of Dorf/Sprite Occluder Cutout"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutout", Range(0.0, 1.0)) = 0.345

        [Header(Character Cutout)]
        [NoScaleOffset] _CutoutMask("Hole Shape (Alpha)", 2D) = "white" {}
        [NoScaleOffset] _CutoutEdgeTex("Edge Break Up (R)", 2D) = "grey" {}
        _CutoutEdgeTiling("Edge Break Up Tiling", Float) = 6
        _CutoutEdgeStrength("Edge Break Up Strength", Range(0.0, 1.0)) = 0
        _CutoutSoftness("Edge Softness (blur)", Range(0.01, 1.0)) = 0.6
        _CutoutMinAlpha("Alpha inside the hole", Range(0.0, 1.0)) = 0.0
        _CutoutRimWidth("Rim Width", Range(0.0, 1.0)) = 0.18
        _CutoutRimColor("Rim Color (A = strength)", Color) = (0.16, 0.13, 0.11, 0.85)
        _CutoutDepthFade("Depth Fade (world units)", Float) = 1.0
        _CutoutMaxDepthSlope("Max Depth Slope (skips ground planes)", Float) = 0.005

        // BlendMode
        [HideInInspector] _Surface("__surface", Float) = 1.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("Src", Float) = 5.0
        [HideInInspector] _DstBlend("Dst", Float) = 10.0
        [HideInInspector] _ZWrite("ZWrite", Float) = 0.0

        // Editmode props
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
        [HideInInspector] _SampleGI("SampleGI", float) = 0.0 // needed from bakedlit
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "ShaderModel" = "4.5"
        }
        LOD 100

        Blend [_SrcBlend][_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "SpriteOccluderCutout.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 vertex : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.color = input.color;
                output.vertex = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.screenPos = vertexInput.positionNDC;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half2 uv = input.uv;
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half3 color = texColor.rgb * input.color.rgb;
                half alpha = texColor.a * input.color.a;
                clip(alpha - _Cutoff);

                // Whatever survived the cutoff used to be fully opaque under the One Zero blend.
                // Keep it that way, so alpha blending only ever shows up in the hole itself.
                alpha = input.color.a;

                half rim;
                half hole = CharacterHole(input.screenPos.xy / max(input.screenPos.w, 1e-5), input.positionWS.z, rim);
                alpha *= lerp(1.0, _CutoutMinAlpha, hole);
                color = lerp(color, _CutoutRimColor.rgb, rim * _CutoutRimColor.a);
                clip(alpha - 0.002);

                #ifdef _ALPHAPREMULTIPLY_ON
                color *= alpha;
                #endif

                color = MixFog(color, input.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "ShaderModel" = "2.0"
        }
        LOD 100

        Blend [_SrcBlend][_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma only_renderers gles gles3 glcore
            #pragma target 2.0

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "SpriteOccluderCutout.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 vertex : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.color = input.color;
                output.vertex = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.screenPos = vertexInput.positionNDC;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half2 uv = input.uv;
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half3 color = texColor.rgb * input.color.rgb;
                half alpha = texColor.a * input.color.a;
                clip(alpha - _Cutoff);

                // Whatever survived the cutoff used to be fully opaque under the One Zero blend.
                // Keep it that way, so alpha blending only ever shows up in the hole itself.
                alpha = input.color.a;

                half rim;
                half hole = CharacterHole(input.screenPos.xy / max(input.screenPos.w, 1e-5), input.positionWS.z, rim);
                alpha *= lerp(1.0, _CutoutMinAlpha, hole);
                color = lerp(color, _CutoutRimColor.rgb, rim * _CutoutRimColor.a);
                clip(alpha - 0.002);

                #ifdef _ALPHAPREMULTIPLY_ON
                color *= alpha;
                #endif

                color = MixFog(color, input.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
