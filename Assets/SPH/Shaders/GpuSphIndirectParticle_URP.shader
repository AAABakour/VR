Shader "VR/Paint/GPU SPH Indirect URP"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (0.85, 0.03, 0.015, 0.86)
        _RimStrength ("Rim Highlight", Range(0, 1)) = 0.32
        _Alpha ("Alpha", Range(0, 1)) = 0.86
        _ContainedSoftness ("Contained Softness", Range(0, 1)) = 1.0
        _DensityBoost ("Density Color Boost", Range(0, 1)) = 0.16
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
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4> _RenderParticles;
            float4 _BaseColor;
            float _RimStrength;
            float _Alpha;
            float _ContainedSoftness;
            float _DensityBoost;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float4 particle = _RenderParticles[input.instanceID];
                float radius = max(0.0005, particle.w);
                float3 worldPosition = particle.xyz + input.positionOS * radius;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.normalWS = normalize(input.normalOS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(worldPosition));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                float facing = saturate(dot(n, v));
                float rim = pow(saturate(1.0 - facing), 2.2) * _RimStrength;
                float verticalLight = saturate(n.y * 0.35 + 0.75);
                float liquidSpec = pow(saturate(dot(reflect(-v, n), normalize(float3(0.3, 0.7, 0.2)))), 18.0) * 0.16;
                float3 color = _BaseColor.rgb * verticalLight * (1.0 + _DensityBoost * 0.35) + rim.xxx + liquidSpec.xxx;
                return half4(saturate(color), saturate(_BaseColor.a * _Alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
