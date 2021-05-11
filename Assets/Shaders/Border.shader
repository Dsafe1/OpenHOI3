Shader "Custom/Border"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _ProvincesTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_ProvincesTex, i.uv);

                fixed4 right = tex2D(_ProvincesTex, i.uv + fixed2(_MainTex_TexelSize.x, 0));
                fixed4 down = tex2D(_ProvincesTex, i.uv + fixed2(0, _MainTex_TexelSize.y));

                fixed d = distance(col.rgb, right.rgb);
                if (d > 1/255)
                {
                    return float4(0,0,0,0.8);
                }
                d = distance(col.rgb, down.rgb);
                if (d > 1/255)
                {
                    return float4(0,0,0,0.8);
                }

                return float4(0,0,0,0);
            }
            ENDCG
        }
    }
}
