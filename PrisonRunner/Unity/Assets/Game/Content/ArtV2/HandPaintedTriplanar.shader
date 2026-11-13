Shader "Muhanok/HandPaintedTriplanar"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo atlas",2D)="white"{}
        [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
        _AtlasTile("Tile scale and offset",Vector)=(.5,.5,0,.5)
        _WorldScale("Tiles per metre",Float)=.3
        _TextureStrength("Painted detail strength",Range(0,1))=1
        _Smoothness("Smoothness",Range(0,1))=.2
        _Metallic("Metallic",Range(0,1))=0
        _Cutoff("Cutoff",Float)=.5
        [HideInInspector] _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseMap_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor,_AtlasTile;
                float _WorldScale,_TextureStrength,_Smoothness,_Metallic,_Cutoff,_Cull;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half fog:TEXCOORD2;};
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half3 SampleTile(float2 planar)
            {
                float2 inset=_BaseMap_TexelSize.xy*1.5;
                float2 extent=_AtlasTile.xy-2*inset;
                float2 uv=frac(planar)*extent+_AtlasTile.zw+inset;
                return SAMPLE_TEXTURE2D_GRAD(_BaseMap,sampler_BaseMap,uv,ddx(planar)*extent,ddy(planar)*extent).rgb;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 normal=normalize(i.normalWS);
                half3 weights=pow(abs(normal),4);weights/=max(dot(weights,half3(1,1,1)),.001);
                float3 p=i.positionWS*_WorldScale;
                half3 albedo=SampleTile(p.zy)*weights.x+SampleTile(p.xz)*weights.y+SampleTile(p.xy)*weights.z;
                InputData input=(InputData)0;
                input.positionWS=i.positionWS;input.positionCS=i.positionCS;input.normalWS=normal;
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord=i.fog;input.bakedGI=SampleSH(normal);input.shadowMask=half4(1,1,1,1);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.vertexLighting=VertexLighting(i.positionWS,normal);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=lerp(half3(.48,.40,.32),albedo,_TextureStrength)*_BaseColor.rgb;surface.alpha=1;surface.occlusion=1;
                surface.normalTS=half3(0,0,1);surface.metallic=_Metallic;surface.smoothness=_Smoothness;
                half4 color=UniversalFragmentPBR(input,surface);color.rgb=MixFog(color.rgb,i.fog);return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
