// The pole the mod can draw under a flag (FlagPoleMesh): painted, matte to glossy (the player's
// Shine), a gilt top, braided ropes, worn, rusted and dirtied as the player sets, in the game's
// own lighting.
//
// A SURFACE shader, as the cloth's is, so that Unity writes it into Cities: Skylines' legacy
// deferred ("prepass") lighting like the game's props: in the G-buffer and the depth buffer, lit
// by the sun with its shadows and by the city's lights at night, casting its own shadow.
//
// The mesh's first uv: x the part (0 the pole, 1 its top, 2 a rope), y a tube's radius (the
// shaft's, a rope's) or 0, z a fluted shaft's flutes, w their depth. A tube's vertices lie ON its
// axis with the radial direction as their normal, and are put out here by the radius -- or by
// half a pixel's worth of angle times the distance (_RFPixelRadius, set by FlagRenderer from the
// camera) when that is more: a rope a centimetre thick is a steady thin line at 300 m instead of
// a flicker, and a pole never vanishes into the gaps between the pixels.
//
// The second uv places the textures: x how far along (tiles), y and z the direction round the
// axis (cosine, sine), w the tiles round. The angle is taken per pixel from the two, and the
// coordinate round is the one of two -- [0, 1) or [-0.5, 0.5) -- whose screen derivative is the
// smaller (Tarini, "Cylindrical and Toroidal Parameterizations Without Vertex Seams", JGT 2012),
// so a texture wraps the pole with no seam and no line of the smallest mip down it. The mesh's
// tangents run round a pole (along a rope's twist), so the normal maps are in tangent space.
//
// Blinn-Phong in both paths: the paint's highlight sharper and brighter as it shines; a gilt top;
// matte rope. In prepass the light buffer's alpha is the specular (the power from o.Specular,
// written into the G-buffer), tinted here.
//
// THE SHINE (_RFPoleWear.w, 0 matte .. 1 a polished clear coat; the author, 2026-11-09, in place
// of the materials): a gloss coat over the paint, as Real Glass's Reflective props' Gloss coat --
// 4% reflected head-on, more at a grazing angle (Schlick 1994; Hoffman 2013's clear coat), the
// paint under it lit as before. What it reflects is the sky (_RFSky: Real Glass's live sky when that
// mod draws one, else the game's environment map; _RFSkyInfo x its mips less one, y its gain, z 1
// when bound -- PoleSky.cs), blurred down its mips as the shine falls, added as emission (in prepass
// the final pass adds it; no reflection in the ForwardAdd lights). Chips, rust and grime take the
// coat away where they are. With Real Glass on, its Reflective finish (RealGlass.Api.Reflective,
// RealGlassFinish.cs) draws the paint's reflection over a plain copy of the pole -- the city in it
// too -- and _RFPoleFinish 1 leaves ours out; the gilt top keeps its own.
//
// Wear (_RFPoleWear: x worn, y rust, z dirt), scattered by _RFPoleSeed (0..255: FlagWear.SeedFor
// its prop) in METRES on the pole (its own frame: y up from the foot), value noise on Hoskins'
// hash (MIT, as FlagWear.cginc's): the paint chipped to grey primer -- more at the foot and on
// the hoist's side, where the halyard rubs -- and, with rust, rusted in its chips, in patches and
// in runs down it; grime up from the foot and in streaks down. Textures (PoleTextures.cs, CC0):
// Poly Haven's rust, ambientCG's rope.
//
// GALVANIZED (_RFGalvanized 1, FlagPoleFinish; 2026-11-19): bare hot-dip zinc in place of the
// paint -- every painted part (the shaft, the truck, a Cap top, a banner's arm), the gilt top
// kept. A metal: a low diffuse and a highlight tinted by zinc's own bluish grey, its sky reflected
// as a metal's (F from zinc's reflectance, Schlick), never a mirror's polish. The Shine runs from
// dull aged zinc to bright new. Its SPANGLE, the zinc's crystal grains: a cellular texture
// (Worley, "A Cellular Texture Basis Function", SIGGRAPH 1996) of grains about 3 cm across in
// METRES on the pole, each a little brighter or duller and its highlight tilted its own way --
// faded out where a pixel covers more than a grain. Worn: patches of pale matte patina (white
// rust) instead of chips to primer; rust and dirt as on paint.
//
// THE CONCRETE BLOCK (part 3: its own mesh, FlagPoleMesh.BuildBlock, drawn with this material and
// the pole's block; 2026-11-19): ambientCG's Concrete016 (CC0), grey, times twice _RFBlockColor;
// its uv metres along each face, its tangents along them (its normal map in tangent space). Out
// of the paint, the coat, the sky's reflection and Real Glass's finish; its vertices carry no
// tube radius, so the vertex program leaves them where they are. Its wear (_RFBlock: x half its
// width, y its height, z its FlagBlockShape, w its wear; positions in its own frame, y up from
// the pole's foot): darker stains, grime up from the ground, streaks down from its top edges, and
// lighter chipped arrises -- the edges and corners, found from the block's own shape.
//
// Per flag, in a MaterialPropertyBlock: _RFPoleColor, _RFTopColor, _RFRopeColor (LINEAR),
// _RFPoleWear, _RFPoleSeed, _RFGalvanized, _RFBlock, _RFBlockColor (LINEAR), the textures,
// _RFHighlight (rgb x a, added: the panel's selection).
// `#pragma target 3.5`, never 4.0: Unity 5.6 builds NO Metal code at 4.0 (an empty blob).
Shader "RealFlag/FlagPole"
{
    Properties
    {
        _RFPoleColor ("Pole (linear)", Color) = (0.78, 0.79, 0.76, 1)
        _RFTopColor ("Top (linear)", Color) = (0.66, 0.43, 0.04, 1)
        _RFRopeColor ("Ropes (linear)", Color) = (0.89, 0.86, 0.79, 1)
        _RFHighlight ("Highlight", Vector) = (0, 0, 0, 0)
        _RFPoleWear ("Worn, rust, dirt, shine", Vector) = (0, 0, 0, 0.35)
        _RFPoleSeed ("Seed", Float) = 0
        _RFPoleFinish ("Real Glass draws the paint's reflection", Float) = 0
        _RFRopeLook ("Rope: 0 a twist, 1 the texture", Float) = 1
        _RFRustTex ("Rust", 2D) = "white" {}
        _RFRustNormal ("Rust normal", 2D) = "bump" {}
        _RFRopeTex ("Rope", 2D) = "white" {}
        _RFRopeNormal ("Rope normal", 2D) = "bump" {}
        _RFGalvanized ("Galvanized", Float) = 0
        _RFBlock ("Block: half width, height, shape, wear", Vector) = (0.4, 0.5, 0, 0)
        _RFBlockColor ("Block (linear)", Color) = (0.39, 0.38, 0.35, 1)
        _RFConcreteTex ("Concrete", 2D) = "gray" {}
        _RFConcreteNormal ("Concrete normal", 2D) = "bump" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Back
        LOD 200

        CGPROGRAM
        #pragma surface surf Pole vertex:vert addshadow exclude_path:deferred nolightmap nodynlightmap nodirlightmap nometa noinstancing
        #pragma target 3.5

        float4 _RFPoleColor;
        float4 _RFTopColor;
        float4 _RFRopeColor;
        float4 _RFHighlight;
        float4 _RFPoleWear;
        float _RFPoleSeed;
        float _RFPoleFinish;
        float _RFRopeLook;
        sampler2D _RFRustTex;
        sampler2D _RFRustNormal;
        sampler2D _RFRopeTex;
        sampler2D _RFRopeNormal;
        float _RFGalvanized;
        float4 _RFBlock;
        float4 _RFBlockColor;
        sampler2D _RFConcreteTex;
        sampler2D _RFConcreteNormal;

        // Set globally by FlagRenderer (and the preview): half a pixel's angle, radians.
        float _RFPixelRadius;

        // The sky a glossy pole reflects (PoleSky.cs): x its mips less one, y its gain, z 1 when bound.
        samplerCUBE _RFSky;
        float4 _RFSkyInfo;

        struct SurfaceOutputPole
        {
            fixed3 Albedo;
            fixed3 Normal;
            half3 Emission;
            half Specular;
            fixed Gloss;
            fixed Alpha;
            half3 SpecTint;
        };

        struct Input
        {
            float4 rfPart;
            float4 rfMap;
            float3 rfObj;
            float3 rfWorld;
            float3 rfWorldNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.rfPart = v.texcoord;
            o.rfMap = v.texcoord1;

            // A tube: out from its axis by its radius, or by half a pixel's worth.
            float radius = v.texcoord.y;
            float3 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
            float dist = distance(_WorldSpaceCameraPos, world);
            float r = max(radius, dist * _RFPixelRadius) * step(1e-6, radius);
            v.vertex.xyz += v.normal * r;
            o.rfObj = v.vertex.xyz;
            o.rfWorld = world + mul((float3x3)unity_ObjectToWorld, v.normal * r);
            o.rfWorldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
        }

        // Hoskins' hash (MIT): 0..1 from a point.
        float RFPoleHash(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        // Value noise, quintic-free (smoothstep) fade, 0..1.
        float RFPoleNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = lerp(RFPoleHash(i), RFPoleHash(i + float3(1, 0, 0)), f.x);
            float b = lerp(RFPoleHash(i + float3(0, 1, 0)), RFPoleHash(i + float3(1, 1, 0)), f.x);
            float c = lerp(RFPoleHash(i + float3(0, 0, 1)), RFPoleHash(i + float3(1, 0, 1)), f.x);
            float d = lerp(RFPoleHash(i + float3(0, 1, 1)), RFPoleHash(i + float3(1, 1, 1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }

        // Three octaves, 0..1.
        float RFPoleFbm(float3 p)
        {
            float n = 0.5714 * RFPoleNoise(p);
            n += 0.2857 * RFPoleNoise(p * 2.03 + 17.1);
            n += 0.1429 * RFPoleNoise(p * 4.01 + 41.7);
            return n;
        }

        // Three octaves spread back over 0..1: summed octaves bunch round a half (a third of the
        // spread of one), so a threshold on them would only ever take a sliver.
        float RFPoleSpread(float3 p)
        {
            return saturate((RFPoleFbm(p) - 0.5) * 2.6 + 0.5);
        }

        // An OpenGL normal map's texel (green up) to a tangent-space normal.
        float3 RFPoleUnpack(float4 t, float strength)
        {
            float2 xy = (t.rg * 2.0 - 1.0) * strength;
            return float3(xy, sqrt(saturate(1.0 - dot(xy, xy))));
        }

        // One cell of the spangle's lattice (Worley 1996): its feature point jittered within its
        // cell; the nearer it is than the best so far, the grain it owns (three values 0..1) taken.
        void RFGrainTap(float3 cell, float3 q, inout float best, inout float3 grain)
        {
            float3 h = float3(RFPoleHash(cell), RFPoleHash(cell + 19.19), RFPoleHash(cell + 47.47));
            float3 d = q - (cell + 0.5 + 0.8 * (h - 0.5));
            float dd = dot(d, d);
            float nearer = step(dd, best);
            best = lerp(best, dd, nearer);
            grain = lerp(grain, h, nearer);
        }

        // The grain at q (lattice units): the nearest feature point among the 2 x 2 x 2 cells round
        // the lattice corner nearest q -- a feature point jittered at most 0.4 from its cell's middle
        // is nearly always among them (a rare miss only moves a grain's edge). Unrolled: no loops.
        float3 RFGrain(float3 q)
        {
            float3 c = floor(q - 0.5);
            float best = 1e6;
            float3 grain = float3(0.5, 0.5, 0.5);
            RFGrainTap(c + float3(0, 0, 0), q, best, grain);
            RFGrainTap(c + float3(1, 0, 0), q, best, grain);
            RFGrainTap(c + float3(0, 1, 0), q, best, grain);
            RFGrainTap(c + float3(1, 1, 0), q, best, grain);
            RFGrainTap(c + float3(0, 0, 1), q, best, grain);
            RFGrainTap(c + float3(1, 0, 1), q, best, grain);
            RFGrainTap(c + float3(0, 1, 1), q, best, grain);
            RFGrainTap(c + float3(1, 1, 1), q, best, grain);
            return grain;
        }

        void surf(Input IN, inout SurfaceOutputPole o)
        {
            float top = step(0.5, IN.rfPart.x) * step(IN.rfPart.x, 1.5);
            float rope = step(1.5, IN.rfPart.x) * step(IN.rfPart.x, 2.5);
            float block = step(2.5, IN.rfPart.x);
            float pole = 1.0 - top - rope - block;
            float galvanized = step(0.5, _RFGalvanized);

            // Zinc's colour (linear: sRGB 0.86 / 0.88 / 0.90, FlagPoleMesh.ZincColour), and its
            // reflectance head-on at full shine (FlagRenderer.ZincReflectivity).
            float3 zinc = float3(0.71, 0.75, 0.79);
            float zincReflectance = 0.5;

            // Round the axis: the angle per pixel, the coordinate round with the smaller derivative (Tarini).
            float turn = atan2(IN.rfMap.z, IN.rfMap.y) * 0.15915494;
            float u1 = frac(turn);
            float u2 = frac(turn + 0.5) - 0.5;
            float u = fwidth(u1) <= fwidth(u2) + 1e-6 ? u1 : u2;
            float2 uv = float2(u * max(IN.rfMap.w, 1.0), IN.rfMap.x);

            float3 p = IN.rfObj + _RFPoleSeed * float3(7.31, 0.0, 3.17);
            float worn = _RFPoleWear.x;
            float rust = _RFPoleWear.y;
            float dirt = _RFPoleWear.z;
            float shine = saturate(_RFPoleWear.w);
            float height = max(IN.rfObj.y, 0.0);

            // The paint's highlight: a power of 8 (matte) to 128 (the G-buffer's most), as bright as
            // the coat is smooth -- at the default shine 0.35 a power of 46 and a tint of 0.355, the
            // satin (45, 0.35) every pole had.
            float3 normal = float3(0, 0, 1);
            float3 albedo = _RFPoleColor.rgb;
            float coat = 1.0;
            float specular = (8.0 + 120.0 * pow(shine, 1.1)) / 128.0;
            float3 specTint = (0.04 + 0.9 * shine).xxx;

            // A fluted shaft's grooves, in the normal: the radius r(a) = R (1 - d (0.5 - 0.5 cos F a)).
            if (IN.rfPart.z > 0.5)
            {
                float a = turn * 6.2831853;
                normal = normalize(float3(0.5 * IN.rfPart.w * IN.rfPart.z * sin(IN.rfPart.z * a), 0, 1));
            }

            // Galvanized: the zinc's grains (the spangle), faded out where a pixel spans more than one.
            float3 grain = float3(0.5, 0.5, 0.5);
            float grainShows = 0.0;
            if (galvanized > 0.5)
            {
                float grainSize = 0.03;
                float3 q = IN.rfObj / grainSize + _RFPoleSeed * float3(1.7, 0.0, 2.3);
                grain = RFGrain(q);
                grainShows = saturate(2.0 - length(fwidth(IN.rfObj)) / grainSize);
                float s = (grain.x - 0.5) * grainShows;

                // A metal: the highlight in zinc's grey; dull aged zinc scatters more of its light
                // (a pale matte grey), bright new zinc less and reflects more.
                albedo = zinc * lerp(0.6, 0.36, shine) * (1.0 + 0.08 * s);
                specTint = zinc * lerp(0.12, 0.75, shine) * (1.0 + 0.4 * s);
                specular = lerp(16.0, 96.0, shine) / 128.0;

                // Each grain's facet tilted its own way, a little: its highlight on or off as the view moves.
                normal = normalize(normal + float3(grain.y - 0.5, grain.z - 0.5, 0.0) * (0.05 * grainShows));
            }

            // The paint: worn, chipped to grey primer; rusted in its chips, in patches, in runs down.
            // Galvanized: worn to a pale matte patina (zinc's white rust) in patches, never chipped.
            {
                float rub = 0.12 * smoothstep(0.6, 1.0, IN.rfMap.y) + 0.3 * exp(-height / 0.5);
                float chipped = 0.0;
                if (galvanized > 0.5)
                {
                    float spots = RFPoleSpread(p * 3.0 + 5.0) * 0.6 + RFPoleSpread(p * 14.0 + 9.0) * 0.4 + 0.5 * rub;
                    float patina = worn > 0.0 ? smoothstep(1.0 - 0.55 * worn, 1.08 - 0.55 * worn, spots) : 0.0;
                    albedo = lerp(albedo, float3(0.42, 0.43, 0.42), patina);
                    specTint = lerp(specTint, float3(0.03, 0.03, 0.03), patina) * (1.0 - 0.4 * worn);
                    specular = lerp(specular, 0.06, patina);
                    normal = normalize(lerp(normal, float3(0, 0, 1), patina));
                    coat *= 1.0 - patina;
                }
                else
                {
                    // Small chips (a few centimetres), gathered where a broader field says (knocks come in clusters).
                    float chips = RFPoleSpread(p * 35.0) * 0.65 + RFPoleSpread(p * 5.0 + 23.0) * 0.35 + rub;
                    chipped = worn > 0.0 ? smoothstep(1.0 - 0.4 * worn, 1.02 - 0.4 * worn, chips) : 0.0;
                    float rim = worn > 0.0 ? smoothstep(0.975 - 0.4 * worn, 1.0 - 0.4 * worn, chips) - chipped : 0.0;
                    albedo = lerp(albedo * (1.0 - 0.35 * rim), float3(0.16, 0.16, 0.155), chipped);
                    specTint = lerp(specTint, float3(0.1, 0.1, 0.1), chipped);
                    specular = lerp(specular, 0.15, chipped);
                    coat *= 1.0 - chipped;
                }

                float patches = smoothstep(1.0 - 0.45 * rust, 1.06 - 0.45 * rust, RFPoleSpread(p * 4.0 + 11.0) * 0.7 + RFPoleSpread(p * 20.0 + 3.0) * 0.3);
                float runs = smoothstep(0.62, 0.9, RFPoleNoise(float3(p.x * 45.0, p.y * 0.7, p.z * 45.0))) * rust * 0.8;
                float rusted = rust > 0.0 ? saturate(max(max(chipped * smoothstep(0.0, 0.3, rust), patches), runs)) : 0.0;
                if (rusted > 0.0)
                {
                    float4 rt = tex2D(_RFRustTex, uv * 1.5);
                    float3 rn = RFPoleUnpack(tex2D(_RFRustNormal, uv * 1.5), 1.0);
                    albedo = lerp(albedo, rt.rgb, rusted);
                    specTint = lerp(specTint, float3(0.03, 0.025, 0.02), rusted);
                    specular = lerp(specular, 0.08, rusted);
                    normal = normalize(lerp(normal, rn, rusted));
                    coat *= 1.0 - rusted;
                }

                // A worn coat dulls all over, a little: weathered paint loses its gloss before it chips.
                coat *= 1.0 - 0.5 * worn;
            }

            // The gilt top: dark diffuse, a bright specular in its own colour; worn, tarnished.
            float3 topAlbedo = _RFTopColor.rgb * 0.35 * (1.0 - 0.4 * worn);
            float3 topTint = _RFTopColor.rgb * 1.6 * (1.0 - 0.6 * worn);

            // The rope: the texture's braid, or a three-strand twist laid 3.5 diameters a turn.
            float3 ropeAlbedo = _RFRopeColor.rgb;
            float3 ropeNormal = float3(0, 0, 1);
            if (rope > 0.5)
            {
                if (_RFRopeLook > 0.5)
                {
                    // One tile round (the texture is twice as wide as high: twice as many along).
                    float2 ruv = float2(u, IN.rfMap.x * 2.0);
                    ropeAlbedo *= 2.0 * tex2D(_RFRopeTex, ruv).r;
                    ropeNormal = RFPoleUnpack(tex2D(_RFRopeNormal, ruv), 1.0);
                }
                else
                {
                    // Along in rope circumferences; a turn of the lay every 7 r = 1.11 of them.
                    float phase = frac(3.0 * (u + IN.rfMap.x / 1.11));
                    float bump = sin(3.14159265 * phase);
                    float slope = cos(3.14159265 * phase) * 0.7;
                    ropeNormal = normalize(float3(-slope * 0.743, -slope * 0.669, 1.0));
                    ropeAlbedo *= 0.6 + 0.4 * bump;
                }

                ropeAlbedo *= 1.0 - 0.25 * worn;
            }

            // The concrete block: the scan's grey times twice its colour, matte, its own weathering.
            float3 blockAlbedo = float3(0, 0, 0);
            float3 blockNormal = float3(0, 0, 1);
            float blockSpec = 0.0;
            if (block > 0.5)
            {
                float4 ct = tex2D(_RFConcreteTex, IN.rfMap.xy);
                blockNormal = RFPoleUnpack(tex2D(_RFConcreteNormal, IN.rfMap.xy), 1.0);
                blockAlbedo = _RFBlockColor.rgb * 2.0 * ct.r;
                blockSpec = 0.05 * (1.0 - ct.a);

                // Where on the block: in from its nearest side, from its upright arrises (a round one
                // has none), down from its top -- the nearest arris of all.
                float3 q = IN.rfObj;
                float halfWidth = _RFBlock.x, tall = _RFBlock.y, wear = _RFBlock.w;
                float isRound = step(1.5, _RFBlock.z);
                float ax = halfWidth - abs(q.x), az = halfWidth - abs(q.z);
                float side = max(lerp(min(ax, az), halfWidth - length(q.xz), isRound), 0.0);
                float upright = lerp(ax + az, 1000.0, isRound);
                float fromTop = max(tall - q.y, 0.0);
                float arris = min(upright, fromTop + side);
                float3 bp = q + _RFPoleSeed * float3(5.3, 0.0, 2.9);
                float sideways = 1.0 - abs(normalize(IN.rfWorldNormal).y);

                // Darker stains in broad patches; grime up from the ground (and all of it below);
                // streaks down from the top edges, washed off it, on the sides.
                float stain = smoothstep(0.35, 0.85, RFPoleSpread(bp * 1.8 + 7.0)) * (0.6 + 0.4 * RFPoleSpread(bp * 9.0)) * wear;
                float soil = saturate(exp(-max(q.y, 0.0) / 0.18) * (0.75 + 0.5 * RFPoleSpread(bp * 6.0 + 3.0))) * wear;
                float streak = smoothstep(0.45, 0.8, RFPoleNoise(float3(bp.x * 16.0, bp.y * 1.1, bp.z * 16.0))) *
                               exp(-fromTop / max(0.7 * tall, 0.2)) * sideways * wear;

                // Chipped arrises: knocked off along the edges and corners, a few centimetres in, the
                // fresh concrete under the weathered skin lighter (a rounded edge chips less).
                float reach = 0.05 * wear * (0.3 + 0.7 * RFPoleSpread(bp * 18.0)) * lerp(1.0, 0.5, step(0.5, _RFBlock.z));
                float chip = wear > 0.0 ? 1.0 - smoothstep(0.55 * reach, reach, arris) : 0.0;

                blockAlbedo *= 1.0 - 0.45 * stain;
                blockAlbedo = lerp(blockAlbedo, blockAlbedo * float3(0.42, 0.4, 0.33), 0.85 * soil);
                blockAlbedo *= 1.0 - 0.5 * streak;
                blockAlbedo = lerp(blockAlbedo, saturate(blockAlbedo * 1.25 + 0.08), chip);
                blockNormal = normalize(lerp(blockNormal, float3(0.25 * (RFPoleNoise(bp * 90.0) - 0.5), 0.25 * (RFPoleNoise(bp * 90.0 + 13.0) - 0.5), 1.0), chip));
                blockSpec *= 1.0 - 0.6 * max(soil, streak);
            }

            // Dirt, every part of the pole: a film, grime up from the foot, streaks down.
            float grimeMask = dirt * saturate(0.35 + 0.75 * exp(-height / 1.2) + 0.7 * smoothstep(0.55, 0.9,
                              RFPoleNoise(float3(p.x * 30.0, p.y * 0.8, p.z * 30.0)))) * (0.6 + 0.4 * RFPoleSpread(p * 8.0));
            float3 grime = float3(0.05, 0.045, 0.035);

            o.Albedo = lerp(albedo, grime, grimeMask) * pole + lerp(topAlbedo, grime, 0.5 * grimeMask) * top +
                       lerp(ropeAlbedo, ropeAlbedo * 0.35 + 0.02, 0.8 * dirt) * rope + blockAlbedo * block;
            o.SpecTint = specTint * (1.0 - 0.8 * grimeMask) * pole + topTint * (1.0 - 0.5 * grimeMask) * top + float3(0.04, 0.04, 0.04) * rope +
                         blockSpec.xxx * block;
            o.Specular = specular * pole + 0.7 * top + 0.08 * rope + 0.06 * block;
            o.Normal = normalize(normal * pole + float3(0, 0, 1) * top + ropeNormal * rope + blockNormal * block);
            o.Gloss = 1.0;
            o.Alpha = 1.0;

            // What the coat reflects: the sky along the mirrored view ray, F by Schlick from 4%
            // head-on, blurred down the sky's mips as the shine falls; the gilt top a metal, F its
            // own colour, a little blurred. The geometric normal (a fluted groove's slope in it):
            // the paint's bumps are below the coat.
            float3 reflection = float3(0, 0, 0);
            if (_RFSkyInfo.z > 0.5 && rope < 0.5)
            {
                float3 n = normalize(IN.rfWorldNormal);
                float3 v = normalize(_WorldSpaceCameraPos - IN.rfWorld);
                float3 r = reflect(-v, n);
                float facing = 1.0 - saturate(dot(n, v));
                float facing5 = facing * facing * facing * facing * facing;

                // Real Glass's Reflective finish draws the paint's reflection (the city in it) when
                // _RFPoleFinish is 1 (RealGlassFinish): ours is left out, the gilt top's kept.
                float paintCoat = shine * coat * (1.0 - grimeMask) * (1.0 - saturate(_RFPoleFinish));
                float3 paintF = (0.04 + 0.96 * facing5).xxx * paintCoat;

                // Galvanized: a metal's F, zinc's reflectance and colour head-on (Schlick), less as it
                // dulls (its shine) and where its patina, rust and grime are; never polished.
                float zincF0 = zincReflectance * (0.35 + 0.65 * shine) * (1.0 + 0.4 * (grain.x - 0.5) * grainShows);
                float3 zincF = (zinc * zincF0 + (1.0 - zinc * zincF0) * facing5) * coat * (1.0 - grimeMask) * (1.0 - saturate(_RFPoleFinish));
                paintF = lerp(paintF, zincF, galvanized);

                float3 topF = (_RFTopColor.rgb + (1.0 - _RFTopColor.rgb) * facing5) * 0.8 * (1.0 - 0.6 * worn) * (1.0 - 0.5 * grimeMask);
                float rough = lerp(lerp(1.0 - shine, 1.0 - 0.7 * shine, galvanized), 0.3, top);
                float3 sky = texCUBElod(_RFSky, float4(r, rough * _RFSkyInfo.x)).rgb * _RFSkyInfo.y;
                reflection = sky * (paintF * pole + topF * top);
            }

            o.Emission = reflection + _RFHighlight.rgb * _RFHighlight.a;
        }

        half4 LightingPole_PrePass(SurfaceOutputPole s, half4 light)
        {
            half3 c = s.Albedo * light.rgb + light.rgb * light.a * s.SpecTint;
            return half4(c, s.Alpha);
        }

        half4 LightingPole(SurfaceOutputPole s, half3 lightDir, half3 viewDir, half atten)
        {
            half3 h = normalize(lightDir + viewDir);
            half nl = saturate(dot(s.Normal, lightDir));
            half nh = saturate(dot(s.Normal, h));
            half spec = pow(nh, s.Specular * 128.0) * s.Gloss;
            half3 c = (s.Albedo * nl + s.SpecTint * spec * nl) * _LightColor0.rgb * atten;
            return half4(c, s.Alpha);
        }
        ENDCG
    }

    Fallback Off
}
