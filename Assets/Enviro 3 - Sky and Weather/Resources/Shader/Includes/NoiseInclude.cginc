//#if defined(UNITY_PS5)
 //   #define WORLEY_SAFE 1
//#else
    #define WORLEY_SAFE 0
//#endif



///////////PERLIN NOISE////////////////
//////// NOT USED ANYMORE ////////////

// Hash to get pseudo-random gradient direction
float2 hash2(float2 p)
{
    // A simple 2D hash
    p = frac(p * float2(127.1, 311.7));
    p += dot(p, p + 74.7);
    return normalize(frac(float2(p.x * p.y, p.x + p.y)) * 2.0 - 1.0);
}

// Quintic fade curve for smooth interpolation
float fade(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

float penoise(float2 uv, float scale)
{
    // Scale and wrap UVs to create tileable domain
    float2 p = uv * scale;

    // Integer cell coordinates
    float2 i0 = floor(p);
    float2 f  = frac(p);

    // Wrap to ensure seamless tiling
    float2 i1 = fmod(i0 + 1.0, scale);

    // Gradient vectors for the four cell corners
    float2 g00 = hash2(fmod(i0, scale));
    float2 g10 = hash2(float2(i1.x, i0.y));
    float2 g01 = hash2(float2(i0.x, i1.y));
    float2 g11 = hash2(i1);

    // Distance vectors to corners
    float2 d00 = f - float2(0.0, 0.0);
    float2 d10 = f - float2(1.0, 0.0);
    float2 d01 = f - float2(0.0, 1.0);
    float2 d11 = f - float2(1.0, 1.0);

    // Dot products
    float n00 = dot(g00, d00);
    float n10 = dot(g10, d10);
    float n01 = dot(g01, d01);
    float n11 = dot(g11, d11);

    // Smooth interpolation
    float2 u = fade(f);
    float nx0 = lerp(n00, n10, u.x);
    float nx1 = lerp(n01, n11, u.x);
    float nxy = lerp(nx0, nx1, u.y);

    // Normalize to [0,1]
    return 0.5 + 0.5 * nxy;
} 

float perlinFBM(float2 uv, float baseScale, int octaves, float gain, float lacun)
{
    float total  = 0.0;
    float amp    = 0.5;       // starting amplitude
    float freq   = 1.0;       // starting frequency
    float weight = 0.0;

    // Always wrap UV first to avoid precision drift
    uv = frac(uv);

    [unroll]
    for (int i = 0; i < octaves; i++)
    {
        // Sample tileable perlin at increasing frequency
        // Keep tiling domain proportional to frequency for seamless wrap
        total  += amp * penoise(uv * freq, baseScale * freq);
        weight += amp;

        freq *= lacun; // e.g. 2.0
        amp  *= gain;  // e.g. 0.5
    }

    // Normalize to 0..1
    return saturate(total / weight);
}
//////////////////////////////////////////
///////////WORLEY NOISE///////////////////
//////////////////////////////////////////

#if WORLEY_SAFE

// ================= PS5 SAFE FLOAT HASH =================
float hash21(float2 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float2 hash22(float2 p)
{
    float n = hash21(p);
    return float2(n, hash21(p + 17.0));
}

#else

// ================= ORIGINAL INTEGER HASH =================
float hash21_stable(int2 p, int period)
{
    uint2 pu = (uint2)(p + period * 4096);
    pu = pu % (uint)period;

    uint n = pu.x * 73856093u ^ pu.y * 19349663u;
    return frac((float)n * 0.000000119f);
}

float2 hash22_stable(int2 p, int period)
{
    float h1 = hash21_stable(p, period);
    float h2 = hash21_stable(p + int2(17, 37), period);
    return float2(h1, h2);
}

#endif

#if WORLEY_SAFE

float Worley2D(float2 uv, float tiles)
{
    float2 p = uv * tiles;
    float2 i = floor(p);
    float2 f = frac(p);

    float minDist = 1.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 cell = i + float2(x, y);
            float2 rand = hash22(cell);
            float2 diff = float2(x, y) + rand - f;

            minDist = min(minDist, dot(diff, diff));
        }
    }

    return sqrt(minDist);
}

#else

float Worley2D_Stable(float2 uv, int tileCount)
{
    float2 p = uv * tileCount;
    int2 baseCell = int2(floor(p));
    float2 f = frac(p);

    float minDist = 1.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            int2 neighbor = baseCell + int2(x, y);
            float2 rand = hash22_stable(neighbor, tileCount);
            float2 feature = float2(x, y) + rand;

            float d = length(f - feature);
            minDist = min(minDist, d);
        }
    }
    return saturate(minDist);
}

#endif

float WorleyFBM2D(float2 uv, int baseTiles, float bias)
{
    uv = uv - floor(uv); // safer than frac()

    float n   = 0.0;
    float amp = 0.5;

#if WORLEY_SAFE
    float tiles = (float)baseTiles;

    [unroll]
    for (int octave = 0; octave < 3; octave++)
    {
        n += amp * Worley2D(uv, tiles);
        tiles *= 2.0;
        amp   *= 0.5;
    }
#else
    int tiles = baseTiles;

    [unroll]
    for (int octave = 0; octave < 3; octave++)
    {
        n += amp * Worley2D_Stable(uv, tiles);
        tiles *= 2;
        amp   *= 0.5;
    }
#endif

    n = pow(1.0 - n, bias);
    return saturate(n);
}