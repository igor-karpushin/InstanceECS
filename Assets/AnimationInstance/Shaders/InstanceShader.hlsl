
float2 GetUV(int index)
{
    uint row = index / (uint)_AnimTex_TexelSize.z;
    uint col = index % (uint)_AnimTex_TexelSize.z;

    return float2(col / _AnimTex_TexelSize.z, row / _AnimTex_TexelSize.w);
}

float4x4 GetMatrix(int startIndex, float boneIndex, SamplerState samplerState)
{
    int matrixIndex = startIndex + boneIndex * 3;

    float4 row0 = _AnimTex.SampleLevel(samplerState, GetUV(matrixIndex), 0);
    float4 row1 = _AnimTex.SampleLevel(samplerState, GetUV(matrixIndex + 1), 0);
    float4 row2 = _AnimTex.SampleLevel(samplerState, GetUV(matrixIndex + 2), 0);
    
    float4 row3 = float4(0, 0, 0, 1);

    return float4x4(row0, row1, row2, row3);
}

void GetMatrix_float(
    float4 position, 
    float4 normal, 
    half4 boneIndex, 
    float4 boneWeight, 
    SamplerState samplerState, 
    float frame,
    out float4 animPosition,
    out float4 animNormal
    )
{
    float4 animSettings = _AnimTex.SampleLevel(samplerState, GetUV(_AnimationType), 0);     
    uint offsetFrame = (uint)((frame + _AnimOffset) * 30);
	uint currentFrame = (uint)animSettings.x + offsetFrame % (uint)animSettings.y;
	uint clampedIndex = 255 + currentFrame * (uint)_PixelCountPerFrame;
					
    float4x4 bone1Matrix = GetMatrix(clampedIndex, boneIndex.x, samplerState);
    float4x4 bone2Matrix = GetMatrix(clampedIndex, boneIndex.y, samplerState);
    float4x4 bone3Matrix = GetMatrix(clampedIndex, boneIndex.z, samplerState);
    float4x4 bone4Matrix = GetMatrix(clampedIndex, boneIndex.w, samplerState);
			
    animPosition =
        mul(bone1Matrix, position) * boneWeight.x +
        mul(bone2Matrix, position) * boneWeight.y +
        mul(bone3Matrix, position) * boneWeight.z +
        mul(bone4Matrix, position) * boneWeight.w;
        
    animNormal =
        mul(bone1Matrix, normal) * boneWeight.x +
        mul(bone2Matrix, normal) * boneWeight.y +
        mul(bone3Matrix, normal) * boneWeight.z +
        mul(bone4Matrix, normal) * boneWeight.w;
}
