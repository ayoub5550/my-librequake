Shader "LQ/Liquid" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Alpha ("Alpha", Range(0,1)) = 0.65
        _Brightness ("Brightness", Range(0.5, 3)) = 1.2
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) float4 vertex : SV_POSITION; };
            sampler2D _MainTex; float4 _MainTex_ST; float _Alpha; float _Brightness;
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                // Quake turbulent warp
                float2 uv = i.uv;
                uv.x += sin((i.uv.y * 8.0 + _Time.y * 1.5)) * 0.03;
                uv.y += sin((i.uv.x * 8.0 + _Time.y * 1.5)) * 0.03;
                fixed4 tex = tex2D(_MainTex, uv);
                fixed4 col = fixed4(tex.rgb * _Brightness * max(i.color.rgb * 2, 0.6), _Alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
