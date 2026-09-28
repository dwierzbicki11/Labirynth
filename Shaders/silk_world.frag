#version 330 core
layout(location=0) out vec4 FragColor;

in vec3 vNormal;
in vec2 vUV;
flat in int vMatId;

layout(std140) uniform LightBlock
{
    vec4 FlashlightPos;
    vec4 FlashlightDir;
    vec4 Lantern0;
    vec4 Lantern1;
    vec4 Lantern2;
    vec4 Lantern3;
    vec4 Lantern4;
    vec4 Lantern5;
    vec4 Lantern6;
    vec4 Lantern7;
    int LanternCount;
    float Time;
};

uniform sampler2D u_Texture;

void main()
{
    vec3 base = texture(u_Texture, vUV).rgb;
    vec3 n = normalize(vNormal);
    vec3 color = base * 0.16;

    vec3 toFlash = FlashlightPos.xyz - gl_FragCoord.xyz;
    float flash = max(dot(n, normalize(toFlash)), 0.0);
    color += base * flash * FlashlightPos.w;

    vec4 lanterns[8] = vec4[8](Lantern0,Lantern1,Lantern2,Lantern3,Lantern4,Lantern5,Lantern6,Lantern7);
    for (int i = 0; i < LanternCount && i < 8; ++i)
    {
        float d = max(length(lanterns[i].xyz - gl_FragCoord.xyz), 0.25);
        color += base * (lanterns[i].w / (1.0 + d * d * 0.04));
    }

    FragColor = vec4(color, 1.0);
}