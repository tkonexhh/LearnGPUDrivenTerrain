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

            #pragma target 5.0

            //Keywords
            #pragma shader_feature ENABLE_MIP_DEBUG
            #pragma shader_feature ENABLE_PATCH_DEBUG
            #pragma shader_feature ENABLE_NODE_DEBUG
            #pragma shader_feature ENABLE_LOD_SEAMLESS//处理LOD 接缝

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "./TerrainInput.hlsl"
            // ./ 的写法

            StructuredBuffer<RenderPatch> PatchList;//这个StructuredBuffer在大部分手机上都不支持 但是要切到Vulkan

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
                float4 color: TEXCOORD1;
                float height: TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _HeightMap;
            sampler2D _NormalMap;
            uniform float3 _WorldSize;//世界大小
            float4x4 _WorldToNormalMapMatrix;

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

            float3 TransformNormalToWorldSpace(float3 normal)
            {
                return SafeNormalize(mul(normal, (float3x3)_WorldToNormalMapMatrix));
            }

            float3 SampleNormal(float2 uv)
            {
                float3 normal;
                normal.xz = tex2Dlod(_NormalMap, float4(uv, 0, 0)).xy * 2 - 1;
                normal.y = sqrt(max(0, 1 - dot(normal.xz, normal.xz)));
                normal = TransformNormalToWorldSpace(normal);
                return normal;
            }

            #if ENABLE_LOD_SEAMLESS
                //修复接缝
                void FixLODConnectSeam(inout float4 vertex, inout float2 uv, RenderPatch patch)
                {
                    uint4 lodTrans = patch.lodTrans;//四个方向上的差值
                    //模型空间坐标原先范围是(-PATCH_MESH_SIZE * 0.5,PATCH_MESH_SIZE * 0.5) 先转到(0,8)之间
                    //然后再 /PATCH_MESH_GRID_SIZE 得到是顶点坐标顺序 vertexIndex 范围是0-16
                    uint2 vertexIndex = floor((vertex.xz + PATCH_MESH_SIZE * 0.5 + 0.01) / PATCH_MESH_GRID_SIZE);

                    float uvGridStrip = 1.0 / PATCH_MESH_GRID_COUNT;//单位Grid的UV值

                    uint lodDelta = lodTrans.x;//处理左侧接缝
                    //如果是最左边的点 并且只处理Lod上升的情况 避免多次执行
                    if (lodDelta > 0 && vertexIndex.x == 0)
                    {
                        uint gridStripCount = pow(2, lodDelta);//间隔的不需要处理的顶点
                        uint modIndex = vertexIndex.y % gridStripCount;//如果不是需要处理的顶点
                        if (modIndex != 0)
                        {
                            vertex.z -= PATCH_MESH_GRID_SIZE * modIndex;//下移到下方 那个不需要处理的定点位置
                            uv.y -= uvGridStrip * modIndex;//同时改变UV
                            return;
                        }
                    }

                    lodDelta = lodTrans.y;//处理下侧接缝
                    //如果是最下边的点
                    if (lodDelta > 0 && vertexIndex.y == 0)
                    {
                        uint gridStripCount = pow(2, lodDelta);
                        uint modIndex = vertexIndex.x % gridStripCount;
                        if (modIndex != 0)
                        {
                            vertex.x -= PATCH_MESH_GRID_SIZE * modIndex;
                            uv.x -= uvGridStrip * modIndex;
                            return;
                        }
                    }

                    lodDelta = lodTrans.z;//处理右侧接缝
                    //如果是最右侧的点
                    if (lodDelta > 0 && vertexIndex.x == PATCH_MESH_GRID_COUNT)
                    {
                        uint gridStripCount = pow(2, lodDelta);
                        uint modIndex = vertexIndex.y % gridStripCount;
                        if (modIndex != 0)
                        {
                            vertex.z += PATCH_MESH_GRID_SIZE * (gridStripCount - modIndex);
                            uv.y += uvGridStrip * (gridStripCount - modIndex);
                            return;
                        }
                    }

                    lodDelta = lodTrans.w;//处理上侧接缝
                    //如果是最上侧的点
                    if (lodDelta > 0 && vertexIndex.y == PATCH_MESH_GRID_COUNT)
                    {
                        uint gridStripCount = pow(2, lodDelta);
                        uint modIndex = vertexIndex.x % gridStripCount;
                        if (modIndex != 0)
                        {
                            vertex.x += PATCH_MESH_GRID_SIZE * (gridStripCount - modIndex);
                            uv.x += uvGridStrip * (gridStripCount - modIndex);
                            return;
                        }
                    }
                }
            #endif

            v2f vert(appdata v)
            {
                v2f o;

                float4 inVertex = v.vertex;
                RenderPatch patch = PatchList[v.instanceID];
                #if ENABLE_LOD_SEAMLESS
                    FixLODConnectSeam(inVertex, v.uv, patch);
                #endif
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
                o.height = height;
                inVertex.y = height * _WorldSize.y;
                
                
                o.vertex = TransformObjectToHClip(inVertex.xyz);
                o.uv = v.uv * scale * 8;//*8是为什么

                //Normal
                float3 normal = SampleNormal(heightUV);
                Light light = GetMainLight();
                o.color = max(0.05, dot(light.direction, normal));

                #if ENABLE_MIP_DEBUG
                    uint4 lodColorIndex = lod + patch.lodTrans;
                    o.color.xyz *= (debugColorForMip[lodColorIndex.x] +
                    debugColorForMip[lodColorIndex.y] +
                    debugColorForMip[lodColorIndex.z] +
                    debugColorForMip[lodColorIndex.w]) * 0.25;
                #endif

                
                return o;
            }

            half4 frag(v2f i): SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.color;
                return col;
                return i.height;
            }
            ENDHLSL

        }
    }
}
