Shader "VR/Paint/Phase05C Instanced Droplet URP"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (0.42, 0.0025, 0.0015, 0.96)
        _Alpha ("Alpha", Range(0, 1)) = 0.82
        _GlossBoost ("Gloss Boost", Range(0, 2)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Phase05CInstancedForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Alpha;
                float _GlossBoost;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.normalWS = normalize(nrm.normalWS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                Light light = GetMainLight();
                float3 l = normalize(light.direction);
                float ndl = saturate(dot(n, l)) * 0.35 + 0.65;
                float rim = pow(saturate(1.0 - dot(n, v)), 2.6) * 0.23 * _GlossBoost;
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), 54.0) * 0.30 * _GlossBoost;
                float3 color = saturate(_BaseColor.rgb * ndl + rim.xxx + spec.xxx);
                return half4(color, saturate(_BaseColor.a * _Alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
