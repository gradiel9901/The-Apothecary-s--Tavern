// WorldSpaceTiling.shader
// Tiles textures by real-world size (triplanar projection).
// One material works on any sized wall — no stretching.
//
// Features:
//   - Triplanar world-space UV tiling
//   - Albedo, Normal Map, AO, Roughness
//   - Parallax Occlusion Mapping (POM) for fake depth / displacement
//   - Correct URP PBR lighting with shadow support
//   - Normal-offset shadow bias

Shader "Custom/WorldSpaceTiling"
{
    Properties
    {
        [Header(Albedo)]
        _BaseMap        ("Albedo Map",          2D)         = "white" {}
        _BaseColor      ("Tint Color",          Color)      = (1,1,1,1)

        [Header(Surface Maps)]
        [Normal]
        _NormalMap      ("Normal Map",          2D)         = "bump"  {}
        _NormalStrength ("Normal Strength",     Range(0,3)) = 1.0

        _AOMap          ("Ambient Occlusion",   2D)         = "white" {}
        _AOStrength     ("AO Strength",         Range(0,1)) = 1.0

        _RoughnessMap   ("Roughness Map",       2D)         = "white" {}
        _Roughness      ("Roughness Scale",     Range(0,1)) = 0.8

        [Header(Parallax Displacement)]
        _HeightMap      ("Height Map",          2D)         = "gray"  {}
        _HeightScale    ("Height Scale",        Range(0, 0.08)) = 0.02
        _ParallaxSteps  ("Parallax Steps",      Range(4, 32))   = 16

        [Header(Tiling)]
        _TileSize       ("Tile Size",           Float)      = 1.0
        _BlendSharpness ("Blend Sharpness",     Range(1,16))= 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 300

        // ─────────────────────────────────────────────────────────────────────
        // Main Forward Lit Pass
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Textures ──────────────────────────────────────────────────────
            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_AOMap);        SAMPLER(sampler_AOMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_HeightMap);    SAMPLER(sampler_HeightMap);

            // ── Per-material constants ────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _NormalStrength;
                float  _AOStrength;
                float  _Roughness;
                float  _HeightScale;
                float  _ParallaxSteps;
                float  _TileSize;
                float  _BlendSharpness;
            CBUFFER_END

            // ── Vertex I/O ────────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float4 tangentOS   : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
            };

            // ── Vertex Shader ─────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = nrm.normalWS;
                OUT.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // ── Triplanar blend weights ───────────────────────────────────────
            // Higher _BlendSharpness = cleaner seams between the three projections
            float3 GetWeights(float3 nWS)
            {
                float3 w = pow(abs(nWS), _BlendSharpness);
                return w / (w.x + w.y + w.z + 1e-5);
            }

            // ── Triplanar RGBA sample ─────────────────────────────────────────
            float4 TriSample(TEXTURE2D_PARAM(tex, smp), float3 posWS, float3 w)
            {
                float4 cx = SAMPLE_TEXTURE2D(tex, smp, posWS.zy / _TileSize);
                float4 cy = SAMPLE_TEXTURE2D(tex, smp, posWS.xz / _TileSize);
                float4 cz = SAMPLE_TEXTURE2D(tex, smp, posWS.xy / _TileSize);
                return cx * w.x + cy * w.y + cz * w.z;
            }

            // ── Triplanar normal → world space ────────────────────────────────
            // Uses the "whiteout" blending technique:
            // each face's tangent-space normal is swizzled into its world-space
            // orientation, then blended by the triplanar weights.
            float3 TriNormal(float3 posWS, float3 nWS, float3 w)
            {
                float3 nX = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, posWS.zy / _TileSize), _NormalStrength);
                float3 nY = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, posWS.xz / _TileSize), _NormalStrength);
                float3 nZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, posWS.xy / _TileSize), _NormalStrength);

                // Whiteout blend: add the surface normal into the z channel so the
                // base geometry normal is preserved at face boundaries.
                nX = float3(nX.xy + nWS.zy, abs(nX.z) * nWS.x);
                nY = float3(nY.xy + nWS.xz, abs(nY.z) * nWS.y);
                nZ = float3(nZ.xy + nWS.xy, abs(nZ.z) * nWS.z);

                // Swizzle each face's components back to world XYZ
                return normalize(nX.zyx * w.x + nY.xzy * w.y + nZ.xyz * w.z);
            }

            // ── Parallax Occlusion Mapping ─────────────────────────────────────
            // Ray-marches along the view direction in UV space using the height map.
            // Gives the impression of actual surface depth (grooves between planks etc.)
            // viewDirTS must be in the tangent space of the sampled face.
            float2 POM(float2 uv, float3 viewDirTS)
            {
                int steps = clamp((int)_ParallaxSteps, 4, 32);
                float stepSize = 1.0 / (float)steps;

                // Divide by abs(z) so near-grazing rays travel further in UV
                float2 uvStep = (viewDirTS.xy / (abs(viewDirTS.z) + 0.001)) * _HeightScale * stepSize;

                float layerHeight = 0.0;
                float2 currentUV = uv;
                float  h = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, currentUV, 0).r;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    if (h <= 1.0 - layerHeight) break;
                    layerHeight += stepSize;
                    currentUV   -= uvStep;
                    h = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, currentUV, 0).r;
                }

                // Refine: linear interpolation between the last two steps
                float2 prevUV  = currentUV + uvStep;
                float  hPrev   = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, prevUV, 0).r;
                float  afterD  = h  - (1.0 - layerHeight);
                float  beforeD = hPrev - (1.0 - (layerHeight - stepSize));
                float  frac_t  = afterD / (afterD - beforeD);
                return lerp(currentUV, prevUV, frac_t);
            }

            // ── Fragment Shader ───────────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                float3 nWS      = normalize(IN.normalWS);
                float3 w        = GetWeights(nWS);
                float3 viewWS   = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 posWS    = IN.positionWS;

                // ── Parallax Occlusion Mapping ─────────────────────────────────
                // Pick the dominant face and build its TBN to transform viewWS
                // into that face's tangent space, then run POM there.
                float3 displaced = posWS;

                if (w.z >= w.x && w.z >= w.y)
                {
                    // Front/Back face — XY plane
                    float3 T = float3(sign(nWS.z), 0, 0);
                    float3 B = float3(0, 1, 0);
                    float3 vTS  = float3(dot(viewWS, T), dot(viewWS, B), dot(viewWS, nWS));
                    float2 uv   = posWS.xy / _TileSize;
                    float2 newUV = POM(uv, vTS);
                    float2 delta = newUV - uv;
                    displaced.x += delta.x * _TileSize * sign(nWS.z);
                    displaced.y += delta.y * _TileSize;
                }
                else if (w.y >= w.x)
                {
                    // Top/Bottom face — XZ plane
                    float3 T = float3(1, 0, 0);
                    float3 B = float3(0, 0, sign(nWS.y));
                    float3 vTS  = float3(dot(viewWS, T), dot(viewWS, B), dot(viewWS, nWS));
                    float2 uv   = posWS.xz / _TileSize;
                    float2 newUV = POM(uv, vTS);
                    float2 delta = newUV - uv;
                    displaced.x += delta.x * _TileSize;
                    displaced.z += delta.y * _TileSize * sign(nWS.y);
                }
                else
                {
                    // Side face — ZY plane
                    float3 T = float3(0, 0, sign(nWS.x));
                    float3 B = float3(0, 1, 0);
                    float3 vTS  = float3(dot(viewWS, T), dot(viewWS, B), dot(viewWS, nWS));
                    float2 uv   = posWS.zy / _TileSize;
                    float2 newUV = POM(uv, vTS);
                    float2 delta = newUV - uv;
                    displaced.z += delta.x * _TileSize * sign(nWS.x);
                    displaced.y += delta.y * _TileSize;
                }

                // ── Sample all maps with displaced UVs ─────────────────────────
                float4 albedo    = TriSample(TEXTURE2D_ARGS(_BaseMap,      sampler_BaseMap),      displaced, w) * _BaseColor;
                float  ao        = TriSample(TEXTURE2D_ARGS(_AOMap,        sampler_AOMap),        displaced, w).r;
                float  roughness = TriSample(TEXTURE2D_ARGS(_RoughnessMap, sampler_RoughnessMap), displaced, w).r * _Roughness;

                // World-space normal from normal map — fully drives all lighting
                float3 finalNWS  = TriNormal(displaced, nWS, w);

                ao = lerp(1.0, ao, _AOStrength);
                float smoothness = 1.0 - roughness;

                // Normal-offset shadow position — reduces self-shadowing artifacts
                // on surface details from the normal map
                float3 shadowPos = IN.positionWS + finalNWS * 0.015;

                // ── URP PBR Lighting ───────────────────────────────────────────
                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.normalWS                = finalNWS;          // World-space normal from triplanar normal map
                inputData.viewDirectionWS         = viewWS;
                inputData.shadowCoord             = TransformWorldToShadowCoord(shadowPos);
                inputData.fogCoord                = IN.fogFactor;
                inputData.bakedGI                 = SampleSH(finalNWS) * ao;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask              = SAMPLE_SHADOWMASK(float2(0,0));

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = albedo.rgb;
                surfaceData.alpha       = albedo.a;
                surfaceData.metallic    = 0.0;               // Wood is non-metallic
                surfaceData.smoothness  = smoothness;
                // normalTS = flat because we've already baked normals into inputData.normalWS
                // through world-space triplanar blending. Passing normalTS separately would
                // cause a double-transformation and mess up the lighting.
                surfaceData.normalTS    = float3(0, 0, 1);
                surfaceData.occlusion   = ao;
                surfaceData.emission    = float3(0, 0, 0);
                surfaceData.specular    = float3(0, 0, 0);

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb    = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shadow Caster — correct kernel names from URP's ShadowCasterPass.hlsl
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        // Depth Only — for depth prepass and SSAO
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
