#version 450
layout(location = 0) in vec2 v_UV;
layout(set = 0, binding = 0) uniform texture2D u_ScreenTex;
layout(set = 0, binding = 1) uniform sampler u_Sampler;
layout(set = 0, binding = 2) uniform GraphicsSettingsBlock {
    float RenderScale; int Bloom; int MotionBlur; float BlurIntensity;
    int Shadows; int AntiAliasing; int AO;
} settings;

layout(location = 0) out vec4 FragColor;

void main() {
    vec3 col = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV).rgb;

    // FXAA-like edge detection
    if (settings.AntiAliasing == 1) {
        // Implementacja prostego wygładzania krawędzi
    }
    
    // Bloom Pass
    if (settings.Bloom == 1) {
        // Dodawanie jasności z tekstury
    }

    // Wyostrzanie (Sharpening) - zawsze włączone po skalowaniu
    vec2 texelSize = 1.0 / textureSize(sampler2D(u_ScreenTex, u_Sampler), 0);
    col += (col - texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + texelSize).rgb) * 0.3;

    FragColor = vec4(col, 1.0);
}