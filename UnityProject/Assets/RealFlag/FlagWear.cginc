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
// - TORN (_RFTear.x): the fly ripped away to a wandering line with ragged tongues of every width
//   and length hanging from it and V gaps between (a pirate's flag), rips running in from it, the
//   fly corners bitten off, past half pieces gone from the cloth; each flag leans its own way (one
//   end of the fly torn deeper, one corner bitten worse);
// - TORN EDGES (_RFTear.y): the top and bottom edges ripped in stretches, deeper towards the fly,
//   with rips running in across the cloth, one edge worse than the other (each flag's own);
//   every torn edge ragged down to a few millimetres (fBm, its octaves faded under a few pixels),
//   threads hanging from it; the hoist's end always stays;
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

// A torn edge's raggedness (metres, about +-0.025 k): fBm from 6 cm down to 4 mm (x k), each octave
// faded out as it nears 3 pixels (finer it would only shimmer), the finest stretched along the
// warp as torn threads lie. Five calls, no loop (ShaderCheck).
float RFRag(float2 m, float k, float px, float2 o)
{
    float s0 = 0.06 * k;
    float r = 0.024 * k * (RFValue2(m / s0 + o + 13.0) - 0.5);
    float s1 = 0.5 * s0;
    r += 0.013 * k * saturate(s1 / (3.0 * px) - 1.0) * (RFValue2(m / s1 + o + 19.0) - 0.5);
    float s2 = 0.25 * s0;
    r += 0.007 * k * saturate(s2 / (3.0 * px) - 1.0) * (RFValue2(m / s2 + o + 29.0) - 0.5);
    float s3 = 0.125 * s0;
    r += 0.004 * k * saturate(s3 / (3.0 * px) - 1.0) * (RFValue2(m / s3 + o + 37.0) - 0.5);
    float s4 = 0.0625 * s0;
    r += 0.0025 * k * saturate(s4 / (3.0 * px) - 1.0) * (RFValue2(float2(m.x / s4, m.y / (0.4 * s4)) + o + 43.0) - 0.5);
    return r;
}

