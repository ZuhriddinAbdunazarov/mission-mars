// Procedural Mars / space skybox.
// Day: butterscotch dusty sky. Sunset: the famous blue glow around the Sun.
// Night / space: real NASA Hipparcos star map + tiny blue Earth.
Shader "MissionMars/MarsSky"
{
    Properties
    {
        _StarTex ("Star Map (equirect)", 2D) = "black" {}
        _StarIntensity ("Star Intensity", Float) = 1.6
        _SunDir ("Sun Direction", Vector) = (0, 0.5, 1, 0)
        _EarthDir ("Earth Direction", Vector) = (0.3, 0.4, -1, 0)
        _Day ("Daylight 0..1", Range(0,1)) = 1
        _Space ("Space Mode 0..1", Range(0,1)) = 0
        _ZenithDay ("Zenith Day", Color) = (0.62, 0.43, 0.30, 1)
        _HorizonDay ("Horizon Day", Color) = (0.93, 0.70, 0.50, 1)
        _ZenithNight ("Zenith Night", Color) = (0.010, 0.012, 0.025, 1)
        _HorizonNight ("Horizon Night", Color) = (0.05, 0.035, 0.04, 1)
        _SunsetBlue ("Sunset Blue", Color) = (0.35, 0.62, 1.0, 1)
        _SunColor ("Sun Color", Color) = (1.0, 0.92, 0.80, 1)
        _SunSize ("Sun Size", Float) = 0.9994
        _Exposure ("Exposure", Float) = 1
        _EarthGlow ("Earth Glow", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _StarTex;
            float _StarIntensity, _Day, _Space, _SunSize, _Exposure, _EarthGlow;
            float4 _SunDir, _EarthDir;
            float4 _ZenithDay, _HorizonDay, _ZenithNight, _HorizonNight, _SunsetBlue, _SunColor;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float3 stars(float3 d)
            {
                float u = atan2(d.x, d.z) / (2.0 * UNITY_PI) + 0.5;
                float v = asin(clamp(d.y, -1, 1)) / UNITY_PI + 0.5;
                float3 s = tex2Dlod(_StarTex, float4(u, v, 0, 0)).rgb;
                s = pow(s, 1.6) * _StarIntensity;
                // extra procedural twinkle points for crispness
                float3 g = floor(d * 380.0);
                float h = hash(g);
                float pt = step(0.9975, h) * (0.6 + 0.4 * sin(_Time.y * (2.0 + h * 5.0) + h * 40.0));
                return s + pt * float3(0.9, 0.95, 1.0) * 0.9;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sunDir = normalize(_SunDir.xyz);
                float sunDot = dot(d, sunDir);
                float h = d.y;

                // ---------- Martian atmosphere ----------
                float hz = pow(saturate(h), 0.45);
                float3 dayCol = lerp(_HorizonDay.rgb, _ZenithDay.rgb, hz);
                // forward scattering haze near the sun
                dayCol += _SunColor.rgb * pow(saturate(sunDot), 6.0) * 0.35;

                float3 nightCol = lerp(_HorizonNight.rgb, _ZenithNight.rgb, hz);

                // low sun factor (sunset / sunrise)
                float sunLow = saturate(1.0 - abs(sunDir.y) * 3.2);
                float3 sky = lerp(nightCol, dayCol, _Day);

                // Mars' blue sunset: fine dust scatters blue light forward around the sun
                float blue = pow(saturate(sunDot), 14.0) * sunLow * saturate(sunDir.y + 0.25) * 3.0;
                sky += _SunsetBlue.rgb * blue;
                // dim warm band on the horizon at dusk
                sky += float3(0.45, 0.22, 0.12) * sunLow * pow(1.0 - saturate(abs(h)), 6.0) * 0.35 * saturate(_Day + 0.4);

                // ground below horizon (mostly hidden by terrain)
                float below = saturate(-h * 6.0);
                sky = lerp(sky, sky * 0.35, below * (1.0 - _Space));

                // ---------- stars ----------
                float starVis = saturate(1.0 - _Day * 1.4);
                starVis *= saturate(h * 4.0 + 0.3);
                starVis = lerp(starVis, 1.0, _Space);
                float3 col = lerp(sky, float3(0,0,0), _Space) + stars(d) * starVis;

                // ---------- sun disk ----------
                float disk = smoothstep(_SunSize, _SunSize + 0.0004, sunDot);
                float corona = pow(saturate(sunDot), 600.0) * 2.0 + pow(saturate(sunDot), 60.0) * 0.25 * _Space;
                col += _SunColor.rgb * (disk * 30.0 + corona) * saturate(h * 30.0 + 1.0 + _Space * 10.0);

                // ---------- Earth: a tiny blue "star" ----------
                float3 eDir = normalize(_EarthDir.xyz);
                float eDot = dot(d, eDir);
                float earth = (smoothstep(0.999993, 0.999998, eDot) * 5.0 + pow(saturate(eDot), 9000.0) * 1.5) * _EarthGlow;
                col += float3(0.35, 0.6, 1.0) * earth * saturate(starVis * 1.5 + 0.15);

                return float4(col * _Exposure, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
