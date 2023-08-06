Shader "Hidden/RadialBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurWidth("Blur Width", Range(0,1)) = 0.85
        _Intensity("Intensity", Range(0,1)) = 1
        _Center("Center", Vector) = (0.5,0.5,0,0)
        _Tint("Tint", Color) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        Blend One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _BlurWidth;
                float _Intensity;
                float4 _Center;
                float4 _Tint;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #define SAMPLE_COUNT 200

            half4 frag (Varyings i) : SV_Target
            {
                half4 color = half4(0.0f, 0.0f, 0.0f, 1.0f);

                float2 ray = i.uv - _Center.xy;
                
                float segment = _BlurWidth / float(SAMPLE_COUNT);

                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    //float scale = 1.0f - _BlurWidth * (float(i) / float(SAMPLE_COUNT - 1));
                    float scale = 1.0f - segment * float(i);
                    color.xyz += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, (ray * scale) +  _Center.xy).xyz / float(SAMPLE_COUNT);
                }

                color = color * _Intensity * _Tint;
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }
    }
}
