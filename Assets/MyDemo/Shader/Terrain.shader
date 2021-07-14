Shader "XHH/Terrain"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Texture", 2D) = "white" { }
        [NoScaleOffset]_HeightMap ("Texture", 2D) = "white" { }
        [NoScaleOffset]_NormalMap ("Texture", 2D) = "white" { }
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "LightMode" = "UniversalForward" }
        LOD 100

        Pass
        {
            HLSLPROGRAM

            //Keywords
            #pragma shader_feature ENABLE_MIP_DEBUG
            #pragma shader_feature ENABLE_PATCH_DEBUG
            #pragma shader_feature ENABLE_NODE_DEBUG

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "./TerrainInput.hlsl"
            // ./ 的写法

            StructuredBuffer<RenderPatch> PatchList;

            struct appdata
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;
                uint instanceID: SV_InstanceID;
            };

            struct v2f
            {
                float2 uv: TEXCOORD0;
                float4 vertex: SV_POSITION;
                float4 color: TEXCOORD1;//定点色

            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _HeightMap;
            uniform float3 _WorldSize;//世界大小

            #if ENABLE_MIP_DEBUG
                //用于Mipmap的调试颜色
                static half3 debugColorForMip[6] = {
                    half3(0, 1, 0),
                    half3(0, 0, 1),
                    half3(1, 0, 0),
                    half3(1, 1, 0),
                    half3(0, 1, 1),
                    half3(1, 0, 1),
                };
            #endif

            #if ENABLE_NODE_DEBUG
                //在Node之间留出缝隙供Debug
                float3 ApplyNodeDebug(RenderPatch patch, float3 vertex)
                {
                    uint nodeCount = (uint) (5 * pow(2, 5 - patch.lod));
                    float nodeSize = _WorldSize.x / nodeCount;
                    uint2 nodeLoc = floor((patch.position + _WorldSize.xz * 0.5) / nodeSize);
                    float2 nodeCenterPosition = -_WorldSize.xz * 0.5 + (nodeLoc + 0.5) * nodeSize ;
                    vertex.xz = nodeCenterPosition + (vertex.xz - nodeCenterPosition) * 0.95;
                    return vertex;
                }
            #endif

            v2f vert(appdata v)
            {
                v2f o;

                float3 inVertex = v.vertex;

                RenderPatch patch = PatchList[v.instanceID];
                uint lod = patch.lod;
                float scale = pow(2, lod);
                inVertex.xz *= scale;
                #if ENABLE_PATCH_DEBUG
                    inVertex.xz *= 0.9;
                #endif
                inVertex.xz += patch.position;

                #if ENABLE_NODE_DEBUG
                    inVertex.xyz = ApplyNodeDebug(patch, inVertex.xyz);
                #endif

                //这里的UV是如何处理的？
                float2 heightUV = (inVertex.xz + (_WorldSize.xz * 0.5) + 0.5) / (_WorldSize.xz + 1);
                float height = tex2Dlod(_HeightMap, float4(heightUV, 0, 0)).r;
                o.color = height;
                inVertex.y = height * _WorldSize.y;
                
                
                o.vertex = TransformObjectToHClip(inVertex.xyz);
                // o.color = heightUV;
                
                o.uv = v.uv * scale * 8;


                #if ENABLE_MIP_DEBUG
                    uint4 lodColorIndex = lod;
                    o.color.xyz = (debugColorForMip[lodColorIndex.x] +
                    debugColorForMip[lodColorIndex.y] +
                    debugColorForMip[lodColorIndex.z] +
                    debugColorForMip[lodColorIndex.w]) * 0.25;
                #endif


                return o;
            }

            half4 frag(v2f i): SV_Target
            {
                // sample the texture
                half4 col = tex2D(_MainTex, i.uv);
                // return float4(i.color, 0, 0);
                // return col;
                #if ENABLE_MIP_DEBUG
                    return col * float4(i.color.xyz, 1);
                #endif
                return col * float4(i.color.xyz * i.color.z, 1);
            }
            ENDHLSL

        }
    }
}
