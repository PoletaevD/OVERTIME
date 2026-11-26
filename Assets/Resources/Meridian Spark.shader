Shader "MERIDIAN/Discharge"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv * 2 - 1;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half radius = dot(input.uv, input.uv);
                half core = saturate(1 - radius * 2);
                return half4(lerp(input.color.rgb, 1, core * 0.6), input.color.a * saturate(1 - radius));
            }
            ENDHLSL
        }
    }
}
