#version 450
layout(location = 0) in vec2 v_UV;
layout(set = 0, binding = 0) uniform texture2D u_ScreenTex;
layout(set = 0, binding = 1) uniform sampler u_Sampler;

// Bind 2 to SettingsBuffer
layout(set = 0, binding = 2) uniform GraphicsSettingsBlock {
    float RenderScale; 
    int Bloom; 
    int MotionBlur; 
    float BlurIntensity;
    int Shadows; 
    int AntiAliasing; 
    int AO;
    float DrawDistance;
} settings;

layout(location = 0) out vec4 FragColor;

void main() {
    vec3 col = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV).rgb;

    if (settings.Bloom == 1){
        vec3 bloom = vec3(0.0);
        float threshold = 0.7;
        vec2 texelSize = 1.0/ textureSize(sampler2D(u_ScreenTex, u_Sampler),0);
        for(int x=-1; x<=1; x++){
            for(int y=-1; y<=1; y++){
                vec3 sampleColor = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(x,y) * texelSize * settings.BlurIntensity).rgb;
                float brightness = dot(sampleColor, vec3(0.2126, 0.7152, 0.0722));
                if(brightness> threshold){
                    bloom += sampleColor;
                }
            }
        }
        col += (bloom / 9.0) * 0.8;
    }    

    // Wyostrzanie (Sharpening) - zawsze włączone po skalowaniu
    vec2 texelSize = 1.0 / textureSize(sampler2D(u_ScreenTex, u_Sampler), 0);
    col += (col - texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + texelSize).rgb) * 0.3;

    FragColor = vec4(col, 1.0);
}
