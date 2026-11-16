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
            // Bilinear pairs implement the 5x5 Gaussian [1 4 6 4 1] with nine reads.
            // At radius 2, samples at +/-1.2 combine the adjacent +/-1 and +/-2 texels.
            // Merely spacing the old 3x3 taps two pixels apart would leave visible gaps.
            float2 x = float2(_FatigueOptics.x * .6, 0), y = float2(0, _FatigueOptics.y * .6);
            half4 value = ReadScene(uv) * .140625;
            value += (ReadScene(uv-x) + ReadScene(uv+x) + ReadScene(uv-y) + ReadScene(uv+y)) * .1171875;
            value += (ReadScene(uv-x-y) + ReadScene(uv+x-y) + ReadScene(uv-x+y) + ReadScene(uv+x+y)) * .09765625;
            return value;
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
