Shader "LQ/World" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Range(0.5, 4)) = 2
        _Fullbright ("Fullbright", Range(0,1)) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) float4 vertex : SV_POSITION; };
            sampler2D _MainTex; float4 _MainTex_ST; float _Brightness; float _Fullbright;
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                fixed4 tex = tex2D(_MainTex, i.uv);
                // baked vertex light is stored as 0..1 = 0..2x brightness
                // lift the darkest areas a little (phone screens in daylight) and soften the curve like Quake's
                // overbright lightmaps; keeps fully lit areas unchanged.
                fixed3 baked = saturate(i.color.rgb * _Brightness);
                baked = pow(baked, 0.8) * 0.94 + 0.06;
                fixed3 light = lerp(baked, fixed3(1,1,1), _Fullbright);
                fixed4 col = fixed4(tex.rgb * light, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Mobile/VertexLit"
}
