#!/bin/bash
set -e # Krytyczne: Przerwij skrypt natychmiast, jeśli wystąpi błąd kompilacji

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
EOF

cat << 'EOF' > hud_vertex.vert
#version 450
layout(location = 0) in vec2 InsidePos; layout(location = 1) in vec4 InColor; layout(location = 0) out vec4 v_Color;
void main() { gl_Position = vec4(InsidePos, 0.0, 1.0); v_Color = InColor; }
EOF

cat << 'EOF' > hud_fragment.frag
#version 450
layout(location = 0) in vec4 v_Color; layout(location = 0) out vec4 FragColor; void main() { FragColor = v_Color; }
EOF

# 🔥 NOWE SHADERY POST-PROCESSINGU (MASTER SHADER)
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

// --- ZASOBY ---
layout(set = 0, binding = 0) uniform texture2D u_ScreenTex;
layout(set = 0, binding = 1) uniform sampler u_Sampler;
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

// NOWOŚĆ: Bufor głębi z Fazy 1
layout(set = 0, binding = 3) uniform texture2D u_DepthTex; 

layout(location = 0) out vec4 FragColor;

// --- FUNKCJE POMOCNICZE ---
vec3 ACESFilm(vec3 x) {
    float a = 2.51f; float b = 0.03f; float c = 2.43f; float d = 0.59f; float e = 0.14f;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}

float LinearizeDepth(float depth) {
    float zNear = 0.05;  // Zgodne z Matrix4x4 w C#
    float zFar = 100.0;  // Zgodne z Matrix4x4 w C#
    return (zNear * zFar) / (zFar - depth * (zFar - zNear));
}

float rand(vec2 co){ return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453); }

