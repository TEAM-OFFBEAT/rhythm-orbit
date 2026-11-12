Shader "Hidden/VignetteOverlay"
{
    Properties
    {
        _VignetteTex ("Vignette Texture", 2D) = "black" {}
        _Opacity ("Opacity", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_VignetteTex); SAMPLER(sampler_VignetteTex);

            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
            CBUFFER_END

            half3 HardLight(half3 base, half3 blend)
            {
                half3 mul  = 2.0h * base * blend;
                half3 scrn = 1.0h - 2.0h * (1.0h - base) * (1.0h - blend);
                return lerp(mul, scrn, step(0.5h, blend));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
                half4 v     = SAMPLE_TEXTURE2D(_VignetteTex, sampler_VignetteTex, i.texcoord);
                half3 hl    = HardLight(scene, v.rgb);
                return half4(lerp(scene, hl, v.a * _Opacity), 1.0h);
            }
            ENDHLSL
        }
    }
}
