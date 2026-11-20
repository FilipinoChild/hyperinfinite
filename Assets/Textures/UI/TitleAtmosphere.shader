Shader "HyperInfinite/Title Atmosphere"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original Title Sprite", 2D) = "white" {}
        _CleanTex ("Clean Background", 2D) = "black" {}
        _MaskTex ("Smoke / Stars / Lamp", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SmokeSpeed ("Smoke Speed", Range(0,2)) = 0.65
        _SmokeMotion ("Smoke Opacity", Range(0,1)) = 1
        _StarMotion ("Star Twinkle", Range(0,1)) = 1
        _LampFlicker ("Damaged Light", Range(0,1)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "TitleAtmosphere"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 localPosition : TEXCOORD1; };
            sampler2D _MainTex, _CleanTex, _MaskTex;
            float4 _Color, _ClipRect;
            float _SmokeSpeed, _SmokeMotion, _StarMotion, _LampFlicker;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.localPosition = input.vertex;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            // Smooth-edged outages, rather than an intense full-screen strobe.
            float Outage(float time, float start, float end)
            {
                return smoothstep(start, start + 0.045, time) * (1.0 - smoothstep(end - 0.06, end, time));
            }

            // A puff uses the original artwork and mask, rather than a new art style.
            // Each is born at the vent, grows as it rises, then leaves the canvas.
            float4 RisingPuff(float2 uv, float travel, float variant)
            {
                float height = travel * 0.77;
                float path = saturate(height / 0.60);
                float2 center = float2(0.740 + 0.190 * path + 0.025 * sin(path * 3.14159265),
                                       0.426 + height);
                float growth = smoothstep(0.0, 0.50, travel);
                float2 size = lerp(float2(0.061, 0.105), float2(0.112, 0.185), growth);
                size *= lerp(0.90, 1.10, variant);
                float2 p = (uv - center) / size + 0.5;
                if (p.x <= 0.0 || p.x >= 1.0 || p.y <= 0.0 || p.y >= 1.0)
                    return float4(0, 0, 0, 0);

                // Sample just the upper smoke cloud. Alternate its orientation for variety.
                p.x = lerp(p.x, 1.0 - p.x, step(0.5, variant));
                float2 sampleUV = lerp(float2(0.890, 0.769), float2(0.988, 0.925), p);
                float alpha = tex2Dlod(_MaskTex, float4(sampleUV, 0, 0)).r;
                alpha *= smoothstep(0.0, 0.16, p.y);
                alpha *= smoothstep(0.0, 0.072, travel);
                alpha *= lerp(0.96, 0.69, growth);
                // Smoke must not paint across the foreground hull.
                alpha *= smoothstep(0.0, 0.008, uv.y - (0.425 - 0.24 * (uv.x - 0.74)));
                return float4(tex2Dlod(_MainTex, float4(sampleUV, 0, 0)).rgb, alpha);
            }

            // Adjacent pixels of a star share the same clock. No positional motion.
            float StarBrightness(float2 uv, float time)
            {
                float2 cell = floor(uv * float2(20.0, 12.0));
                float seed = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                float phase = frac(time * lerp(0.70, 1.25, seed) + seed);
                return 1.0 - smoothstep(0.48, 0.58, phase) * (1.0 - smoothstep(0.82, 0.94, phase));
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y;
                float3 mask = tex2D(_MaskTex, uv).rgb;
                float4 original = tex2D(_MainTex, uv);
                float3 clean = tex2D(_CleanTex, uv).rgb;
                // Start from the fully smoke-free artwork. Never reveal the baked
                // plume, even at the vent or when animated smoke opacity is zero.
                float3 col = clean;

                // Restore the original stars only, preserving their V2 twinkle.
                float starBrightness = StarBrightness(uv, t);
                float3 starColor = lerp(original.rgb, clean, mask.g * (1.0 - starBrightness) * _StarMotion);
                col = lerp(col, starColor, step(0.0001, mask.g));

                // Independent, evenly staggered puffs. The oldest puff resets only
                // after its entire silhouette has passed beyond the top of the screen.
                if (uv.x > 0.68 && uv.y > 0.415)
                {
                    float clock = t * _SmokeSpeed * 0.24;
                    [unroll]
                    for (int i = 0; i < 8; i++)
                    {
                        float travel = frac(clock + (float)i / 8.0);
                        float variant = frac((float)i * 0.6180339);
                        float4 puff = RisingPuff(uv, travel, variant);
                        col = lerp(col, puff.rgb, puff.a * _SmokeMotion);
                    }
                }

                // Only the front cyan lamp is affected. Uneven interruptions give the
                // impression of a damaged electrical system; the other lights stay on.
                float cycle = fmod(t + 1.25, 8.6);
                float outage = max(Outage(cycle, 1.8, 2.04), Outage(cycle, 2.28, 2.66));
                outage = max(outage, Outage(cycle, 5.6, 6.95));
                outage = max(outage, 0.55 * Outage(cycle, 7.18, 7.48));
                float3 darkLamp = float3(0.047, 0.061, 0.092);
                #ifndef UNITY_COLORSPACE_GAMMA
                    darkLamp = GammaToLinearSpace(darkLamp);
                #endif
                col = lerp(col, darkLamp, mask.b * outage * _LampFlicker);
                fixed4 result = fixed4(col, original.a) * input.color;
                #ifdef UNITY_UI_CLIP_RECT
                    result.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
