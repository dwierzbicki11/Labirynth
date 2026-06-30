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
using CyberEngine.Entities;
using CyberEngine.Core;
using CyberEngine.Scenes;
using System.Runtime.InteropServices;

namespace CyberEngine;

public class GameEngine
{
    private Sdl2Window _window = null!;
    private GraphicsDevice _device = null!;
    private CommandList _commandList = null!;

    private Pipeline _pipeline = null!;
    private DeviceBuffer _vertexBuffer = null!;
    private DeviceBuffer _viewProjBuffer = null!;
    private DeviceBuffer _lightBuffer = null!; 
    private ResourceSet _resourceSet = null!;
    private readonly float[] _batchVertices = new float[2000000]; 
    private int _batchIndex = 0;
    private readonly List<Vector3> _frameLanterns = new();

    private Pipeline _hudPipeline = null!;
    private DeviceBuffer _hudVertexBuffer = null!;
    private readonly float[] _hudBatchVertices = new float[500000];
    private int _hudBatchIndex = 0;

    // 🔥 NOWE: Zmienne potoku Post-Process i Off-screen
    private Pipeline _postPipeline = null!;
    private ResourceLayout _postResourceLayout = null!;
    private Framebuffer _offscreenFB = null!;
    private Texture _offscreenColor = null!;
    private Texture _offscreenDepth = null!;
    private TextureView _offscreenColorView = null!;
    private ResourceSet _postResourceSet = null!;
    private float _currentRenderScale = 1.0f;
    private int _currentResW=1280;
    private int _currentResH=720;

    private Texture _wallTexture = null!;
    private TextureView _wallTextureView = null!;
    private Sampler _sampler = null!;

    private double _lastCpuTime = 0;
    private readonly Stopwatch _cpuStopwatch = Stopwatch.StartNew();
    private double _currentCpuPercent = 0;
    private double _ramUsage = 0;
    private double _statTimer = 0;
    private double _currentFps = 0;
    private int _frameCount = 0;
    private int _gpuDrawCallsCounter = 0;
    private int _gpuVerticesCounter = 0;
    private int _lastGpuDrawCalls = 0;
    private int _lastGpuVertices = 0;

    private Scene _currentScene = null!;
    private Scene? _nextScene = null;

    public Vector3 CameraPosition { get; set; } = Vector3.Zero;
    public float CameraYaw { get; set; } = 0f;
    public float CameraPitch { get; set; } = 0f;
    public float WeaponRecoil { get; set; } = 0f;
    public int PlayerEnergy { get; set; } = 100;
    public bool ShowGameplayHud { get; set; } = false;

    public Vector3 CameraForward { get; set; } = Vector3.UnitZ;
    public Vector2 MouseDelta { get; private set; } = Vector2.Zero;
    public bool TriggerMuzzleFlash { get; set; } = false;

    public float Width => SystemConfig.ResolutionWidth;
    public float Height => SystemConfig.ResolutionHeight;
    public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black; 
    public string GpuName => _device.DeviceName;

