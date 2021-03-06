#ifndef TERRAIN_INPUT
#define TERRAIN_INPUT

// //最大的LOD级别是5
#define MAX_TERRAIN_LOD 5
// #define MAX_NODE_ID 34124

// //一个PatchMesh由16x16网格组成
#define PATCH_MESH_GRID_COUNT 16

// //一个PatchMesh边长8米
#define PATCH_MESH_SIZE 8 //16*0.5 PATCH_MESH_GRID_COUNT*PATCH_MESH_GRID_SIZE

//一个Node拆成8x8个Patch
#define PATCH_COUNT_PER_NODE 8

// //PatchMesh一个格子的大小为0.5x0.5
#define PATCH_MESH_GRID_SIZE 0.5

#define SECTOR_COUNT_WORLD 160 //最大Node的情况 160*160 5*2^6

//节点分化评价C值
#define NodeEvaluationC 1.2

//节点描述
struct NodeDescriptor
{
    uint branch; //1 细分过,0 没有细分

};

struct RenderPatch
{
    float2 position;
    float2 minMaxHeight;//x:最小Y y:最大Y
    uint lod;
    uint4 lodTrans;//+x,-x,+z,-z 4个方向的LOD变化情况

};

struct Bounds
{
    float3 minPosition;
    float3 maxPosition;
};

struct BoundsDebug
{
    Bounds bounds;
    float4 color;
};


#endif