Shader "SquareCountry/Soft Particle Glow"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;

            v2f vert(appdata v)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(v.vertex);
                output.color = v.color * _Color;
                output.uv = v.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // A radial falloff makes each additive particle read as a soft blur halo.
                float2 fromCenter = input.uv - 0.5;
                float softness = saturate(1.0 - dot(fromCenter, fromCenter) * 4.0);
                softness *= softness;
                return fixed4(input.color.rgb * softness, input.color.a * softness);
            }
            ENDCG
        }
    }
}
