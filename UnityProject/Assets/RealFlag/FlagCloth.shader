// The flag's cloth: a two-sided sheet of dyed fabric, drawn in the game's own lighting.
//
// It is a SURFACE shader so that Unity writes it into Cities: Skylines' legacy deferred
// ("prepass") lighting like the game's props: its normal goes into the G-buffer, the game's
// sun (with its shadows), the moon and -- at night -- the city's own lights, which the game draws
// into the light buffer at CameraEvent.AfterLighting (LightSystem.InitializeProperties, read from
// the IL), all light it; it is in the depth buffer, so the game's fog, depth of field and
// ambient occlusion treat it as solid; and its shadow caster puts its moving shadow on the city.
// A forward shader would get none of that (the explosions' solids, which are forward, are not
// in the depth texture and not lit by street lights).
//
// Cloth, after the documented models:
// - two-sided: the back face is lit as the front seen from behind (its normal flipped by VFACE);
// - diffuse from the light buffer (Lambert), plus a SHEEN -- fabric's soft brightening towards
//   grazing angles. In forward, the "Charlie" sheen distribution (Estevez and Kulla 2017,
//   "Production Friendly Microfacet Sheen BRDF", Sony Pictures Imageworks) with the cloth
//   visibility term of Neubelt and Pettineo 2013 ("Crafting a Next-Gen Material Pipeline for
//   The Order: 1886"); in prepass, where the light buffer carries no light direction, its
//   grazing-angle falloff alone, scaled by the light the cloth receives;
// - light through the cloth: a thin sheet lit from behind glows with its own colour (GPU Gems
//   ch. 16, Green 2004, "Real-Time Approximations to Subsurface Scattering": wrap and
//   translucency). In forward, per light and shadowed; in prepass, the sun's (_RFSunDir,
//   _RFSunColor, set every frame by FlagRenderer from the game's sun) -- unshadowed, the one
//   known compromise;
// - no gloss while it is dry: flag fabric is matte. The first version gave the dry cloth a
//   Blinn-Phong highlight (gloss 0.04, exponent 15), and the prepass multiplied it by the light
//   twice (light.rgb x light.a): under the author's Render It! sun of 2.7 it streaked the folds
//   white -- "making the flag cloth look wet ... Only in rain should this happen";
// - WET in the rain, as the game's ground is: _WeatherParams.w (WeatherManager.EndRenderingImpl
//   sets _WeatherParams = temperature, rain, fog, ground wetness every frame; the wetness rises
//   while it rains and dries over minutes after). The diffuse darkens as Lagarde 2013, "Water
//   drop 3b - Physically based wet surfaces", has it, by lerp(1, 0.2, porosity); the water's film
//   reflects (F0 0.02, IOR 1.33) as a normalised Blinn-Phong lobe ((n + 8) / 8 pi, pi for
//   Unity's lights), n = 8192^gloss (Lazarov 2011, "Physically-based lighting in Call of Duty:
//   Black Ops"), as much of it as the cloth is wet; the sheen's fuzz lies flat under the water.
//   (Lagarde's own gloss for a damp porous surface, 0.2 here, drew a broad veil that paled the
//   colours -- wet cloth looks deeper, not paler: its film is smoother than the knit);
// - the design from three colours (FlagPattern.cginc) -- or a country's flag, its image
//   (_RFImage with _RFUseImage 1; sRGB, so linear once sampled; cut out where the image is
//   transparent, Nepal's two pennants, in every pass and the shadow: alphatest) -- then the
//   canvas heading at the hoist with its two grommets, and the hems; all of it times the
//   flag's _RFBrightness (FlagSpec.Brightness: its Darkness setting, 1 as designed), so the
//   light through the cloth and the sheen darken with it;
// - or a picture of the player's, on the pattern: sampled through _RFImageMap / _RFImageMap2, an
//   affine map from the cloth's uv to the texture's coordinates (FlagCrop: the picture cropped,
//   never stretched, turned, upright on a banner), which a country's flag has as (u, 1 - v).
//   Where the picture is smaller than the flag (zoomed out) the pattern shows round it, its
//   edge anti-aliased over a pixel (fwidth, as the patterns' are); its see-through parts are cut
//   out as a country's are, or (_RFImageThrough 1) the pattern shows through them;
// - worn, dirty, holed (FlagWear.cginc, _RFWear and _RFWearSeed): faded dyes and a frayed fly,
//   grime and rain's streaks, bullet holes, burns and tears cut out by the same alphatest.
//
// A flag drawn finer than its particles (_RFUseGrid 1: the flags big on the screen) takes its
// SHAPE from the particles, not the mesh: FlagMesh hands them over as a float texture (_RFGrid, a
// texel per particle: its position relative to the anchor; _RFGridSize its columns and rows), and
// the vertex program lays each vertex of a finer grid (its uv: where on the flag it is) on the
// Catmull-Rom surface through them -- the 16 particles round its cell (Catmull and Rom 1974), the
// missing ones past an edge reflected in (P(-1) = 2 P(0) - P(1)) -- with the normal and tangent
// from the surface's own derivatives. ClothSurface.cs has the same arithmetic in C#, and the tests
// check it. Every pass runs it, the shadow caster's too. A flag drawn a vertex a particle
// (_RFUseGrid 0, most of a city's) comes with its vertices, normals and tangents as they are.
//
// Per flag, in a MaterialPropertyBlock: _RFColor1..3 (LINEAR), _RFPattern, _RFAspect (height /
// length), _RFLength (metres), _RFHighlight (rgb x a, added: the panel's selection), _RFImage,
// _RFUseImage, _RFImageMap, _RFImageMap2 and _RFImageThrough, _RFBrightness, _RFWear, _RFWearSeed,
// _RFGrid and _RFGridSize.
// `#pragma target 3.5`, never 4.0: Unity 5.6 builds NO Metal code at 4.0 (an empty blob).
Shader "RealFlag/FlagCloth"
{
    Properties
    {
        _RFColor1 ("Colour 1 (linear)", Color) = (0.02, 0.03, 0.13, 1)
        _RFColor2 ("Colour 2 (linear)", Color) = (1, 1, 1, 1)
        _RFColor3 ("Colour 3 (linear)", Color) = (1, 1, 1, 1)
        _RFPattern ("Pattern", Float) = 5
        _RFAspect ("Height / length", Float) = 0.667
        _RFLength ("Length (m)", Float) = 3
        _RFHighlight ("Highlight", Vector) = (0, 0, 0, 0)
        _RFImage ("Country's flag or the player's picture (sRGB)", 2D) = "white" {}
        _RFUseImage ("Draw the image, not the pattern", Float) = 0
        _RFImageMap ("Image: its origin (xy) and along the fly (zw), texture coordinates", Vector) = (0, 1, 1, 0)
        _RFImageMap2 ("Image: down the hoist (xy); it may end inside the flag along s (z), t (w)", Vector) = (0, -1, 0, 0)
        _RFImageThrough ("Image: see-through parts show the pattern (1) or are cut out (0)", Float) = 0
        _RFCutoff ("Image cut-out", Float) = 0.5
        _RFBrightness ("Brightness (linear, 1 as designed)", Float) = 1
        _RFWear ("Wear, dirt, holes (0..1); 1 hoisted from its top", Vector) = (0, 0, 0, 0)
        _RFWearSeed ("Wear's scatter (0..255)", Float) = 0
        _RFTear ("Torn (0..1)", Float) = 0
        _RFGrid ("The cloth's particles (float, point)", 2D) = "black" {}
        _RFGridSize ("Columns, rows, 1 / columns, 1 / rows", Vector) = (2, 2, 0.5, 0.5)
        _RFUseGrid ("Shape from the particles' texture", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Cloth vertex:vert addshadow alphatest:_RFCutoff exclude_path:deferred nolightmap nodynlightmap nodirlightmap nometa noinstancing
        #pragma target 3.5

        #include "FlagPattern.cginc"
        #include "FlagWear.cginc"

        float4 _RFColor1;
        float4 _RFColor2;
        float4 _RFColor3;
        float _RFPattern;
        float _RFAspect;
        float _RFLength;
        float4 _RFHighlight;
        sampler2D _RFImage;
        float _RFUseImage;
        float4 _RFImageMap;
        float4 _RFImageMap2;
        float _RFImageThrough;
        float _RFBrightness;
        float4 _RFWear;
        float _RFWearSeed;
        float _RFTear;
        sampler2D _RFGrid;
        float4 _RFGridSize;
        float _RFUseGrid;

        // Set globally by FlagRenderer (and the preview): towards the sun, and its light (linear).
        float4 _RFSunDir;
        float4 _RFSunColor;

        // Set globally by the game (and the preview): temperature, rain, fog, the ground's wetness.
        float4 _WeatherParams;

        // How much of the light falling on one side comes through the other, for flag cloth
        // (knitted polyester lets a third or so through: the design shows through it against the sky).
        #define TRANSMITTANCE 0.35
        // The sheen's strength and roughness (Estevez and Kulla's alpha).
        #define SHEEN 0.45
        #define SHEEN_ROUGHNESS 0.45
        // Wet (Lagarde 2013): the knit's porosity -- polyester fibres take up almost no water, the
        // gaps between them do -- so soaked it keeps lerp(1, 0.2, 0.5) = 0.6 of its diffuse; and
        // the water film's reflectance at normal incidence.
        #define POROSITY 0.5
        #define WATER_F0 0.02
        // The film's Blinn-Phong exponent: 8192^gloss (Lazarov 2011) at gloss 0.4, a film that
        // follows the knit's weave (water's own is 1.0; a bare knit's, dry, ~0).
        #define FILM_POWER 36.8

        // How wet the cloth is, 0..1: the game's ground (it wets and dries on the game's clock).
        float RFWetness()
        {
            return saturate(_WeatherParams.w);
        }

        struct SurfaceOutputCloth
        {
            fixed3 Albedo;
            fixed3 Normal;
            half3 Emission;
            half Specular;
            fixed Gloss;
            fixed Alpha;
            half Sheen;
        };

        struct Input
        {
            float2 flagUV;
            float3 viewDir;
            float3 worldNormal;
            INTERNAL_DATA
            float face : VFACE;
        };

        // Particle (i, j) of the cloth.
        float3 RFGrid(float i, float j)
        {
            return tex2Dlod(_RFGrid, float4((i + 0.5) * _RFGridSize.z, (j + 0.5) * _RFGridSize.w, 0.0, 0.0)).xyz;
        }

        // Catmull-Rom weights at t of the points i0 - 1 .. i0 + 2 of n, and their derivatives,
        // with the phantom point past either end (the reflection 2 P(0) - P(1)) folded in.
        void RFWeights(float t, float i0, float n, out float4 w, out float4 d)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float4 a = 0.5 * float4(-t3 + 2.0 * t2 - t, 3.0 * t3 - 5.0 * t2 + 2.0, -3.0 * t3 + 4.0 * t2 + t, t3 - t2);
            float4 b = 0.5 * float4(-3.0 * t2 + 4.0 * t - 1.0, 9.0 * t2 - 10.0 * t, -9.0 * t2 + 8.0 * t + 1.0, 3.0 * t2 - 2.0 * t);
            float lo = step(i0, 0.5);
            float hi = step(n - 2.5, i0);
            w = float4(a.x * (1.0 - lo), a.y + 2.0 * a.x * lo - a.w * hi, a.z - a.x * lo + 2.0 * a.w * hi, a.w * (1.0 - hi));
            d = float4(b.x * (1.0 - lo), b.y + 2.0 * b.x * lo - b.w * hi, b.z - b.x * lo + 2.0 * b.w * hi, b.w * (1.0 - hi));
        }

        // One row of the 4 x 4 patch into the position and its two derivatives.
        void RFRow(float j, float wj, float dj, float4 i4, float4 wu, float4 du, inout float3 p, inout float3 pu, inout float3 pv)
        {
            float3 a = RFGrid(i4.x, j);
            float3 b = RFGrid(i4.y, j);
            float3 c = RFGrid(i4.z, j);
            float3 e = RFGrid(i4.w, j);
            float3 row = wu.x * a + wu.y * b + wu.z * c + wu.w * e;
            float3 rowd = du.x * a + du.y * b + du.z * c + du.w * e;
            p += wj * row;
            pu += wj * rowd;
            pv += dj * row;
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.flagUV = v.texcoord.xy;

            // Drawn a vertex a particle: the mesh is the shape.
            if (_RFUseGrid < 0.5)
                return;

            // Where on the cloth, in particles, and the cell it is in (the last cell at the far edges).
            float cols = _RFGridSize.x;
            float rows = _RFGridSize.y;
            float gu = v.texcoord.x * (cols - 1.0);
            float gv = v.texcoord.y * (rows - 1.0);
            float i0 = min(floor(gu), cols - 2.0);
            float j0 = min(floor(gv), rows - 2.0);

            float4 wu;
            float4 du;
            float4 wv;
            float4 dv;
            RFWeights(gu - i0, i0, cols, wu, du);
            RFWeights(gv - j0, j0, rows, wv, dv);

            float4 i4 = float4(max(i0 - 1.0, 0.0), i0, i0 + 1.0, min(i0 + 2.0, cols - 1.0));
            float3 p = float3(0.0, 0.0, 0.0);
            float3 pu = float3(0.0, 0.0, 0.0);
            float3 pv = float3(0.0, 0.0, 0.0);
            RFRow(max(j0 - 1.0, 0.0), wv.x, dv.x, i4, wu, du, p, pu, pv);
            RFRow(j0, wv.y, dv.y, i4, wu, du, p, pu, pv);
            RFRow(j0 + 1.0, wv.z, dv.z, i4, wu, du, p, pu, pv);
            RFRow(min(j0 + 2.0, rows - 1.0), wv.w, dv.w, i4, wu, du, p, pu, pv);

            // The front: along the fly x down the hoist, as FlagCloth.ComputeNormals.
            float3 n = cross(pu, pv);
            v.vertex.xyz = p;
            v.normal = n * rsqrt(max(dot(n, n), 1e-12));
            v.tangent = float4(pu * rsqrt(max(dot(pu, pu), 1e-12)), 1.0);
        }

        // The canvas heading the pole runs through (5 cm, off-white) with a grommet near each
        // corner, and the double-stitched hems of the other three edges: a real flag's make.
        float3 Make(float2 uv, float3 albedo)
        {
            float len = max(_RFLength, 0.1);
            float height = len * max(_RFAspect, 0.05);
            float2 m = float2(uv.x * len, uv.y * height);        // metres from the top of the hoist
            float2 fw = fwidth(m);
            float w = max(max(fw.x, fw.y), 1e-4);

            float heading = RFInside(m.x - 0.05, w);
            albedo = lerp(albedo, float3(0.74, 0.72, 0.66), heading);

            float2 g1 = m - float2(0.025, 0.03);
            float2 g2 = m - float2(0.025, height - 0.03);
            float ring = min(abs(length(g1) - 0.011), abs(length(g2) - 0.011)) - 0.004;
            albedo = lerp(albedo, float3(0.30, 0.29, 0.27), RFInside(ring, w));

            // Hems: a faint stitch line 1.5 cm in from the fly, the top and the bottom.
            float edge = min(len - m.x, min(m.y, height - m.y));
            float stitch = RFInside(abs(edge - 0.015) - 0.0015, w);
            albedo *= 1.0 - 0.18 * stitch * (1.0 - heading);
            return albedo;
        }

        void surf(Input IN, inout SurfaceOutputCloth o)
        {
            float face = IN.face >= 0 ? 1.0 : -1.0;

            // The pattern, or the image: both worked out, one kept (a texture fetch inside a
            // branch would need the branch flattened anyway, for its derivatives). The image where
            // the map puts it (a country's: its rows from the top as the flag's uv.y runs, a
            // texture's v from the bottom -- (u, 1 - v)); where it ends inside the flag, the
            // pattern.
            float3 pattern = RFPatternColour(IN.flagUV, _RFPattern, _RFAspect, _RFColor1.rgb, _RFColor2.rgb, _RFColor3.rgb);
            float2 st = _RFImageMap.xy + IN.flagUV.x * _RFImageMap.zw + IN.flagUV.y * _RFImageMap2.xy;
            float4 image = tex2D(_RFImage, st);
            float2 edge = max(fwidth(st), 1e-5);
            float2 inside2 = saturate(min(st, 1.0 - st) / edge + 0.5);
            float inside = lerp(1.0, inside2.x, _RFImageMap2.z) * lerp(1.0, inside2.y, _RFImageMap2.w);
            float useImage = step(0.5, _RFUseImage) * inside;

            // Its see-through parts: cut out (below, by the alpha), or the pattern through them.
            float over = lerp(1.0, image.a, _RFImageThrough);
            float3 albedo = lerp(pattern, image.rgb, useImage * over);
            albedo = Make(IN.flagUV, albedo);

            // Worn, dirty, holed (FlagWear.cginc): in metres on the cloth, a pixel's size there
            // from the derivatives taken here, outside its branches.
            float2 size = float2(max(_RFLength, 0.1), max(_RFLength, 0.1) * max(_RFAspect, 0.05));
            float2 metres = IN.flagUV * size;
            float2 mw = fwidth(metres);
            float px = max(max(mw.x, mw.y), 1e-4);
            float sheenLeft = 1.0;
            float kept = RFWeather(metres, size, px, _RFWear, _RFWearSeed, _RFTear, albedo, sheenLeft);
            albedo *= saturate(_RFBrightness);

            // Wet: darker (Lagarde's diffuse factor), and the water's film on it reflects -- as
            // much of it as the cloth is wet, the film's own lobe always (a broad lobe at part
            // wetness veils the colours pale: the opposite of how wet cloth looks). None when dry.
            float wet = RFWetness();
            albedo *= lerp(1.0, lerp(1.0, 0.2, POROSITY), wet);

            o.Albedo = albedo;
            o.Normal = float3(0.0, 0.0, face);
            o.Specular = FILM_POWER / 128.0;
            o.Gloss = wet * WATER_F0 * (FILM_POWER + 8.0) / 8.0;
            o.Alpha = min(lerp(1.0, lerp(image.a, 1.0, _RFImageThrough), useImage), kept);

            // Seen side's cosine to the eye (viewDir is in tangent space once o.Normal is written).
            float nv = saturate(abs(normalize(IN.viewDir).z));
            o.Sheen = SHEEN * pow(1.0 - nv, 3.0) * (1.0 - wet) * sheenLeft;

            o.Emission = _RFHighlight.rgb * _RFHighlight.a;

        #if defined(UNITY_PASS_PREPASSFINAL)
            // The sun through the cloth, from behind the side we see.
            float3 n = WorldNormalVector(IN, o.Normal);
            float back = saturate(-dot(n, _RFSunDir.xyz));
            o.Emission += albedo * TRANSMITTANCE * _RFSunColor.rgb * back;
        #endif
        }

        // Legacy deferred: the light buffer holds the diffuse light (sun and moon with their
        // shadows, the city's lights) and, in alpha, the specular; Unity has added the ambient.
        // The specular is a LUMINANCE (Internal-PrePassLighting: spec x Luminance(_LightColor)),
        // so it takes only its hue from the diffuse light: Unity's own BlinnPhong_PrePass
        // multiplies the two, which grows as the square of the light (7.3 x under a sun of 2.7).
        half4 LightingCloth_PrePass(SurfaceOutputCloth s, half4 light)
        {
            half3 c = s.Albedo * light.rgb * (1.0 + s.Sheen);
            half3 hue = light.rgb / max(Luminance(light.rgb), 1e-3);
            c += hue * light.a * s.Gloss;
            return half4(c, s.Alpha);
        }

        // Forward (the previews' forward path; the game draws the prepass): per light, with its
        // shadow in atten.
        half4 LightingCloth(SurfaceOutputCloth s, half3 lightDir, half3 viewDir, half atten)
        {
            half3 n = s.Normal;
            half ndl = dot(n, lightDir);
            half nl = saturate(ndl);
            half nv = saturate(dot(n, viewDir));

            // Charlie sheen D and the Neubelt-Pettineo visibility, times pi (Unity's lights carry no 1/pi).
            half3 h = normalize(lightDir + viewDir);
            half nh = saturate(dot(n, h));
            half sin2 = max(1.0 - nh * nh, 1e-4);
            half inv = 1.0 / SHEEN_ROUGHNESS;
            half d = (2.0 + inv) * pow(sin2, 0.5 * inv) * 0.5;      // (2 + 1/a) sin^(1/a) / (2 pi), x pi
            half vis = 1.0 / (4.0 * (nl + nv - nl * nv) + 1e-4);
            half3 sheenColour = lerp(s.Albedo, half3(1, 1, 1), 0.5) * SHEEN * (1.0 - RFWetness());

            // The water's film when wet (s.Gloss 0 dry), as the prepass's light buffer has it.
            half spec = pow(nh, s.Specular * 128.0) * s.Gloss;

            half3 c = s.Albedo * nl + sheenColour * d * vis * nl + spec * nl;
            c += s.Albedo * TRANSMITTANCE * saturate(-ndl);
            c *= _LightColor0.rgb * atten;
            return half4(c, s.Alpha);
        }
        ENDCG
    }

    Fallback Off
}
