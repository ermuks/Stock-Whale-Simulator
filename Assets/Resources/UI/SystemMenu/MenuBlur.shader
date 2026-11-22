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
                // 9탭 가우시안을 이웃한 두 픽셀 사이에서 읽어 5번으로 줄인 것이다. _Direction은 꼭 한 픽셀 크기여야 한다
                // (두 픽셀로 벌리면 사이가 비어서 줄무늬와 십자 모양이 생긴다). 더 크게 번지게 하려면 작은 그림에서 여러 번 돌린다.
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
