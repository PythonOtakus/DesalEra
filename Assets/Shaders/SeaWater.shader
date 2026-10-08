Shader "DesalEra/SeaWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.08, 0.24, 0.30, 0.96)
        _DeepColor ("Deep", Color) = (0.02, 0.07, 0.12, 0.99)
        _HorizonColor ("Horizon", Color) = (0.78, 0.80, 0.82, 1)
        _SkyZenith ("Sky Zenith", Color) = (0.88, 0.89, 0.90, 1)
        _SkyTex ("Sky Panorama", 2D) = "white" {}
        _SkyRotation ("Sky Rotation", Float) = 120
        _FresnelPower ("Fresnel", Range(1, 8)) = 3.2
        _FresnelBias ("Fresnel Bias", Range(0, 0.15)) = 0.01
        _ReflectionStrength ("Reflection", Range(0, 1)) = 1.0
        _ReflectionBlur ("Reflection Blur", Range(0, 8)) = 1.0
        _FoamColor ("Foam", Color) = (0.88, 0.90, 0.91, 0.4)
        _FoamAmount ("Foam", Range(0, 1)) = 0.22
        _ContactFoam ("Contact Foam", Range(0.05, 2)) = 0.9
        _Amplitude ("Amplitude", Float) = 1.05
        _Speed ("Speed", Float) = 1
        _Detail ("Detail Ripple", Range(0, 1)) = 0.8
        _NormalStrength ("Normal Strength", Range(0.2, 2)) = 1.3
        _DistanceFade ("Distance Fade Start", Float) = 80
        _DistanceFadeEnd ("Distance Fade End", Float) = 220
        _Wave0 ("Wave 0", Vector) = (0.95, 0.31, 0.16, 38)
        _Wave1 ("Wave 1", Vector) = (-0.55, 0.84, 0.13, 24)
        _Wave2 ("Wave 2", Vector) = (0.62, -0.78, 0.10, 15)
        _Wave3 ("Wave 3", Vector) = (-0.88, -0.47, 0.07, 9.5)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 300
        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            sampler2D _SkyTex;
            float4 _SkyTex_TexelSize;

            fixed4 _ShallowColor, _DeepColor, _HorizonColor, _SkyZenith, _FoamColor;
            half _FresnelPower, _FresnelBias, _ReflectionStrength, _ReflectionBlur;
            half _FoamAmount, _ContactFoam, _Amplitude, _Speed, _Detail, _NormalStrength;
            half _SkyRotation, _DistanceFade, _DistanceFadeEnd;
            float4 _Wave0, _Wave1, _Wave2, _Wave3;

            struct appdata { float4 vertex : POSITION; };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 seaXZ : TEXCOORD1;
                float height : TEXCOORD2;
                float fade : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                UNITY_FOG_COORDS(5)
            };

            float WaveHeight(float2 xz, float4 wave, float time, float phase, float ampScale)
            {
                float2 d = normalize(wave.xy);
                float steep = saturate(wave.z);
                float lambda = max(wave.w, 0.1);
                float k = 6.2831853 / lambda;
                float c = sqrt(9.8 / k);
                float f = k * (dot(d, xz) - c * time) + phase;
                return (steep / k * _Amplitude * ampScale) * sin(f);
            }

            float SwellHeight(float2 xz, float time)
            {
                float h = 0;
                h += WaveHeight(xz, _Wave0, time, 0.0, 1);
                h += WaveHeight(xz, _Wave1, time, 1.6180339, 1);
                h += WaveHeight(xz, _Wave2, time, 2.7182818, 1);
                h += WaveHeight(xz, _Wave3, time, 0.5772156, 1);
                return h;
            }

            float RippleHeight(float2 xz, float time)
            {
                float h = 0;
                h += WaveHeight(xz, float4(0.82, 0.57, 0.28, 7.5), time, 0.4, 0.4);
                h += WaveHeight(xz, float4(-0.65, 0.76, 0.24, 4.8), time, 1.7, 0.32);
                h += WaveHeight(xz, float4(0.41, -0.91, 0.20, 2.6), time, 2.9, 0.22);
                h += WaveHeight(xz, float4(-0.93, -0.37, 0.16, 1.5), time, 0.9, 0.15);
                return h * _Detail;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float MicroChop(float2 xz, float time)
            {
                float2 p = xz * 0.35 + time * float2(0.05, -0.04);
                return (Noise(p) * 2.0 - 1.0) * 0.03 * _Detail;
            }

            // Detail normal — warps the sky reflection ray only.
            float3 DetailNormal(float2 xz, float time, float detailFade)
            {
                float e = 0.4;
                float h = SwellHeight(xz, time) + (RippleHeight(xz, time) + MicroChop(xz, time)) * detailFade;
                float hx = SwellHeight(xz + float2(e, 0), time)
                         + (RippleHeight(xz + float2(e, 0), time) + MicroChop(xz + float2(e, 0), time)) * detailFade;
                float hz = SwellHeight(xz + float2(0, e), time)
                         + (RippleHeight(xz + float2(0, e), time) + MicroChop(xz + float2(0, e), time)) * detailFade;
                float s = _NormalStrength / e;
                return normalize(float3(-(hx - h) * s, 1.0, -(hz - h) * s));
            }

            float2 SkyUV(float3 dir)
            {
                float3 n = normalize(dir);
                float latitude = acos(clamp(n.y, -1.0, 1.0));
                float longitude = atan2(n.z, n.x);
                float2 uv = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                uv = float2(0.5, 1.0) - uv;
                uv.x = frac(uv.x + _SkyRotation / 360.0);
                return uv;
            }

            half3 SampleSky(float3 dir, float blur)
            {
                float2 uv = SkyUV(dir);
                float2 px = max(_SkyTex_TexelSize.xy, 1.0 / 2048.0) * (1.5 + blur * 3.0);
                half3 c = 0;
                c += tex2Dlod(_SkyTex, float4(uv, 0, 0)).rgb * 0.4;
                c += tex2Dlod(_SkyTex, float4(uv + float2(px.x, 0), 0, 0)).rgb * 0.15;
                c += tex2Dlod(_SkyTex, float4(uv - float2(px.x, 0), 0, 0)).rgb * 0.15;
                c += tex2Dlod(_SkyTex, float4(uv + float2(0, px.y), 0, 0)).rgb * 0.15;
                c += tex2Dlod(_SkyTex, float4(uv - float2(0, px.y), 0, 0)).rgb * 0.15;
                half lum = dot(c, half3(0.33, 0.33, 0.33));
                half3 proc = lerp(_HorizonColor.rgb, _SkyZenith.rgb, saturate(dir.y * 0.5 + 0.35));
                return lum < 0.02 ? proc : c;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float time = _Time.y * _Speed;
                float dist = length(_WorldSpaceCameraPos.xz - world.xz);
                float fade = 1.0 - saturate((dist - _DistanceFade) / max(_DistanceFadeEnd - _DistanceFade, 1));
                fade = fade * fade * (3.0 - 2.0 * fade);

                float h = SwellHeight(world.xz, time);
                float3 displaced = world + float3(0, h, 0);
                o.worldPos = displaced;
                o.seaXZ = world.xz;
                o.height = h;
                o.fade = fade;
                o.pos = UnityWorldToClipPos(float4(displaced, 1));
                o.screenPos = ComputeScreenPos(o.pos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y * _Speed;
                float3 N = DetailNormal(i.seaXZ, time, i.fade);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Fresnel vs flat sea — NOT wave normals (those made slope stripes).
                float ndotvFlat = saturate(dot(float3(0, 1, 0), V));
                float fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - ndotvFlat, _FresnelPower);
                fresnel = saturate(fresnel * _ReflectionStrength);

                float dist = length(_WorldSpaceCameraPos - i.worldPos);
                float depthCue = saturate(dist / 50.0);
                depthCue = depthCue * depthCue * (3.0 - 2.0 * depthCue);

                // Scene depth behind the surface = how much "water volume" to the post/hull.
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                float surfZ = i.screenPos.w;
                float waterColumn = saturate((sceneZ - surfZ) / 3.5);

                // Body: dark teal when looking down. Must stay opaque enough that the
                // skybox cannot bleed through alpha and wash near-water into bright grey.
                fixed3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depthCue * 0.55 + waterColumn * 0.65));
                float form = saturate(dot(N, normalize(float3(0.15, 0.95, 0.2))) * 0.2 + 0.8);
                body *= form;

                // Wavy normal only warps the reflection ray (twisted clouds).
                float3 R = reflect(-V, N);
                R.y = max(R.y, 0.05);
                R = normalize(R);
                float blur = lerp(1.2, _ReflectionBlur, fresnel);
                half3 sky = SampleSky(R, blur);

                // Clear two-layer read: look-down = body, grazing = sky mirror.
                fixed3 waterRgb = lerp(body, sky, fresnel);
                waterRgb = lerp(waterRgb, sky, depthCue * fresnel * 0.35);

                float crest = saturate((i.height / max(_Amplitude, 0.05) - 0.35) * 1.2);
                float foam = crest * (0.4 + Noise(i.seaXZ * 0.18 + time * 0.04));
                foam = smoothstep(0.45, 0.95, foam) * _FoamAmount * (0.3 + i.fade * 0.7);

                float contact = 1.0 - saturate((sceneZ - surfZ) / max(_ContactFoam, 0.05));
                contact = contact * contact * (3.0 - 2.0 * contact);
                // Waterline rides the swell height so posts don't show a dead flat cut.
                float waterlineWobble = saturate(0.55 + i.height * 0.8);
                foam = saturate(foam + contact * 0.8 * waterlineWobble);
                // Soften / darken the column just under the waterline.
                waterRgb = lerp(waterRgb, body * 0.75, contact * waterColumn * 0.35 * (1.0 - fresnel));

                waterRgb = lerp(waterRgb, _FoamColor.rgb, foam * _FoamColor.a * 0.55);

                // High base alpha — transparency only at contacts when looking down.
                float alpha = lerp(0.92, 0.98, fresnel);
                alpha = lerp(alpha, 0.72, contact * ndotvFlat * (1.0 - depthCue));
                alpha = saturate(alpha + depthCue * 0.05);

                fixed4 col = fixed4(waterRgb, alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                // Haze only at real range — don't grey out the mid field.
                float haze = saturate((dist - 55.0) / 150.0);
                haze = haze * haze * (3.0 - 2.0 * haze);
                half3 hazeCol = SampleSky(normalize(float3(V.x, 0.1, V.z)), 3.5);
                col.rgb = lerp(col.rgb, hazeCol, haze * 0.8);
                col.a = lerp(col.a, 1.0, haze * 0.55);
                return col;
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}
