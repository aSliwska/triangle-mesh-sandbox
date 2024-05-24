Shader "Custom/AxisXShader"
{
    Properties {
        _AxisThickness ("Axis Thickness", Float) = 0.01
        _TickThickness ("Tick Thickness", Float) = 0.05
        _AxisSpacing ("Axis Spacing", Float) = 1.0
        _AxisColour ("Axis Colour", Color) = (0.5, 0.5, 0.5, 0.5)
        _BaseColour ("Base Colour", Color) = (0.0, 0.0, 0.0, 0.0)
    }
     
    SubShader {
        Tags { 
            "Queue" = "Transparent" 
        }
     
        Pass {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
     
            CGPROGRAM
     
            // Define the vertex and fragment shader functions
            #pragma vertex vert
            #pragma fragment frag
     
            // Access Shaderlab properties
            uniform float _AxisThickness;
            uniform float _TickThickness;
            uniform float _AxisSpacing;
            uniform float4 _AxisColour;
            uniform float4 _BaseColour;
     
            // Input into the vertex shader
            struct vertexInput {
                float4 vertex : POSITION;
            };
 
            // Output from vertex shader into fragment shader
            struct vertexOutput {
                float4 pos : SV_POSITION;
                float4 worldPos : TEXCOORD0;
            };
     
            // VERTEX SHADER
            vertexOutput vert(vertexInput input) {
                vertexOutput output;
                output.pos = UnityObjectToClipPos(input.vertex);
                // Calculate the world position coordinates to pass to the fragment shader
                output.worldPos = mul(unity_ObjectToWorld, input.vertex);
                return output;
            }
 
            // FRAGMENT SHADER
            float4 frag(vertexOutput input) : COLOR {
                if (abs(input.worldPos.z) < _AxisThickness) {
                    return _AxisColour;
                }
                else if ((frac(input.worldPos.y/_AxisSpacing) < _AxisThickness*2) && (abs(input.worldPos.z) < _TickThickness)) {
                    return _AxisColour;
                }
                else {
                    return _BaseColour;
                }
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}
