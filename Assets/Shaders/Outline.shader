Shader "Unlit/Outline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineWidth("OutlineWidth", Range(0, 20)) = 1
        _OutlineColor("OutlineColor", Color) = (0.5, 0.5, 0.5, 1)
        [KeywordEnum(UV2, UV3)]_NormalsStoreIn("StoreUvChannel", int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags {"LightMode" = "SRPDefaultUnlit"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDHLSL
        }
        
        Pass
        {
            Tags {"LightMode" = "UniversalForward"}
            Name "OUTLINE"
            Cull Front
            
            HLSLPROGRAM


            #pragma shader_feature_local _NORMALSSTOREIN_UV2 _NORMALSSTOREIN_UV3
            
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 OctahedronToUnitVector(float2 oct)
            {
                float3 unitVec = float3(oct, 1 - dot(float2(1, 1), abs(oct)));

                if (unitVec.z < 0)
                {
                    unitVec.xy = (1 - abs(unitVec.yx)) * float2(unitVec.x >= 0 ? 1 : -1, unitVec.y >= 0 ? 1 : -1);
                }
                
                return normalize(unitVec);
            }
    
            struct OutlineVertexInput
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            #if defined(_NORMALSSTOREIN_UV2)
                float2 smoothNormal : TEXCOORD1;
            #else
                float2 smoothNormal : TEXCOORD2;
            #endif
            };
                
            struct OutlineVertexOutput
            {
                float4 positionCS : SV_POSITION;
            };

            half _OutlineWidth;
            half3 _OutlineColor;
            
            OutlineVertexOutput OutlineVertex(OutlineVertexInput input)
            {
                OutlineVertexOutput output = (OutlineVertexOutput)0;

                output.positionCS = TransformObjectToHClip(input.positionOS);
                
                float3 normalTS = OctahedronToUnitVector(input.smoothNormal);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                float3x3 tangentToWorld = float3x3(normalInputs.tangentWS, normalInputs.bitangentWS, normalInputs.normalWS);
                float3 normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                float3 normalCS = TransformWorldToHClipDir(normalWS);
                
                float2 offset = normalize(normalCS.xy) / _ScreenParams.xy * _OutlineWidth * output.positionCS.w * 2;
                output.positionCS.xy += offset;
                return output;
            }
    
            half4 OutlineFragment(OutlineVertexOutput i) : SV_Target
            {
                return half4(_OutlineColor, 1);
            }
            ENDHLSL
        }
    }
}
