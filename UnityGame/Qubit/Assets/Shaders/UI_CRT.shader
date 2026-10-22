// Old-school CRT look for a UI RawImage showing the terminal render texture:
// curved glass, scanlines, aperture-grille mask, phosphor glow, colour fringing,
// flicker, a rolling refresh band, grain, signal jitter, vignette and a plastic bezel.
Shader "UI/CRT"
{
    Properties
    {
        [PerRendererData] _MainTex ("Screen", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Tube)]
        _Curvature ("Curvature", Range(0, 0.5)) = 0.12
        _CornerRadius ("Corner Radius", Range(0, 0.2)) = 0.06
        _Zoom ("Zoom", Range(0.8, 1.2)) = 1.03
        _BezelColor ("Bezel Colour", Color) = (0.07, 0.065, 0.06, 1)

        [Header(Scanlines and Mask)]
        _ScanlineCount ("Scanline Count", Float) = 270
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.55
        _MaskCount ("Mask Triads Across", Float) = 480
        _MaskStrength ("Mask Strength", Range(0, 1)) = 0.3

        [Header(Phosphor)]
        _Bloom ("Glow", Range(0, 3)) = 1.1
        _BloomRadius ("Glow Radius", Range(0, 0.02)) = 0.006
        _Chroma ("Colour Fringing", Range(0, 0.01)) = 0.0025
        _Brightness ("Brightness", Range(0.5, 3)) = 1.35

        [Header(Signal)]
        _Flicker ("Flicker", Range(0, 0.2)) = 0.035
        _RollStrength ("Rolling Band", Range(0, 0.3)) = 0.07
        _RollSpeed ("Rolling Band Speed", Float) = 0.12
        _Noise ("Grain", Range(0, 0.3)) = 0.06
        _Jitter ("Signal Jitter", Range(0, 0.02)) = 0.004

        [Header(Glass)]
        _Vignette ("Vignette", Range(0, 1)) = 0.35
        _Glare ("Glass Glare", Range(0, 0.3)) = 0.06
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Curvature, _CornerRadius, _Zoom;
            fixed4 _BezelColor;
            float _ScanlineCount, _ScanlineStrength, _MaskCount, _MaskStrength;
            float _Bloom, _BloomRadius, _Chroma, _Brightness;
            float _Flicker, _RollStrength, _RollSpeed, _Noise, _Jitter;
            float _Vignette, _Glare;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            // Barrel distortion: pushes the picture outward toward the edges like a curved tube.
            float2 curve(float2 uv)
            {
                float2 c = (uv * 2.0 - 1.0) / _Zoom;
                float2 off = c.yx * c.yx * _Curvature;
                c += c * off;
                return c * 0.5 + 0.5;
            }

            // Signed distance to a rounded rectangle covering 0..1.
            float roundedBox(float2 uv, float r)
            {
                float2 q = abs(uv - 0.5) - (0.5 - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float3 sampleRGB(float2 uv, float2 dir)
            {
                float r = tex2D(_MainTex, uv + dir * _Chroma).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - dir * _Chroma).b;
                return float3(r, g, b);
            }

            float3 glow(float2 uv)
            {
                float3 acc = 0;
                float r = _BloomRadius;
                acc += tex2D(_MainTex, uv + float2( r, 0)).rgb;
                acc += tex2D(_MainTex, uv + float2(-r, 0)).rgb;
                acc += tex2D(_MainTex, uv + float2(0,  r)).rgb;
                acc += tex2D(_MainTex, uv + float2(0, -r)).rgb;
                acc += tex2D(_MainTex, uv + float2( r,  r) * 0.7).rgb;
                acc += tex2D(_MainTex, uv + float2(-r,  r) * 0.7).rgb;
                acc += tex2D(_MainTex, uv + float2( r, -r) * 0.7).rgb;
                acc += tex2D(_MainTex, uv + float2(-r, -r) * 0.7).rgb;
                acc += tex2D(_MainTex, uv + float2( 2*r, 0)).rgb * 0.5;
                acc += tex2D(_MainTex, uv + float2(-2*r, 0)).rgb * 0.5;
                return acc / 9.0;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = curve(i.uv);

                // Occasional horizontal tearing, like a weak signal.
                float tear = step(0.985, hash(float2(floor(t * 12.0), 3.1)));
                uv.x += (sin(uv.y * 90.0 + t * 40.0) * _Jitter) * tear;
                uv.x += (hash(float2(floor(uv.y * _ScanlineCount), floor(t * 30.0))) - 0.5) * _Jitter * 0.25;

                // Outside the tube: plastic bezel with a soft inner shadow.
                float d = roundedBox(uv, _CornerRadius);
                if (d > 0.0)
                {
                    float shade = saturate(1.0 - d * 25.0);
                    return fixed4(_BezelColor.rgb * (0.55 + 0.45 * shade), i.color.a);
                }

                float2 dir = uv - 0.5;
                float3 col = sampleRGB(uv, dir);
                col += glow(uv) * _Bloom;

                // Scanlines: dark gaps between beam lines, a little thinner on bright pixels.
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float s = sin(uv.y * _ScanlineCount * 3.14159265);
                float beam = lerp(1.0, pow(abs(s), 1.0 + (1.0 - saturate(lum)) * 1.5), _ScanlineStrength);
                col *= beam;

                // Aperture grille: vertical red / green / blue phosphor stripes.
                float m = frac(uv.x * _MaskCount);
                float3 mask = m < 0.333 ? float3(1.0, 0.72, 0.72) : (m < 0.666 ? float3(0.72, 1.0, 0.72) : float3(0.72, 0.72, 1.0));
                col *= lerp(float3(1, 1, 1), mask, _MaskStrength);

                // Rolling refresh band, flicker and grain.
                float band = frac(uv.y - t * _RollSpeed);
                col *= 1.0 + _RollStrength * smoothstep(0.0, 0.08, band) * smoothstep(0.2, 0.08, band);
                col *= 1.0 - _Flicker * (0.5 + 0.5 * sin(t * 377.0));
                col += (hash(uv * 480.0 + frac(t * 7.0)) - 0.5) * _Noise;

                // Vignette toward the curved edges, then glass glare from the top-left.
                float vig = pow(saturate(16.0 * uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y)), _Vignette);
                col *= vig;
                float glare = saturate(1.0 - length((uv - float2(0.22, 0.85)) * float2(1.2, 2.2)));
                col += glare * glare * _Glare;

                // Soft edge where the glass meets the bezel.
                float edge = smoothstep(0.0, -0.012, d);
                col *= edge;

                col *= _Brightness;
                return fixed4(col, i.color.a);
            }
            ENDCG
        }
    }
}
