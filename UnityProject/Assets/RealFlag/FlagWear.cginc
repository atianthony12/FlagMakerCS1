// Wear and tear, dirt and holes: what time, weather and worse do to a flag's cloth (FlagSpec.Wear,
// Dirt, Holes; _RFWear in the property block, _RFWearSeed each flag's own scatter). The cloth's
// LOOK only: the cloth itself moves as a new one would.
//
// Everything is worked out in METRES on the cloth (m: along the fly from the hoist, down the
// hoist from its top), so a hole is a hole's size on any flag, and from procedural noise:
// - hashes: Hoskins 2014, "Hash without Sine" (shadertoy 4djSRW, MIT) -- arithmetic only, the
//   same on every GPU (no sin() whose precision differs);
// - value noise with the quintic fade (Perlin 2002, "Improving Noise"), as FlagNoise.cs's, and
//   its sum over octaves (fBm: Musgrave et al., "Texturing and Modeling", 1994);
// - the holes scattered one per jittered cell with the 3 x 3 cells round a point searched, as
//   Worley's cellular texture is (Worley 1996, "A Cellular Texture Basis Function");
// - shapes as signed distances (Quilez's 2-D distance functions), cut out by the shader's own
//   alphatest, in every pass and the shadow caster -- a picture's see-through parts' way.
//
// What it draws, after how real flags wear (flag makers' care notes: the fly end goes first, it
// whips hardest; sun fades the dyes; city grime gathers at the hoist and the lower edge):
// - WORN: the dyes faded towards a pale grey (more at the fly, unevenly), the fly end frayed back
//   (deeper at its corners) with loose threads hanging from it while they are bigger than a
//   pixel, and past 35% strips torn in from it;
// - TORN: the fly ripped away to a wandering line with ragged tongues hanging from it and V gaps
//   between (a pirate's flag), the fly corners bitten off, past half pieces gone from the cloth;
//   threads hang from the torn edges; the hoist's end always stays;
// - DIRTY: a film of grime, thicker in blotches, darker at the hoist (hands, the pole, the
//   halyard) and the lower edge (splashes, where water drips off), and streaks run down the cloth
//   as it hangs (a banner hoisted from its top hangs along its fly);
// - HOLED: bullet holes (9-17 mm, ragged, each with the dark ring a bullet wipes on cloth --
//   forensic science's "bullet wipe"), burns (charred, irregular, a few cm, larger on big flags)
//   and tears (slits gaping open). A hole smaller than a pixel is not cut (it would flicker on
//   and off): its dark rim stays, a speck.
//
// The ifs are flat (no else-if ladder, no loop) for Unity 5.6's surface shader analyser
// (FlagPattern.cginc says why); the 3 x 3 cells are nine calls.

#ifndef REALFLAG_WEAR_INCLUDED
#define REALFLAG_WEAR_INCLUDED

float RFHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float RFHash12(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 RFHash42(float2 p)
{
    float4 p4 = frac(p.xyxy * float4(0.1031, 0.1030, 0.0973, 0.1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

// Value noise, 0..1.
float RFValue1(float x)
{
    float i = floor(x);
    float f = x - i;
    float u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return lerp(RFHash11(i), RFHash11(i + 1.0), u);
}

float RFValue2(float2 p)
{
    float2 i = floor(p);
    float2 f = p - i;
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = RFHash12(i);
    float b = RFHash12(i + float2(1.0, 0.0));
    float c = RFHash12(i + float2(0.0, 1.0));
    float d = RFHash12(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Three octaves, 0..1.
float RFFbm2(float2 p)
{
    return 0.5714 * RFValue2(p) + 0.2857 * RFValue2(p * 2.03 + 17.1) + 0.1429 * RFValue2(p * 4.01 + 41.7);
}

float RFFbm1(float x)
{
    return 0.5714 * RFValue1(x) + 0.2857 * RFValue1(x * 2.03 + 17.1) + 0.1429 * RFValue1(x * 4.01 + 41.7);
}

// One cell's hole, if it has one: the distance to its edge folded into dHole (metres, < 0 inside)
// and its rim's darkness into rim. k: the flag's size against 3 m (burns and tears grow with it).
void RFHoleCell(float2 cell, float2 m, float2 o, float size, float k, float holes, float px, inout float dHole, inout float rim)
{
    float4 h = RFHash42(cell + o);
    if (h.x >= holes * 0.7)
        return;

    float4 h2 = RFHash42(cell + o + 101.0);
    float2 centre = (cell + 0.15 + 0.7 * h.yz) * size;
    // None through the canvas heading the pole runs through.
    if (centre.x < 0.08)
        return;

    float2 p = m - centre;
    float d = 1e3;
    float width = 1.0;      // how big it is across (metres): under a pixel it is not cut
    float rimWidth = 0.01;
    float rimDark = 0.0;

    // A bullet hole (half of them): round, ragged, its dark wipe round it.
    if (h.w < 0.5)
    {
        float r = 0.0045 + 0.004 * h2.x;
        float rough = RFValue2(p / r * 1.5 + h2.yz * 50.0);
        d = length(p) - r * (0.8 + 0.4 * rough);
        width = r;
        rimWidth = 0.35 * r + 0.002;
        rimDark = 0.8;
    }

    // A burn: a few centimetres, its edge irregular, charred round it.
    if (h.w >= 0.5 && h.w < 0.78)
    {
        float r = (0.02 + 0.05 * h2.x) * k;
        float rough = RFFbm2(p / r * 1.2 + h2.yz * 50.0);
        d = length(p) - r * (0.65 + 0.7 * rough);
        width = r;
        rimWidth = 0.6 * r;
        rimDark = 0.95;
    }

    // A tear: a slit gaping open in the middle, its lips ragged.
    if (h.w >= 0.78)
    {
        float hl = (0.04 + 0.12 * h2.x) * k;
        float a = h2.y * 6.2832;
        float2 dir = float2(cos(a), sin(a));
        float along = dot(p, dir);
        float across = dot(p, float2(-dir.y, dir.x));
        across += (RFValue1(along / 0.01 + h2.w * 90.0) - 0.5) * 0.006 * k;
        float gape = (0.004 + 0.01 * h2.z) * k;
        float t = saturate(abs(along) / hl);
        d = max(abs(across) - gape * (1.0 - t * t), abs(along) - hl);
        width = gape;
        rimWidth = 0.004 * k;
        rimDark = 0.3;
    }

    dHole = min(dHole, width > 0.6 * px ? d : 1e3);
    rim = max(rim, rimDark * saturate(1.0 - max(d, 0.0) / rimWidth));
}

// What wear, dirt and holes do at m (metres along the fly, down the hoist) on a cloth size.x long
// and size.y high, px a pixel's size there (metres): albedo (linear) changed in place, sheen
// scaled; the alpha returned (1 cloth, 0 a hole or torn away; the alphatest cuts at 0.5).
// worn: wear, dirt, holes (0..1), and 1 for a flag hoisted from its top. seed: 0..255. tear: 0..1.
float RFWeather(float2 m, float2 size, float px, float4 worn, float seed, float tear, inout float3 albedo, inout float sheen)
{
    float len = size.x;
    float height = size.y;
    float2 o = seed * float2(7.31, 3.17);
    float alpha = 1.0;
    float k = max(1.0, sqrt(len / 3.0));

    float wear = worn.x;
    if (wear > 0.0)
    {
        // Faded: the dyes towards a pale grey -- worked on sqrt (about the sRGB curve) so it
        // fades evenly to the eye -- more towards the fly, in uneven patches.
        float patches = RFValue2(m / max(0.6, 0.3 * height) + o);
        float f = wear * (0.75 + 0.25 * saturate(m.x / len)) * (0.8 + 0.4 * patches);
        float3 s = sqrt(max(albedo, 0.0));
        float grey = dot(s, float3(0.299, 0.587, 0.114));
        s = lerp(s, float3(grey, grey, grey), 0.55 * f);
        s = lerp(s, float3(0.86, 0.85, 0.82), 0.3 * f);
        albedo = s * s;

        // Frayed: the fly end lost to a depth that wanders along it, deeper at its corners.
        float e = len - m.x;
        float across = min(m.y, height - m.y) / max(height, 1e-3);
        float corner = 1.0 - saturate(across * 4.0);
        float depth = pow(wear, 1.5) * (0.07 * len + 0.05) * (0.55 + 0.9 * corner * corner) *
                      (0.3 + 0.7 * RFFbm1(m.y / (0.035 * len) + o.x));
        float dCloth = e - depth;

        // Loose threads (the warp, along the fly) hanging past it, while a pixel holds them.
        float pitch = 0.004 * max(1.0, len / 3.0);
        float thread = m.y / pitch;
        float hang = depth * 0.45 * RFHash12(float2(floor(thread), 7.0) + o);
        float dThread = min(e - (depth - hang), (0.25 - abs(frac(thread) - 0.5)) * pitch);
        if (pitch > 2.0 * px)
            dCloth = max(dCloth, dThread);

        // Torn in strips from the fly, past 35%: a slit in some bands down the hoist, wandering,
        // widest at the edge.
        float band = max(0.12 * height, 0.1);
        float bi = floor(m.y / band);
        float4 t4 = RFHash42(float2(bi, 3.0) + o);
        if (t4.x < saturate((wear - 0.35) * 2.0))
        {
            float tear = (0.1 + 0.25 * t4.z) * len * wear;
            float y = (bi + 0.25 + 0.5 * t4.y) * band + (RFValue1(e / 0.15 + bi * 13.0) - 0.5) * 0.3 * band;
            float open = 1.0 - saturate(e / tear);
            float gape = (0.008 + 0.03 * t4.w) * wear * sqrt(max(1.0, len / 3.0)) * open * open;
            // The slit's own distance (< 0 inside it): the cloth is where it is not.
            dCloth = min(dCloth, max(abs(m.y - y) - gape, e - tear));
        }

        alpha = min(alpha, saturate(0.5 + dCloth / px));
    }

    // TORN (a pirate's flag): the fly ripped away to a wandering line, ragged tongues hanging
    // from it with V-shaped gaps between them, the fly corners bitten off, and past half, pieces
    // gone from the cloth itself. The hoist's end always stays.
    if (tear > 0.0)
    {
        float e = len - m.x;

        // The torn line: up to ~0.7 L in at the most, wandering along the hoist, ragged.
        float deep = tear * (0.12 + 0.43 * tear) * len;
        float tornLine = deep * (0.75 + 0.5 * RFFbm1(m.y / (0.3 * height) + o.x + 3.0));

        // Tongues: bands down the hoist of uneven width (the band coordinate warped by a slow
        // noise), each its own length, pointed or blunt at its end; a few torn off short.
        float band = max(0.09 * height, 0.06);
        float ty = m.y / band + 1.5 * RFValue1(m.y / (0.5 * height) + o.y);
        float ti = floor(ty);
        float tt = ty - ti;
        float4 th = RFHash42(float2(ti, 23.0) + o);
        float reach = deep * (0.35 + 0.65 * th.x) * (th.z < 0.25 * tear ? 0.2 : 1.0);
        float tip = 1.0 - pow(abs(2.0 * tt - 1.0), 1.0 + 2.0 * th.y);
        float dTorn = e - (tornLine - reach * tip);

        // The fly corners bitten off, deeper towards the fly.
        float across = min(m.y, height - m.y);
        float bite = tear * 0.25 * height * smoothstep(0.3 * len, len, m.x) * (0.4 + 0.6 * RFFbm1(m.x / (0.15 * len) + o.y + 7.0));
        dTorn = min(dTorn, across - bite);

        // Pieces gone from the cloth past half: where a broad noise runs high (its distance steep,
        // so the ragged noise below does not break its edge into specks), never near the hoist nor
        // across the tongues' roots (a tongue never left hanging in the air).
        float chunk = RFFbm2(m / (0.3 * height) + o + 31.0);
        float level = 0.88 - 0.16 * saturate((tear - 0.45) / 0.55);
        float dChunk = (level - chunk) * height;
        dChunk = max(dChunk, 0.3 * len - m.x);
        dChunk = max(dChunk, tornLine + 0.12 * height - e);
        dTorn = min(dTorn, tear > 0.45 ? dChunk : 1e3);

        // Every torn edge ragged at a few centimetres, and never into the hoist's end.
        dTorn += (RFValue2(m / (0.015 * k) + o + 13.0) - 0.5) * 0.025 * k;
        dTorn = max(dTorn, 0.15 * len - m.x);

        // Loose threads (the warp, along the fly) hanging from the torn edges, while a pixel holds them.
        float pitch = 0.004 * max(1.0, len / 3.0);
        float thread = m.y / pitch;
        float hang = 0.05 * k * RFHash12(float2(floor(thread), 29.0) + o);
        float dThread = min(dTorn + hang, (0.25 - abs(frac(thread) - 0.5)) * pitch);
        if (pitch > 2.0 * px)
            dTorn = max(dTorn, dThread);

        // The torn edges a little darker: frayed fibres and the grime they catch.
        float edge = saturate(1.0 - max(dTorn, 0.0) / (0.012 * k));
        albedo *= 1.0 - 0.25 * edge;

        alpha = min(alpha, saturate(0.5 + dTorn / px));
    }

    float dirt = worn.y;
    if (dirt > 0.0)
    {
        // As it hangs: a banner hoisted from its top hangs along its fly, its lower edge the fly's.
        float top = step(0.5, worn.w);
        float span = lerp(height, len, top);
        float down = lerp(m.y, m.x, top);
        float side = lerp(m.x, m.y, top);
        float bottom = span - down;

        float g = 0.3 + 0.7 * smoothstep(0.35, 0.75, RFFbm2(m / 0.4 + o + 11.0));
        g += 0.9 * exp(-m.x / 0.2);
        g += 0.7 * exp(-bottom / max(0.15, 0.06 * span));
        // Streaks: thin runs down the cloth (noise fast across, slow down), in clusters where
        // water gathered (a broad noise across), longer and fainter as they run.
        float streak = RFValue2(float2(side / 0.02, down / 0.8) + o + 5.0);
        float gather = smoothstep(0.45, 0.8, RFValue1(side / 0.3 + o.y + 9.0));
        g += 0.9 * smoothstep(0.7, 0.95, streak) * gather * saturate(down / span + 0.2);
        g = saturate(g * dirt);

        // Soot and road dust: the colours dulled and browned.
        float3 dirty = albedo * float3(0.42, 0.37, 0.30) + float3(0.05, 0.042, 0.03);
        albedo = lerp(albedo, dirty, g);
        sheen *= 1.0 - 0.7 * g;
    }

    float holes = worn.z;
    if (holes > 0.0)
    {
        float size3 = 0.3 * k;
        float2 base = floor(m / size3);
        float dHole = 1e3;
        float rim = 0.0;
        RFHoleCell(base + float2(-1.0, -1.0), m, o, size3, k, holes, px, dHole, rim);
        RFHoleCell(base + float2(0.0, -1.0), m, o, size3, k, holes, px, dHole, rim);
        RFHoleCell(base + float2(1.0, -1.0), m, o, size3, k, holes, px, dHole, rim);
        RFHoleCell(base + float2(-1.0, 0.0), m, o, size3, k, holes, px, dHole, rim);
        RFHoleCell(base, m, o, size3, k, holes, px, dHole, rim);
        RFHoleCell(base + float2(1.0, 0.0), m, o, size3, k, holes, px, dHole, rim);
        RFHoleCell(base + float2(-1.0, 1.0), m, o, size3, k, holes, px, dHole, rim);
        RFHoleCell(base + float2(0.0, 1.0), m, o, size3, k, holes, px, dHole, rim);
        RFHoleCell(base + float2(1.0, 1.0), m, o, size3, k, holes, px, dHole, rim);

        albedo = lerp(albedo, float3(0.025, 0.018, 0.012), rim);
        sheen *= 1.0 - rim;
        alpha = min(alpha, saturate(0.5 + dHole / px));
    }

    return alpha;
}

#endif
