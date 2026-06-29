using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using StbImageSharp;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Veldrid.SPIRV;
using CyberEngine.Entities;
using CyberEngine.Scene;

namespace CyberEngine;

public class GameEngine
{
    private Sdl2Window _window = null!;
    private GraphicsDevice _device = null!;
    private CommandList _commandList = null!;
    private string _baseTitle = "";

    // POTOK 3D
    private Pipeline _pipeline = null!;
    private DeviceBuffer _vertexBuffer = null!;
    private DeviceBuffer _viewProjBuffer = null!;
    private DeviceBuffer _lightBuffer = null!; 
    private ResourceSet _resourceSet = null!;
    private readonly List<float> _batchVertices = new();
    private readonly List<Vector3> _frameLanterns = new();

    // POTOK HUD 2D
    private Pipeline _hudPipeline = null!;
    private DeviceBuffer _hudVertexBuffer = null!;
    private readonly List<float> _hudBatchVertices = new();

    private Texture _wallTexture = null!;
    private TextureView _wallTextureView = null!;
    private Sampler _sampler = null!;

    // DIAGNOSTYKA SYSTEMOWA
    private double _lastCpuTime = 0;
    private readonly Stopwatch _cpuStopwatch = Stopwatch.StartNew();
    private double _currentCpuPercent = 0;
    private double _ramUsage = 0;
    private double _statTimer = 0;
    private double _currentFps = 0;
    private int _frameCount = 0;

    // METRYKI DRAW CALLS
    private int _gpuDrawCallsCounter = 0;
    private int _gpuVerticesCounter = 0;
    private int _lastGpuDrawCalls = 0;
    private int _lastGpuVertices = 0;

    // --- SYSTEM SCEN ---
    private Scene.Scene _currentScene = null!;
    private Scene.Scene? _nextScene = null;

    // TELEMETRIA ODSEPAROWANA OD GRACZA
    public Core.Math.Vector3 CameraPosition { get; set; } = Core.Math.Vector3.Zero;
    public float CameraYaw { get; set; } = 0f;
    public float CameraPitch { get; set; } = 0f;
    public float WeaponRecoil { get; set; } = 0f;
    public int PlayerEnergy { get; set; } = 100;
    public bool ShowGameplayHud { get; set; } = false;

    public Vector3 CameraForward { get; set; } = Vector3.UnitZ;
    public Vector2 MouseDelta { get; private set; } = Vector2.Zero;
    public bool TriggerMuzzleFlash { get; set; } = false;

    public float Width => _window.Width;
    public float Height => _window.Height;
    public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black; 
    public string GpuName => _device.DeviceName;

