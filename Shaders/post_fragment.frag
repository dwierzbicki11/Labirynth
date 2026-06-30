#version 450 
layout(location = 0) in vec2 v_UV; 
layout(set = 0, binding = 0) uniform texture2D u_ScreenTex; 
layout(set = 0, binding = 1) uniform sampler u_Sampler; 
layout(location = 0) out vec4 FragColor; 
vec3 ACESFilm(vec3 x) { 
float a = 2.51f; 
float b = 0.03f; 
float c = 2.43f; 
float d = 0.59f; 
float e = 0.14f; 
return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0); 
} void main() { 
// DIAGNOSTYKA: Pobieramy piksel tylko i wyłącznie JEDEN raz. Brak próbek sąsiadujących. 
vec3 col = texture(sampler2D(u_ScreenTex, u_Sampler), v_UV).bgr; 
// Matematyka procesora (ALU) - to nie obciąża przepustowości VRAM
 col = ACESFilm(col); col = pow(col, vec3(1.0 / 2.2)); 
 FragColor = vec4(col, 1.0);
} 
