float3 _PlayerPosition;
sampler2D _TraceHeightmap;
float4 _TraceHeightmap_ST;

bool haveDeformation(float2 worldPositionXZ)
{
    float2 relPosToPlayer = worldPositionXZ - _PlayerPosition;
    if (abs(relPosToPlayer.x) > GRID_SIZE_2 ||
        abs(relPosToPlayer.y) > GRID_SIZE_2)
        return false;
    return true;
}

float2 worldPosToTextureUV(float2 worldPos)
{
    // 16 m grids
    // -x-z corner of the grid
    float2 xzBias = _PlayerPosition.xz - fmod(_PlayerPosition.xz, GRID_SIZE);
    float2 mainGridPos = worldPos - xzBias;
    mainGridPos.x = mainGridPos.x < 0 ? mainGridPos.x + GRID_SIZE : mainGridPos.x;
    mainGridPos.y = mainGridPos.y < 0 ? mainGridPos.y + GRID_SIZE : mainGridPos.y;
    // 32 x 32 = 1024
    return mainGridPos / GRID_SIZE;
}
