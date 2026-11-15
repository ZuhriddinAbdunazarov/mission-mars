// Additive glow shell around a planet (render on a slightly larger sphere).
Shader "MissionMars/AtmoHalo"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.5, 0.3, 1)
        _Power ("Falloff", Float) = 4
        _Intensity ("Intensity", Float) = 1.2
        _SunDirW ("Sun Direction (world)", Vector) = (1, 0.3, 0.2, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Front
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _SunDirW; float _Power, _Intensity;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float g = pow(saturate(-dot(n, V)), _Power);
                float lit = saturate(dot(n, normalize(_SunDirW.xyz)) + 0.45);
                return float4(_Color.rgb * g * lit * _Intensity, 1);
            }
            ENDCG
        }
    }
}
