Shader "SwingingPaintBucket/SPH/GPU Particle Unlit"
{
    Properties
    {
        _ParticleColor("Particle Color", Color) = (0.18, 0.65, 1, 0.75)
        _ParticleSize("Particle Size", Float) = 0.045
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct ParticleState
            {
                float3 position;
                float3 velocity;
                float density;
                float pressure;
            };

            StructuredBuffer<ParticleState> _Particles;
            int _RenderStride;
            float _ParticleSize;
            float4 _ParticleColor;
            float4x4 _BoxLocalToWorld;
            float3 _CameraRight;
            float3 _CameraUp;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float speed : TEXCOORD1;
            };

            Varyings vert(uint vertexId : SV_VertexID)
            {
                uint particleSlot = vertexId / 6;
                uint cornerId = vertexId % 6;
                uint particleIndex = particleSlot * max(1, _RenderStride);

                float2 corners[6] =
                {
                    float2(-1.0, -1.0),
                    float2( 1.0, -1.0),
                    float2( 1.0,  1.0),
                    float2(-1.0, -1.0),
                    float2( 1.0,  1.0),
                    float2(-1.0,  1.0)
                };

                ParticleState particle = _Particles[particleIndex];
                float3 worldPosition = mul(_BoxLocalToWorld, float4(particle.position, 1.0)).xyz;
                float2 corner = corners[cornerId];
                worldPosition += (_CameraRight * corner.x + _CameraUp * corner.y) * _ParticleSize;

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = corner * 0.5 + 0.5;
                output.speed = length(particle.velocity);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float d = dot(centered, centered);
                clip(1.0 - d);
                float rim = saturate(1.0 - d);
                float3 color = lerp(_ParticleColor.rgb, float3(0.85, 0.95, 1.0), saturate(input.speed * 0.08));
                return half4(color, _ParticleColor.a * rim);
            }
            ENDHLSL
        }
    }
}
