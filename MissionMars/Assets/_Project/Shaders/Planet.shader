// Planet surface seen from space: textured sphere lit by a single sun direction,
// with soft terminator, night side and atmospheric rim.
Shader "MissionMars/Planet"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _SunDirW ("Sun Direction (world)", Vector) = (1, 0.3, 0.2, 0)
        _AtmoColor ("Atmosphere Color", Color) = (1.0, 0.55, 0.35, 1)
        _AtmoStrength ("Atmosphere Strength", Float) = 1.0
        _NightLevel ("Night Level", Float) = 0.015
        _Emission ("Self Emission", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            float4 _Tint, _SunDirW, _AtmoColor;
            float _AtmoStrength, _NightLevel, _Emission;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 wp : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 L = normalize(_SunDirW.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float ndl = dot(n, L);
                float lit = smoothstep(-0.12, 0.35, ndl);
                float3 alb = tex2D(_MainTex, i.uv).rgb * _Tint.rgb;
                float3 col = alb * (lit * 1.25 + _NightLevel);
                float fres = pow(1.0 - saturate(dot(n, V)), 3.0);
                col += _AtmoColor.rgb * fres * _AtmoStrength * saturate(ndl + 0.35);
                col += alb * _Emission;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
