// Additive hologram: fresnel rim + scrolling scanlines + optional vertical fade (for beacon beams).
Shader "MissionMars/Holo"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.9, 1.0, 1)
        _Intensity ("Intensity", Float) = 1.5
        _Rim ("Rim Power", Float) = 2.0
        _Base ("Base Alpha", Float) = 0.15
        _Scan ("Scanline Density", Float) = 40
        _ScanSpeed ("Scan Speed", Float) = 1.5
        _VFade ("Vertical Fade (beam)", Float) = 0
        _Pulse ("Pulse", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Intensity, _Rim, _Base, _Scan, _ScanSpeed, _VFade, _Pulse;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; float2 uv : TEXCOORD2; };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; o.uv = v.uv; return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float rim = pow(1.0 - abs(dot(n, V)), _Rim);
                float scan = 0.65 + 0.35 * sin(i.wp.y * _Scan - _Time.y * _ScanSpeed * 6.0);
                float a = (rim + _Base) * scan;
                float fade = lerp(1.0, saturate(1.0 - i.uv.y) * saturate(i.uv.y * 8.0), _VFade);
                float pulse = 1.0 + _Pulse * 0.35 * sin(_Time.y * 4.0);
                return float4(_Color.rgb * a * fade * _Intensity * pulse, 1);
            }
            ENDCG
        }
    }
}
