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
        _StepCount ("Ray Steps", Range(32, 160)) = 80
        _LargeStepScale ("Large Step Scale", Range(2, 32)) = 10.0

        [Header(Lighting)]
        _LightDir ("Light Dir (world)", Vector) = (0, 1, 1, 0)
        _LightIntensity ("Light Intensity", Range(0, 5)) = 3.0

        // Lumière 2 (secondaire/rim)
        _LightDir2 ("Light 2 Dir (world)", Vector) = (0, -1, -1, 0)
        _LightIntensity2 ("Light 2 Intensity", Range(0, 5)) = 1.5

        _LightIntensity3 ("Light 3 (Headlight) Intensity", Range(0, 5)) = 5

        _Ambient ("Ambient", Range(0, 1)) = 0.55
        _EpsNormal ("Normal Epsilon", Float) = 0.002

        _Brightness   ("Brightness", Range(0,3)) = 1.2
        _AlphaScale   ("Alpha Scale (global)", Range(0,2)) = 1.0
        _AlphaGamma   ("Alpha Gamma (shape)", Range(0.2,3)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

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
            float _LargeStepScale;

            float _ScaleMax;       // NEW : max scale monde (x/y/z)
            float _DensityComp;    // NEW : 1/_ScaleMax par défaut

            float _Brightness;
            float _AlphaScale;
            float _AlphaGamma;

            float4 _LightDir;
            float  _LightIntensity;

            float4 _LightDir2;
            float  _LightIntensity2;

            float  _LightIntensity3;

            float  _Ambient;
            float  _EpsNormal;

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

            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898,78.233))) * 43758.5453);
            }

            inline float3 EstimateNormalFromLabels(float3 pObj)
            {
                float eps = _EpsNormal; // Utilise la propriété pour le 'pas'
                
                float3 uvw_xp = (pObj + float3(+eps,0,0)) + 0.5;
                float3 uvw_xm = (pObj + float3(-eps,0,0)) + 0.5;
                float3 uvw_yp = (pObj + float3(0,+eps,0)) + 0.5;
                float3 uvw_ym = (pObj + float3(0,-eps,0)) + 0.5;
                float3 uvw_zp = (pObj + float3(0,0,+eps)) + 0.5;
                float3 uvw_zm = (pObj + float3(0,0,-eps)) + 0.5;

                // Goûter le volume (valeur de label, 0..1)
                float vxp = SAMPLE_TEXTURE3D(_VolumeTexLabels, sampler_VolumeTexLabels, uvw_xp).r;
                float vxm = SAMPLE_TEXTURE3D(_VolumeTexLabels, sampler_VolumeTexLabels, uvw_xm).r;
                float vyp = SAMPLE_TEXTURE3D(_VolumeTexLabels, sampler_VolumeTexLabels, uvw_yp).r;
                float vym = SAMPLE_TEXTURE3D(_VolumeTexLabels, sampler_VolumeTexLabels, uvw_ym).r;
                float vzp = SAMPLE_TEXTURE3D(_VolumeTexLabels, sampler_VolumeTexLabels, uvw_zp).r;
                float vzm = SAMPLE_TEXTURE3D(_VolumeTexLabels, sampler_VolumeTexLabels, uvw_zm).r;

                // Calculer le gradient (différences centrales)
                float3 grad = float3(
                    vxp - vxm,
                    vyp - vym,
                    vzp - vzm
                );

                // Normaliser (diviser par la longueur)
                float len2 = max(dot(grad, grad), 1e-6);
                return grad / sqrt(len2);
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
                float tStart    = max(t0, 0.0) + 1e-4;

                float tStepSmall = (t1 - tStart) / stepCount;
                float tStepLarge = tStepSmall * _LargeStepScale;

                half4 acc = half4(0.0h,0.0h,0.0h,0.0h);

                float  t   = tStart;

                // jittering - at low sampling 32 - 96 it makes noises on the volume
                // when combined with lambert lighting
                // consume 10 - 20 fps
                // TODO : find a technique to reduce noises at low sampling
                t += rand(i.positionCS.xy) * tStepLarge;
                float3 Lobj1 = normalize(TransformWorldToObjectDir(_LightDir.xyz));
                float3 Lobj2 = normalize(TransformWorldToObjectDir(_LightDir2.xyz));
                float3 Lobj3 = -rdOS;

                int s = 0;
                [loop]
                while (t < t1 && acc.a < 0.995h && s < 512)
                {
                    s++;

                    float3 posOS = roOS + rdOS * t;
                    float3 uvw   = ObjectToUVW(posOS);

                    if (any(uvw < 0) || any(uvw > 1)) { 
                        t += tStepLarge;
                        continue; 
                    }

                    int  lbl = SampleLabelInt(uvw);
                    half w   = SampleWeight(uvw);

                    if (w < 0.01 || lbl == 0)
                    {
                        t += tStepLarge;
                        continue;
                    }

                    #if defined(_DEBUG_MODE_UVW)
                        return float4(uvw, 1);
                    #elif defined(_DEBUG_MODE_WEIGHTS)
                        { half wdbg = SampleWeight(uvw); return float4(wdbg, wdbg, wdbg, 1); }
                    #elif defined(_DEBUG_MODE_LABELS)
                        { int ldbg = SampleLabelInt(uvw); float v=(float)ldbg/255.0h; return float4(v,v,v,1); }
                    #endif

                    half4 col_base; // NOUVEAU: Renommé en "col_base"
                    #if defined(BYPASS_TF_ON)
                        { float v = (half)lbl / 255.0h; col_base = half4(v, v, v, w); }
                    #else
                        col_base = TFColorForLabel(lbl);
                    #endif

                    // NOUVEAU: Calcule l'alpha pour ce pas (aSample)
                    half aSample = pow(saturate(col_base.a), _AlphaGamma) * w * _AlphaScale;
                    aSample *= _DensityComp; // Applique la compensation de densité

                    // lambert lighting - consume 10 - 20 fps
                    if (aSample > 0.001h)
                    {
                        // 1. Estimer la normale
                        float3 N = EstimateNormalFromLabels(posOS);

                        // 2. Calculer l'éclairage de Lambert pour les DEUX lumières
                        float lambert1 = saturate(dot(N, Lobj1));
                        float lambert2 = saturate(dot(N, Lobj2));
                        float lambert3 = saturate(dot(N, Lobj3)); // <-- AJOUTÉ

                        // 3. Accumuler l'éclairage total
                        float lighting = _Ambient 
                                       + (_LightIntensity * lambert1) 
                                       + (_LightIntensity2 * lambert2)
                                       + (_LightIntensity3 * lambert3);

                        // 4. Appliquer l'éclairage à la couleur de base
                        float3 litColor = col_base.rgb * lighting;

                        // 5. Prémultiplier l'alpha (couleur * opacité)
                        litColor *= aSample;

                        // 6. Accumuler (Over)
                        acc.rgb += (1.0h - acc.a) * litColor;
                        acc.a   += (1.0h - acc.a) * aSample;
                    }

                    t += tStepSmall;
                }

                acc.rgb *= _Brightness;

                return (float4)acc;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
