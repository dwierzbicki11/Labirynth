#version 330 core
layout(location=0) in vec2 InsidePos;
layout(location=1) in vec4 InColor;
out vec4 vColor;
void main()
{
    vColor = InColor;
    gl_Position = vec4(InsidePos, 0.0, 1.0);
}