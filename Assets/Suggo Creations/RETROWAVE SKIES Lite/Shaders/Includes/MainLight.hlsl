#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

void CalculateMainLight_float(float3 WorldPos, out float3 Direction, out float3 Color) {
#ifdef SHADERGRAPH_PREVIEW
    Direction = float3(0.5, 0.5, 0);
    Color = 1;
#else
    Light mainLight = GetMainLight(0);
    Direction = mainLight.direction;
    Color = mainLight.color;
#endif
}

#endif