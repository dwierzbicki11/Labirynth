#!/bin/bash
set -e

mkdir -p Shaders
cd Shaders

cat << 'EOF' > vertex.vert
#version 450
layout(location = 0) in vec3 InsidePos; layout(location = 1) in vec3 InNormal; layout(location = 2) in vec2 InUV; layout(location = 3) in float InMatId;
layout(set = 0, binding = 0) uniform ViewProjBlock { mat4 u_ViewProj; };
layout(location = 0) out vec3 v_WorldPos; layout(location = 1) out vec3 v_Normal; layout(location = 2) out vec2 v_UV; layout(location = 3) out float v_MatId;
void main() { gl_Position = u_ViewProj * vec4(InsidePos, 1.0); v_WorldPos = InsidePos; v_Normal = InNormal; v_UV = InUV; v_MatId = InMatId; }
EOF

cat << 'EOF' > fragment.frag
#version 450
layout(location = 0) in vec3 v_WorldPos; layout(location = 1) in vec3 v_Normal; layout(location = 2) in vec2 v_UV; layout(location = 3) in float v_MatId;
layout(set = 0, binding = 1) uniform LightBlock { vec4 u_FlashlightPos; vec4 u_FlashlightDir; vec4 u_Lanterns[8]; int u_LanternCount; float u_Time; };
layout(set = 0, binding = 2) uniform texture2D u_Texture; layout(set = 0, binding = 3) uniform sampler u_Sampler;
layout(location = 0) out vec4 FragColor; 

float random1D(float x) { return fract(sin(x * 12.9898) * 43758.5453); }

vec3 safeNormalize(vec3 v, vec3 fallback) {
    float len = length(v);
    return len > 0.00001 ? v / len : fallback;
}

void main() {
    vec3 pos = isnan(v_WorldPos.x) ? vec3(0.0) : v_WorldPos;
    vec2 uv = (isnan(v_UV.x) || isnan(v_UV.y)) ? vec2(0.5) : v_UV;
    float matId = isnan(v_MatId) ? 1.0 : v_MatId;
    vec3 normal = safeNormalize(v_Normal, vec3(0.0, 1.0, 0.0)); 

    vec3 materialColor = vec3(0.0); 
    vec3 ambient = vec3(0.0);
    vec3 emissive = vec3(0.0);
    float specularStrength = 0.0;

    if (matId < 0.5) {
        emissive = vec3(1.0, 0.2, 0.1) * 2.0; 
    } 
    else if (matId < 1.5) {
        vec2 gridUV = fract(uv * 3.0); 
        float lineX = step(0.96, gridUV.x);
        float lineY = step(0.96, gridUV.y);
        float isGrid = clamp(lineX + lineY, 0.0, 1.0);
        vec3 cyberLineColor = vec3(0.0, 0.4, 0.6); 
        vec3 baseDarkPlate = vec3(0.04, 0.04, 0.05);
        emissive = vec3(0.0); 
        materialColor = mix(baseDarkPlate, cyberLineColor, isGrid); 
        ambient = materialColor * 0.002; 
        specularStrength = isGrid * 1.0 + 0.05; 
    }
    else if (matId < 2.5) {
        vec2 centeredUV = uv * 2.0 - 1.0;
        float edgeCurvature = 1.0 - (centeredUV.x * centeredUV.x * 0.4 + centeredUV.y * centeredUV.y * 0.4);
        materialColor = vec3(0.2, 0.2, 0.22) * edgeCurvature; 
        ambient = vec3(0.03, 0.03, 0.04);
        float highlight = exp(-pow(abs(centeredUV.x), 2.0) * 15.0) * 0.1;
        materialColor += vec3(0.8, 0.85, 0.9) * highlight;
        specularStrength = 1.0; 
    }
    else if (matId < 3.5) {
        float pulse = (sin(u_Time * 6.0) + 1.0) * 0.5;
        vec3 goldColor = vec3(1.0, 0.8, 0.2);
        emissive = goldColor * (1.5 + pulse * 2.0);
        materialColor = vec3(0.1); 
    }
    else if (matId < 4.5) {
        float glitchNoise = random1D(pos.y * 15.0 + u_Time * 30.0);
        float glitch = step(0.85, glitchNoise); 
        vec3 dangerRed = vec3(1.0, 0.05, 0.1);
        emissive = dangerRed * (1.5 + glitch * 3.5);
    }
    else if (matId < 4.6) { 
        // ID 4.5 (STALKER): Mroczny, pulsujący fiolet z glitchem
        materialColor = vec3(0.02, 0.0, 0.04);
        vec3 viewDir = safeNormalize(u_FlashlightPos.xyz - pos, normal);
        float fresnel = pow(1.0 - max(dot(viewDir, normal), 0.0), 3.0);
        float pulse = 0.5 + 0.5 * sin(u_Time * 3.0);
        float noise = fract(sin(dot(pos.xy, vec2(12.9898, 78.233))) * 43758.5453);
        float flicker = step(0.92, noise + sin(u_Time * 20.0) * 0.1);
        vec3 glowColor = vec3(0.7, 0.0, 1.0); 
        emissive = (glowColor * pulse * (fresnel * 2.0 + flicker * 0.8)) * 1.5;
        specularStrength = fresnel * 2.0;
    }
    else if (matId < 5.5) {
        emissive = vec3(1.0, 0.6, 0.2) * 5.0; 
    }
    else {
        materialColor = vec3(0.5); 
    }

    vec3 lighting = vec3(0.0); 
    vec3 safeFlashlightPos = isnan(u_FlashlightPos.x) ? vec3(0.0) : u_FlashlightPos.xyz;
    vec3 safeFlashlightDir = isnan(u_FlashlightDir.x) ? vec3(0.0, -1.0, 0.0) : u_FlashlightDir.xyz;
    vec3 viewDir = safeNormalize(safeFlashlightPos - pos, normal);
    vec3 flashLightDir = viewDir;
    
    float flashDist = length(safeFlashlightPos - pos); 
    float flashAtt = 1.0 / (1.0 + 0.09 * flashDist + 0.003 * flashDist * flashDist);
    
    vec3 forwardDir = safeNormalize(-safeFlashlightDir, vec3(0.0, -1.0, 0.0));
    float theta = dot(flashLightDir, forwardDir);
    
    float outerCutoff = 0.92; 
    float innerCutoff = 0.985; 
    float flashlightIntensity = 2.5;

    if (theta > outerCutoff) {
        float intensity = smoothstep(0.0, 1.0, clamp((theta - outerCutoff) / (innerCutoff - outerCutoff), 0.0, 1.0));
        float diff = max(dot(normal, flashLightDir), 0.0);
        vec3 halfDir = safeNormalize(flashLightDir + viewDir, normal);
        float spec = pow(max(dot(normal, halfDir), 0.0001), 32.0) * specularStrength;
        lighting += (diff * vec3(0.85, 0.9, 1.0) + spec) * flashAtt * intensity * flashlightIntensity;
    }
    
    int maxLanterns = u_LanternCount;
    if (maxLanterns < 0 || maxLanterns > 8) maxLanterns = 8;
    for (int i = 0; i < maxLanterns; i++) {
        vec3 lanternPos = u_Lanterns[i].xyz;
        if (isnan(lanternPos.x)) continue;
        float distToLantern = length(lanternPos - pos);
        float lanternAtt = 1.0 / (1.0 + 0.2 * distToLantern + 0.1 * distToLantern * distToLantern);
        vec3 lanternDir = safeNormalize(lanternPos - pos, normal);
        float diff = max(dot(normal, lanternDir), 0.0);
        vec3 halfDir = safeNormalize(lanternDir + viewDir, normal);
        float spec = pow(max(dot(normal, halfDir), 0.0001), 16.0) * specularStrength;
        lighting += (diff + spec) * vec3(1.0, 0.6, 0.2) * lanternAtt * 1.2; 
    }
    
    vec3 finalColor = ambient + (lighting * materialColor) + emissive;
    if (isnan(finalColor.x) || isnan(finalColor.y) || isnan(finalColor.z)) finalColor = vec3(1.0, 0.0, 0.0); 
    FragColor = vec4(finalColor, 1.0);
}
EOF

