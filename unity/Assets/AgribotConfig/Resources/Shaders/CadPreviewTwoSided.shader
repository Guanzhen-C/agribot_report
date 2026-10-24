Shader "Agribot/CAD Preview Two Sided"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        [HideInInspector] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        fixed4 _Color;
        half _Smoothness;

        struct Input
        {
            float facing : VFACE;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            output.Albedo = _Color.rgb;
            output.Alpha = _Color.a;
            output.Smoothness = _Smoothness;
            output.Metallic = 0;
            output.Normal = float3(0, 0, input.facing >= 0 ? 1 : -1);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