    public void LoadScene(Scene.Scene scene)
    {
        _nextScene = scene;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LightData
    {
        public Vector4 FlashlightPos; 
        public Vector4 FlashlightDir; 
        public Vector4 Lantern0; public Vector4 Lantern1; public Vector4 Lantern2; public Vector4 Lantern3;
        public Vector4 Lantern4; public Vector4 Lantern5; public Vector4 Lantern6; public Vector4 Lantern7;
        public int LanternCount;
        public float Time; 
        private float p2; private float p3; 
    }

    private static readonly Dictionary<char, string[]> Font = new() {
        ['0'] = ["███", "█ █", "█ █", "█ █", "███"], ['1'] = ["  █", "  █", "  █", "  █", "  █"],
        ['2'] = ["███", "  █", "███", "█  ", "███"], ['3'] = ["███", "  █", "███", "  █", "███"],
        ['4'] = ["█ █", "█ █", "███", "  █", "  █"], ['5'] = ["███", "█  ", "███", "  █", "███"],
        ['6'] = ["███", "█  ", "███", "█ █", "███"], ['7'] = ["███", "  █", "  █", "  █", "  █"],
        ['8'] = ["███", "█ █", "███", "█ █", "███"], ['9'] = ["███", "█ █", "███", "  █", "███"],
        ['C'] = ["███", "█  ", "█  ", "█  ", "███"], ['P'] = ["███", "█ █", "███", "█  ", "█  "],
        ['U'] = ["█ █", "█ █", "█ █", "█ █", "███"], ['R'] = ["███", "█ █", "███", "██ ", "█ █"],
        ['A'] = ["███", "█ █", "███", "█ █", "█ █"], ['M'] = ["█ █", "███", "█ █", "█ █", "█ █"],
        ['G'] = ["███", "█  ", "█ ██", "█ █", "███"], ['F'] = ["███", "█  ", "███", "█  ", "█  "],
        ['S'] = ["███", "█  ", "███", "  █", "███"], ['B'] = ["██ ", "█ █", "██ ", "█ █", "██ "],
        ['T'] = ["███", " █ ", " █ ", " █ ", " █ "], ['H'] = ["█ █", "█ █", "███", "█ █", "█ █"],
        ['E'] = ["███", "█  ", "███", "█  ", "███"], ['I'] = ["███", " █ ", " █ ", " █ ", "███"],
        ['N'] = ["███", "█ █", "█ █", "█ █", "███"], ['%'] = ["█ █", "  █", " █ ", "█  ", "█ █"],
        [':'] = ["   ", " █ ", "   ", " █ ", "   "], ['.'] = ["   ", "   ", "   ", "   ", " █ "],
        [' '] = ["   ", "   ", "   ", "   ", "   "], ['W'] = ["█ █", "█ █", "█ █", "███", "█ █"],
        ['L'] = ["█  ", "█  ", "█  ", "█  ", "███"], ['V'] = ["█ █", "█ █", "█ █", "█ █", " █ "],
        ['D'] = ["██ ", "█ █", "█ █", "█ █", "██ "], ['K'] = ["█ █", "█ █", "██ ", "█ █", "█ █"],
        ['O'] = ["███", "█ █", "█ █", "█ █", "███"], ['Z'] = ["███", "  █", " █ ", "█  ", "███"],
        ['Y'] = ["█ █", "█ █", "███", "  █", "███"], ['>'] = ["█  ", " █ ", "  █", " █ ", "█  "],
        ['-'] = ["   ", "   ", "███", "   ", "   "], ['J'] = ["  █", "  █", "  █", "█ █", "███"]
    };

    private const string VertexShaderGL = @"#version 300 es
    precision highp float;
    layout(location = 0) in vec3 InsidePos;
    layout(location = 1) in vec3 InNormal;
    layout(location = 2) in vec2 InUV;
    layout(location = 3) in float InMatId; 
    layout(std140) uniform ViewProjBlock { mat4 u_ViewProj; };
    out vec3 v_WorldPos;
    out vec3 v_Normal;
    out vec2 v_UV;
    out float v_MatId;
    void main() {
        gl_Position = u_ViewProj * vec4(InsidePos, 1.0);
        v_WorldPos = InsidePos; v_Normal = InNormal; v_UV = InUV; v_MatId = InMatId;
    }";

    private const string FragmentShaderGL = @"#version 300 es
    precision highp float;
    in vec3 v_WorldPos; in vec3 v_Normal; in vec2 v_UV; in float v_MatId;
    uniform sampler2D u_Texture;
    layout(std140) uniform LightBlock {
        vec4 u_FlashlightPos; vec4 u_FlashlightDir; vec4 u_Lanterns[8]; int u_LanternCount; float u_Time; 
    };
    out vec4 FragColor;
    float random(float x) { return fract(sin(x * 12.9898) * 43758.5453); }
    void main() {
        vec3 normal = normalize(v_Normal);
        vec3 materialColor = vec3(0.0); vec3 ambient = vec3(0.0);
        if (v_MatId < 0.5) {
            vec2 matrixUV = v_UV; float numColumns = 12.0; float colId = floor(matrixUV.x * numColumns);
            float speed = 0.4 + 1.2 * random(colId * 45.32); float drift = u_Time * speed;
            vec2 fallingUV = vec2(matrixUV.x, fract(matrixUV.y + drift));
            vec4 rawTex = texture(u_Texture, fallingUV);
            float glowWave = fract(matrixUV.y * 3.0 + drift);
            materialColor = vec3(0.0, 1.0, 0.2) * rawTex.rgb * (0.3 + 0.7 * glowWave);
            ambient = vec3(0.0, 0.02, 0.002) * materialColor;
        } else {
            vec2 gridUV = fract(v_WorldPos.xz * 1.0);
            float gridLine = step(0.97, gridUV.x) + step(0.97, gridUV.y);
            materialColor = mix(vec3(0.11, 0.11, 0.14), vec3(0.0, 0.45, 1.0), gridLine);
            ambient = vec3(0.01, 0.01, 0.015); 
        }
        vec3 lighting = vec3(0.0);
        vec3 flashLightDir = normalize(u_FlashlightPos.xyz - v_WorldPos);
        float flashDist = length(u_FlashlightPos.xyz - v_WorldPos);
        float flashAtt = 1.0 / (1.0 + 0.04 * flashDist + 0.008 * flashDist * flashDist);
        float theta = dot(flashLightDir, normalize(-u_FlashlightDir.xyz));
        if (theta > u_FlashlightPos.w) {
            float epsilon = 0.04; float intensity = clamp((theta - u_FlashlightPos.w) / epsilon, 0.0, 1.0);
            float diff = max(dot(normal, flashLightDir), 0.0);
            lighting += diff * vec3(0.95, 0.95, 1.0) * flashAtt * intensity * u_FlashlightDir.w;
        }
        for (int i = 0; i < u_LanternCount; i++) {
            vec3 lanternPos = u_Lanterns[i].xyz;
            float lanternAtt = 1.0 / (1.0 + 0.3 * length(lanternPos - v_WorldPos) + 0.2 * dot(lanternPos - v_WorldPos, lanternPos - v_WorldPos));
            vec3 lanternDir = normalize(lanternPos - v_WorldPos);
            float diff = max(dot(normal, lanternDir), 0.0);
            lighting += diff * vec3(1.0, 0.45, 0.06) * lanternAtt * 2.8;
        }
        FragColor = vec4(ambient + lighting * materialColor, 1.0);
    }";

    private const string HudVertexShaderGL = @"#version 300 es
    precision highp float; layout(location = 0) in vec2 InsidePos; layout(location = 1) in vec4 InColor;
    out vec4 v_Color; void main() { gl_Position = vec4(InsidePos, 0.0, 1.0); v_Color = InColor; }";

    private const string HudFragmentShaderGL = @"#version 300 es
    precision highp float; in vec4 v_Color; out vec4 FragColor; void main() { FragColor = v_Color; }";

    // SHADERY VULKAN (Pominięte by oszczędzić limit kodu, wywołujemy je osobno, aby naprawić błąd z RPi)
    private const string VertexShaderVK = @"#version 450
    layout(location = 0) in vec3 InsidePos; layout(location = 1) in vec3 InNormal; layout(location = 2) in vec2 InUV; layout(location = 3) in float InMatId; 
    layout(set = 0, binding = 0) uniform ViewProjBlock { mat4 u_ViewProj; };
    layout(location = 0) out vec3 v_WorldPos; layout(location = 1) out vec3 v_Normal; layout(location = 2) out vec2 v_UV; layout(location = 3) out float v_MatId;
    void main() { gl_Position = u_ViewProj * vec4(InsidePos, 1.0); v_WorldPos = InsidePos; v_Normal = InNormal; v_UV = InUV; v_MatId = InMatId; }";

    private const string FragmentShaderVK = @"#version 450
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
        FragColor = vec4(ambient + lighting * materialColor, 1.0);
    }";

