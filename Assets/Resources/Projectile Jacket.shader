Shader "MERIDIAN/Projectile Jacket"
{
    Properties { _BaseColor ("Energy", Color) = (1,0.4,0.09,1) }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Vertex { float4 position : POSITION; float3 normal : NORMAL; };
            struct Varyings { float4 clip : SV_POSITION; half3 normal : TEXCOORD0; float3 world : TEXCOORD1; float depth : TEXCOORD2; };
            Varyings Vert(Vertex input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.position.xyz);
                o.clip = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(input.normal);
                o.depth = input.position.z;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half facing = abs(dot(normalize(i.normal), GetWorldSpaceNormalizeViewDir(i.world)));
                half rim = smoothstep(0.05, 0.48, facing);
                half seam = 1 - smoothstep(0.028, 0.078, abs(i.depth + 0.18));
                half3 jacket = lerp(_BaseColor.rgb * 0.26, _BaseColor.rgb, rim);
                half3 core = lerp(jacket, half3(1.45,1.37,1.16), saturate(0.78*seam + pow(facing,12)*0.78));
                return half4(core,1);
            }
            ENDHLSL
        }
    }
}
