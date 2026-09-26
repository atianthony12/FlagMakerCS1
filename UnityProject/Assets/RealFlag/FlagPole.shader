// The pole the mod can draw under a flag (FlagPoleMesh): painted metal, a gilt top, braided ropes,
// in the game's own lighting.
//
// A SURFACE shader, as the cloth's is, so that Unity writes it into Cities: Skylines' legacy
// deferred ("prepass") lighting like the game's props: in the G-buffer and the depth buffer, lit
// by the sun with its shadows and by the city's lights at night, casting its own shadow.
//
// The mesh's first uv: x the part (0 the pole, 1 its top, 2 a rope), y a tube's radius (the
// shaft's, a rope's) or 0. A tube's vertices lie ON its axis with the radial direction as their
// normal, and are put out here by the radius -- or by half a pixel's worth of angle times the
// distance (_RFPixelRadius, set by FlagRenderer from the camera) when that is more: a rope a
// centimetre thick is a steady thin line at 300 m instead of a flicker, and a pole never
// vanishes into the gaps between the pixels.
//
// Blinn-Phong in both paths: a satin paint on the pole, a gilt top (dark diffuse, bright
// specular in its own colour, as metals are), matte rope. In prepass the light buffer's alpha
// is the specular (the power from o.Specular, written into the G-buffer), tinted here per part.
//
// Per flag, in a MaterialPropertyBlock: _RFPoleColor, _RFTopColor, _RFRopeColor (LINEAR),
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
            float part;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.part = v.texcoord.x;

            // A tube: out from its axis by its radius, or by half a pixel's worth.
            float radius = v.texcoord.y;
            float3 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
            float dist = distance(_WorldSpaceCameraPos, world);
            float r = max(radius, dist * _RFPixelRadius) * step(1e-6, radius);
            v.vertex.xyz += v.normal * r;
        }

        void surf(Input IN, inout SurfaceOutputPole o)
        {
            float top = step(0.5, IN.part) * step(IN.part, 1.5);
            float rope = step(1.5, IN.part);
            float pole = 1.0 - top - rope;

            // Gilt: a dark body colour and a bright specular in the metal's own colour.
            o.Albedo = _RFPoleColor.rgb * pole + _RFTopColor.rgb * (0.35 * top) + _RFRopeColor.rgb * rope;
            o.SpecTint = float3(0.35, 0.35, 0.35) * pole + _RFTopColor.rgb * (1.6 * top) + float3(0.04, 0.04, 0.04) * rope;
            o.Specular = 0.35 * pole + 0.7 * top + 0.08 * rope;
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
