Shader "MERIDIAN/Player Muzzle Flash"
{
    Properties
    {
        [HideInInspector] _Flash ("Phase, Age, Energy, Jet", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend One One, Zero One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Flash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            float3 Flare(float2 uv, float phase, float age)
            {
                float radius = length(uv);
                float angle = atan2(uv.y, uv.x + 0.00001);
                float tongues = pow(0.5 + 0.5 * cos(angle * 6.0 + 0.42 * sin(angle * 3.0 + phase)), 3.4);
                float lengthVariation = 0.55 + 0.19 * sin(angle * 3.0 + phase) + 0.13 * cos(angle * 2.0 - phase);
                float reach = 0.22 + lengthVariation * tongues;
                float grain = sin(angle * 21.0 + radius * 24.0 + phase - age * 5.0) * sin(radius * 47.0 - angle * 5.0);
                float flame = saturate((reach - radius + 0.032 * grain) * 13.0) * saturate(1.0 - radius / reach);
                float core = 1.0 - smoothstep(0.035, 0.24, radius);
                float halo = exp2(-5.2 * radius) * (1.0 - smoothstep(0.37, 0.94, radius));
                return float3(1.0, 0.27, 0.033) * halo * 1.05
                     + lerp(float3(1.0, 0.28, 0.028), float3(1.0, 0.72, 0.27), flame) * flame * 3.35
                     + float3(1.0, 0.89, 0.69) * core * 4.3;
            }

            float3 Jet(float2 uv, float phase, float age)
            {
                float along = uv.y * 0.5 + 0.5;
                float taper = pow(saturate(1.0 - along), 1.45);
                float shear = along * (0.13 * sin(along * 10.0 + phase - age * 5.0) + 0.05 * sin(along * 24.0 + phase));
                float width = 0.65 * taper * (0.88 + 0.12 * sin(along * 32.0 + phase));
                float exhaust = smoothstep(0.02, 0.14, along) * (1.0 - smoothstep(0.55, 0.97, along));
                float edge = abs(uv.x - shear) / max(width, 0.004);
                float outer = (1.0 - smoothstep(0.1, 1.1, edge)) * exhaust;
                float inner = (1.0 - smoothstep(0.02, 0.49, edge)) * exhaust * taper;
                return float3(1.0, 0.28, 0.033) * outer * 1.65 + float3(1.0, 0.78, 0.41) * inner * 2.05;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 flash;
                if (_Flash.w > 0.5)
                    flash = Jet(input.uv, _Flash.x, _Flash.y);
                else
                    flash = Flare(input.uv, _Flash.x, _Flash.y);

                return half4(flash * _Flash.z, 0.0);
            }
            ENDHLSL
        }
    }
}
