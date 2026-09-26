// The flag's design, drawn from its three colours: what FlagDesign.cs's FlagPattern values
// mean. uv.x runs from the hoist (0) to the fly (1), uv.y from the top (0) to the bottom (1);
// the back of the cloth shows the same design mirrored, as a printed flag does, because it IS
// the same cloth. Distances are in HOIST units (the flag's height = 1; its length = 1 / aspect),
// so the parts keep their shape at any proportion, and every edge is anti-aliased over the
// pixel's own footprint (fwidth), so a flag far off does not crawl.
//
// The patterns are a row of separate `if`s, NOT an else-if ladder: Unity 5.6's surface shader
// analyser parses a 15-deep else-if chain as 15 nested statements and gives up ("Parser stack
// overflow"), after which Unity silently draws the shader as a one-pass vertex-colour stand-in
// (a flat white flag; Assets/Editor/ShaderCheck.cs now catches it).

#ifndef REALFLAG_PATTERN_INCLUDED
#define REALFLAG_PATTERN_INCLUDED

// 1 inside (d < 0), 0 outside, blended over w.
float RFInside(float d, float w)
{
    return saturate(0.5 - d / max(w, 1e-5));
}

// Signed distance to a straight line through p0 with unit normal n.
float RFLine(float2 p, float2 p0, float2 n)
{
    return dot(p - p0, n);
}

// A cross of the given bar width centred on (cx, cy), in hoist units.
float RFCross(float2 p, float cx, float cy, float width)
{
    float h = abs(p.y - cy) - 0.5 * width;
    float v = abs(p.x - cx) - 0.5 * width;
    return min(h, v);
}

// A saltire: the two diagonals of a flag len x 1 -- (0, 0) to (len, 1) and (0, 1) to (len, 0)
// -- bar width measured across the bar.
float RFSaltire(float2 p, float len, float width)
{
    float2 d = float2(len, 1.0) / sqrt(len * len + 1.0);
    float a = abs(p.x * d.y - p.y * d.x);
    float b = abs(p.x * d.y + (p.y - 1.0) * d.x);
    return min(a, b) - 0.5 * width;
}

// A rectangle centred on c with half-sizes h (negative inside).
float RFBox(float2 p, float2 c, float2 h)
{
    float2 q = abs(p - c) - h;
    return max(q.x, q.y);
}

float3 RFPatternColour(float2 uv, float pattern, float aspect, float3 c1, float3 c2, float3 c3)
{
    float len = 1.0 / max(aspect, 0.05);
    float2 p = float2(uv.x * len, uv.y);
    float2 fw = fwidth(p);
    float w = max(max(fw.x, fw.y), 1e-4) * 1.2;
    int k = (int)(pattern + 0.5);

    // 0 plain: the field alone.
    float3 col = c1;

    // 1 horizontal bicolour: 1 over 2.
    if (k == 1)
        col = lerp(c2, c1, RFInside(p.y - 0.5, w));

    // 2 horizontal tricolour: 1, 2, 3 from the top.
    if (k == 2)
        col = lerp(lerp(c3, c2, RFInside(p.y - 2.0 / 3.0, w)), c1, RFInside(p.y - 1.0 / 3.0, w));

    // 3 vertical tricolour: 1, 2, 3 from the hoist.
    if (k == 3)
        col = lerp(lerp(c3, c2, RFInside(p.x - 2.0 * len / 3.0, w)), c1, RFInside(p.x - len / 3.0, w));

    // 4 vertical bicolour.
    if (k == 4)
        col = lerp(c2, c1, RFInside(p.x - 0.5 * len, w));

    // 5 Nordic cross: field 1, cross 2, inner cross 3 (Norway's fimbriation). Denmark's upright
    // stands a little over a third of the way along (12 + 2 of 37).
    if (k == 5)
        col = lerp(lerp(c1, c2, RFInside(RFCross(p, 0.375 * len, 0.5, 0.2), w)), c3, RFInside(RFCross(p, 0.375 * len, 0.5, 0.1), w));

    // 6 centred cross (St George's): field 1, cross 2, inner 3.
    if (k == 6)
        col = lerp(lerp(c1, c2, RFInside(RFCross(p, 0.5 * len, 0.5, 0.2), w)), c3, RFInside(RFCross(p, 0.5 * len, 0.5, 0.1), w));

    // 7 saltire (St Andrew's): field 1, saltire 2, inner 3.
    if (k == 7)
        col = lerp(lerp(c1, c2, RFInside(RFSaltire(p, len, 0.2), w)), c3, RFInside(RFSaltire(p, len, 0.08), w));

    // 8 canton: field 1, the upper hoist quarter 2, a thin border of 3 round it.
    if (k == 8)
    {
        float cantonBox = RFBox(p, float2(0.25 * len, 0.25), float2(0.25 * len, 0.25));
        col = lerp(lerp(c1, c3, RFInside(cantonBox - 0.03, w)), c2, RFInside(cantonBox, w));
    }

    // 9 thirteen stripes, 1 and 2, and a canton of 3 over the top seven. q runs over two
    // stripes (the first, colour 1, is q < 0.5); dq is q's distance from that stripe's middle,
    // wrapped, so the edge at the wrap is as soft as the others.
    if (k == 9)
    {
        float q = frac(p.y * 6.5);
        float dq = abs(frac(q + 0.25) - 0.5);
        float starBox = RFBox(p, float2(0.2 * len, 3.5 / 13.0), float2(0.2 * len, 3.5 / 13.0));
        col = lerp(lerp(c2, c1, RFInside((dq * 2.0 - 0.5) / 13.0, w)), c3, RFInside(starBox, w));
    }

    // 10 disc: field 1, a disc of 2 three fifths of the height across (Japan's), a ring of 3.
    if (k == 10)
    {
        float r = length(p - float2(0.5 * len, 0.5));
        col = lerp(lerp(c1, c3, RFInside(r - 0.33, w)), c2, RFInside(r - 0.3, w));
    }

    // 11 hoist triangle: 1 over 2, and a triangle of 3 from the hoist to half way. Its two
    // edges meet at the apex (len / 2, 1 / 2), outward normals along (1, -len) and (1, len).
    if (k == 11)
    {
        float2 n = normalize(float2(1.0, len));
        float2 apex = float2(0.5 * len, 0.5);
        float tri = max(RFLine(p, apex, float2(n.x, -n.y)), RFLine(p, apex, n));
        col = lerp(lerp(c2, c1, RFInside(p.y - 0.5, w)), c3, RFInside(tri, w));
    }

    // 12 centre band (Spain's 1:2:1): 1 on top, 2 in the middle, 3 below.
    if (k == 12)
        col = lerp(lerp(c3, c2, RFInside(p.y - 0.75, w)), c1, RFInside(p.y - 0.25, w));

    // 13 diagonal band: 1 at the upper hoist, 2 at the lower fly, a band of 3 between them along
    // the diagonal from the lower hoist (0, 1) to the upper fly (len, 0).
    if (k == 13)
    {
        float diag = RFLine(p, float2(0.0, 1.0), normalize(float2(1.0, len)));
        col = lerp(lerp(c2, c1, RFInside(diag, w)), c3, RFInside(abs(diag) - 0.12, w));
    }

    // 14 border: field 1 inside a border of 2 with a thin line of 3 between them.
    if (k == 14)
    {
        float frame = RFBox(p, float2(0.5 * len, 0.5), float2(0.5 * len, 0.5));
        col = lerp(lerp(c2, c3, RFInside(frame + 0.1, w)), c1, RFInside(frame + 0.13, w));
    }

    return col;
}

#endif
