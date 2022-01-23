//————————————————————————————–
// Constant Buffer Variables
//————————————————————————————–

cbuffer cbShaderParameters : register(b0)
{
	double2 Pan;
	float3 Color;
	int Iterations;
	double Zoom;
	int Width;
	int Height;
	float ModdedTime;
};

//————————————————————————————–
// Methods
//————————————————————————————–

float rand(float2 p)
{
	float2 k = float2(
		23.14069263277926, // e^pi (Gelfond's constant)
		2.665144142690225 // 2^sqrt(2) (Gelfond–Schneider constant)
		);
	return frac(cos(dot(p, k)) * 12345.6789);
}

float random(float2 p)
{
	return rand(p.xy * (rand(p.xy * ModdedTime) - rand(rand(p.xy * ModdedTime) - ModdedTime)));
}

float InvSqrt(float number)
{
	float threehalfs = 1.5f;

	float x2 = number * 0.5F;
	float y = number;
	int i = asint(y);							// evil floating point bit level hacking
	i = 0x5f3759df - (i >> 1);					// what the fuck? 
	y = asfloat(i);
	y = y * (threehalfs - (x2 * y * y));		// 1st iteration
	//y = y * ( threehalfs - ( x2 * y * y ) );    // 2nd iteration, this can be removed

	return y;
}

float3 HUEtoRGB(float h)
{
	float r = abs(h * 6 - 3) - 1;
	float g = 2 - abs(h * 6 - 2);
	float b = 2 - abs(h * 6 - 4);
	return saturate(float3(r, g, b));
}

float3 HSVtoRGB(float3 HSV)
{
	float3 color = HUEtoRGB(HSV.r);
	return ((color - 1) * HSV.g + 1) * HSV.b;
}

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
	double2 C = (position.xy + Pan) * Zoom;
	double y2 = C.y * C.y;
	double xt = (C.x - 0.25);
	double q = xt * xt + y2;
	if ((q * (q + xt) <= 0.25 * y2) || (C.x + 1) * (C.x + 1) + y2 <= 0.0625)
		return 0.0f;

	double2 v = C;
	for (int i = 0; i < Iterations; i++)
	{
		v = double2(v.x * v.x - v.y * v.y, v.x * v.y * 2) + C;

		if (v.x * v.x + v.y * v.y > 4)
		{
			//float ismooth = i + 1 - log(log(length((float2)v))) / log(2);
			//float temp = frac(ismooth / 150.0f);
			//return HSVtoRGB(float3(temp, 1.0f, 1.0f));
			//float temp = ismooth / Iterations;
			//float temp = sqrt((float)i / Iterations);
			//return float3(temp / 4, temp / 2, temp);// +((random(position.xy) - 0.5f) / 255.0f);
			float NIC = (float)(i + 1.0 - log(log((float)(v.x * v.x + v.y * v.y)) / 2.0f / log(2.0f)) / log(2.0f)) / 20.0f;
			return float3((float)sin(NIC * Color.r), (float)sin(NIC * Color.g), (float)sin(NIC * Color.b));
		}
	}
	return 0.0f;
}