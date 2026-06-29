#version 450
layout(location = 0) in vec2 InsidePos; layout(location = 1) in vec4 InColor; layout(location = 0) out vec4 v_Color;
void main() { gl_Position = vec4(InsidePos, 0.0, 1.0); v_Color = InColor; }
