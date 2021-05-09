Shader "Custom/ColorReplacer"
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
            fixed4 _SearchColor1;
            fixed4 _NewColor1;
            fixed4 _SearchColor2;
            fixed4 _NewColor2;
            fixed4 _SearchColor3;
            fixed4 _NewColor3;
            fixed4 _SearchColor4;
            fixed4 _NewColor4;
            fixed4 _SearchColor5;
            fixed4 _NewColor5;
            fixed4 _SearchColor6;
            fixed4 _NewColor6;
            fixed4 _SearchColor7;
            fixed4 _NewColor7;
            fixed4 _SearchColor8;
            fixed4 _NewColor8;

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

                fixed d = distance(_SearchColor1.rgb, col.rgb);
                if (d <= 1/255)
                {
                    return _NewColor1;
                }

                d = distance(_SearchColor2.rgb, col.rgb);
                if (d <= 1/255)
                {
                    return _NewColor2;
                }

                d = distance(_SearchColor3.rgb, col.rgb);
                if (d <= 1/255)
                {
                    return _NewColor3;
                }

                d = distance(_SearchColor4.rgb, col.rgb);
                if (d <= 1/255)
                {
                    return _NewColor4;
                }

                d = distance(_SearchColor5.rgb, col.rgb);
                if (d <= 1/255)
                {
                    return _NewColor5;
                }

                d = distance(_SearchColor6.rgb, col.rgb);
                if (d <= 1/255)
                {
                    return _NewColor6;
                }

                d = distance(_SearchColor7.rgb, col.rgb);
                if (d <= 1/255)
                {
                    return _NewColor7;
                }

                d = distance(_SearchColor8.rgb, col.rgb);
                if (d <= 1/255)
                {
                    return _NewColor8;
                }

                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
