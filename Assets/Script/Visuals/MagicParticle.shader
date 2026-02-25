Shader "Custom/MagicParticle"
{
    Properties
    {
        [HDR] _Color ("Main Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha One // Additive
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * 2.0 - 1.0;
                float dist = length(uv);

                // Simple soft glow circle
                float opacity = smoothstep(1.0, 0.0, dist);
                
                // Add a "core" for brightness
                opacity += smoothstep(0.4, 0.0, dist) * 2.0;

                // Combine strict particle color with vertex color (from particle system)
                float4 finalColor = _Color * input.color;
                finalColor.a *= opacity;

                return float4(finalColor.rgb * finalColor.a, finalColor.a);
            }
            ENDHLSL
        }
    }
}
