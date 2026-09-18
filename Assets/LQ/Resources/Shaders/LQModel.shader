Shader "LQ/Model" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Ambient ("Ambient", Range(0,1)) = 0.35
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Pass {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; fixed3 light : COLOR; UNITY_FOG_COORDS(1) float4 vertex : SV_POSITION; };
            sampler2D _MainTex; float4 _MainTex_ST; float _Ambient; fixed4 _Color;
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                // Quake-ish shading: ambient + a fixed key light from above/front + scene ambient
                float3 keyDir = normalize(float3(0.3, 1.0, 0.4));
                float ndl = saturate(dot(n, keyDir)) * 0.55 + 0.45;
                float3 amb = ShadeSH9(float4(n, 1)) + unity_AmbientSky.rgb * 0.5;
                o.light = saturate(_Ambient + ndl * 0.75 + amb * 0.5) * _Color.rgb;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = fixed4(tex.rgb * i.light, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Mobile/VertexLit"
}
