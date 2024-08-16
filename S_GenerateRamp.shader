Shader "Elysia/Ramp Generator"
{
    Properties
    {
        _AlbedoTex("Albedo Tex", 2D) = "black" {}
        _AlbedoTint("Albedo Tint", Color) = (1, 1, 1, 1)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque"
        }

        HLSLINCLUDE
        #pragma target 4.5
        
        #include_with_pragmas "Assets/Materials/GenerateRamp/GenerateRamp.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "Generate Ramp"
            
            HLSLPROGRAM
            #pragma shader_feature _LERP_MODE
            #pragma shader_feature _GAMMA_MODE
            
            #pragma vertex VS
            #pragma fragment PS
            ENDHLSL
        }
    }
}