void main() {
    vec2 texelSize = 1.0 / textureSize(sampler2D(u_ScreenTex, u_Sampler), 0);
    vec3 col = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV).rgb;
    
    float rawDepth = texture(sampler2D(u_DepthTex, u_Sampler), v_UV).r;
    float linearDepth = LinearizeDepth(rawDepth);

    // 1. FAST APPROXIMATE ANTI-ALIASING (FXAA)
    if (settings.AntiAliasing == 1) {
        float lumaM  = dot(col, vec3(0.299, 0.587, 0.114));
        float lumaN  = dot(texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(0.0, -1.0) * texelSize).rgb, vec3(0.299, 0.587, 0.114));
        float lumaS  = dot(texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(0.0,  1.0) * texelSize).rgb, vec3(0.299, 0.587, 0.114));
        float lumaE  = dot(texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(1.0,  0.0) * texelSize).rgb, vec3(0.299, 0.587, 0.114));
        float lumaW  = dot(texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(-1.0, 0.0) * texelSize).rgb, vec3(0.299, 0.587, 0.114));

        float lumaMin = min(lumaM, min(min(lumaN, lumaS), min(lumaE, lumaW)));
        float lumaMax = max(lumaM, max(max(lumaN, lumaS), max(lumaE, lumaW)));

        if ((lumaMax - lumaMin) > max(lumaMax * 0.125, 0.0312)) {
            vec2 dir = vec2(-((lumaN + lumaS) - (lumaE + lumaW)), ((lumaN + lumaS) - (lumaE + lumaW)));
            float dirReduce = max((lumaN + lumaS + lumaE + lumaW) * (0.25 * 0.125), 0.0078125);
            float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
            dir = min(vec2(8.0, 8.0), max(vec2(-8.0, -8.0), dir * rcpDirMin)) * texelSize;

            vec3 rgbA = 0.5 * (texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + dir * (1.0/3.0 - 0.5)).rgb + texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + dir * (2.0/3.0 - 0.5)).rgb);
            vec3 rgbB = rgbA * 0.5 + 0.25 * (texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + dir * (0.0/3.0 - 0.5)).rgb + texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + dir * (3.0/3.0 - 0.5)).rgb);

            float lumaB = dot(rgbB, vec3(0.299, 0.587, 0.114));
            col = ((lumaB < lumaMin) || (lumaB > lumaMax)) ? rgbA : rgbB;
        }
    }

    // 2. AMBIENT OCCLUSION (SSAO-Lite zoptymalizowane pod RPi4)
    if (settings.AO == 1 && rawDepth < 1.0) {
        float ao = 0.0;
        float radius = 2.0 * texelSize.x;
        vec2 aoOffsets[4] = vec2[](vec2(-radius, -radius), vec2(radius, -radius), vec2(-radius, radius), vec2(radius, radius));
        
        for(int i=0; i<4; i++) {
            float sampleDepth = LinearizeDepth(texture(sampler2D(u_DepthTex, u_Sampler), v_UV + aoOffsets[i]).r);
            if (linearDepth - sampleDepth > 0.1 && linearDepth - sampleDepth < 2.0) {
                ao += 0.25;
            }
        }
        col *= (1.0 - ao * 0.8);
    }

    // 3. MOTION BLUR (Aproksymacja Radialna)
    if (settings.MotionBlur == 1 && settings.BlurIntensity > 0.01) {
        vec2 dir = (vec2(0.5) - v_UV);
        float dist = length(dir);
        dir = normalize(dir);
        
        vec3 mbCol = col;
        for(int i = 1; i <= 4; i++) {
            mbCol += texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + dir * (i * 0.005 * settings.BlurIntensity)).rgb;
        }
        col = mbCol / 5.0;
    }

    // 4. MGŁA I DRAW DISTANCE
    if (rawDepth < 1.0) {
        float fogFactor = clamp((linearDepth - (settings.DrawDistance * 0.2)) / (settings.DrawDistance * 0.8), 0.0, 1.0);
        fogFactor = pow(fogFactor, 1.5); 
        vec3 fogColor = vec3(0.0, 0.05, 0.02); 
        col = mix(col, fogColor, fogFactor);
    }

    // 5. SOFT-KNEE BLOOM
    if (settings.Bloom == 1) {
        vec3 bloom = vec3(0.0);
        float threshold = 0.85; float knee = 0.2; float spread = 2.5;
        vec2 offsets[9] = vec2[](vec2(-1.0, -1.0), vec2(0.0, -1.0), vec2(1.0, -1.0), vec2(-1.0, 0.0), vec2(0.0, 0.0), vec2(1.0, 0.0), vec2(-1.0, 1.0), vec2(0.0, 1.0), vec2(1.0, 1.0));
        float weights[9] = float[](0.0625, 0.125, 0.0625, 0.125, 0.25, 0.125, 0.0625, 0.125, 0.0625);

        for(int i = 0; i < 9; i++) {
            vec3 sampleCol = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + offsets[i] * texelSize * spread).rgb;
            float luma = dot(sampleCol, vec3(0.2126, 0.7152, 0.0722));
            float rq = clamp(luma - threshold + knee, 0.0, knee * 2.0);
            rq = (rq * rq) / (4.0 * knee + 0.0001);
            bloom += (sampleCol * (max(rq, luma - threshold) / (luma + 0.0001))) * weights[i];
        }
        bloom += rand(v_UV * linearDepth) * 0.005; // Dithering
        col += bloom * 1.5;
    }

    // 6. ADAPTIVE SHARPENING (RCAS)
    vec3 n = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(0.0, -1.0) * texelSize).rgb;
    vec3 e = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(1.0, 0.0) * texelSize).rgb;
    vec3 w = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(-1.0, 0.0) * texelSize).rgb;
    vec3 s = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(0.0, 1.0) * texelSize).rgb;
    float lumaCol = dot(col, vec3(0.299, 0.587, 0.114));
    float sharpness = clamp(1.0 - (lumaCol * 0.8), 0.0, 1.0); 
    col = col + (col - (n + e + w + s) * 0.25) * (sharpness * 0.8);

    // 7. TONEMAPPING & GAMMA
    col = ACESFilm(col);
    col = pow(col, vec3(1.0 / 2.2)); 

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
cp -vu Shaders/*.spv bin/Debug/net11.0/Shaders/
echo "Gotowe!"
