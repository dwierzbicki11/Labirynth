#version 450
layout(location = 0) in vec2 v_UV;
layout(set = 0, binding = 0) uniform texture2D u_ScreenTex;
layout(set = 0, binding = 1) uniform sampler u_Sampler;
layout(location = 0) out vec4 FragColor;
void main() {
    vec2 texelSize = 1.0 / textureSize(sampler2D(u_ScreenTex, u_Sampler), 0);
    // Filtr Splotowy (Kernel Sharpening)
    vec3 col = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV).rgb * 5.0;
    col -= texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(texelSize.x, 0)).rgb;
    col -= texture(sampler2D(u_ScreenTex, u_Sampler), v_UV - vec2(texelSize.x, 0)).rgb;
    col -= texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(0, texelSize.y)).rgb;
    col -= texture(sampler2D(u_ScreenTex, u_Sampler), v_UV - vec2(0, texelSize.y)).rgb;
    FragColor = vec4(max(col, 0.0), 1.0);
}
