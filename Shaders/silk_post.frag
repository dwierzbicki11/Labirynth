#version 330 core
in vec2 vUV;
out vec4 FragColor;
uniform sampler2D u_ScreenTex;
uniform sampler2D u_DepthTex;
layout(std140) uniform GraphicsSettingsBlock
{
    float RenderScale;
    int Bloom;
    int MotionBlur;
    float BlurIntensity;
    int Shadows;
    int AntiAliasing;
    int AO;
    float DrawDistance;
};
void main()
{
    vec3 color = texture(u_ScreenTex, vUV).rgb;
    if (Bloom != 0)
        color += max(color - vec3(0.75), vec3(0.0)) * 0.12;
    FragColor = vec4(color, 1.0);
}