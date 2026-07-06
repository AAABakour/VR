Shader "VR/Paint/Hero Paint Surface URP"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (0.55, 0.006, 0.002, 0.96)
        _Alpha ("Alpha", Range(0, 1)) = 0.92
        _GlossBoost ("Gloss Boost", Range(0, 2)) = 1.0
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
            Name "HeroForward"
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
                float _GlossBoost;
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
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(nrm.normalWS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                Light light = GetMainLight();
                float3 l = normalize(light.direction);
                float ndl = saturate(dot(n, l)) * 0.40 + 0.60;
                float fresnel = pow(saturate(1.0 - dot(n, v)), 3.0);
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), 72.0) * 0.38 * _GlossBoost;
                float wetBand = 0.5 + 0.5 * sin(input.uv.x * 26.0 - _Time.y * 2.4);
                float3 color = _BaseColor.rgb * ndl;
                color += fresnel.xxx * 0.22 * _GlossBoost;
                color += spec.xxx;
                color *= 1.0 + wetBand * 0.025;
                return half4(saturate(color), saturate(_BaseColor.a * _Alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
