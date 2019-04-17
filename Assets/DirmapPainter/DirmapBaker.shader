Shader "DirmapPainter/DirmapBaker"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
				float4 dir : TANGENT;
            };

            struct v2f
            {
				float4 dir : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.dir.xyz = v.dir.xyz;
				o.dir.xz = -o.dir.xz;
				o.dir.w = 1;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.dir * 0.5f + 0.5f;
            }
            ENDCG
        }
    }
}
