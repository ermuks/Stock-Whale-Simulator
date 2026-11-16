Shader "Hidden/DonDon/FatigueVision"
{
    Properties { [PerRendererData] _MainTex("Texture", 2D) = "white" {} _Fatigue("Fatigue", Float) = 0 _Breath("Breath", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Fatigue, _Breath;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            half4 frag(v2f i) : SV_Target
            {
                float f=saturate((_Fatigue-.25)/.75);
                float2 p=abs(i.uv-.5)*2;
                float vignette=smoothstep(.2,1.35,length(p))*f*.52;
                float lids=smoothstep(1-.28*f-.08*f*_Breath,1,p.y)*f*.58;
                return half4(.018,.014,.022,saturate(vignette+lids));
            }
            ENDHLSL
        }
    }
}