# [Pozostała część skryptu bez zmian - hud_vertex.vert, etc.]
cat << 'EOF' > hud_vertex.vert
#version 450
layout(location = 0) in vec2 InsidePos; layout(location = 1) in vec4 InColor; layout(location = 0) out vec4 v_Color;
void main() { gl_Position = vec4(InsidePos, 0.0, 1.0); v_Color = InColor; }
EOF

cat << 'EOF' > hud_fragment.frag
#version 450
layout(location = 0) in vec4 v_Color; layout(location = 0) out vec4 FragColor; void main() { FragColor = v_Color; }
EOF

cat << 'EOF' > post_vertex.vert
#version 450
layout(location = 0) out vec2 v_UV;
void main() {
    v_UV = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(v_UV * 2.0f - 1.0f, 0.0f, 1.0f);
}
EOF

cat << 'EOF' > post_fragment.frag
#version 450 
layout(location = 0) in vec2 v_UV; 
layout(set = 0, binding = 0) uniform texture2D u_ScreenTex; 
layout(set = 0, binding = 1) uniform sampler u_Sampler; 
layout(location = 0) out vec4 FragColor; 
vec3 ACESFilm(vec3 x) { 
    float a = 2.51f; float b = 0.03f; float c = 2.43f; float d = 0.59f; float e = 0.14f; 
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0); 
} 
void main() { 
    vec3 col = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV).rgb; 
    if (isnan(col.x) || isnan(col.y) || isnan(col.z)) col = vec3(0.0);
    col = ACESFilm(col); 
    col = pow(max(col, 0.0), vec3(1.0 / 2.2)); 
    FragColor = vec4(col, 1.0);
} 
EOF

echo "Rozpoczynam kompilacje..."
glslangValidator -V vertex.vert -o vertex.spv
glslangValidator -V fragment.frag -o fragment.spv
glslangValidator -V hud_vertex.vert -o hud_vertex.spv
glslangValidator -V hud_fragment.frag -o hud_fragment.spv
glslangValidator -V post_vertex.vert -o post_vertex.spv
glslangValidator -V post_fragment.frag -o post_fragment.spv

echo "Kompilacja pomyslna. Kopiowanie plikow..."
cd ../
mkdir -p bin/Debug/net11.0/Shaders/
mkdir -p bin/Debug/net11.0/Models/
cp -vu Shaders/*.spv bin/Debug/net11.0/Shaders/
cp -vu Models/*.gguf bin/Debug/net11.0/Models/
echo "Gotowe!"