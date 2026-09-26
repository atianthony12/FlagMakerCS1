// The handles the selected flag is moved and turned by (FlagGizmo): arrows and rings in plain
// colours, drawn by the tool in the game's overlay stage -- the same place its own tool overlays
// are drawn (OverlayEffect.OnPostRender, read from the IL: a cleared RGBA target with the scene's
// depth buffer, laid over the finished picture) -- with Graphics.DrawMeshNow, one pass at a time.
//
// Pass 0: the parts behind the city (ZTest Greater), faint, so a handle behind a building is
// never lost. Pass 1: the parts in front, full. A light "headlamp" shade (the normal against the
// eye) makes the arrows and rings read as solid. Colour and alpha "over" the target, alpha kept
// as coverage (One OneMinusSrcAlpha), as the overlay is composited by it. _Color is LINEAR (the
// game's overlays pass Color.linear too, OverlayEffect.DrawCircle's IL).
Shader "RealFlag/FlagGizmo"
{
    Properties
    {
        _Color ("Colour (linear)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        CGINCLUDE
        #include "UnityCG.cginc"

        float4 _Color;

        struct v2f
        {
            float4 pos : SV_POSITION;
            float shade : TEXCOORD0;
        };

        v2f vert(appdata_base v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            float3 n = normalize(UnityObjectToWorldNormal(v.normal));
            float3 eye = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz);
            // Either face alike (Cull Off: the handles are thin, seen from any side).
            o.shade = 0.55 + 0.45 * abs(dot(n, eye));
            return o;
        }
        ENDCG

        Pass
        {
            ZTest Greater

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb * i.shade, _Color.a * 0.3);
            }
            ENDCG
        }

        Pass
        {
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb * i.shade, _Color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
