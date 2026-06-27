using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.IO;
using StbImageSharp;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using CyberEngine.Entities;

namespace CyberEngine;

public class GameEngine
{
    private Sdl2Window _window = null!;
    private GraphicsDevice _device = null!;
    private CommandList _commandList = null!;
    private string _baseTitle = "";

    private Pipeline _pipeline = null!;
    private DeviceBuffer _vertexBuffer = null!;
    private DeviceBuffer _viewProjBuffer = null!;
    private ResourceSet _resourceSet = null!;
    private readonly List<float> _batchVertices = new();

    // NOWE ZASOBY DLA TEKSTUR
    private Texture _wallTexture = null!;
    private TextureView _wallTextureView = null!;
    private Sampler _sampler = null!;

    public Core.Math.Vector3 CameraPosition { get; set; } = Core.Math.Vector3.Zero;
    public float Width => _window.Width;
    public float Height => _window.Height;

    public List<GameObject> GameObjects { get; } = new();
    public RgbaFloat ClearColor { get; set; } = new RgbaFloat(0.01f, 0.01f, 0.02f, 1.0f);

    // KOD SHADERÓW OBSŁUGUJĄCY TEKSTUROWANIE
    private const string VertexShaderSource = @"#version 330 core
    layout(location = 0) in vec3 InsidePos;
    layout(location = 1) in vec2 InUV; // Współrzędne UV zamiast koloru
    layout(std140) uniform ViewProjBlock {
        mat4 u_ViewProj;
    };
    out vec2 v_UV;
    void main() {
        gl_Position = u_ViewProj * vec4(InsidePos, 1.0);
        v_UV = InUV;
    }";

    private const string FragmentShaderSource = @"#version 330 core
    in vec2 v_UV;
    uniform sampler2D u_Texture; // Próbnik tekstury
    out vec4 FragColor;
    void main() {
        FragColor = texture(u_Texture, v_UV);
    }";

    public void Initialize(string title, int width, int height, GraphicsBackend backend)
    {
        _baseTitle = title;
        WindowCreateInfo windowCI = new WindowCreateInfo { 
            X = 100, Y = 100, WindowWidth = width, WindowHeight = height, WindowTitle = title 
        };
        _window = VeldridStartup.CreateWindow(ref windowCI);

        GraphicsDeviceOptions options = new GraphicsDeviceOptions { 
            Debug = false, HasMainSwapchain = true, SyncToVerticalBlank = false,
            PreferStandardClipSpaceYDirection = true,
            SwapchainDepthFormat = PixelFormat.D24_UNorm_S8_UInt
        };

        _device = VeldridStartup.CreateGraphicsDevice(_window, options, backend);
        _commandList = _device.ResourceFactory.CreateCommandList();

        // ŁADUJEMY TEKSTURĘ ŚCIANY PRZED PRZYGOTOWANIEM POTOKU
        LoadWallTexture();
        PrepareGraphicsPipeline();
        
        Message.ok($"Silnik teksturujący gotowy. Aktywne API: {_device.BackendType}");
    }

    private void LoadWallTexture()
{
    string texturePath = Path.Combine(AppContext.BaseDirectory, "Assets", "wall_texture.png");
    if (!File.Exists(texturePath))
    {
        Message.error($"Nie znaleziono tekstury: {texturePath}. Używam pustej.");
        return;
    }

    using (Stream stream = File.OpenRead(texturePath))
    {
        // StbImageSharp automatycznie dekoduje plik do czystej tablicy bajtów RGBA
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        TextureDescription desc = TextureDescription.Texture2D(
            (uint)image.Width, 
            (uint)image.Height, 
            1, 1,
            PixelFormat.R8_G8_B8_A8_UNorm, 
            TextureUsage.Sampled
        );
        
        _wallTexture = _device.ResourceFactory.CreateTexture(desc);
        
        // 🔥 POTĘŻNE UPROSZCZENIE: Veldrid posiada funkcję UpdateTexture,
        // która przyjmuje surową tablicę bajtów z StbImageSharp bez żadnego Map/Unmap!
        _device.UpdateTexture(
            _wallTexture, 
            image.Data, 
            0, 0, 0, 
            (uint)image.Width, 
            (uint)image.Height, 
            1, 0, 0
        );

        _wallTextureView = _device.ResourceFactory.CreateTextureView(_wallTexture);
    }
    
    _sampler = _device.ResourceFactory.CreateSampler(SamplerDescription.Aniso4x);
}

