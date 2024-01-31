float4x4 ViewProjection;
float4x4 World;

float4x4 BoneTransforms[128];

float4 Tint;

float Time;
float2 ScrollSpeed;

texture MainTexture <string defaultTex="white";>;
sampler MainSampler = sampler_state
{
	Texture = MainTexture;
};

texture MaskTexture <string defaultTex="white";>;
sampler MaskSampler = sampler_state
{
	Texture = MaskTexture;
	AddressU = Wrap;
	AddressV = Wrap;
};

struct VertexInput {
    float3 position: POSITION0;
    float4 color: COLOR0;
    float2 texcoord: TEXCOORD0;
    float4 boneJoints: TEXCOORD2;
    float4 boneWeights: TEXCOORD3;
};

struct PixelInput {
    float4 position: SV_Position0;
    float4 color: COLOR0;
    float2 texcoord0: TEXCOORD0;
    float2 texcoord1: TEXCOORD1;
};

PixelInput UnlitVS(VertexInput v) {
    PixelInput o;

    o.position = mul(mul(float4(v.position, 1.0), World), ViewProjection);
    o.color = v.color * Tint;
    o.texcoord0 = v.texcoord;
    o.texcoord1 = v.texcoord + (ScrollSpeed * Time);

    return o;
}

PixelInput UnlitSkinnedVS(VertexInput v) {
    PixelInput o;
    
    float4x4 transform0 = BoneTransforms[(int)v.boneJoints.x];
    float4x4 transform1 = BoneTransforms[(int)v.boneJoints.y];
    float4x4 transform2 = BoneTransforms[(int)v.boneJoints.z];
    float4x4 transform3 = BoneTransforms[(int)v.boneJoints.w];
    
    float4x4 transform = (transform0 * v.boneWeights.x)
    	+ (transform1 * v.boneWeights.y)
    	+ (transform2 * v.boneWeights.z)
    	+ (transform3 * v.boneWeights.w);
    	
    transform = mul(transform, World);

    o.position = mul(mul(float4(v.position, 1.0), transform), ViewProjection);
    o.color = v.color * Tint;
    o.texcoord0 = v.texcoord;
    o.texcoord1 = v.texcoord + (ScrollSpeed * Time);

    return o;
}

float4 UnlitPS(PixelInput p) : SV_TARGET {
    float4 tex = tex2D(MainSampler, p.texcoord0);
    float4 mask = tex2D(MaskSampler, p.texcoord1);
    return tex * mask * 2 * p.color;
}

technique Default {
    pass {
        VertexShader = compile vs_3_0 UnlitVS();
        PixelShader = compile ps_3_0 UnlitPS();
    }
}

technique Skinned {
    pass {
        VertexShader = compile vs_3_0 UnlitSkinnedVS();
        PixelShader = compile ps_3_0 UnlitPS();
    }
}