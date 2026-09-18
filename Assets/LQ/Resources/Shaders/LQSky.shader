Shader "LQ/Sky" {
    Properties {
        _MainTex ("Sky (256x128: alpha layer | solid layer)", 2D) = "black" {}
        _Speed ("Scroll speed", Float) = 1
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" }
        LOD 100
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float3 worldPos : TEXCOORD0; float4 vertex : SV_POSITION; };
            sampler2D _MainTex; float _Speed;
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                // classic Quake sky: project view direction onto a plane, two layers scrolling at different speeds
                float3 dir = i.worldPos - _WorldSpaceCameraPos;
                dir.y *= 3.0;
                float len = 6.0 * 63.0 / length(dir);
                float2 p = dir.xz * len;                     // in "quake units" on the sky plane
                float t = _Time.y * _Speed;
                float2 uvBack  = (p + t * 8.0) / 128.0;
                float2 uvFront = (p + t * 16.0) / 128.0;
                // texture is 256x128: left half = front (alpha, palette index 0 = transparent -> black), right half = back
                float2 b = float2(frac(uvBack.x) * 0.5 + 0.5, frac(uvBack.y));
                float2 f = float2(frac(uvFront.x) * 0.5, frac(uvFront.y));
                fixed4 back = tex2D(_MainTex, b);
                fixed4 front = tex2D(_MainTex, f);
                float a = front.a * step(0.02, dot(front.rgb, front.rgb));
                return fixed4(lerp(back.rgb, front.rgb, a), 1);
            }
            ENDCG
        }
    }
}