    private void PrepareGraphicsPipeline()
    {
        ResourceFactory factory = _device.ResourceFactory;

        Shader vertexShader = factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexShaderSource), "main"));
        Shader fragmentShader = factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentShaderSource), "main"));

        _vertexBuffer = factory.CreateBuffer(new BufferDescription(2000000, BufferUsage.VertexBuffer));
        _viewProjBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

        ResourceLayout resourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("ViewProjBlock", ResourceKind.UniformBuffer, ShaderStages.Vertex),
            new ResourceLayoutElementDescription("u_Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("u_Sampler", ResourceKind.Sampler, ShaderStages.Fragment)
        ));

        _resourceSet = factory.CreateResourceSet(new ResourceSetDescription(resourceLayout, 
            _viewProjBuffer, _wallTextureView, _sampler));

        GraphicsPipelineDescription pd = new GraphicsPipelineDescription {
            BlendState = BlendStateDescription.SingleOverrideBlend,
            DepthStencilState = new DepthStencilStateDescription(true, true, ComparisonKind.LessEqual),
            RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = new[] { resourceLayout },
            ShaderSet = new ShaderSetDescription(
                new[] { 
                    new VertexLayoutDescription(
                        new VertexElementDescription("InsidePos", VertexElementSemantic.Position, VertexElementFormat.Float3, 0),
                        new VertexElementDescription("InUV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2, 12) // UV: 2 floaty (8 bajtów)
                    ) 
                },
                new[] { vertexShader, fragmentShader }
            ),
            Outputs = _device.MainSwapchain.Framebuffer.OutputDescription
        };
        _pipeline = factory.CreateGraphicsPipeline(ref pd);
    }

    public void DrawCube(float x, float y, float z, float width, float height, float depth)
    {
        float minX = x; float maxX = x + width;
        float minY = y; float maxY = y + height;
        float minZ = z; float maxZ = z + depth;

        // DEFINIUJEMY WIERZCHOŁKI XYZ ORAZ WSPÓŁRZĘDNE UV (Od 0.0 do 1.0)
        // Ściana Przednia
        AddVertex(minX, minY, maxZ, 0f, 1f); AddVertex(minX, maxY, maxZ, 0f, 0f); AddVertex(maxX, maxY, maxZ, 1f, 0f);
        AddVertex(maxX, maxY, maxZ, 1f, 0f); AddVertex(maxX, minY, maxZ, 1f, 1f); AddVertex(minX, minY, maxZ, 0f, 1f);
        // Ściana Tylna
        AddVertex(minX, minY, minZ, 1f, 1f); AddVertex(maxX, minY, minZ, 0f, 1f); AddVertex(maxX, maxY, minZ, 0f, 0f);
        AddVertex(maxX, maxY, minZ, 0f, 0f); AddVertex(minX, maxY, minZ, 1f, 0f); AddVertex(minX, minY, minZ, 1f, 1f);
        // Ściana Górna
        AddVertex(minX, maxY, minZ, 0f, 1f); AddVertex(maxX, maxY, minZ, 1f, 1f); AddVertex(maxX, maxY, maxZ, 1f, 0f);
        AddVertex(maxX, maxY, maxZ, 1f, 0f); AddVertex(minX, maxY, maxZ, 0f, 0f); AddVertex(minX, maxY, minZ, 0f, 1f);
        // Ściana Dolna
        AddVertex(minX, minY, minZ, 0f, 0f); AddVertex(minX, minY, maxZ, 0f, 1f); AddVertex(maxX, minY, maxZ, 1f, 1f);
        AddVertex(maxX, minY, maxZ, 1f, 1f); AddVertex(maxX, minY, minZ, 1f, 0f); AddVertex(minX, minY, minZ, 0f, 0f);
        // Ściana Lewa
        AddVertex(minX, minY, minZ, 0f, 1f); AddVertex(minX, maxY, minZ, 0f, 0f); AddVertex(minX, maxY, maxZ, 1f, 0f);
        AddVertex(minX, maxY, maxZ, 1f, 0f); AddVertex(minX, minY, maxZ, 1f, 1f); AddVertex(minX, minY, minZ, 0f, 1f);
        // Ściana Prawa
        AddVertex(maxX, minY, minZ, 1f, 1f); AddVertex(maxX, minY, maxZ, 0f, 1f); AddVertex(maxX, maxY, maxZ, 0f, 0f);
        AddVertex(maxX, maxY, maxZ, 0f, 0f); AddVertex(maxX, maxY, minZ, 1f, 0f); AddVertex(maxX, minY, minZ, 1f, 1f);
    }

    private void AddVertex(float x, float y, float z, float u, float v)
    {
        _batchVertices.Add(x); _batchVertices.Add(y); _batchVertices.Add(z);
        _batchVertices.Add(u); _batchVertices.Add(v);
    }

    public void Run(Action<double, InputSnapshot> onLogicUpdate)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        double lastTime = 0; double fpsTimer = 0; int frameCount = 0;

        while (_window.Exists)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            InputSnapshot snapshot = _window.PumpEvents();
            if (!_window.Exists) break;

            onLogicUpdate(deltaTime, snapshot);
            foreach (var obj in GameObjects) obj.Update(deltaTime);

            _commandList.Begin();
            _commandList.SetFramebuffer(_device.MainSwapchain.Framebuffer);
            _commandList.ClearColorTarget(0, ClearColor);
            _commandList.ClearDepthStencil(1f);

            // AKTUALIZACJA MACIERZY KAMERY FPP
            float playerYaw = 0f;
            if (GameObjects.Count > 0 && GameObjects[0] is Player p) playerYaw = p.Yaw;

            Vector3 forward = new Vector3(MathF.Sin(playerYaw), 0f, -MathF.Cos(playerYaw));
            Vector3 eyePosition = new Vector3(CameraPosition.X, 0.8f, CameraPosition.Z);
            Vector3 targetPosition = eyePosition + forward;

            Matrix4x4 view = Matrix4x4.CreateLookAt(eyePosition, targetPosition, new Vector3(0, 1, 0));
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView((65f * MathF.PI) / 180f, Width / Height, 0.05f, 100f);
            
            Matrix4x4 viewProj = view * proj;
            _commandList.UpdateBuffer(_viewProjBuffer, 0, viewProj);

            _batchVertices.Clear();
            foreach (var obj in GameObjects) obj.Render(this);

            if (_batchVertices.Count > 0)
            {
                _commandList.UpdateBuffer(_vertexBuffer, 0, _batchVertices.ToArray());
                _commandList.SetPipeline(_pipeline);
                _commandList.SetGraphicsResourceSet(0, _resourceSet); // Zawiera teksturę i sampler
                _commandList.SetVertexBuffer(0, _vertexBuffer);
                _commandList.Draw((uint)(_batchVertices.Count / 5)); // 5 floatów na wierzchołek
            }

            _commandList.End();
            _device.SubmitCommands(_commandList);
            _device.SwapBuffers(_device.MainSwapchain);

            frameCount++; fpsTimer += deltaTime;
            if (fpsTimer >= 0.5)
            {
                double fps = frameCount / fpsTimer;
                _window.Title = $"{_baseTitle} [FPS: {fps:F0} (3D TEXTURED)]";
                fpsTimer = 0; frameCount = 0;
            }
        }

        _wallTexture.Dispose(); _wallTextureView.Dispose(); _sampler.Dispose();
        _vertexBuffer.Dispose(); _viewProjBuffer.Dispose(); _pipeline.Dispose(); _commandList.Dispose(); _device.Dispose();
    }
}