Shader "XHH/BoundsDebug"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "LightMode" = "UniversalForward" "Queue" = "Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // #pragma shader_feature ENABLE_MIP_DEBUG
            #pragma shader_feature ENABLE_PATCH_DEBUG
            #pragma shader_feature ENABLE_NODE_DEBUG


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "./TerrainInput.hlsl"

            StructuredBuffer<BoundsDebug> BoundsList;

            struct appdata
            {
                float4 vertex: POSITION;
                uint instanceID: SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex: SV_POSITION;
                float3 color: TEXCOORD1;
            };


            v2f vert(appdata v)
            {
                v2f o;
                
                float4 inVertex = v.vertex;
                BoundsDebug boundsDebug = BoundsList[v.instanceID];
                Bounds bounds = boundsDebug.bounds;

                float3 center = (bounds.minPosition + bounds.maxPosition) * 0.5;

                float3 scale = (bounds.maxPosition - center) / 0.5;

                #if ENABLE_PATCH_DEBUG
                    inVertex.xz *= 0.9;
                #endif
                inVertex.xyz = inVertex.xyz * scale + center;


                float4 vertex = TransformObjectToHClip(inVertex.xyz);
                o.vertex = vertex;
                o.color = boundsDebug.color.rgb;
                return o;
            }
            half4 frag(v2f i): SV_Target
            {
                half4 col = half4(i.color, 0.7);
                return col;
            }
            ENDHLSL

        }
    }
}