    private const string HudVertexShaderVK = @"#version 450
    layout(location = 0) in vec2 InsidePos; layout(location = 1) in vec4 InColor; layout(location = 0) out vec4 v_Color;
    void main() { gl_Position = vec4(InsidePos, 0.0, 1.0); v_Color = InColor; }";

    private const string HudFragmentShaderVK = @"#version 450
    layout(location = 0) in vec4 v_Color; layout(location = 0) out vec4 FragColor; void main() { FragColor = v_Color; }";

    public void Initialize(string title, int width, int height, GraphicsBackend backend)
    {
        _baseTitle = title;
        WindowCreateInfo windowCI = new WindowCreateInfo { X = 0, Y = 0, WindowWidth = width, WindowHeight = height, WindowTitle = title, WindowInitialState = WindowState.FullScreen };
        _window = VeldridStartup.CreateWindow(ref windowCI);

        GraphicsDeviceOptions options = new GraphicsDeviceOptions { 
            Debug = false, HasMainSwapchain = true, SyncToVerticalBlank = false, PreferStandardClipSpaceYDirection = true, SwapchainDepthFormat = PixelFormat.D24_UNorm_S8_UInt
        };

        _device = VeldridStartup.CreateGraphicsDevice(_window, options, backend);
        _device.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
        _commandList = _device.ResourceFactory.CreateCommandList();

        LoadWallTexture();
        PrepareGraphicsPipeline();
    }

    private void LoadWallTexture()
    {
        string assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets"); string texturePath = Path.Combine(assetsDir, "wall_texture.png");
        uint width = 256; uint height = 256; byte[] pixelData;
        if (File.Exists(texturePath))
        {
            using (Stream stream = File.OpenRead(texturePath)) { ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha); width = (uint)image.Width; height = (uint)image.Height; pixelData = image.Data; }
        }
        else
        {
            pixelData = new byte[width * height * 4]; Random rand = new Random(1337);
            for (uint y = 0; y < height; y++) { for (uint x = 0; x < width; x++) {
                    uint index = (y * width + x) * 4; uint columnSize = 16; uint colX = x % columnSize;
                    bool isGlyphPart = colX > 2 && colX < 13 && (rand.Next(100) < 65);
                    if (isGlyphPart) {
                        byte greenIntensity = (byte)rand.Next(140, 255);
                        pixelData[index] = (byte)(greenIntensity / 6); pixelData[index + 1] = greenIntensity; pixelData[index + 2] = (byte)(greenIntensity / 2); pixelData[index + 3] = 255;
                    } else {
                        pixelData[index] = 0; pixelData[index + 1] = 12; pixelData[index + 2] = 0; pixelData[index + 3] = 255;
                    }
            }}
        }

