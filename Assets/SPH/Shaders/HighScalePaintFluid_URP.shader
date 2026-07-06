Shader "VR/Paint/High Scale SPH Fluid Preview"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (0.95, 0.02, 0.0, 0.86)
        _DepthColor ("Depth Color", Color) = (0.28, 0.0, 0.0, 1)
        _Smoothness ("Wet Smoothness", Range(0, 1)) = 0.92
        _FresnelPower ("Rim Fresnel", Range(0.5, 8)) = 3.2
        _ThicknessAlpha ("Thickness Alpha", Range(0, 2)) = 0.85
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
            Name "ForwardHighScalePaint"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DepthColor;
                half _Smoothness;
                half _FresnelPower;
                half _ThicknessAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float depth01 : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.depth01 = saturate(input.positionOS.y * 0.5 + 0.5);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half depth = saturate(input.depth01);
                half4 color = lerp(_DepthColor, _BaseColor, depth);
                color.rgb += fresnel * _Smoothness * 0.35h;
                color.a = saturate(_BaseColor.a * (0.55h + depth * _ThicknessAlpha) + fresnel * 0.18h);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
