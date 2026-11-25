Shader "Overtime/Deadline Scope"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Charge ("Charge", Range(0, 1)) = 0
        _Extent ("Viewport", Vector) = (1.7778, 1, 0.00185, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float4 _Extent;
            half _Charge;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Line(float value, float radius, float aa)
            {
                return 1.0 - smoothstep(radius - aa, radius + aa, abs(value));
            }

            float Span(float value, float start, float end, float aa)
            {
                return smoothstep(start - aa, start + aa, value) * (1 - smoothstep(end - aa, end + aa, value));
            }

            half4 Over(half4 base, half3 color, half alpha)
            {
                return half4(base.rgb * (1 - alpha) + color * alpha, base.a + alpha * (1 - base.a));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv * 2.0 - 1.0) * _Extent.xy;
                float r = length(p);
                float aa = _Extent.z * 0.64;
                float2 axis = abs(p);
                float aperture = 0.80;
                float outer = smoothstep(aperture - aa, aperture + aa, r);
                float housing = smoothstep(0.96, 0.84, r);
                float reflection = saturate(0.46 + 0.52 * p.y - 0.17 * p.x);
                half3 metal = lerp(half3(0.009, 0.015, 0.020), half3(0.055, 0.087, 0.105), housing * reflection);
                half4 optics = half4(metal * outer, outer);
                float rings = Line(r - 0.811, 0.0025, aa) * 0.75 + Line(r - 0.856, 0.0013, aa) * 0.68 + Line(r - 0.943, 0.0017, aa) * 0.43;
                optics = Over(optics, half3(0.18, 0.31, 0.37), saturate(rings * (0.43 + reflection)));
                float tint = (1 - outer) * smoothstep(0.28, aperture, r);
                optics = Over(optics, half3(0.035, 0.12, 0.15), tint * 0.17);
                optics = Over(optics, half3(0.29, 0.57, 0.60), Line(r - 0.788, 0.0015, aa) * reflection * 0.65);

                float azimuth = atan2(p.y, p.x);
                float scallops = Line(sin(azimuth * 36.0), 0.14, aa * 30.0);
                optics = Over(optics, half3(0.13, 0.23, 0.27), Span(r, 0.872, 0.895, aa) * scallops * 0.57);
                float chargeArc = Span(abs(azimuth + 1.570796), 0.16, 0.64, aa);
                float chargeFill = Span(abs(azimuth + 1.570796), 0.16, 0.16 + 0.48 * _Charge, aa);
                float arc = Line(r - 0.836, 0.004, aa);
                optics = Over(optics, half3(0.18, 0.32, 0.35), arc * chargeArc * 0.55);
                optics = Over(optics, half3(0.37, 0.95, 0.89), arc * chargeArc * chargeFill * step(0.01, _Charge));

                float thin = 0.0014;
                float horizontal = Span(axis.x, 0.021, 0.72, aa);
                float vertical = Span(axis.y, 0.021, 0.72, aa);
                float horizontalTick = Line(axis.x - round(axis.x / 0.08) * 0.08, thin, aa) * Span(axis.x, 0.07, 0.41, aa);
                float verticalTick = Line(axis.y - round(axis.y / 0.08) * 0.08, thin, aa) * Span(-p.y, 0.07, 0.49, aa);
                float marks = max(horizontal * Line(p.y, thin, aa), vertical * Line(p.x, thin, aa));
                marks = max(marks, max(horizontalTick * Line(p.y, 0.011, aa), verticalTick * Line(p.x, 0.015, aa)));
                float posts = max(Span(axis.x, 0.50, 0.73, aa) * Line(p.y, 0.0038, aa), Span(axis.y, 0.49, 0.73, aa) * Line(p.x, 0.0038, aa));
                float shadow = max(horizontal * Line(p.y, 0.0047, aa), vertical * Line(p.x, 0.0047, aa));
                optics = Over(optics, half3(0.005, 0.012, 0.015), max(shadow * 0.9, posts));
                optics = Over(optics, half3(0.64, 0.79, 0.79), marks * 0.84);
                optics = Over(optics, half3(0.005, 0.018, 0.022), 1 - smoothstep(0.0055, 0.0065 + aa, r));
                optics = Over(optics, lerp(half3(0.40, 0.91, 0.85), half3(0.82, 1.0, 0.96), _Charge), 1 - smoothstep(0.0027, 0.0037 + aa, r));
                return optics * input.color.a;
            }
            ENDHLSL
        }
    }
}
