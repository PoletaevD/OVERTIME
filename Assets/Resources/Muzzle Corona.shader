Shader "MERIDIAN/Muzzle Corona"
{
    Properties
    {
        _Tint ("Discharge", Color) = (0.2,0.89,1,1)
        _Profile ("Weapon Profile", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _Profile;
            CBUFFER_END
            struct Vertex { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Vertex v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.pos.xyz);
                o.uv = v.uv * 2 - 1;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float r = length(i.uv);
                float a = atan2(i.uv.y, i.uv.x);
                float beams = _Profile < 0.5 ? 3 : _Profile < 1.5 ? 7 : _Profile < 2.5 ? 4 : 5;
                float spokes = pow(saturate(0.5 + 0.5 * cos(a * beams)), 8);
                float teeth = saturate(1 - r / (0.14 + 0.77 * spokes));
                float corona = exp(-6 * r) + teeth * 0.65;
                float ring = saturate(1 - abs(r - 0.48) * 42) * step(1.5, _Profile) * (1 - step(2.5, _Profile));
                float alpha = saturate(corona + ring * 0.44) * saturate((1-r)*9);
                return half4(lerp(_Tint.rgb, half3(1,1,0.91), saturate(1-r*4)), alpha);
            }
            ENDHLSL
        }
    }
}
