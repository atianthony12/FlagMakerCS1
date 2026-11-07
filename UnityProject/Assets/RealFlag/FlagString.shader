// The flags on a string (StringRenderer): every flag of a span in ONE mesh and one draw, each
// flag's design carried by its own vertices instead of a property block -- the cloth shader's
// look (FlagCloth.shader: two-sided dyed fabric in the game's legacy deferred lighting, sheen,
// light through the cloth, wet in the rain, the patterns of FlagPattern.cginc), drawn per vertex.
//
// Per vertex (StringMesh):
//   uv0 = the cloth (s from the string 0 to the free edge 1, t along the string 0..1) and the
//         design (x hoist to fly, y top to bottom, as FlagPattern.cginc reads them: a flag
//         hanging by its hoist is its design turned a quarter, one hanging by its top upright);
//   uv1 = colour 1 (linear), pattern + 16 x (atlas cell + 1, 0 for none);
//   uv2 = colour 2, the cloth's length from the string (metres; NEGATIVE: hung by its top);
//   uv3 = colour 3, the cloth's length along the string (metres);
//   color = brightness (FlagSpec.Brightness), the image's see-through parts show the pattern (1)
//         or are cut (0), the cut (FlagCut: x / 8), its depth.
// A country's flag or the player's picture is a cell of _RFAtlas (StringAtlas), the design
// already baked into it; a pattern is drawn here, sharp at any distance.
//
// The CUT (swallowtail, pennant, burgee...: the flags' own, FlagWear.cginc's RFCutEdge) is cut
// out by the alphatest along the cloth's own fly (from the string down): a pennant hung by its
// hoist points down. The shared cloths the flags play are cut the same (FlagCloth's cells).
// `#pragma target 3.5`, never 4.0: Unity 5.6 builds NO Metal code at 4.0 (an empty blob).
Shader "RealFlag/FlagString"
{
    Properties
    {
        _RFAtlas ("The designs with an image, a cell each (sRGB)", 2D) = "white" {}
        _RFAtlasGrid ("Atlas: columns, rows, 1 / columns, 1 / rows", Vector) = (16, 16, 0.0625, 0.0625)
        _RFAtlasInset ("Atlas: a cell's inset, its share", Float) = 0.03
        _RFHighlight ("Highlight", Vector) = (0, 0, 0, 0)
        _RFCutoff ("Cut-out", Float) = 0.5
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
        // For RFCutEdge: the flags' own outline (FlagCut), so a string's cuts are the flags'.
        #include "FlagWear.cginc"

        sampler2D _RFAtlas;
        float4 _RFAtlasGrid;
        float _RFAtlasInset;
        float4 _RFHighlight;

        // Set globally by FlagRenderer (and the preview): towards the sun, and its light (linear).
        float4 _RFSunDir;
        float4 _RFSunColor;

        // Set globally by the game: temperature, rain, fog, the ground's wetness.
        float4 _WeatherParams;

        // As FlagCloth.shader's (its notes say where each comes from).
        #define TRANSMITTANCE 0.35
        #define SHEEN 0.45
        #define SHEEN_ROUGHNESS 0.45
        #define POROSITY 0.5
        #define WATER_F0 0.02
        #define FILM_POWER 36.8

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
            float4 flagUV;
            float4 c1p;
            float4 c2h;
            float4 c3a;
            float4 look;
            float3 viewDir;
            float3 worldNormal;
            INTERNAL_DATA
            float face : VFACE;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.flagUV = v.texcoord;
            o.c1p = v.texcoord1;
            o.c2h = v.texcoord2;
            o.c3a = v.texcoord3;
            o.look = v.color;
        }

        // The heading along the string (a 2 cm band, off-white: bunting's tape or a flag's
        // heading) and the double-stitched hems of the other edges.
        float3 Make(float2 m, float hang, float along, float3 albedo)
        {
            float2 fw = fwidth(m);
            float w = max(max(fw.x, fw.y), 1e-4);
            float heading = RFInside(m.x - 0.02, w);
            albedo = lerp(albedo, float3(0.74, 0.72, 0.66), heading);
            float edge = min(hang - m.x, min(m.y, along - m.y));
            float stitch = RFInside(abs(edge - 0.01) - 0.001, w);
            albedo *= 1.0 - 0.15 * stitch * (1.0 - heading);
            return albedo;
        }

        void surf(Input IN, inout SurfaceOutputCloth o)
        {
            float face = IN.face >= 0 ? 1.0 : -1.0;
            float hangSigned = IN.c2h.w;
            float hang = max(abs(hangSigned), 0.02);
            float along = max(IN.c3a.w, 0.02);
            bool top = hangSigned < 0.0;
            // The design's height over its width: hung by its top, the cloth's hang is the design's height.
            float aspect = top ? hang / along : along / hang;

            // "packed" is a reserved word to the surface shader's analyser (HLSL2GLSL): its line went wrong whole.
            float code = IN.c1p.w;
            float cell = floor((code + 0.5) / 16.0);
            float pattern = code - cell * 16.0;

            float2 design = IN.flagUV.zw;
            float3 colour = RFPatternColour(design, pattern, aspect, IN.c1p.rgb, IN.c2h.rgb, IN.c3a.rgb);

            // The atlas's cell (cell - 1), its rows from the top as the design's y runs.
            float index = cell - 1.0;
            float row = floor((index + 0.5) * _RFAtlasGrid.z);
            float col = index - row * _RFAtlasGrid.x;
            float2 inCell = lerp(float2(_RFAtlasInset, _RFAtlasInset), float2(1.0 - _RFAtlasInset, 1.0 - _RFAtlasInset),
                                 float2(design.x, 1.0 - design.y));
            float2 st = float2((col + inCell.x) * _RFAtlasGrid.z, 1.0 - (row + 1.0 - inCell.y) * _RFAtlasGrid.w);
            float4 image = tex2D(_RFAtlas, st);
            float useImage = step(0.5, cell);
            float through = IN.look.y;
            float over = lerp(1.0, image.a, through);
            float3 albedo = lerp(colour, image.rgb, useImage * over);

            float2 metres = float2(IN.flagUV.x * hang, IN.flagUV.y * along);
            albedo = Make(metres, hang, along, albedo);
            albedo *= saturate(IN.look.x);

            float wet = RFWetness();
            albedo *= lerp(1.0, lerp(1.0, 0.2, POROSITY), wet);

            // The cut (FlagCut, the flags' own: RFCutEdge), anti-aliased over a pixel's footprint
            // along the fly (the alphatest cuts at half).
            float2 cut = float2(floor(IN.look.z * 8.0 + 0.5), IN.look.w);
            float inside = RFCutEdge(cut, IN.flagUV.y) * hang - metres.x;
            float px = max(fwidth(metres.x), 1e-4);
            float shapeAlpha = saturate(0.5 + inside / px);

            o.Albedo = albedo;
            o.Normal = float3(0.0, 0.0, face);
            o.Specular = FILM_POWER / 128.0;
            o.Gloss = wet * WATER_F0 * (FILM_POWER + 8.0) / 8.0;
            o.Alpha = min(lerp(1.0, lerp(image.a, 1.0, through), useImage), shapeAlpha);

            float nv = saturate(abs(normalize(IN.viewDir).z));
            o.Sheen = SHEEN * pow(1.0 - nv, 3.0) * (1.0 - wet);

            o.Emission = _RFHighlight.rgb * _RFHighlight.a;

        #if defined(UNITY_PASS_PREPASSFINAL)
            float3 n = WorldNormalVector(IN, o.Normal);
            float back = saturate(-dot(n, _RFSunDir.xyz));
            o.Emission += albedo * TRANSMITTANCE * _RFSunColor.rgb * back;
        #endif
        }

        // As FlagCloth.shader's: the light buffer's diffuse with the sheen, the film's specular a luminance.
        half4 LightingCloth_PrePass(SurfaceOutputCloth s, half4 light)
        {
            half3 c = s.Albedo * light.rgb * (1.0 + s.Sheen);
            half3 hue = light.rgb / max(Luminance(light.rgb), 1e-3);
            c += hue * light.a * s.Gloss;
            return half4(c, s.Alpha);
        }

        half4 LightingCloth(SurfaceOutputCloth s, half3 lightDir, half3 viewDir, half atten)
        {
            half3 n = s.Normal;
            half ndl = dot(n, lightDir);
            half nl = saturate(ndl);
            half nv = saturate(dot(n, viewDir));
            half3 h = normalize(lightDir + viewDir);
            half nh = saturate(dot(n, h));
            half sin2 = max(1.0 - nh * nh, 1e-4);
            half inv = 1.0 / SHEEN_ROUGHNESS;
            half d = (2.0 + inv) * pow(sin2, 0.5 * inv) * 0.5;
            half vis = 1.0 / (4.0 * (nl + nv - nl * nv) + 1e-4);
            half3 sheenColour = lerp(s.Albedo, half3(1, 1, 1), 0.5) * SHEEN * (1.0 - RFWetness());
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
