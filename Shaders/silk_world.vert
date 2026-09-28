#version 330 core
layout(location=0) in vec3 InsidePos;
layout(location=1) in vec3 InNormal;
layout(location=2) in vec2 InUV;
layout(location=3) in float InMatId;

layout(std140) uniform ViewProjBlock { mat4 ViewProj; };

out vec3 vNormal;
out vec2 vUV;
flat out int vMatId;

void main()
{
    vNormal = InNormal;
    vUV = InUV;
    vMatId = int(InMatId);
    gl_Position = ViewProj * vec4(InsidePos, 1.0);
}