        TextureDescription desc = TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled);
        _wallTexture = _device.ResourceFactory.CreateTexture(desc);
        _device.UpdateTexture(_wallTexture, pixelData, 0, 0, 0, width, height, 1, 0, 0);
        _wallTextureView = _device.ResourceFactory.CreateTextureView(_wallTexture);
        _sampler = _device.ResourceFactory.CreateSampler(SamplerDescription.Aniso4x);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Shader[] CreateVulkanShaders(ResourceFactory factory) => factory.CreateFromSpirv(new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexShaderVK), "main"), new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentShaderVK), "main"));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Shader[] CreateVulkanHudShaders(ResourceFactory factory) => factory.CreateFromSpirv(new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(HudVertexShaderVK), "main"), new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(HudFragmentShaderVK), "main"));

    private void PrepareGraphicsPipeline()
    {
        ResourceFactory factory = _device.ResourceFactory;
        Shader[] shaders; Shader[] hudShaders;

        switch (_device.BackendType)
        {
            case GraphicsBackend.Vulkan:
                shaders = CreateVulkanShaders(factory);
                hudShaders = CreateVulkanHudShaders(factory);
                break;
            case GraphicsBackend.OpenGL:
            case GraphicsBackend.OpenGLES:
                shaders = new[] { factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexShaderGL), "main")), factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentShaderGL), "main")) };
                hudShaders = new[] { factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(HudVertexShaderGL), "main")), factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(HudFragmentShaderGL), "main")) };
                break;
            default:
                throw new NotSupportedException();
        }

        _vertexBuffer = factory.CreateBuffer(new BufferDescription(4000000, BufferUsage.VertexBuffer));
        _viewProjBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
        _lightBuffer = factory.CreateBuffer(new BufferDescription(176, BufferUsage.UniformBuffer)); 

        ResourceLayout resourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("ViewProjBlock", ResourceKind.UniformBuffer, ShaderStages.Vertex),
            new ResourceLayoutElementDescription("LightBlock", ResourceKind.UniformBuffer, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("u_Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("u_Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
        ));

        _resourceSet = factory.CreateResourceSet(new ResourceSetDescription(resourceLayout, _viewProjBuffer, _lightBuffer, _wallTextureView, _sampler));

        GraphicsPipelineDescription pd = new GraphicsPipelineDescription {
            BlendState = BlendStateDescription.SingleOverrideBlend, DepthStencilState = new DepthStencilStateDescription(true, true, ComparisonKind.LessEqual),
            RasterizerState = RasterizerStateDescription.CullNone, PrimitiveTopology = PrimitiveTopology.TriangleList, ResourceLayouts = new[] { resourceLayout },
            ShaderSet = new ShaderSetDescription(
                new[] { new VertexLayoutDescription(new VertexElementDescription("InsidePos", VertexElementSemantic.Position, VertexElementFormat.Float3, 0), new VertexElementDescription("InNormal", VertexElementSemantic.Normal, VertexElementFormat.Float3, 12), new VertexElementDescription("InUV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2, 24), new VertexElementDescription("InMatId", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float1, 32)) },
                shaders
            ), Outputs = _device.MainSwapchain.Framebuffer.OutputDescription
        };
        _pipeline = factory.CreateGraphicsPipeline(ref pd);

        GraphicsPipelineDescription hpd = new GraphicsPipelineDescription {
            BlendState = BlendStateDescription.SingleAlphaBlend, DepthStencilState = DepthStencilStateDescription.Disabled, RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = PrimitiveTopology.TriangleList, ResourceLayouts = Array.Empty<ResourceLayout>(),
            ShaderSet = new ShaderSetDescription(
                new[] { new VertexLayoutDescription(new VertexElementDescription("InsidePos", VertexElementSemantic.Position, VertexElementFormat.Float2, 0), new VertexElementDescription("InColor", VertexElementSemantic.Color, VertexElementFormat.Float4, 8)) },
                hudShaders
            ), Outputs = _device.MainSwapchain.Framebuffer.OutputDescription
        };
        _hudPipeline = factory.CreateGraphicsPipeline(ref hpd);
        _hudVertexBuffer = factory.CreateBuffer(new BufferDescription(1000000, BufferUsage.VertexBuffer));
    }

    public void RegisterLantern(Vector3 position) { _frameLanterns.Add(position); }

    public void DrawHorizontalPlane(float x, float y, float z, float width, float depth, float matId, float nx, float ny, float nz)
    {
        float minX = x; float maxX = x + width; float minZ = z; float maxZ = z + depth;
        AddVertex(minX, y, minZ, nx, ny, nz, 0f, 0f, matId); AddVertex(minX, y, maxZ, nx, ny, nz, 0f, 1f, matId); AddVertex(maxX, y, maxZ, nx, ny, nz, 1f, 1f, matId);
        AddVertex(maxX, y, maxZ, nx, ny, nz, 1f, 1f, matId); AddVertex(maxX, y, minZ, nx, ny, nz, 1f, 0f, matId); AddVertex(minX, y, minZ, nx, ny, nz, 0f, 0f, matId);
        _gpuVerticesCounter += 6;
    }

    public void DrawCube(float x, float y, float z, float width, float height, float depth)
    {
        float minX = x; float maxX = x + width; float minY = y; float maxY = y + height; float minZ = z; float maxZ = z + depth;
        AddVertex(minX, minY, maxZ, 0f, 0f, 1f, 0f, 1f, 0f); AddVertex(minX, maxY, maxZ, 0f, 0f, 1f, 0f, 0f, 0f); AddVertex(maxX, maxY, maxZ, 0f, 0f, 1f, 1f, 0f, 0f);
        AddVertex(maxX, maxY, maxZ, 0f, 0f, 1f, 1f, 0f, 0f); AddVertex(maxX, minY, maxZ, 0f, 0f, 1f, 1f, 1f, 0f); AddVertex(minX, minY, maxZ, 0f, 0f, 1f, 0f, 1f, 0f);
        AddVertex(minX, minY, minZ, 0f, 0f, -1f, 1f, 1f, 0f); AddVertex(maxX, minY, minZ, 0f, 0f, -1f, 0f, 1f, 0f); AddVertex(maxX, maxY, minZ, 0f, 0f, -1f, 0f, 0f, 0f);
        AddVertex(maxX, maxY, minZ, 0f, 0f, -1f, 0f, 0f, 0f); AddVertex(minX, maxY, minZ, 0f, 0f, -1f, 1f, 0f, 0f); AddVertex(minX, minY, minZ, 0f, 0f, -1f, 1f, 1f, 0f);
        AddVertex(minX, maxY, minZ, 0f, 1f, 0f, 0f, 1f, 0f); AddVertex(maxX, maxY, minZ, 0f, 1f, 0f, 1f, 1f, 0f); AddVertex(maxX, maxY, maxZ, 0f, 1f, 0f, 1f, 0f, 0f);
        AddVertex(maxX, maxY, maxZ, 0f, 1f, 0f, 1f, 0f, 0f); AddVertex(minX, maxY, maxZ, 0f, 1f, 0f, 0f, 0f, 0f); AddVertex(minX, maxY, minZ, 0f, 1f, 0f, 0f, 1f, 0f);
        AddVertex(minX, minY, minZ, 0f, -1f, 0f, 0f, 0f, 0f); AddVertex(minX, minY, maxZ, 0f, -1f, 0f, 0f, 1f, 0f); AddVertex(maxX, minY, maxZ, 0f, -1f, 0f, 1f, 1f, 0f);
        AddVertex(maxX, minY, maxZ, 0f, -1f, 0f, 1f, 1f, 0f); AddVertex(maxX, minY, minZ, 0f, -1f, 0f, 1f, 0f, 0f); AddVertex(minX, minY, minZ, 0f, -1f, 0f, 0f, 0f, 0f);
        AddVertex(minX, minY, minZ, -1f, 0f, 0f, 0f, 1f, 0f); AddVertex(minX, maxY, minZ, -1f, 0f, 0f, 0f, 0f, 0f); AddVertex(minX, maxY, maxZ, -1f, 0f, 0f, 1f, 0f, 0f);
        AddVertex(minX, maxY, maxZ, -1f, 0f, 0f, 1f, 0f, 0f); AddVertex(minX, minY, maxZ, -1f, 0f, 0f, 1f, 1f, 0f); AddVertex(minX, minY, minZ, -1f, 0f, 0f, 0f, 1f, 0f);
        AddVertex(maxX, minY, minZ, 1f, 0f, 0f, 1f, 1f, 0f); AddVertex(maxX, minY, maxZ, 1f, 0f, 0f, 0f, 1f, 0f); AddVertex(maxX, maxY, maxZ, 1f, 0f, 0f, 0f, 0f, 0f);
        AddVertex(maxX, maxY, maxZ, 1f, 0f, 0f, 0f, 0f, 0f); AddVertex(maxX, maxY, minZ, 1f, 0f, 0f, 1f, 0f, 0f); AddVertex(maxX, minY, minZ, 1f, 0f, 0f, 1f, 1f, 0f);
        _gpuVerticesCounter += 36;
    }

    private void AddVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v, float matId)
    {
        _batchVertices.Add(x); _batchVertices.Add(y); _batchVertices.Add(z);
        _batchVertices.Add(nx); _batchVertices.Add(ny); _batchVertices.Add(nz);
        _batchVertices.Add(u); _batchVertices.Add(v); _batchVertices.Add(matId);
    }

    public void DrawHudRectangle(float screenX, float screenY, float width, float height, RgbaFloat color)
    {
        float ndcX = (screenX / _window.Width) * 2f - 1f; float ndcY = 1f - (screenY / _window.Height) * 2f;
        float ndcW = (width / _window.Width) * 2f; float ndcH = (height / _window.Height) * 2f;
        float left = ndcX; float right = ndcX + ndcW; float top = ndcY; float bottom = ndcY - ndcH;
        AddHudVertex(left, top, color); AddHudVertex(left, bottom, color); AddHudVertex(right, top, color);
        AddHudVertex(right, top, color); AddHudVertex(left, bottom, color); AddHudVertex(right, bottom, color);
    }

    private void AddHudVertex(float x, float y, RgbaFloat c)
    {
        _hudBatchVertices.Add(x); _hudBatchVertices.Add(y);
        _hudBatchVertices.Add(c.R); _hudBatchVertices.Add(c.G); _hudBatchVertices.Add(c.B); _hudBatchVertices.Add(c.A);
    }

    public void DrawHudText(string text, float startX, float startY, float pixelSize, RgbaFloat color)
    {
        float currentX = startX;
        foreach (char c in text)
        {
            if (Font.TryGetValue(char.ToUpper(c), out string[]? lines) && lines != null)
            {
                for (int r = 0; r < 5; r++) {
                    for (int col = 0; col < lines[r].Length; col++) {
                        if (lines[r][col] != ' ') DrawHudRectangle(currentX + col * pixelSize, startY + r * pixelSize, pixelSize, pixelSize, color);
                    }
                }
                currentX += (lines[0].Length + 1) * pixelSize; 
            }
            else currentX += 4 * pixelSize; 
        }
    }

    public void Run()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        double lastTime = 0;

        while (_window.Exists)
        {
            // --- SYSTEM SCEN ---
            if (_nextScene != null)
            {
                _currentScene?.OnUnload(this);
                _currentScene = _nextScene;
                _nextScene = null;
                _currentScene.OnLoad(this);
            }

            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            InputSnapshot snapshot = _window.PumpEvents();
            if (!_window.Exists) break;

            if (_window.Focused)
            {
                _window.CursorVisible = false;
                int cx = _window.Width / 2; int cy = _window.Height / 2;
                MouseDelta = new Vector2(snapshot.MousePosition.X - cx, snapshot.MousePosition.Y - cy);
                _window.SetMousePosition(cx, cy); 
            }
            else
            {
                _window.CursorVisible = true;
                MouseDelta = Vector2.Zero;
            }

            // Rozproszona aktualizacja sceny
            _currentScene?.OnUpdate(deltaTime, snapshot, this);

            // Bezpieczna aktualizacja i usuwanie obiektów
            if (_currentScene != null)
            {
                for (int i = 0; i < _currentScene.GameObjects.Count; i++)
                {
                    if (!_currentScene.GameObjects[i].IsDestroyed)
                        _currentScene.GameObjects[i].Update(deltaTime);
                }
                _currentScene.GameObjects.RemoveAll(o => o.IsDestroyed);
            }

            _frameCount++;
            _statTimer += deltaTime;
            if (_statTimer >= 0.25) 
            {
                _currentFps = _frameCount / _statTimer;
                double totalCpuTime = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
                double elapsedWallTime = _cpuStopwatch.Elapsed.TotalSeconds;
                if (elapsedWallTime > 0.0)
                {
                    _currentCpuPercent = ((totalCpuTime - _lastCpuTime) / elapsedWallTime) * 100.0 / Environment.ProcessorCount;
                    _ramUsage = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
                    _lastCpuTime = totalCpuTime;
                }
                _cpuStopwatch.Restart();
                _lastGpuDrawCalls = _gpuDrawCallsCounter; _lastGpuVertices = _gpuVerticesCounter;
                _frameCount = 0; _statTimer = 0;
            }

            _gpuDrawCallsCounter = 0; _gpuVerticesCounter = 0;

            _commandList.Begin();
            _commandList.SetFramebuffer(_device.MainSwapchain.Framebuffer);
            _commandList.SetViewport(0, new Viewport(0, 0, _window.Width, _window.Height, 0, 1));
            _commandList.ClearColorTarget(0, ClearColor);
            _commandList.ClearDepthStencil(1f);

            Vector3 forward = new Vector3(
                MathF.Sin(CameraYaw) * MathF.Cos(CameraPitch),
                MathF.Sin(CameraPitch),
                -MathF.Cos(CameraYaw) * MathF.Cos(CameraPitch)
            );
            CameraForward = forward;

            Vector3 eyePosition = new Vector3(CameraPosition.X, 0.8f, CameraPosition.Z);
            Vector3 targetPosition = eyePosition + forward;

            Matrix4x4 view = Matrix4x4.CreateLookAt(eyePosition, targetPosition, new Vector3(0, 1, 0));
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView((75f * MathF.PI) / 180f, Width / Height, 0.05f, 100f);
            _commandList.UpdateBuffer(_viewProjBuffer, 0, view * proj);

            _frameLanterns.Clear(); _batchVertices.Clear();

            if (ShowGameplayHud)
            {
                float viewDist = 36f; 
                float fXStart = MathF.Floor(CameraPosition.X / 2f) * 2f - viewDist;
                float fXEnd = MathF.Floor(CameraPosition.X / 2f) * 2f + viewDist;
                float fZStart = MathF.Floor(CameraPosition.Z / 2f) * 2f - viewDist;
                float fZEnd = MathF.Floor(CameraPosition.Z / 2f) * 2f + viewDist;

                for (float x = fXStart; x <= fXEnd; x += 2f) {
                    for (float z = fZStart; z <= fZEnd; z += 2f) {
                        DrawHorizontalPlane(x, 0.0f, z, 2f, 2f, 1.0f, 0f, 1f, 0f);
                        DrawHorizontalPlane(x, 2.0f, z, 2f, 2f, 1.0f, 0f, -1f, 0f);
                    }
                }

                if (_currentScene != null) {
                    foreach (var obj in _currentScene.GameObjects) obj.Render(this);
                }
            }

            _frameLanterns.Sort((a, b) => Vector3.DistanceSquared(eyePosition, a).CompareTo(Vector3.DistanceSquared(eyePosition, b)));
            
            LightData lightData = new LightData();
            lightData.FlashlightPos = new Vector4(eyePosition, 0.92f); 
            float flashBrightness = TriggerMuzzleFlash ? 12.0f : 3.0f;
            lightData.FlashlightDir = new Vector4(forward, flashBrightness);
            lightData.Time = (float)currentTime; 

            lightData.LanternCount = Math.Min(_frameLanterns.Count, 8);
            if (lightData.LanternCount > 0) lightData.Lantern0 = new Vector4(_frameLanterns[0], 1.0f);
            if (lightData.LanternCount > 1) lightData.Lantern1 = new Vector4(_frameLanterns[1], 1.0f);
            if (lightData.LanternCount > 2) lightData.Lantern2 = new Vector4(_frameLanterns[2], 1.0f);
            if (lightData.LanternCount > 3) lightData.Lantern3 = new Vector4(_frameLanterns[3], 1.0f);
            if (lightData.LanternCount > 4) lightData.Lantern4 = new Vector4(_frameLanterns[4], 1.0f);
            if (lightData.LanternCount > 5) lightData.Lantern5 = new Vector4(_frameLanterns[5], 1.0f);
            if (lightData.LanternCount > 6) lightData.Lantern6 = new Vector4(_frameLanterns[6], 1.0f);
            if (lightData.LanternCount > 7) lightData.Lantern7 = new Vector4(_frameLanterns[7], 1.0f);

            _commandList.UpdateBuffer(_lightBuffer, 0, lightData);

            if (_batchVertices.Count > 0)
            {
                _commandList.UpdateBuffer(_vertexBuffer, 0, _batchVertices.ToArray());
                _commandList.SetPipeline(_pipeline);
                _commandList.SetGraphicsResourceSet(0, _resourceSet);
                _commandList.SetVertexBuffer(0, _vertexBuffer);
                _commandList.Draw((uint)(_batchVertices.Count / 9)); 
                _gpuDrawCallsCounter++;
            }

            _hudBatchVertices.Clear();
            RgbaFloat matrixGreen = new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f);
            RgbaFloat darkGreenBar = new RgbaFloat(0.0f, 0.25f, 0.05f, 1.0f);

            _currentScene?.OnRenderUI(this);

            // Diagnostyka stała
            DrawHudRectangle(15, 15, 360, 205, new RgbaFloat(0.0f, 0.05f, 0.01f, 0.70f));
            DrawHudText($"FPS: {_currentFps:F0}", 25, 25, 3f, matrixGreen);
            DrawHudText($"CPU: {_currentCpuPercent:F1} %", 25, 55, 3f, matrixGreen);
            DrawHudRectangle(25, 75, 340, 6, darkGreenBar);
            DrawHudRectangle(25, 75, Math.Clamp((float)(_currentCpuPercent / 100.0 * 340.0), 0f, 340f), 6, matrixGreen);
            DrawHudText($"RAM: {_ramUsage:F0} MB", 25, 90, 3f, matrixGreen);
            DrawHudText($"GPU DC: {_lastGpuDrawCalls}", 25, 120, 3f, matrixGreen);
            DrawHudText($"GPU POLY: {_lastGpuVertices}", 25, 150, 3f, matrixGreen);
            double vramAllocated = (4000000 + 1000000 + 64 + 176) / (1024.0 * 1024.0); 
            DrawHudText($"VRAM EST: {vramAllocated:F2} MB", 25, 180, 3f, matrixGreen);

            if (ShowGameplayHud)
            {
                float midX = _window.Width / 2f; float midY = _window.Height / 2f;
                DrawHudRectangle(midX - 12, midY - 1, 24, 2, matrixGreen); 
                DrawHudRectangle(midX - 1, midY - 12, 2, 24, matrixGreen); 
                DrawHudRectangle(midX - 2, midY - 2, 4, 4, new RgbaFloat(1.0f, 0.0f, 0.0f, 0.9f)); 

                DrawHudRectangle(15, _window.Height - 65, 250, 50, new RgbaFloat(0.0f, 0.05f, 0.01f, 0.70f));
                DrawHudText($"ENG: {PlayerEnergy} %", 30, _window.Height - 53, 4f, matrixGreen);

                float gunBaseX = _window.Width - 320f;
                float gunBaseY = _window.Height - 240f + (WeaponRecoil * 120f);
                DrawHudRectangle(gunBaseX, gunBaseY, 140, 240, new RgbaFloat(0.05f, 0.15f, 0.08f, 0.95f));
                DrawHudRectangle(gunBaseX + 20, gunBaseY - 80, 25, 100, new RgbaFloat(0.02f, 0.22f, 0.05f, 1.0f));
                DrawHudRectangle(gunBaseX + 95, gunBaseY - 80, 25, 100, new RgbaFloat(0.02f, 0.22f, 0.05f, 1.0f));
                DrawHudRectangle(gunBaseX + 55, gunBaseY - 40, 30, 160, matrixGreen);

                if (WeaponRecoil > 0.15f)
                {
                    DrawHudRectangle(gunBaseX + 15, gunBaseY - 140, 110, 60, new RgbaFloat(0.5f, 1.0f, 0.6f, 0.8f));
                    DrawHudRectangle(gunBaseX + 45, gunBaseY - 180, 50, 40, new RgbaFloat(1.0f, 1.0f, 1.0f, 0.9f));
                }
            }

            if (_hudBatchVertices.Count > 0)
            {
                _commandList.UpdateBuffer(_hudVertexBuffer, 0, _hudBatchVertices.ToArray());
                _commandList.SetPipeline(_hudPipeline);
                _commandList.SetVertexBuffer(0, _hudVertexBuffer);
                _commandList.Draw((uint)(_hudBatchVertices.Count / 6)); 
                _gpuDrawCallsCounter++;
            }

            _commandList.End();
            _device.SubmitCommands(_commandList);
            _device.SwapBuffers(_device.MainSwapchain);
            TriggerMuzzleFlash = false;
        }

        _wallTexture.Dispose(); _wallTextureView.Dispose(); _sampler.Dispose();
        _vertexBuffer.Dispose(); _viewProjBuffer.Dispose(); _lightBuffer.Dispose(); 
        _hudVertexBuffer.Dispose(); _hudPipeline.Dispose(); _pipeline.Dispose(); _commandList.Dispose(); _device.Dispose();
    }
}