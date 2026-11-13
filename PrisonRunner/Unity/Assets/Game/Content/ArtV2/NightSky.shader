Shader "Muhanok/NightSky"
{
    Properties
    {
        _Horizon("Horizon",Color)=(.22,.38,.65,1)
        _Zenith("Zenith",Color)=(.055,.10,.23,1)
        _Cloud("Cloud",Color)=(.10,.20,.36,1)
    }
    SubShader
    {
        Tags{"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Horizon,_Zenith,_Cloud;
            struct A{float4 positionOS:POSITION;};
            struct V{float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;};
            V Vert(A a){V v;v.positionCS=TransformObjectToHClip(a.positionOS.xyz);v.direction=a.positionOS.xyz;return v;}
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(V v):SV_Target
            {
                float3 d=normalize(v.direction);float height=saturate(d.y*.9+.16);
                half3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(height,.6));
                float2 uv=float2(atan2(d.x,d.z)*.1591549+.5,d.y);
                float2 p=uv*float2(18,9);
                float cloud=Noise(p)*.7+Noise(p*2.1)*.3;
                float mask=smoothstep(.49,.66,cloud)*smoothstep(.02,.16,d.y)*(1-smoothstep(.60,.80,d.y));
                sky=lerp(sky,_Cloud.rgb,mask*.8);
                float2 star=uv*float2(480,180);float2 cell=frac(star)-.5;
                float dots=step(.997,Hash(floor(star)))*smoothstep(.16,0,length(cell))*smoothstep(.20,.5,d.y)*(1-mask);
                float angle=acos(clamp(dot(d,normalize(float3(.20,.32,.93))),-1,1));
                float halo=exp(-angle*angle*120)*.10;
                float disk=1-smoothstep(.032,.036,angle);
                float crater=Noise(uv*240)*.25+Noise(uv*510)*.12;
                sky+=halo*half3(.35,.55,1);
                sky=lerp(sky,half3(.58,.76,1)*(1-crater),disk);
                return half4(sky+dots*half3(.24,.32,.45),1);
            }
            ENDHLSL
        }
    }
}
