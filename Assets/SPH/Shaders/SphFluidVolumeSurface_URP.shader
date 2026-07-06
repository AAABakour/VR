Shader "VR/Paint/SPH Volume Surface URP"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (0.88, 0.02, 0.01, 0.96)
        _Alpha ("Alpha", Range(0, 1)) = 0.96
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.72
        _DepthDarkening ("Depth Darkening", Range(0, 1)) = 0.44
        _SloshIntensity ("Slosh Intensity", Range(0, 1)) = 0.0
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Alpha;
                float _HighlightStrength;
                float _DepthDarkening;
                float _SloshIntensity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                Light mainLight = GetMainLight();
                float ndl = saturate(dot(n, normalize(mainLight.direction))) * 0.32 + 0.68;
                float fresnel = pow(saturate(1.0 - dot(n, v)), 2.4);
                float center = saturate(1.0 - length(input.uv - 0.5) * 1.75);
                float depth = lerp(1.0 - _DepthDarkening, 1.0, center);
                float waveSparkle = sin((input.uv.x + input.uv.y) * 48.0 + _Time.y * 4.0) * 0.015 * _SloshIntensity;
                float3 color = _BaseColor.rgb * ndl * depth;
                float3 halfDir = normalize(normalize(mainLight.direction) + v);
                float spec = pow(saturate(dot(n, halfDir)), 80.0) * 0.22 * _HighlightStrength;
                color += fresnel.xxx * _HighlightStrength * 0.28 + spec.xxx;
                color += waveSparkle.xxx;
                return half4(saturate(color), saturate(_BaseColor.a * _Alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
