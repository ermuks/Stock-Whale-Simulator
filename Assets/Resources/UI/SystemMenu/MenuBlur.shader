Shader "Hidden/DonDon/MenuBlur"
{
    Properties { _MainTex("Source", 2D) = "white" {} }
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float2 _Direction;
            half4 frag(v2f_img i) : SV_Target
            {
                half4 c=tex2D(_MainTex,i.uv)*.227027;
                c+=tex2D(_MainTex,i.uv+_Direction*1.384615)*.316216;
                c+=tex2D(_MainTex,i.uv-_Direction*1.384615)*.316216;
                c+=tex2D(_MainTex,i.uv+_Direction*3.230769)*.070270;
                c+=tex2D(_MainTex,i.uv-_Direction*3.230769)*.070270;
                return c;
            }
            ENDHLSL
        }
    }
}
