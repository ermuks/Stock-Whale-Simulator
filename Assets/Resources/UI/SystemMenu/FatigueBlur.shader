Shader "Hidden/DonDon/FatigueBlur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _FatigueOptics;
        half4 ReadScene(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
        }
        half4 Gaussian(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            // 둥글게 번지는 블러. 예전에는 가로·세로·대각선의 아홉 점만 읽어서, 반경이 커지면 밝은 곳이 십자(＋) 모양으로 번졌다.
            // 이제는 원 안에 황금각 나선으로 고르게 흩은 열여섯 점을 읽는다. 어느 축에도 줄이 서지 않아 번짐이 둥글다.
            // 가운데일수록 무겁게(가우시안) 더한다.
            half4 value = ReadScene(uv); float total = 1.0;
            for (int i = 0; i < 16; i++)
            {
                float ring = sqrt((i + .5) / 16.0), angle = i * 2.3999632;
                float weight = exp(-2.0 * ring * ring);
                value += ReadScene(uv + float2(cos(angle), sin(angle)) * ring * _FatigueOptics.xy * 1.25) * weight;
                total += weight;
            }
            return value / total;
        }
        half4 CameraMotion(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord, direction = _FatigueOptics.zw;
            // Symmetric shutter, no lingering ghost frame; full end-to-end span <= four output pixels.
            return (ReadScene(uv-direction*.5) + ReadScene(uv-direction*.25) + ReadScene(uv)
                + ReadScene(uv+direction*.25) + ReadScene(uv+direction*.5)) * .2;
        }
        ENDHLSL
        Pass
        {
            Name "Gaussian"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Gaussian
            ENDHLSL
        }
        Pass
        {
            Name "Camera Motion"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraMotion
            ENDHLSL
        }
    }
}
