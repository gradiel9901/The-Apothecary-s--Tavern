Shader "Custom/MagicCircle"
{
    Properties
    {
        [HDR] _Color ("Main Color", Color) = (0.0, 1.0, 1.0, 1.0)
        _Speed ("Rotation Speed", Float) = 1.0
        _ParticleSpeed ("Particle Speed", Float) = 0.5
        _RingWidth ("Ring Width", Range(0.01, 0.5)) = 0.05
        _Radius ("Radius", Range(0.1, 0.5)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Blend SrcAlpha One // Additive blending for magic glow
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Speed;
                float _ParticleSpeed;
                float _RingWidth;
                float _Radius;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            // Simple pseudo-random function
            float random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // Simple noise function
            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            float DrawRing(float2 uv, float radius, float width)
            {
                float d = length(uv);
                return smoothstep(width, 0.0, abs(d - radius));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * 2.0 - 1.0; // Center UV at (0,0)
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);

                // --- Rings ---
                // Outer Ring
                float ring1 = DrawRing(uv, _Radius, _RingWidth);
                
                // Inner Ring (Rotate in opposite direction)
                float innerAngle = angle + _Time.y * _Speed;
                float2 innerUV = float2(cos(innerAngle) * dist, sin(innerAngle) * dist);
                // Creating a pattern for the inner ring
                float pattern = sin(innerAngle * 10.0) * 0.5 + 0.5;
                float ring2 = DrawRing(uv, _Radius * 0.7, _RingWidth * 0.5) * pattern;

                // --- Particles ---
                // Polar coordinates for upward/outward movement simulation
                float2 polarUV = float2(angle / (2.0 * PI), dist);
                
                // Animate particles moving outwards and rotating
                float2 particleUV = polarUV;
                particleUV.y -= _Time.y * _ParticleSpeed; // Move outwards
                particleUV.x += _Time.y * _ParticleSpeed * 0.2; // Rotate slightly

                float particleNoise = noise(particleUV * 20.0);
                float particleMask = smoothstep(0.6, 0.8, particleNoise); // Threshold noise to create separate sparkles
                
                // Fade particles at center and edge
                particleMask *= smoothstep(0.0, 0.2, dist) * smoothstep(0.5, 0.3, dist);

                // Combine
                float alpha = ring1 + ring2 + particleMask;
                float3 finalColor = _Color.rgb * alpha * 2.0; // * 2.0 for emission intensity

                return float4(finalColor, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
