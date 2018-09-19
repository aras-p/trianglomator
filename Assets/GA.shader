Shader "Unlit/GA"
{
	SubShader
	{
		Pass
		{
		    Blend SrcAlpha OneMinusSrcAlpha
		    ZWrite Off
		    ZTest Always
		    Cull Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct v2f
			{
				float4 col : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			struct Triangle
			{
			    float2 posA, posB, posC;
			    float4 col;
			};

			StructuredBuffer<Triangle> _DNA;

			v2f vert (uint vid : SV_VertexID)
			{
				v2f o;
				uint tid = vid / 3;
				uint index = vid % 3;
				float2 pos = _DNA[tid].posA;
				if (index == 1)
				    pos = _DNA[tid].posB;
				if (index == 2)
				    pos = _DNA[tid].posC;
				o.vertex = float4(pos.x * 2 - 1, pos.y * 2 - 1, 0, 1);
				o.col = _DNA[tid].col;
				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				return i.col;
			}
			ENDCG
		}
	}
}
