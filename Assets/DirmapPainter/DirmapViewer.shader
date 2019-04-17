Shader "DirmapPainter/DirmapViewer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		_FlowMap ("FlowMap", 2D) = "bump" {}
    }
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
				float2 uv2 : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
			sampler2D _FlowMap;
			float4 _FlowMap_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.uv2 = TRANSFORM_TEX(v.uv, _FlowMap);
                return o;
            }

			float3 GetPhase()
			{
				float3 flowPhase;
				float2 timePhase = _Time.yy + float2(0.5, 1.0);

				flowPhase.xy = frac(timePhase);				//phase xy
				flowPhase.z = abs(1 - flowPhase.x * 2);		//flow blend
				return flowPhase;
			}

            fixed4 frag (v2f i) : SV_Target
            {
				fixed4 flow = tex2D(_FlowMap, i.uv2) * 2 - 1;

				float3 phase = GetPhase();

				float4 uvFlow = i.uv.xyxy + flow.xzxz * 0.05 * phase.xxyy;

				fixed4 tex0 = tex2D(_MainTex, uvFlow.xy);
				fixed4 tex1 = tex2D(_MainTex, uvFlow.zw);

                return lerp(tex0, tex1, phase.z);
            }
            ENDCG
        }
    }
}