    private TextureView _offscreenDepthView = null!;
    public void LoadScene(Scene scene) { _nextScene = scene; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LightData
    {
        public Vector4 FlashlightPos; 
        public Vector4 FlashlightDir; 
        public Vector4 Lantern0; public Vector4 Lantern1; public Vector4 Lantern2; public Vector4 Lantern3;
        public Vector4 Lantern4; public Vector4 Lantern5; public Vector4 Lantern6; public Vector4 Lantern7;
        public int LanternCount; public float Time; private float p2; private float p3; 
    }
    [StructLayout(LayoutKind.Sequential)]
public struct GraphicsSettingsBlock {
    public float RenderScale;
    public int Bloom; 
    public int MotionBlur; 
    public float BlurIntensity;
    public int Shadows;
    public int AntiAliasing;
    public int AO;
    public float DrawDistance; // Teraz jest
}

private DeviceBuffer _settingsBuffer;

    private static readonly Dictionary<char, string[]> Font = new() {
        ['0']=["███","█ █","█ █","█ █","███"], ['1']=["  █","  █","  █","  █","  █"], ['2']=["███","  █","███","█  ","███"], ['3']=["███","  █","███","  █","███"],
        ['4']=["█ █","█ █","███","  █","  █"], ['5']=["███","█  ","███","  █","███"], ['6']=["███","█  ","███","█ █","███"], ['7']=["███","  █","  █","  █","  █"],
        ['8']=["███","█ █","███","█ █","███"], ['9']=["███","█ █","███","  █","███"], ['C']=["███","█  ","█  ","█  ","███"], ['P']=["███","█ █","███","█  ","█  "],
        ['U']=["█ █","█ █","█ █","█ █","███"], ['R']=["███","█ █","███","██ ","█ █"], ['A']=["███","█ █","███","█ █","█ █"], ['M']=["█ █","███","█ █","█ █","█ █"],
        ['G']=["███","█  ","█ ██","█ █","███"], ['F']=["███","█  ","███","█  ","█  "], ['S']=["███","█  ","███","  █","███"], ['B']=["██ ","█ █","██ ","█ █","██ "],
        ['T']=["███"," █ "," █ "," █ "," █ "], ['H']=["█ █","█ █","███","█ █","█ █"], ['E']=["███","█  ","███","█  ","███"], ['I']=["███"," █ "," █ "," █ ","███"],
        ['N']=["███","█ █","█ █","█ █","███"], ['%']=["█ █","  █"," █ ","█  ","█ █"], [':']=["   "," █ ","   "," █ ","   "], ['.']=["   ","   ","   ","   "," █ "],
        [' ']=["   ","   ","   ","   ","   "], ['W']=["█ █","█ █","█ █","███","█ █"], ['L']=["█  ","█  ","█  ","█  ","███"], ['V']=["█ █","█ █","█ █","█ █"," █ "],
        ['D']=["██ ","█ █","█ █","█ █","██ "], ['K']=["█ █","█ █","██ ","█ █","█ █"], ['O']=["███","█ █","█ █","█ █","███"], ['Z']=["███","  █"," █ ","█  ","███"],
        ['Y']=["█ █","█ █","███","  █","███"], ['>']=["█  "," █ ","  █"," █ ","█  "], ['-']=["   ","   ","███","   ","   "], ['J']=["  █","  █","  █","█ █","███"]
    };
    public void UpdateSettingsBuffer()
{
    // Mapowanie C# -> Struktura GPU
    GraphicsSettingsBlock settings = new GraphicsSettingsBlock {
        RenderScale = SystemConfig.RenderScale,
        Bloom = SystemConfig.BloomEnabled ? 1 : 0,
        MotionBlur = SystemConfig.MotionBlurIntensity > 0 ? 1 : 0,
        BlurIntensity = SystemConfig.MotionBlurIntensity,
        Shadows = SystemConfig.ShadowsEnabled ? 1 : 0,
        AntiAliasing = SystemConfig.AntiAliasingMode,
        AO = SystemConfig.AmbientOcclusionEnabled ? 1 : 0,
        DrawDistance = SystemConfig.DrawDistance
    };
    // Używamy UpdateBuffer (to jest metoda Veldrid, która bezpiecznie nadpisuje dane w już istniejącym buforze)
    _commandList.UpdateBuffer(_settingsBuffer, 0, settings);
}

    public void ApplyGraphicsSettings()
    {
        bool rebuildOffscreen = false;

        if (_device != null && _device.SyncToVerticalBlank != SystemConfig.VSync)
            _device.SyncToVerticalBlank = SystemConfig.VSync;

        if ( _device != null)
        {
            if(_currentResW != SystemConfig.ResolutionWidth || _currentResH != SystemConfig.ResolutionHeight)
            {
                _currentResW = SystemConfig.ResolutionWidth;
                _currentResH = SystemConfig.ResolutionHeight;
                if (_window != null)
                {
                    _window.Width = SystemConfig.ResolutionWidth;
                    _window.Height = SystemConfig.ResolutionHeight;
                }
                rebuildOffscreen = true;
            }
            if (Math.Abs(_currentRenderScale - SystemConfig.RenderScale) > 0.01f)
            {
                _currentRenderScale = SystemConfig.RenderScale;
                rebuildOffscreen = true;
            }
        }
        
        if (rebuildOffscreen) CreateOffscreenFramebuffer();
    }

    public void Initialize(string title, int width, int height, GraphicsBackend backend)
    {
        WindowCreateInfo windowCI = new WindowCreateInfo { X = 0, Y = 0, WindowWidth = width, WindowHeight = height, WindowTitle = title, WindowInitialState = WindowState.FullScreen };
        _window = VeldridStartup.CreateWindow(ref windowCI);
        _device = VeldridStartup.CreateGraphicsDevice(_window, new GraphicsDeviceOptions { Debug = false, HasMainSwapchain = true, SyncToVerticalBlank = SystemConfig.VSync, PreferStandardClipSpaceYDirection = true, SwapchainDepthFormat = PixelFormat.D24_UNorm_S8_UInt }, backend);
        _commandList = _device.ResourceFactory.CreateCommandList();
        _currentRenderScale = SystemConfig.RenderScale;
        uint size = (uint)Marshal.SizeOf<GraphicsSettingsBlock>();
        _settingsBuffer = _device.ResourceFactory.CreateBuffer(new BufferDescription(size, BufferUsage.UniformBuffer));
        Console.WriteLine(size);
        LoadWallTexture();
        PrepareGraphicsPipeline();
        CreateOffscreenFramebuffer();
    }

    private void CreateOffscreenFramebuffer()
    {
        if (_offscreenFB != null) {
            _offscreenFB.Dispose(); _offscreenColor.Dispose(); _offscreenDepth.Dispose(); _offscreenColorView.Dispose(); _postResourceSet.Dispose();
        }

        uint w = (uint)(SystemConfig.ResolutionWidth * _currentRenderScale);
        uint h = (uint)(SystemConfig.ResolutionHeight * _currentRenderScale);
        if (w < 1) w = 1; if (h < 1) h = 1;

        _offscreenColor = _device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(w, h, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled));
        _offscreenDepth = _device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(w, h, 1, 1, PixelFormat.D24_UNorm_S8_UInt, TextureUsage.DepthStencil | TextureUsage.Sampled));
        _offscreenDepthView = _device.ResourceFactory.CreateTextureView(_offscreenDepth);
        
