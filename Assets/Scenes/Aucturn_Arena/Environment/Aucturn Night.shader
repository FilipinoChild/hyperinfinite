Shader "HyperInfinite/Aucturn Night"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(float4 position : POSITION)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(position);
                output.direction = position.xyz;
                return output;
            }

            float Hash(float2 cell)
            {
                float3 p = frac(cell.xyx * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float2 angles = float2(atan2(direction.x, direction.z),
                                      asin(clamp(direction.y, -1.0, 1.0)));
                float2 pixel = (floor(angles * 285.0) + 0.5) / 285.0;
                float altitude = abs(pixel.y);
                float horizon = 1.0 - smoothstep(0.02, 0.53, altitude);
                float3 color = lerp(float3(0.092, 0.053, 0.164),
                                    float3(0.32, 0.155, 0.305), horizon);

                float2 cell = floor(pixel * 34.0);
                float2 local = frac(pixel * 34.0);
                float star = Hash(cell);
                float2 center = 0.23 + 0.54 * float2(Hash(cell + 19.0), Hash(cell - 41.0));
                float2 offset = abs(local - center);
                float radius = lerp(0.061, 0.16, Hash(cell + 77.0));
                float square = step(max(offset.x, offset.y), radius);
                float visible = step(0.941, star) * smoothstep(-0.02, 0.09, pixel.y);
                float3 starlight = lerp(float3(0.67, 0.75, 0.52),
                                        float3(0.93, 0.88, 0.71), Hash(cell - 29.0));
                color = lerp(color, starlight, visible * square * lerp(0.38, 0.92, Hash(cell + 31.0)));

                float amberMoon = length((pixel - float2(-0.43, 0.48)) * float2(1.0, 1.13));
                float mintMoon = length((pixel - float2(-0.20, 0.36)) * float2(1.0, 1.12));
                color += float3(0.06, 0.025, 0.01) * (1.0 - smoothstep(0.065, 0.175, amberMoon));
                color = lerp(color, float3(1.0, 0.82, 0.49), step(amberMoon, 0.065));
                color = lerp(color, float3(0.63, 0.91, 0.80), step(mintMoon, 0.024));

                #ifndef UNITY_COLORSPACE_GAMMA
                color = GammaToLinearSpace(color);
                #endif
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
