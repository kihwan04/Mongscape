Shader "Muhanok/DepthText"
{
    Properties { _MainTex ("Font atlas", 2D) = "white" {} _BaseColor ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; float distance:TEXCOORD2; };
            Varyings Vert(Attributes input) { Varyings output; float3 world=TransformObjectToWorld(input.positionOS.xyz); output.positionCS=TransformWorldToHClip(world); output.uv=input.uv; output.fog=ComputeFogFactor(output.positionCS.z); output.distance=distance(world,_WorldSpaceCameraPos); return output; }
            half4 Frag(Varyings input):SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a-.35h);
                clip(30-input.distance);
                return half4(MixFog(_BaseColor.rgb,input.fog),_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
