#version 450
layout(location = 0) in vec3 InsidePos; layout(location = 1) in vec3 InNormal; layout(location = 2) in vec2 InUV; layout(location = 3) in float InMatId;
layout(set = 0, binding = 0) uniform ViewProjBlock { mat4 u_ViewProj; };
layout(location = 0) out vec3 v_WorldPos; layout(location = 1) out vec3 v_Normal; layout(location = 2) out vec2 v_UV; layout(location = 3) out float v_MatId;
void main() { gl_Position = u_ViewProj * vec4(InsidePos, 1.0); v_WorldPos = InsidePos; v_Normal = InNormal; v_UV = InUV; v_MatId = InMatId; }
