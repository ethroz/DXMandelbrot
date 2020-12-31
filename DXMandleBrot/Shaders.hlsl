//————————————————————————————–
// Constant Buffer Variables
//————————————————————————————–

cbuffer cbShaderParameters : register(b0)
{
	double2 Pan;
	float3 Color;
	int Itterations;
	double Zoom;
	float Width;
	float Height;
	int SampleCount;
};

//————————————————————————————–
// Vertex Shader
//————————————————————————————–

float4 vertexShader(float4 position : POSITION) : SV_POSITION
{
	return position;
}

//————————————————————————————–
// Pixel Shader
//————————————————————————————–

float3 pixelShader(float4 position : SV_POSITION) : SV_TARGET
{
	float3 color = float3(0.0f, 0.0f, 0.0f);
	double2 Offset = double2((double)Width / (double)Height / 2.0L, 0.5L);
	for (int y = 0; y < SampleCount; y++)
	{
		for (int x = 0; x < SampleCount; x++)
		{
			double2 C = (double2((position.x + (x / float(SampleCount))) / Height, (position.y + (y / float(SampleCount))) / Height) - Offset) * Zoom + Pan;
			double2 v = C;
			
			int prevItteration = Itterations;
			for (int i = 0; i < prevItteration; i++)
			{
				v = double2((v.x * v.x) - (v.y * v.y), v.x * v.y * 2.0L) + C;
			
				if ((prevItteration == Itterations) && (v.x * v.x + v.y * v.y) > 4.0L)
				{
					//float temp = float(i) / Itterations;
					//color = color + float3(temp, temp, temp);
					float temp = sqrt(float(i) / Itterations);
					color = color + float3( temp / 4, temp / 2, temp);
					prevItteration = i + 1;
				}
			}
		}
	}
	color = color / float(SampleCount * SampleCount);
	return color;
}