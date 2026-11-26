Shader "MERIDIAN/Ballistic Trace"
{
    Properties
    {
        _Tint ("Flight Color", Vector) = (1,0.75,0.016,1)
        _Wake ("Temporal Wake", Range(0, 1)) = 0
        [Toggle(_PROJECTILE_HALO)] _Halo ("Projectile Halo", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _PROJECTILE_HALO
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _Wake;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half fade : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float grainPhase : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                #if defined(_PROJECTILE_HALO)
                    float4 center = TransformObjectToHClip(float3(0, 0, 0));
                    float worldDiameter = length(TransformObjectToWorldDir(float3(1, 0, 0), false));
                    float screenHeight = _ScaledScreenParams.y;
                    float diameterPx = worldDiameter * abs(GetViewToHClipMatrix()._m11) * screenHeight / (2 * max(0.01, abs(center.w)));
                    // Keep head-on shots readable at distance, with a cap near the camera.
                    diameterPx = clamp(diameterPx, 16, max(48, screenHeight * 0.082));
                    center.xy += (input.uv * 2 - 1) * diameterPx * center.w / _ScaledScreenParams.xy;
                    output.position = center;
                    output.fade = 1;
                    output.grainPhase = 0;
                #else
                    float3 world = TransformObjectToWorld(input.position.xyz);
                    output.position = TransformWorldToHClip(world);
                    output.grainPhase = dot(world, float3(7.2, 11.3, 4.7));
                    output.fade = input.color.a;
                #endif
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                #if defined(_PROJECTILE_HALO)
                    float radius = length(input.uv * 2 - 1);
                    half inner = 1 - smoothstep(0.14, 0.36, radius);
                    half glow = 1 - smoothstep(0.12, 1, radius);
                    half opacity = saturate(inner * 0.91 + glow * glow * 0.36);
                    half energy = inner * 1.65 + glow * glow * 0.66;
                    return half4(_Tint.rgb * (energy * _Tint.a), opacity);
                #else
                    float tail = saturate(input.uv.x);
                    float axis = input.uv.y * 2 - 1;
                    float tapered = abs(axis) / lerp(1, 0.34, tail);
                    half core = 1 - smoothstep(0.13, 0.54, tapered);
                    half glow = 1 - smoothstep(0.12, 1, tapered);
                    half fade = input.fade * smoothstep(0, 0.045, tail);

                    // The soft wake has irregular edges, with the same luminous core in both modes.
                    float phase = input.grainPhase + tail * 13;
                    float drift = (0.07 + tail * 0.23) * sin(phase);
                    float filament = 1 - smoothstep(0.12, 0.89, abs(axis - drift));
                    float broken = 0.68 + 0.22 * sin(phase * 1.7) + 0.10 * sin(phase * 3.4 + 1);
                    half wake = filament * broken * _Wake;
                    half opacity = saturate((core * 0.94 + glow * 0.32 + wake * 0.19) * fade);
                    half energy = (core * 2.2 + glow * 0.61 + wake * 0.47) * fade;
                    return half4(_Tint.rgb * (energy * _Tint.a), opacity);
                #endif
            }
            ENDHLSL
        }
    }
}
