Shader "Volume/VolumeDVR_URP_Quest"
{
    Properties
    {
        // Données volume
        _VolumeTexLabels ("Labels (R8)", 3D) = "" {}
        _VolumeTexWeights("Weights (RHalf)", 3D) = "" {}

        // Tables 2D
        _TFTex        ("Transfer Function (2D)", 2D) = "white" {}
        _LabelCtrlTex ("Label Ctrl (RGBA)",     2D) = "white" {}

        // Contrôles
        _HasWeights ("Has Weights", Int) = 1
        _IsLabelMap ("Is LabelMap", Int) = 1

        // Qualité/perf
        _StepCount ("Ray Steps", Range(32, 160)) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back

        Pass
        {
            Name "Raymarch"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   vert
            #pragma fragment frag

            // XR & instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ _STEREO_MULTIVIEW_ON _STEREO_INSTANCING_ON
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            // Keywords optionnels (si tu veux piloter par material.EnableKeyword)
            #pragma shader_feature_local _ _DEBUG_MODE_LABELS _DEBUG_MODE_WEIGHTS _DEBUG_MODE_UVW
            #pragma shader_feature_local _ BYPASS_TF_ON
            #pragma shader_feature_local _ HAS_WEIGHTS_ON

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

            // ===== Textures & samplers =====
            TEXTURE3D(_VolumeTexLabels);
            SAMPLER(sampler_VolumeTexLabels);

            TEXTURE3D(_VolumeTexWeights);
            SAMPLER(sampler_VolumeTexWeights);

            TEXTURE2D(_TFTex);
            SAMPLER(sampler_TFTex);

            TEXTURE2D(_LabelCtrlTex);
            SAMPLER(sampler_LabelCtrlTex);

            // ===== Uniforms =====
            int   _HasWeights;
            int   _IsLabelMap;
            float _StepCount;

            // ===== Structs =====
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 posWS       : TEXCOORD2;     // position monde du fragment
                float3 rayOriginWS : TEXCOORD0;     // non utilisé après patch (conservé pour compat)
                float3 rayDirWS    : TEXCOORD1;     // non utilisé après patch (conservé pour compat)
                UNITY_VERTEX_OUTPUT_STEREO
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            inline float safe1(float v){ return (isnan(v)||isinf(v))?0.0:v; }
            inline float3 safe3(float3 v){ return float3(safe1(v.x),safe1(v.y),safe1(v.z)); }
            inline float4 safe4(float4 v){ return float4(safe1(v.x),safe1(v.y),safe1(v.z),safe1(v.w)); }

            // ===== Helpers =====

            // OBJET -> UVW [0..1] (cube Unity ~ [-0.5..+0.5]^3)
            inline float3 ObjectToUVW(float3 posOS)
            {
                return posOS + 0.5;
            }

            // Labels R8 (UNorm) -> entier 0..255
            inline int SampleLabelInt(float3 uvw)
            {
                float r = SAMPLE_TEXTURE3D(_VolumeTexLabels, sampler_VolumeTexLabels, uvw).r;
                return (int)round(saturate(r) * 255.0);
            }

            // Weight RHalf -> [0..1]
            inline half SampleWeight(float3 uvw)
            {
                #if defined(HAS_WEIGHTS_ON)
                    return (half)SAMPLE_TEXTURE3D(_VolumeTexWeights, sampler_VolumeTexWeights, uvw).r;
                #else
                    return half(1.0);
                #endif
            }

            // TF 1D packée en 2D (1 x 256) + contrôles RGBA
            inline float4 TFColorForLabel(int lbl)
            {
                float u = (lbl + 0.5) / 256.0;
                float2 uv = float2(u, 0.5);

                float4 col  = SAMPLE_TEXTURE2D_LOD(_TFTex,        sampler_TFTex,        uv, 0.0);
float4 ctrl = SAMPLE_TEXTURE2D_LOD(_LabelCtrlTex, sampler_LabelCtrlTex, uv, 0.0);


                col.rgb *= ctrl.rgb;
                col.a   *= ctrl.a;
                return col;
            }

            // Intersection ray/cube en ESPACE OBJET : [-0.5..+0.5]^3
            bool RayBoxIntersectOS(
                float3 roWS, float3 rdWS,
                out float tEnter, out float tExit,
                out float3 roOS, out float3 rdOS)
            {
                roOS = TransformWorldToObject(roWS);
                float3 pFarOS = TransformWorldToObject(roWS + rdWS * 1000.0);
                rdOS = normalize(pFarOS - roOS);

                const float3 boxMin = float3(-0.5, -0.5, -0.5);
                const float3 boxMax = float3(+0.5, +0.5, +0.5);

                // éviter divisions par zéro
                float3 dir = rdOS;
                dir = max(abs(dir), 1e-6) * sign(dir);

                float3 tmin = (boxMin - roOS) / dir;
                float3 tmax = (boxMax - roOS) / dir;

                float3 t1 = min(tmin, tmax);
                float3 t2 = max(tmin, tmax);

                tEnter = max(t1.x, max(t1.y, t1.z));
                tExit  = min(t2.x, min(t2.y, t2.z));

                return tExit > max(tEnter, 0.0);
            }

            // ===== Vertex & Fragment =====

            Varyings vert (Attributes v)
            {
                Varyings o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS   = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS   = TransformWorldToHClip(posWS);
                o.posWS        = posWS;

                // (gardés pour compat, mais non utilisés ensuite)
                float3 camPosWS = GetCameraPositionWS();
                o.rayOriginWS   = camPosWS;
                o.rayDirWS      = normalize(posWS - camPosWS);

                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Rayon exact par pixel (pas interpolé)
                float3 roWS = GetCameraPositionWS();
                float3 rdWS = normalize(i.posWS - roWS);

                float t0, t1; float3 roOS, rdOS;
                if (!RayBoxIntersectOS(roWS, rdWS, t0, t1, roOS, rdOS))
                    return float4(0,0,0,0);

                // Marching
                half stepCount = max(_StepCount, 32.0h);
                float tStart    = max(t0, 0.0);
                float tStep     = (t1 - tStart) / stepCount;

                half4 acc = half4(0.0h,0.0h,0.0h,0.0h);
                float  t   = tStart;

                [loop]
                for (int s = 0; s < 512; s++)
                {
                    if (s >= (int)stepCount) break;

                    float3 posOS = roOS + rdOS * t;
                    float3 uvw   = ObjectToUVW(posOS);

                    int  lbl = SampleLabelInt(uvw);
                    half w   = SampleWeight(uvw);

                    if (any(uvw < 0) || any(uvw > 1)) { t += tStep; continue; }

                    if (w < 0.01 || lbl == 0) 
                    {
                        t += tStep;
                        continue;
                    }

                    #if defined(_DEBUG_MODE_UVW)
                        return float4(uvw, 1);
                    #elif defined(_DEBUG_MODE_WEIGHTS)
                        { half wdbg = SampleWeight(uvw); return float4(wdbg, wdbg, wdbg, 1); }
                    #elif defined(_DEBUG_MODE_LABELS)
                        { int ldbg = SampleLabelInt(uvw); float v=(float)ldbg/255.0h; return float4(v,v,v,1); }
                    #endif

                    half4 col;
                    #if defined(BYPASS_TF_ON)
                        { float v = (half)lbl / 255.0h; col = half4(v, v, v, w); }
                    #else
                        col   = TFColorForLabel(lbl);
                        col.a = saturate(col.a) * w;
                        col.rgb *= col.a; // premul
                    #endif

                    // Over
                    acc.rgb += (1.0h - acc.a) * col.rgb;
                    acc.a   += (1.0h - acc.a) * col.a;

                    if (acc.a > 0.995h) break;
                    t += tStep;
                }

                return (float4)acc;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