        _offscreenFB = _device.ResourceFactory.CreateFramebuffer(new FramebufferDescription(_offscreenDepth, _offscreenColor));
        _offscreenColorView = _device.ResourceFactory.CreateTextureView(_offscreenColor);

        _postResourceSet = _device.ResourceFactory.CreateResourceSet(new ResourceSetDescription(_postResourceLayout, _offscreenColorView, _device.LinearSampler,_settingsBuffer,_offscreenDepthView));
    }

    private void LoadWallTexture()
    {
        uint width = 256; uint height = 256; byte[] pixelData = new byte[width * height * 4]; Random rand = new Random(1337);
        for (uint y = 0; y < height; y++) { for (uint x = 0; x < width; x++) {
            uint index = (y * width + x) * 4; uint colX = x % 16;
            if (colX > 2 && colX < 13 && (rand.Next(100) < 65)) {
                byte green = (byte)rand.Next(140, 255);
                pixelData[index] = (byte)(green / 6); pixelData[index + 1] = green; pixelData[index + 2] = (byte)(green / 2); pixelData[index + 3] = 255;
            } else {
                pixelData[index] = 0; pixelData[index + 1] = 12; pixelData[index + 2] = 0; pixelData[index + 3] = 255;
            }
        }}
        _wallTexture = _device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));
        _device.UpdateTexture(_wallTexture, pixelData, 0, 0, 0, width, height, 1, 0, 0);
        _wallTextureView = _device.ResourceFactory.CreateTextureView(_wallTexture);
        _sampler = _device.ResourceFactory.CreateSampler(SamplerDescription.Point);
    }

    private void PrepareGraphicsPipeline()
    {
        ResourceFactory factory = _device.ResourceFactory;
        Shader[] shaders; Shader[] hudShaders; Shader[] postShaders;

        if (_device.BackendType != GraphicsBackend.Vulkan) throw new NotSupportedException("Wymagany Vulkan.");

         try
        {
            // Bezpieczne kotwiczenie ścieżek dla skompilowanego pliku binarnego
            string baseDir = AppContext.BaseDirectory;
            Console.WriteLine(baseDir);
            byte[] vertSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "vertex.spv"));
            byte[] fragSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "fragment.spv"));
            byte[] hudVertSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "hud_vertex.spv"));
            byte[] hudFragSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "hud_fragment.spv"));
            byte[] postVertSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "post_vertex.spv"));
            byte[] postFragSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "post_fragment.spv"));

            shaders = new[] {
                factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, vertSpv, "main")),
                factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, fragSpv, "main"))
            };
            hudShaders = new[] {
                factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, hudVertSpv, "main")),
                factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, hudFragSpv, "main"))
            };
            postShaders = new[] {
                factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, postVertSpv, "main")),
                factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, postFragSpv, "main"))
            };
        }
        catch (Exception ex) { throw new Exception("Błąd Vulkana! Brak plików SPV. Uruchom skrypt CompileShaders.sh. " + ex.Message); }
        _vertexBuffer = factory.CreateBuffer(new BufferDescription(8000000, BufferUsage.VertexBuffer));
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
            ShaderSet = new ShaderSetDescription(new[] { new VertexLayoutDescription(new VertexElementDescription("InsidePos", VertexElementSemantic.Position, VertexElementFormat.Float3, 0), new VertexElementDescription("InNormal", VertexElementSemantic.Normal, VertexElementFormat.Float3, 12), new VertexElementDescription("InUV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2, 24), new VertexElementDescription("InMatId", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float1, 32)) }, shaders),
            // Wpinamy logikę w dynamiczny framebuffer!
            Outputs = new OutputDescription(new OutputAttachmentDescription(PixelFormat.D24_UNorm_S8_UInt), new OutputAttachmentDescription(PixelFormat.R8_G8_B8_A8_UNorm))
        };
        _pipeline = factory.CreateGraphicsPipeline(ref pd);

        _postResourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("u_ScreenTex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("u_Sampler", ResourceKind.Sampler, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("GraphicsSettingsBlock",ResourceKind.UniformBuffer,ShaderStages.Fragment),
            new ResourceLayoutElementDescription("u_DethText", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
        ));

        GraphicsPipelineDescription postPd = new GraphicsPipelineDescription {
            BlendState = BlendStateDescription.SingleOverrideBlend, DepthStencilState = DepthStencilStateDescription.Disabled, RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = PrimitiveTopology.TriangleList, ResourceLayouts = new[] { _postResourceLayout },
            ShaderSet = new ShaderSetDescription(Array.Empty<VertexLayoutDescription>(), postShaders), Outputs = _device.MainSwapchain.Framebuffer.OutputDescription
        };
        _postPipeline = factory.CreateGraphicsPipeline(ref postPd);

        GraphicsPipelineDescription hpd = new GraphicsPipelineDescription {
            BlendState = BlendStateDescription.SingleAlphaBlend, DepthStencilState = DepthStencilStateDescription.Disabled, RasterizerState = RasterizerStateDescription.CullNone, PrimitiveTopology = PrimitiveTopology.TriangleList, ResourceLayouts = Array.Empty<ResourceLayout>(),
            ShaderSet = new ShaderSetDescription(new[] { new VertexLayoutDescription(new VertexElementDescription("InsidePos", VertexElementSemantic.Position, VertexElementFormat.Float2, 0), new VertexElementDescription("InColor", VertexElementSemantic.Color, VertexElementFormat.Float4, 8)) }, hudShaders), Outputs = _device.MainSwapchain.Framebuffer.OutputDescription
        };
        _hudPipeline = factory.CreateGraphicsPipeline(ref hpd);
        _hudVertexBuffer = factory.CreateBuffer(new BufferDescription(2000000, BufferUsage.VertexBuffer));
    }

    public void RegisterLantern(Vector3 position) { _frameLanterns.Add(position); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v, float matId)
    {
        _batchVertices[_batchIndex++] = x; _batchVertices[_batchIndex++] = y; _batchVertices[_batchIndex++] = z;
        _batchVertices[_batchIndex++] = nx; _batchVertices[_batchIndex++] = ny; _batchVertices[_batchIndex++] = nz;
        _batchVertices[_batchIndex++] = u; _batchVertices[_batchIndex++] = v; _batchVertices[_batchIndex++] = matId;
    }

    public void DrawHorizontalPlane(float x, float y, float z, float width, float depth, float matId, float nx, float ny, float nz)
    {
        float mx = x + width; float mz = z + depth;
        AddVertex(x, y, z, nx, ny, nz, 0f, 0f, matId); AddVertex(x, y, mz, nx, ny, nz, 0f, 1f, matId); AddVertex(mx, y, mz, nx, ny, nz, 1f, 1f, matId);
        AddVertex(mx, y, mz, nx, ny, nz, 1f, 1f, matId); AddVertex(mx, y, z, nx, ny, nz, 1f, 0f, matId); AddVertex(x, y, z, nx, ny, nz, 0f, 0f, matId);
        _gpuVerticesCounter += 6;
    }

    public void DrawCube(float x, float y, float z, float width, float height, float depth)
    {
        float mx = x + width; float my = y + height; float mz = z + depth;
        AddVertex(x, y, mz, 0f, 0f, 1f, 0f, 1f, 0f); AddVertex(x, my, mz, 0f, 0f, 1f, 0f, 0f, 0f); AddVertex(mx, my, mz, 0f, 0f, 1f, 1f, 0f, 0f);
        AddVertex(mx, my, mz, 0f, 0f, 1f, 1f, 0f, 0f); AddVertex(mx, y, mz, 0f, 0f, 1f, 1f, 1f, 0f); AddVertex(x, y, mz, 0f, 0f, 1f, 0f, 1f, 0f);
        AddVertex(x, y, z, 0f, 0f, -1f, 1f, 1f, 0f); AddVertex(mx, y, z, 0f, 0f, -1f, 0f, 1f, 0f); AddVertex(mx, my, z, 0f, 0f, -1f, 0f, 0f, 0f);
        AddVertex(mx, my, z, 0f, 0f, -1f, 0f, 0f, 0f); AddVertex(x, my, z, 0f, 0f, -1f, 1f, 0f, 0f); AddVertex(x, y, z, 0f, 0f, -1f, 1f, 1f, 0f);
        AddVertex(x, my, z, 0f, 1f, 0f, 0f, 1f, 0f); AddVertex(mx, my, z, 0f, 1f, 0f, 1f, 1f, 0f); AddVertex(mx, my, mz, 0f, 1f, 0f, 1f, 0f, 0f);
        AddVertex(mx, my, mz, 0f, 1f, 0f, 1f, 0f, 0f); AddVertex(x, my, mz, 0f, 1f, 0f, 0f, 0f, 0f); AddVertex(x, my, z, 0f, 1f, 0f, 0f, 1f, 0f);
        AddVertex(x, y, z, 0f, -1f, 0f, 0f, 0f, 0f); AddVertex(x, y, mz, 0f, -1f, 0f, 0f, 1f, 0f); AddVertex(mx, y, mz, 0f, -1f, 0f, 1f, 1f, 0f);
        AddVertex(mx, y, mz, 0f, -1f, 0f, 1f, 1f, 0f); AddVertex(mx, y, z, 0f, -1f, 0f, 1f, 0f, 0f); AddVertex(x, y, z, 0f, -1f, 0f, 0f, 0f, 0f);
        AddVertex(x, y, z, -1f, 0f, 0f, 0f, 1f, 0f); AddVertex(x, my, z, -1f, 0f, 0f, 0f, 0f, 0f); AddVertex(x, my, mz, -1f, 0f, 0f, 1f, 0f, 0f);
        AddVertex(x, my, mz, -1f, 0f, 0f, 1f, 0f, 0f); AddVertex(x, y, mz, -1f, 0f, 0f, 1f, 1f, 0f); AddVertex(x, y, z, -1f, 0f, 0f, 0f, 1f, 0f);
        AddVertex(mx, y, z, 1f, 0f, 0f, 1f, 1f, 0f); AddVertex(mx, y, mz, 1f, 0f, 0f, 0f, 1f, 0f); AddVertex(mx, my, mz, 1f, 0f, 0f, 0f, 0f, 0f);
        AddVertex(mx, my, mz, 1f, 0f, 0f, 0f, 0f, 0f); AddVertex(mx, my, z, 1f, 0f, 0f, 1f, 0f, 0f); AddVertex(mx, y, z, 1f, 0f, 0f, 1f, 1f, 0f);
        _gpuVerticesCounter += 36;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHudVertex(float x, float y, RgbaFloat c)
    {
        _hudBatchVertices[_hudBatchIndex++] = x; _hudBatchVertices[_hudBatchIndex++] = y;
        _hudBatchVertices[_hudBatchIndex++] = c.R; _hudBatchVertices[_hudBatchIndex++] = c.G;
        _hudBatchVertices[_hudBatchIndex++] = c.B; _hudBatchVertices[_hudBatchIndex++] = c.A;
    }

    public void DrawHudRectangle(float screenX, float screenY, float width, float height, RgbaFloat color)
    {
        float left = (screenX / SystemConfig.ResolutionWidth) * 2f - 1f; 
        float right = left + (width / SystemConfig.ResolutionWidth) * 2f;
        float top = 1f - (screenY / SystemConfig.ResolutionHeight) * 2f; 
        float bottom = top - (height / SystemConfig.ResolutionHeight) * 2f;
        AddHudVertex(left, top, color); AddHudVertex(left, bottom, color); 
        AddHudVertex(right, top, color);
        AddHudVertex(right, top, color); AddHudVertex(left, bottom, color); 
        AddHudVertex(right, bottom, color);
    }

    public void DrawHudText(string text, float startX, float startY, float pixelSize, RgbaFloat color)
    {
        float currentX = startX;
        foreach (char c in text)
        {
            if (Font.TryGetValue(char.ToUpper(c), out string[]? lines) && lines != null) {
                for (int r = 0; r < 5; r++) {
                    for (int col = 0; col < lines[r].Length; col++) {
                        if (lines[r][col] != ' ') DrawHudRectangle(currentX + col * pixelSize, startY + r * pixelSize, pixelSize, pixelSize, color);
                    }
                }
                currentX += (lines[0].Length + 1) * pixelSize; 
            } else currentX += 4 * pixelSize; 
        }
    }

    public unsafe void Run()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        double lastTime = 0;

        while (_window.Exists)
        {
            if (_nextScene != null)
            {
                _currentScene?.OnUnload(this); _currentScene = _nextScene; _nextScene = null; _currentScene.OnLoad(this);
            }

            double currentTime = stopwatch.Elapsed.TotalSeconds; double deltaTime = currentTime - lastTime; lastTime = currentTime;
            InputSnapshot snapshot = _window.PumpEvents();
            if (!_window.Exists) break;

            if (_window.Focused) {
                _window.CursorVisible = false;
                int cx = _window.Width / 2; int cy = _window.Height / 2;
                MouseDelta = new Vector2(snapshot.MousePosition.X - cx, snapshot.MousePosition.Y - cy);
                _window.SetMousePosition(cx, cy); 
            } else { _window.CursorVisible = true; MouseDelta = Vector2.Zero; }

            _currentScene?.OnUpdate(deltaTime, snapshot, this);

            if (_currentScene != null)
            {
                for (int i = 0; i < _currentScene.GameObjects.Count; i++)
                    if (!_currentScene.GameObjects[i].IsDestroyed) _currentScene.GameObjects[i].Update(deltaTime);
                _currentScene.GameObjects.RemoveAll(o => o.IsDestroyed);
            }

            _frameCount++; _statTimer += deltaTime;
            if (_statTimer >= 0.25) 
            {
                _currentFps = _frameCount / _statTimer;
                double totalCpuTime = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds; double elapsedWallTime = _cpuStopwatch.Elapsed.TotalSeconds;
                if (elapsedWallTime > 0.0) {
                    _currentCpuPercent = ((totalCpuTime - _lastCpuTime) / elapsedWallTime) * 100.0 / Environment.ProcessorCount;
                    _ramUsage = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0); _lastCpuTime = totalCpuTime;
                }
                _cpuStopwatch.Restart(); _lastGpuDrawCalls = _gpuDrawCallsCounter; _lastGpuVertices = _gpuVerticesCounter;
                _frameCount = 0; _statTimer = 0;
            }

            _gpuDrawCallsCounter = 0; _gpuVerticesCounter = 0;

            _commandList.Begin();
            UpdateSettingsBuffer();

            // 🔥 PRZEBIEG 1: Renderowanie 3D do tekstury mniejszej o wskaźnik RenderScale
            _commandList.SetFramebuffer(_offscreenFB);
            _commandList.SetViewport(0, new Viewport(0, 0, _offscreenFB.Width, _offscreenFB.Height, 0, 1));
            _commandList.ClearColorTarget(0, ClearColor); 
            _commandList.ClearDepthStencil(1f);

            Vector3 forward = new Vector3(MathF.Sin(CameraYaw) * MathF.Cos(CameraPitch), MathF.Sin(CameraPitch), -MathF.Cos(CameraYaw) * MathF.Cos(CameraPitch));
            CameraForward = forward;
            Vector3 eyePosition = new Vector3(CameraPosition.X, 0.8f, CameraPosition.Z);
            Vector3 targetPosition = eyePosition + forward;

            Matrix4x4 view = Matrix4x4.CreateLookAt(eyePosition, targetPosition, new Vector3(0, 1, 0));
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView((SystemConfig.Fov * MathF.PI) / 180f, Width / Height, 0.05f, 100f);
            _commandList.UpdateBuffer(_viewProjBuffer, 0, view * proj);

            _frameLanterns.Clear(); 
            _batchIndex = 0; 

            if (ShowGameplayHud)
            {
                float viewDist = SystemConfig.GraphicsQuality switch { 0 => 16f, 1 => 24f, 2 => 36f, _ => 24f };
                float fXStart = MathF.Floor(CameraPosition.X / 2f) * 2f - viewDist; float fXEnd = MathF.Floor(CameraPosition.X / 2f) * 2f + viewDist;
                float fZStart = MathF.Floor(CameraPosition.Z / 2f) * 2f - viewDist; float fZEnd = MathF.Floor(CameraPosition.Z / 2f) * 2f + viewDist;
                for (float x = fXStart; x <= fXEnd; x += 2f) {
                    for (float z = fZStart; z <= fZEnd; z += 2f) {
                        DrawHorizontalPlane(x, 0.0f, z, 2f, 2f, 1.0f, 0f, 1f, 0f); DrawHorizontalPlane(x, 2.0f, z, 2f, 2f, 1.0f, 0f, -1f, 0f);
                    }
                }
                if (_currentScene != null) foreach (var obj in _currentScene.GameObjects) obj.Render(this);
            }

            _frameLanterns.Sort((a, b) => Vector3.DistanceSquared(eyePosition, a).CompareTo(Vector3.DistanceSquared(eyePosition, b)));
            
            LightData lightData = new LightData();
            lightData.FlashlightPos = new Vector4(eyePosition, 0.92f); 
            lightData.FlashlightDir = new Vector4(forward, TriggerMuzzleFlash ? 12.0f : 3.0f);
            lightData.Time = (float)currentTime; 
            
            int maxLanterns = SystemConfig.GraphicsQuality switch { 0 => 2, 1 => 4, 2 => 8, _ => 4 };
            lightData.LanternCount = Math.Min(_frameLanterns.Count, maxLanterns);
            
            if (lightData.LanternCount > 0) lightData.Lantern0 = new Vector4(_frameLanterns[0], 1.0f);
            if (lightData.LanternCount > 1) lightData.Lantern1 = new Vector4(_frameLanterns[1], 1.0f);
            if (lightData.LanternCount > 2) lightData.Lantern2 = new Vector4(_frameLanterns[2], 1.0f);
            if (lightData.LanternCount > 3) lightData.Lantern3 = new Vector4(_frameLanterns[3], 1.0f);
            if (lightData.LanternCount > 4) lightData.Lantern4 = new Vector4(_frameLanterns[4], 1.0f);
            if (lightData.LanternCount > 5) lightData.Lantern5 = new Vector4(_frameLanterns[5], 1.0f);
            if (lightData.LanternCount > 6) lightData.Lantern6 = new Vector4(_frameLanterns[6], 1.0f);
            if (lightData.LanternCount > 7) lightData.Lantern7 = new Vector4(_frameLanterns[7], 1.0f);

            _commandList.UpdateBuffer(_lightBuffer, 0, lightData);

            if (_batchIndex > 0)
            {
                fixed (float* ptr = _batchVertices) { _commandList.UpdateBuffer(_vertexBuffer, 0, (IntPtr)ptr, (uint)(_batchIndex * sizeof(float))); }
                _commandList.SetPipeline(_pipeline);
                _commandList.SetGraphicsResourceSet(0, _resourceSet);
                _commandList.SetVertexBuffer(0, _vertexBuffer);
                _commandList.Draw((uint)(_batchIndex / 9)); 
                _gpuDrawCallsCounter++;
            }

            // 🔥 PRZEBIEG 2: Wyciągamy Off-screen Texture, nakładamy wyostrzenie splotowe (Sharpen) i skalujemy na MainSwapchain
            _commandList.SetFramebuffer(_device.MainSwapchain.Framebuffer);
            _commandList.SetViewport(0, new Viewport(0, 0, _window.Width, _window.Height, 0, 1));
            _commandList.ClearColorTarget(0, RgbaFloat.Black);

            _commandList.SetPipeline(_postPipeline);
            _commandList.SetGraphicsResourceSet(0, _postResourceSet);
            _commandList.Draw(3); // 3 wierzchołki trójkąta wypełniającego ekran wygenerowane logicznie w Shaderze

            // 🔥 PRZEBIEG 3: HUD 2D (Renderowany w natywnej rozdzielczości ekranu bez rozmycia!)
            _hudBatchIndex = 0;
            RgbaFloat matrixGreen = new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f);
            RgbaFloat darkGreenBar = new RgbaFloat(0.0f, 0.25f, 0.05f, 1.0f);

            _currentScene?.OnRenderUI(this);

            DrawHudRectangle(15, 15, 360, 205, new RgbaFloat(0.0f, 0.05f, 0.01f, 0.70f));
            DrawHudText($"FPS: {_currentFps:F0}", 25, 25, 3f, matrixGreen);
            DrawHudText($"CPU: {_currentCpuPercent:F1} %", 25, 55, 3f, matrixGreen);
            DrawHudRectangle(25, 75, 340, 6, darkGreenBar);
            DrawHudRectangle(25, 75, Math.Clamp((float)(_currentCpuPercent / 100.0 * 340.0), 0f, 340f), 6, matrixGreen);
            DrawHudText($"RAM: {_ramUsage:F0} MB", 25, 90, 3f, matrixGreen);
            DrawHudText($"GPU DC: {_lastGpuDrawCalls}", 25, 120, 3f, matrixGreen);
            DrawHudText($"GPU POLY: {_lastGpuVertices}", 25, 150, 3f, matrixGreen);

            if (ShowGameplayHud)
            {
                float midX = Width / 2f; float midY = Height / 2f;
                DrawHudRectangle(midX - 12, midY - 1, 24, 2, matrixGreen); 
                DrawHudRectangle(midX - 1, midY - 12, 2, 24, matrixGreen); 
                DrawHudRectangle(15, Height - 65, 250, 50, new RgbaFloat(0.0f, 0.05f, 0.01f, 0.70f));
                DrawHudText($"ENG: {PlayerEnergy} %", 30, Height - 53, 4f, matrixGreen);

                float gunBaseX = Width - 320f; float gunBaseY = Height - 240f + (WeaponRecoil * 120f);
                DrawHudRectangle(gunBaseX, gunBaseY, 140, 240, new RgbaFloat(0.05f, 0.15f, 0.08f, 0.95f));
                DrawHudRectangle(gunBaseX + 20, gunBaseY - 80, 25, 100, new RgbaFloat(0.02f, 0.22f, 0.05f, 1.0f));
                DrawHudRectangle(gunBaseX + 95, gunBaseY - 80, 25, 100, new RgbaFloat(0.02f, 0.22f, 0.05f, 1.0f));
                DrawHudRectangle(gunBaseX + 55, gunBaseY - 40, 30, 160, matrixGreen);

                if (WeaponRecoil > 0.15f) {
                    DrawHudRectangle(gunBaseX + 15, gunBaseY - 140, 110, 60, new RgbaFloat(0.5f, 1.0f, 0.6f, 0.8f));
                    DrawHudRectangle(gunBaseX + 45, gunBaseY - 180, 50, 40, new RgbaFloat(1.0f, 1.0f, 1.0f, 0.9f));
                }
            }

            if (_hudBatchIndex > 0)
            {
                fixed (float* ptr = _hudBatchVertices) { _commandList.UpdateBuffer(_hudVertexBuffer, 0, (IntPtr)ptr, (uint)(_hudBatchIndex * sizeof(float))); }
                _commandList.SetPipeline(_hudPipeline);
                _commandList.SetVertexBuffer(0, _hudVertexBuffer);
                _commandList.Draw((uint)(_hudBatchIndex / 6)); 
                _gpuDrawCallsCounter++;
            }

            _commandList.End(); _device.SubmitCommands(_commandList); _device.SwapBuffers(_device.MainSwapchain);
            TriggerMuzzleFlash = false;
        }

        if (_offscreenFB != null) { _offscreenFB.Dispose(); _offscreenColor.Dispose(); _offscreenDepth.Dispose(); _offscreenColorView.Dispose();_offscreenDepthView.Dispose(); _postResourceSet.Dispose(); }
        _wallTexture.Dispose(); _wallTextureView.Dispose(); _sampler.Dispose();
        _vertexBuffer.Dispose(); _viewProjBuffer.Dispose(); _lightBuffer.Dispose(); 
        _hudVertexBuffer.Dispose(); _hudPipeline.Dispose(); _pipeline.Dispose(); _postPipeline.Dispose(); _commandList.Dispose(); _device.Dispose();
    }
}
