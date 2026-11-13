Shader "Mongscape/FacePhoto"
{
    Properties { [MainTexture] _BaseMap("Face photo",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input) { Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o; }
            half4 Frag(Varyings input):SV_Target
            {
                float radius=length((input.uv-.5)/float2(.48,.5));
                float edge=saturate((1-radius)/max(fwidth(radius),.002));clip(edge-.001);
                half4 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv);color.a*=edge;return color;
            }
            ENDHLSL
        }
    }
}
