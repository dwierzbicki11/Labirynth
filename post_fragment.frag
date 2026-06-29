#version 450
layout(location = 0) in vec2 v_UV;
layout(set = 0, binding = 0) uniform texture2D u_ScreenTex;
layout(set = 0, binding = 1) uniform sampler u_Sampler;
layout(location = 0) out vec4 FragColor;

// Szybki generator pseudolosowy na układach ARM
float rand(vec2 co) {
    return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453);
}

void main() {
    vec2 texelSize = 1.0 / textureSize(sampler2D(u_ScreenTex, u_Sampler), 0);
    
    // 1. POBRANIE OBRAZU BAZOWEGO (Z interpolacją LinearSampler z C#)
    vec3 color = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV).rgb;
    
    // 2. ŁAGODNE WYOSTRZANIE (High-Pass Filter)
    // Redukujemy siłę z 5.0 do bardziej subtelnego mnożnika, aby uniknąć "prześwietlenia"
    vec3 sharp = color * 4.5;
    sharp -= texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(texelSize.x, 0)).rgb * 0.875;
    sharp -= texture(sampler2D(u_ScreenTex, u_Sampler), v_UV - vec2(texelSize.x, 0)).rgb * 0.875;
    sharp -= texture(sampler2D(u_ScreenTex, u_Sampler), v_UV + vec2(0, texelSize.y)).rgb * 0.875;
    sharp -= texture(sampler2D(u_ScreenTex, u_Sampler), v_UV - vec2(0, texelSize.y)).rgb * 0.875;
    
    // Mieszamy oryginalny obraz z wyostrzonym (Tylko 40% siły ostrej maski)
    color = mix(color, max(sharp, 0.0), 0.4); 

    // 3. DITHERING / FILM GRAIN (Szum)
    // Dodajemy losowy szum w zakresie od -0.02 do +0.02.
    // Oszukuje to mózg gracza, maskując brakujące piksele z upscalingu.
    float noise = (rand(v_UV * 100.0) - 0.5) * 0.04;
    color += noise;

    // 4. WINIETA (Vignette)
    // Przyciemniamy rogi ekranu. Centrum = 1.0 (jasne), Rogi = bliżej 0.0 (ciemne).
    float dist = distance(v_UV, vec2(0.5, 0.5));
    float vignette = smoothstep(0.85, 0.35, dist);
    color *= vignette;

    FragColor = vec4(color, 1.0);
}
