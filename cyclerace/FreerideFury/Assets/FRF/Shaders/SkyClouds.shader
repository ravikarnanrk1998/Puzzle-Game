// Cloud layer for the sky dome: unlit, alpha blended, no fog (the dome sits far beyond
// the fog end), drifting slowly. Vertex alpha fades the clouds out towards the horizon.
Shader "FRF/SkyClouds"
{
    Properties
    {
        _BaseMap ("Clouds", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Underside", Color) = (0.72, 0.78, 0.88, 1)
        _Drift ("Drift (uv per second)", Vector) = (0.004, 0.0015, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Clouds"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadeColor;
                float4 _Drift;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Drift.xy * _Time.y;
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                // The texture's red channel is lightness: bright tops, greyer bases.
                half3 c = lerp(_ShadeColor.rgb, _BaseColor.rgb, t.r);
                return half4(c, t.a * _BaseColor.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
