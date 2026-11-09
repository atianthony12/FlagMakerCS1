// The pole the mod can draw under a flag (FlagPoleMesh): painted steel, bare aluminium,
// varnished wood or fibreglass, a gilt top, braided ropes, worn, rusted and dirtied as the
// player sets, in the game's own lighting.
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
// Blinn-Phong in both paths: a satin paint, a bare metal (dark diffuse, its specular its own
// colour, as metals are), a varnish, a gel coat; a gilt top; matte rope. In prepass the light
// buffer's alpha is the specular (the power from o.Specular, written into the G-buffer),
// tinted here per material.
//
// Wear (_RFPoleWear: x worn, y rust, z dirt, w the material), scattered by _RFPoleSeed (0..255:
// FlagWear.SeedFor its prop) in METRES on the pole (its own frame: y up from the foot), value
// noise on Hoskins' hash (MIT, as FlagWear.cginc's): painted steel's paint chipped to grey
// primer -- more at the foot and on the hoist's side, where the halyard rubs -- and, with rust,
// rusted in its chips, in patches and in runs down it; aluminium's dull oxide; wood silvered and
// its varnish gone; fibreglass chalky with its fibres showing; grime up from the foot and in
// streaks down. Textures (PoleTextures.cs, CC0): Poly Haven's walnut and rust, ambientCG's rope.
//
// Per flag, in a MaterialPropertyBlock: _RFPoleColor, _RFTopColor, _RFRopeColor (LINEAR),
// _RFPoleWear, _RFPoleSeed, the textures, _RFHighlight (rgb x a, added: the panel's selection).
// `#pragma target 3.5`, never 4.0: Unity 5.6 builds NO Metal code at 4.0 (an empty blob).
Shader "RealFlag/FlagPole"
{
    Properties
    {
        _RFPoleColor ("Pole (linear)", Color) = (0.78, 0.79, 0.76, 1)
        _RFTopColor ("Top (linear)", Color) = (0.66, 0.43, 0.04, 1)
        _RFRopeColor ("Ropes (linear)", Color) = (0.89, 0.86, 0.79, 1)
        _RFHighlight ("Highlight", Vector) = (0, 0, 0, 0)
        _RFPoleWear ("Worn, rust, dirt, material", Vector) = (0, 0, 0, 0)
        _RFPoleSeed ("Seed", Float) = 0
        _RFRopeLook ("Rope: 0 a twist, 1 the texture", Float) = 1
        _RFWoodTex ("Wood", 2D) = "white" {}
        _RFWoodNormal ("Wood normal", 2D) = "bump" {}
        _RFRustTex ("Rust", 2D) = "white" {}
        _RFRustNormal ("Rust normal", 2D) = "bump" {}
        _RFRopeTex ("Rope", 2D) = "white" {}
        _RFRopeNormal ("Rope normal", 2D) = "bump" {}
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
        float _RFRopeLook;
        sampler2D _RFWoodTex;
        sampler2D _RFWoodNormal;
        sampler2D _RFRustTex;
        sampler2D _RFRustNormal;
        sampler2D _RFRopeTex;
        sampler2D _RFRopeNormal;

        // Set globally by FlagRenderer (and the preview): half a pixel's angle, radians.
        float _RFPixelRadius;

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

        void surf(Input IN, inout SurfaceOutputPole o)
        {
            float top = step(0.5, IN.rfPart.x) * step(IN.rfPart.x, 1.5);
            float rope = step(1.5, IN.rfPart.x);
            float pole = 1.0 - top - rope;

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
            float material = _RFPoleWear.w;
            float height = max(IN.rfObj.y, 0.0);

            float3 normal = float3(0, 0, 1);
            float3 albedo = _RFPoleColor.rgb;
            float3 specTint = float3(0.35, 0.35, 0.35);
            float specular = 0.35;

            // A fluted shaft's grooves, in the normal: the radius r(a) = R (1 - d (0.5 - 0.5 cos F a)).
            if (IN.rfPart.z > 0.5)
            {
                float a = turn * 6.2831853;
                normal = normalize(float3(0.5 * IN.rfPart.w * IN.rfPart.z * sin(IN.rfPart.z * a), 0, 1));
            }

            // Painted steel: satin paint; worn, chipped to grey primer; rusted in its chips, in patches, in runs down.
            if (material < 0.5)
            {
                float rub = 0.12 * smoothstep(0.6, 1.0, IN.rfMap.y) + 0.3 * exp(-height / 0.5);
                // Small chips (a few centimetres), gathered where a broader field says (knocks come in clusters).
                float chips = RFPoleSpread(p * 35.0) * 0.65 + RFPoleSpread(p * 5.0 + 23.0) * 0.35 + rub;
                float chipped = worn > 0.0 ? smoothstep(1.0 - 0.4 * worn, 1.02 - 0.4 * worn, chips) : 0.0;
                float rim = worn > 0.0 ? smoothstep(0.975 - 0.4 * worn, 1.0 - 0.4 * worn, chips) - chipped : 0.0;
                albedo = lerp(albedo * (1.0 - 0.35 * rim), float3(0.16, 0.16, 0.155), chipped);
                specTint = lerp(specTint, float3(0.5, 0.5, 0.5), chipped);

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
                }
            }

            // Bare aluminium, brushed along: its colour tints it (anodised); worn, a dull oxide.
            if (material > 0.5 && material < 1.5)
            {
                float brushed = RFPoleNoise(float3(p.x * 300.0, p.y * 3.0, p.z * 300.0)) - 0.5;
                normal = normalize(normal + float3(0.12 * brushed, 0, 0));
                float oxide = worn * smoothstep(0.45, 0.8, RFPoleSpread(p * 6.0 + 5.0) * 0.7 + RFPoleSpread(p * 30.0) * 0.3 + 0.25 * worn);
                albedo = lerp(albedo * 0.45, float3(0.42, 0.42, 0.41), oxide);
                specTint = lerp(_RFPoleColor.rgb * 0.9, float3(0.12, 0.12, 0.12), oxide);
                specular = lerp(0.5, 0.12, oxide);
            }

            // Varnished wood, its grain up the pole: the colour stains it; worn, silvered, the varnish gone, the grain open.
            if (material > 1.5 && material < 2.5)
            {
                float4 wt = tex2D(_RFWoodTex, uv);
                float silver = worn * smoothstep(0.2, 0.7, RFPoleFbm(p * 2.0 + 3.0) * 0.6 + 0.6 * worn);
                float3 wood = wt.rgb * _RFPoleColor.rgb * 1.3;
                float grey = dot(wood, float3(0.2126, 0.7152, 0.0722));
                albedo = lerp(wood, float3(0.16, 0.155, 0.15) + grey * 0.4, silver);
                normal = normalize(normal + RFPoleUnpack(tex2D(_RFWoodNormal, uv), 1.0 + 1.5 * silver) - float3(0, 0, 1));
                specTint = lerp(float3(0.07, 0.065, 0.06), float3(0.01, 0.01, 0.01), saturate(silver + 0.5 * worn)) * (1.2 - wt.a);
                specular = lerp(0.3, 0.06, saturate(silver + 0.5 * worn));
            }

            // Fibreglass: a glossy gel coat; worn, chalky, its fibres showing up it.
            if (material > 2.5)
            {
                float fibres = RFPoleNoise(float3(p.x * 160.0, p.y * 6.0, p.z * 160.0));
                float chalk = worn * (0.6 + 0.4 * RFPoleFbm(p * 5.0 + 8.0));
                // Chalky, yellowed by the sun, the fibres in streaks up it.
                float3 faded = albedo * float3(0.78, 0.74, 0.6) + float3(0.08, 0.075, 0.04) + 0.22 * (fibres - 0.5);
                albedo = lerp(albedo, faded, chalk);
                normal = normalize(normal + float3(0.15 * (fibres - 0.5) * chalk, 0, 0));
                specTint = lerp(float3(0.1, 0.1, 0.1), float3(0.02, 0.02, 0.02), chalk);
                specular = lerp(0.6, 0.15, chalk);
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

            // Dirt, every part: a film, grime up from the foot, streaks down.
            float grimeMask = dirt * saturate(0.35 + 0.75 * exp(-height / 1.2) + 0.7 * smoothstep(0.55, 0.9,
                              RFPoleNoise(float3(p.x * 30.0, p.y * 0.8, p.z * 30.0)))) * (0.6 + 0.4 * RFPoleSpread(p * 8.0));
            float3 grime = float3(0.05, 0.045, 0.035);

            o.Albedo = lerp(albedo, grime, grimeMask) * pole + lerp(topAlbedo, grime, 0.5 * grimeMask) * top +
                       lerp(ropeAlbedo, ropeAlbedo * 0.35 + 0.02, 0.8 * dirt) * rope;
            o.SpecTint = specTint * (1.0 - 0.8 * grimeMask) * pole + topTint * (1.0 - 0.5 * grimeMask) * top + float3(0.04, 0.04, 0.04) * rope;
            o.Specular = specular * pole + 0.7 * top + 0.08 * rope;
            o.Normal = normalize(normal * pole + float3(0, 0, 1) * top + ropeNormal * rope);
            o.Gloss = 1.0;
            o.Alpha = 1.0;
            o.Emission = _RFHighlight.rgb * _RFHighlight.a;
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
