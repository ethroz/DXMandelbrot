/////////////////////////////////////////
//              Buffers                //
/////////////////////////////////////////

Texture2D ShaderTexture : register(t0);
SamplerState Sampler : register(s0);

/////////////////////////////////////////
//            Declarations             //
/////////////////////////////////////////

struct VertexShaderInput
{
    float4 Position : POSITION;
    float2 TextureUV : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_Position;
    float2 TextureUV : TEXCOORD0;
};

/////////////////////////////////////////
//              Methods                //
/////////////////////////////////////////

/////////////////////////////////////////
//        Shader Entry Points          //
/////////////////////////////////////////

//float3 pixelShader(VertexShaderOutput In) : SV_TARGET
//{
//    return float3(1.0f, 1.0f, In.TextureUV.x);
//}

VertexShaderOutput vertexShader(VertexShaderInput In)
{
    VertexShaderOutput Out;
    Out.Position = In.Position;
    Out.TextureUV = In.TextureUV;
    return Out;
}

float3 pixelShader(VertexShaderOutput In) : SV_TARGET
{
    //return float3(In.Position.x / 1920, In.Position.y / 1080, 0.0f);
    return ShaderTexture.Sample(Sampler, In.TextureUV);
}