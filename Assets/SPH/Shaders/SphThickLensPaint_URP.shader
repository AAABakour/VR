Shader "VR/Paint/Thick Lens Paint URP"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (0.42, 0.0025, 0.0015, 0.98)
        _Alpha ("Alpha", Range(0, 1)) = 0.96
        _GlossBoost ("Wet Gloss", Range(0, 2)) = 1.35
        _HeightGloss ("Raised Height Gloss", Range(0, 2)) = 1.2
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
            Name "ThickWetPaintLens"
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
                float _HeightGloss;
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
                float height01 : TEXCOORD4;
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
                output.height01 = saturate(input.positionOS.y * 26.0);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radial = saturate(length(centered));
                float lens = saturate(1.0 - radial);
                float rim = exp(-pow((radial - 0.72) / 0.11, 2.0));

                float3 n = normalize(input.normalWS + float3(centered.x, lens * 0.25, centered.y) * 0.18);
                float3 v = normalize(input.viewDirWS);
                Light light = GetMainLight();
                float3 l = normalize(light.direction);
                float ndl = saturate(dot(n, l)) * 0.34 + 0.66;
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), 96.0) * (0.30 + input.height01 * 0.32) * _GlossBoost;
                float fresnel = pow(saturate(1.0 - dot(n, v)), 3.2) * 0.22 * _HeightGloss;

                float pigment = 1.0 + sin(input.uv.x * 37.0 + input.uv.y * 23.0 + _Time.y * 0.45) * 0.018;
                float3 color = _BaseColor.rgb * ndl * pigment;
                color *= lerp(0.88, 1.10, lens);
                color += rim.xxx * 0.045 * _HeightGloss;
                color += spec.xxx + fresnel.xxx;

                float alpha = _BaseColor.a * _Alpha * saturate(0.76 + lens * 0.28 + rim * 0.10);
                return half4(saturate(color), alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
