#version 450
layout(location = 0) in vec3 v_WorldPos; layout(location = 1) in vec3 v_Normal; layout(location = 2) in vec2 v_UV; layout(location = 3) in float v_MatId;
layout(set = 0, binding = 1) uniform LightBlock { vec4 u_FlashlightPos; vec4 u_FlashlightDir; vec4 u_Lanterns[8]; int u_LanternCount; float u_Time; };
layout(set = 0, binding = 2) uniform texture2D u_Texture; layout(set = 0, binding = 3) uniform sampler u_Sampler;
layout(location = 0) out vec4 FragColor; float random(float x) { return fract(sin(x * 12.9898) * 43758.5453); }
void main() {
    vec3 normal = normalize(v_Normal); vec3 materialColor = vec3(0.0); vec3 ambient = vec3(0.0);
    if (v_MatId < 0.5) {
        vec2 matrixUV = v_UV; float numColumns = 12.0; float colId = floor(matrixUV.x * numColumns);
        float speed = 0.4 + 1.2 * random(colId * 45.32); float drift = u_Time * speed;
        vec2 fallingUV = vec2(matrixUV.x, fract(matrixUV.y + drift)); vec4 rawTex = texture(sampler2D(u_Texture, u_Sampler), fallingUV);
        float glowWave = fract(matrixUV.y * 3.0 + drift); materialColor = vec3(0.0, 1.0, 0.2) * rawTex.rgb * (0.3 + 0.7 * glowWave); ambient = vec3(0.0, 0.02, 0.002) * materialColor;
    } else {
        vec2 gridUV = fract(v_WorldPos.xz * 1.0); float gridLine = step(0.97, gridUV.x) + step(0.97, gridUV.y);
        materialColor = mix(vec3(0.11, 0.11, 0.14), vec3(0.0, 0.45, 1.0), gridLine); ambient = vec3(0.01, 0.01, 0.015); 
    }
    vec3 lighting = vec3(0.0); vec3 flashLightDir = normalize(u_FlashlightPos.xyz - v_WorldPos);
    float flashDist = length(u_FlashlightPos.xyz - v_WorldPos); float flashAtt = 1.0 / (1.0 + 0.04 * flashDist + 0.008 * flashDist * flashDist);
    float theta = dot(flashLightDir, normalize(-u_FlashlightDir.xyz));
    if (theta > u_FlashlightPos.w) {
        float epsilon = 0.04; float intensity = clamp((theta - u_FlashlightPos.w) / epsilon, 0.0, 1.0);
        float diff = max(dot(normal, flashLightDir), 0.0); lighting += diff * vec3(0.95, 0.95, 1.0) * flashAtt * intensity * u_FlashlightDir.w;
    }
    for (int i = 0; i < u_LanternCount; i++) {
        vec3 lanternPos = u_Lanterns[i].xyz;
        float lanternAtt = 1.0 / (1.0 + 0.3 * length(lanternPos - v_WorldPos) + 0.2 * dot(lanternPos - v_WorldPos, lanternPos - v_WorldPos));
        vec3 lanternDir = normalize(lanternPos - v_WorldPos);
        float diff = max(dot(normal, lanternDir), 0.0);
        lighting += diff * vec3(1.0, 0.45, 0.06) * lanternAtt * 2.8;
    }
    FragColor = vec4(ambient + lighting * materialColor, 1.0);
}
