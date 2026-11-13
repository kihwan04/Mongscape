Shader "Mongscape/ForkSurface"
{
    Properties { [MainColor] _BaseColor("Color",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; half fog:TEXCOORD0; };
            Varyings Vert(Attributes input) { Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.fog=ComputeFogFactor(o.positionCS.z);return o; }
            half4 Frag(Varyings input):SV_Target { return half4(MixFog(_BaseColor.rgb,input.fog),_BaseColor.a); }
            ENDHLSL
        }
    }
}
