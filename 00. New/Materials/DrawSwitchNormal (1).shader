Shader "Custom/ToonOutlineOnTop"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)

        // Normal map
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0,2)) = 1

        // Toon
        _ShadeSteps ("Shade Steps (1-5)", Range(1,5)) = 4
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.6
        _RimPower ("Rim Power", Range(0.5,8)) = 3.0
        _RimStrength ("Rim Strength", Range(0,1)) = 0.25

        // Outline
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (world)", Range(0,0.02)) = 0.004

        // OnTop Toggle
        _OnTop ("Force On Top (0 off / 1 on)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        // -------- Pass 1: Outline (back-face expand)
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            struct v2f {
                float4 pos : SV_POSITION;
                fixed4 col : COLOR;
            };

            float _OutlineWidth;
            fixed4 _OutlineColor;

            v2f vert(appdata v)
            {
                v2f o;
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float3 offset = n * _OutlineWidth;
                float4 world = mul(unity_ObjectToWorld, v.vertex + float4(offset,0));
                o.pos = UnityWorldToClipPos(world.xyz);
                o.col = _OutlineColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return i.col; }
            ENDCG
        }

        // -------- Pass 2: Toon Lit (normal-mapped)
        Pass
        {
            Name "TOON"
            Tags { "LightMode"="ForwardBase" }
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;

            sampler2D _BumpMap; float4 _BumpMap_ST; float _BumpScale;

            float _ShadeSteps;
            float _ShadowStrength;
            float _RimPower;
            float _RimStrength;

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
                float2 uv      : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                // TBN in world
                float3 wT   : TEXCOORD2;
                float3 wB   : TEXCOORD3;
                float3 wN   : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos  = UnityObjectToClipPos(v.vertex);
                o.uv   = TRANSFORM_TEX(v.uv, _MainTex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;

                float3 t = normalize(UnityObjectToWorldDir(v.tangent.xyz));
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float3 b = normalize(cross(n, t) * v.tangent.w);
                o.wT = t; o.wB = b; o.wN = n;
                return o;
            }

            float3 ApplyNormalMap(float2 uv, float3 wT, float3 wB, float3 wN)
            {
                float3 tn = UnpackNormal(tex2D(_BumpMap, TRANSFORM_TEX(uv, _BumpMap)));
                tn.xy *= _BumpScale;
                tn.z = sqrt(saturate(1.0 - dot(tn.xy, tn.xy)));
                float3 N = normalize(wT * tn.x + wB * tn.y + wN * tn.z);
                return N;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;

                float3 N = ApplyNormalMap(i.uv, i.wT, i.wB, i.wN);

                float3 Ldir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = saturate(dot(N, Ldir));

                float steps = max(1.0, _ShadeSteps);
                float band  = floor(NdotL * steps) / (steps - 1.0);
                band = saturate(band);

                fixed3 litCol = albedo * (_LightColor0.rgb * band * (1.0 - _ShadowStrength) + (1.0 - _ShadowStrength));

                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.wpos);
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower);
                litCol += albedo * rim * _RimStrength;

                return fixed4(litCol, 1);
            }
            ENDCG
        }

        // -------- Pass 3: OnTop Overlay (normal-mapped)
        Pass
        {
            Name "ONTOP_OVERLAY"
            Cull Back
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;

            sampler2D _BumpMap; float4 _BumpMap_ST; float _BumpScale;

            float _OnTop;
            float _ShadeSteps;
            float _ShadowStrength;

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent: TANGENT;
                float2 uv     : TEXCOORD0;
            };
            struct v2f {
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float3 wT   : TEXCOORD1;
                float3 wB   : TEXCOORD2;
                float3 wN   : TEXCOORD3;
            };

            v2f vert(appdata v){
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);

                float3 t = normalize(UnityObjectToWorldDir(v.tangent.xyz));
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float3 b = normalize(cross(n, t) * v.tangent.w);
                o.wT = t; o.wB = b; o.wN = n;
                return o;
            }

            float3 ApplyNormalMap(float2 uv, float3 wT, float3 wB, float3 wN)
            {
                float3 tn = UnpackNormal(tex2D(_BumpMap, TRANSFORM_TEX(uv, _BumpMap)));
                tn.xy *= _BumpScale;
                tn.z = sqrt(saturate(1.0 - dot(tn.xy, tn.xy)));
                return normalize(wT * tn.x + wB * tn.y + wN * tn.z);
            }

            fixed4 frag(v2f i):SV_Target
            {
                clip(_OnTop - 0.5);

                fixed3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;

                float3 N = ApplyNormalMap(i.uv, i.wT, i.wB, i.wN);

                float3 Ldir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = saturate(dot(N, Ldir));
                float steps = max(2.0, _ShadeSteps);
                float band  = floor(NdotL * steps) / (steps - 1.0);
                band = saturate(band);

                fixed3 col = albedo * lerp(1.0 - _ShadowStrength, 1.0, band);
                return fixed4(col, 0.98);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