// One edge's tear (top or bottom; across = the distance in from that edge, metres): torn in
// stretches along the fly, deeper towards it, jagged, with rips running in across the cloth.
// f: this edge's share (one edge worse than the other), salt: its own noise. Faded to nothing
// where the fly's tongues hang (fade), so no tongue is cut loose. < 0 where the cloth is gone.
float RFEdgeTear(float2 m, float across, float len, float height, float k, float t, float f, float fade, float2 o, float salt)
{
    float run = smoothstep(0.06 * len, 0.5 * len, m.x);
    float reach = t * (0.1 + 0.15 * t) * height * (0.4 + 0.6 * run) * f * fade;
    float stretch = smoothstep(0.62 - 0.45 * t, 0.78 - 0.3 * t, RFFbm1(m.x / (0.22 * len) + o.x + salt));
    float jag = 0.5 + RFFbm1(m.x / (0.035 * len) + o.y + salt + 2.0) + 0.4 * (RFValue1(m.x / (0.012 * len) + salt + 4.0) - 0.5);
    float depth = min(reach * stretch * jag, 0.3 * height);
    float d = across - depth;

    // Rips across the cloth from the edge: some cells along the fly have one, wandering, open at the edge.
    float cell = max(0.12 * len, 0.1);
    float ci = floor(m.x / cell);
    float4 h = RFHash42(float2(ci, salt) + o);
    if (h.x < 0.6 * t * min(f, 1.0) * fade)
    {
        float x = (ci + 0.2 + 0.6 * h.y) * cell + (RFValue1(across / 0.08 + ci * 5.0 + salt) - 0.5) * 0.3 * cell;
        float rip = min(depth + (0.05 + 0.25 * h.z) * height * t, 0.4 * height);
        float open = 1.0 - saturate(across / max(rip, 1e-3));
        float gape = (0.004 + 0.014 * h.w) * k * open;
        d = min(d, max(abs(m.x - x) - gape, across - rip));
    }
    return d;
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
// worn: wear, dirt, holes (0..1), and 1 for a flag hoisted from its top. seed: 0..255.
// tear: x the fly torn, y the top and bottom edges torn (0..1).
float RFWeather(float2 m, float2 size, float px, float4 worn, float seed, float4 tear, inout float3 albedo, inout float sheen)
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
            float strip = (0.1 + 0.25 * t4.z) * len * wear;
            float y = (bi + 0.25 + 0.5 * t4.y) * band + (RFValue1(e / 0.15 + bi * 13.0) - 0.5) * 0.3 * band;
            float open = 1.0 - saturate(e / strip);
            float gape = (0.008 + 0.03 * t4.w) * wear * sqrt(max(1.0, len / 3.0)) * open * open;
            // The slit's own distance (< 0 inside it): the cloth is where it is not.
            dCloth = min(dCloth, max(abs(m.y - y) - gape, e - strip));
        }

        alpha = min(alpha, saturate(0.5 + dCloth / px));
    }

    // TORN (a pirate's flag): the fly ripped away to a wandering line, ragged tongues hanging
    // from it with V-shaped gaps between them, rips running in, the fly corners bitten off, and
    // past half, pieces gone from the cloth itself (tear.x); the top and bottom edges ripped in
    // stretches (tear.y). The hoist's end always stays.
    if (tear.x > 0.0 || tear.y > 0.0)
    {
        // THE FLY EDGE: how far in from it (metres). The one place the fly's outline comes in --
        // a flag cut to another shape (a swallowtail, a pennant) would measure from its own here.
        float e = len - m.x;

        // This flag's own way of tearing: which end of the fly goes deeper, which corner and which
        // edge suffer most (its scatter seed, so two like flags tear differently).
        float4 fs = RFHash42(o + 61.0);
        float4 fs2 = RFHash42(o + 97.0);
        float pitch = 0.004 * max(1.0, len / 3.0);
        float rag = RFRag(m, k, px, o);

        // A slow warp of the place on the cloth: no torn line nor tongue runs straight. Grown in
        // with the tear, so a little tear does not shave the whole fly.
        float tx = tear.x;
        float2 warp = (float2(RFValue2(m / (0.35 * height) + o + 71.0), RFValue2(m / (0.35 * height) + o + 83.0)) - 0.5) *
                      0.08 * height * saturate(3.0 * tx);
        float2 wm = m + warp;
        float we = e - warp.x;

        // The torn line: up to ~0.7 L in at the most, leaning (one end of the fly deeper),
        // wandering along the hoist at two scales.
        float deep = tx * (0.12 + 0.43 * tx) * len;
        float lean = (fs.x - 0.5) * 1.2;
        float tornLine = deep * (1.0 + lean * (wm.y / height - 0.5)) *
                         (0.7 + 0.6 * RFFbm1(wm.y / (0.25 * height) + o.x + 3.0) + 0.15 * (RFValue1(wm.y / (0.06 * height) + o.x + 9.0) - 0.5));

        float dFly = 1e3;
        if (tx > 0.0)
        {
            // Tongues: bands down the hoist of very uneven width (the band coordinate warped at two
            // scales), each its own length, its point off its middle, sharp or blunt; some split
            // at the end, a few torn off short.
            float band = max(0.08 * height, 0.05);
            float ty = wm.y / band + 2.0 * RFValue1(wm.y / (0.4 * height) + o.y) + 0.7 * RFValue1(wm.y / (0.13 * height) + o.y + 5.0);
            float ti = floor(ty);
            float tt = ty - ti;
            float4 th = RFHash42(float2(ti, 23.0) + o);
            float4 th2 = RFHash42(float2(ti, 47.0) + o);
            float reach = deep * (0.2 + 0.8 * th.x) * (th.z < 0.3 * tx ? 0.15 : 1.0);
            float c = 0.25 + 0.5 * th.w;
            float s = abs(tt - c) / lerp(c, 1.0 - c, step(c, tt));
            float tip = 1.0 - pow(saturate(s), 0.9 + 2.2 * th.y);
            tip *= 1.0 - (th2.x < 0.3 ? 0.7 : 0.0) * saturate(1.0 - s / 0.3);
            dFly = we - (tornLine - reach * tip);

            // Rips running in from the torn line (some bands down the hoist), wandering, open at
            // the line; inside the cloth only (through the tongues they left slivers in the air).
            float sband = max(0.14 * height, 0.08);
            float si = floor(wm.y / sband);
            float4 sh = RFHash42(float2(si, 37.0) + o);
            if (sh.x < 0.55 * tx)
            {
                float sy = (si + 0.2 + 0.6 * sh.y) * sband + (RFValue1(we / 0.1 + si * 11.0) - 0.5) * 0.25 * sband;
                float slen = (0.04 + 0.16 * sh.z) * len * tx;
                float into = we - tornLine;
                float open = 1.0 - saturate(into / slen);
                float gape = (0.004 + 0.012 * sh.w) * k * open;
                dFly = min(dFly, max(max(abs(wm.y - sy) - gape, into - slen), -into));
            }

            // The fly corners bitten off, deeper towards the fly, one worse than the other.
            float bite = tx * 0.25 * height * smoothstep(0.3 * len, len, m.x) * (0.4 + 0.6 * RFFbm1(m.x / (0.15 * len) + o.y + 7.0));
            dFly = min(dFly, min(m.y - bite * (0.3 + 1.2 * fs.y), height - m.y - bite * (0.3 + 1.2 * fs.z)));

            // Pieces gone from the cloth past half: where a broad noise runs high (its distance
            // steep, so the ragged noise does not break its edge into specks), never near the hoist
            // nor across the tongues' roots (a tongue never left hanging in the air).
            float chunk = RFFbm2(m / (0.3 * height) + o + 31.0);
            float level = 0.88 - 0.16 * saturate((tx - 0.45) / 0.55);
            float dChunk = (level - chunk) * height;
            dChunk = max(dChunk, 0.3 * len - m.x);
            dChunk = max(dChunk, tornLine + 0.12 * height - we);
            dFly = min(dFly, tx > 0.45 ? dChunk : 1e3);

            // Ragged, never into the hoist's end; the warp's threads hang from it (along the fly).
            dFly += rag;
            dFly = max(dFly, 0.15 * len - m.x);
            float thread = m.y / pitch;
            float hang = 0.05 * k * RFHash12(float2(floor(thread), 29.0) + o);
            float dThread = min(dFly + hang, (0.25 - abs(frac(thread) - 0.5)) * pitch);
            if (pitch > 2.0 * px)
                dFly = max(dFly, dThread);
        }

        float dEdge = 1e3;
        if (tear.y > 0.0)
        {
            // Faded out where the fly's tongues hang (no tongue cut loose), up to the corner when
            // the fly is whole.
            float fade = saturate((we - tornLine) / max(0.1 * height * saturate(5.0 * tx), 1e-3));
            float fTop = 0.55 + 0.9 * fs.w;
            float fBottom = 0.55 + 0.9 * fs2.x;
            dEdge = min(RFEdgeTear(m, m.y, len, height, k, tear.y, fTop, fade, o, 50.0),
                        RFEdgeTear(m, height - m.y, len, height, k, tear.y, fBottom, fade, o, 80.0));

            // Ragged, never at the hoist; the weft's threads hang from it (across the fly).
            dEdge += rag;
            dEdge = max(dEdge, max(0.06 * len, 0.1) - m.x);
            float thread = m.x / pitch;
            float hang = 0.04 * k * RFHash12(float2(floor(thread), 31.0) + o);
            float dThread = min(dEdge + hang, (0.25 - abs(frac(thread) - 0.5)) * pitch);
            if (pitch > 2.0 * px)
                dEdge = max(dEdge, dThread);
        }

        float dTorn = min(dFly, dEdge);

        // The torn edges a touch darker, unevenly: frayed fibres and the grime they catch.
        float edge = saturate(1.0 - max(dTorn, 0.0) / (0.008 * k));
        albedo *= 1.0 - 0.15 * edge * (0.4 + 1.2 * RFValue2(m / 0.05 + o + 91.0));

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
