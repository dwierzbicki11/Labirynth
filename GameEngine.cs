using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics; // Macierze 3D
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

    public Core.Math.Vector3 CameraPosition { get; set; } = Core.Math.Vector3.Zero;
    public float Width => _window.Width;
    public float Height => _window.Height;

    public List<GameObject> GameObjects { get; } = new();
    public RgbaFloat ClearColor { get; set; } = new RgbaFloat(0.03f, 0.03f, 0.05f, 1.0f);

    // POTĘŻNE SHADERY 3D Z MACIERZĄ WIDOKU
    private const string VertexShaderSource = @"#version 330 core
    layout(location = 0) in vec3 InsidePos;
    layout(location = 1) in vec4 InColor;
    layout(std140) uniform ViewProjBlock {
        mat4 u_ViewProj;
    };
    out vec4 v_Color;
    void main() {
        gl_Position = u_ViewProj * vec4(InsidePos, 1.0);
        v_Color = InColor;
    }";

    private const string FragmentShaderSource = @"#version 330 core
    in vec4 v_Color;
    out vec4 FragColor;
    void main() {
        FragColor = v_Color;
    }";

    public void Initialize(string title, int width, int height, GraphicsBackend backend)
{
    _baseTitle = title;
    WindowCreateInfo windowCI = new WindowCreateInfo { 
        X = 100, Y = 100, WindowWidth = width, WindowHeight = height, WindowTitle = title 
    };
    _window = VeldridStartup.CreateWindow(ref windowCI);

    GraphicsDeviceOptions options = new GraphicsDeviceOptions { 
        Debug = false, 
        HasMainSwapchain = true, 
        SyncToVerticalBlank = false,
        PreferStandardClipSpaceYDirection = true,
        // 🔥 POPRAWKA: Przydzielamy standardowy 24-bitowy bufor głębokości dla okna gry
        SwapchainDepthFormat = PixelFormat.D24_UNorm_S8_UInt 
    };

    _device = VeldridStartup.CreateGraphicsDevice(_window, options, backend);
    _commandList = _device.ResourceFactory.CreateCommandList();

    PrepareGraphicsPipeline();
    Message.ok($"Silnik 3D gotowy. Aktywne API: {_device.BackendType}");
}

    private void PrepareGraphicsPipeline()
    {
        ResourceFactory factory = _device.ResourceFactory;

        Shader vertexShader = factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexShaderSource), "main"));
        Shader fragmentShader = factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentShaderSource), "main"));

        // Zwiększamy rozmiar bufora wierzchołków do 2MB na potrzeby złożonej geometrii 3D
        _vertexBuffer = factory.CreateBuffer(new BufferDescription(2000000, BufferUsage.VertexBuffer));
        
        // Bufor uniform na macierz 4x4 (64 bajty)
        _viewProjBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

        ResourceLayout layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("ViewProjBlock", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        ));
        _resourceSet = factory.CreateResourceSet(new ResourceSetDescription(layout, _viewProjBuffer));

        GraphicsPipelineDescription pd = new GraphicsPipelineDescription {
            BlendState = BlendStateDescription.SingleOverrideBlend,
            // AKTUALIZACJA: Włączamy sprzętowy Z-Buffer (sprawdzanie głębokości 3D)
            DepthStencilState = new DepthStencilStateDescription(
                depthTestEnabled: true,
                depthWriteEnabled: true,
                comparisonKind: ComparisonKind.LessEqual),
            RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = new[] { layout },
            ShaderSet = new ShaderSetDescription(
                new[] { 
                    new VertexLayoutDescription(
                        new VertexElementDescription("InsidePos", VertexElementSemantic.Position, VertexElementFormat.Float3),
                        new VertexElementDescription("InColor", VertexElementSemantic.Color, VertexElementFormat.Float4)
                    ) 
                },
                new[] { vertexShader, fragmentShader }
            ),
            Outputs = _device.MainSwapchain.Framebuffer.OutputDescription
        };
        _pipeline = factory.CreateGraphicsPipeline(ref pd);
    }

    public void DrawCube(float x, float y, float z, float width, float height, float depth, RgbaFloat color)
    {
        float minX = x; float maxX = x + width;
        float minY = y; float maxY = y + height;
        float minZ = z; float maxZ = z + depth;

        // Generowanie 6 ścian sześcianu (36 wierzchołków) bezpośrednio do batchera
        // Ściana Przednia
        AddVertex(minX, minY, maxZ, color); AddVertex(minX, maxY, maxZ, color); AddVertex(maxX, maxY, maxZ, color);
        AddVertex(maxX, maxY, maxZ, color); AddVertex(maxX, minY, maxZ, color); AddVertex(minX, minY, maxZ, color);
        // Ściana Tylna
        AddVertex(minX, minY, minZ, color); AddVertex(maxX, minY, minZ, color); AddVertex(maxX, maxY, minZ, color);
        AddVertex(maxX, maxY, minZ, color); AddVertex(minX, maxY, minZ, color); AddVertex(minX, minY, minZ, color);
        // Ściana Górna
        AddVertex(minX, maxY, minZ, color); AddVertex(maxX, maxY, minZ, color); AddVertex(maxX, maxY, maxZ, color);
        AddVertex(maxX, maxY, maxZ, color); AddVertex(minX, maxY, maxZ, color); AddVertex(minX, maxY, minZ, color);
        // Ściana Dolna
        AddVertex(minX, minY, minZ, color); AddVertex(minX, minY, maxZ, color); AddVertex(maxX, minY, maxZ, color);
        AddVertex(maxX, minY, maxZ, color); AddVertex(maxX, minY, minZ, color); AddVertex(minX, minY, minZ, color);
        // Ściana Lewa
        AddVertex(minX, minY, minZ, color); AddVertex(minX, maxY, minZ, color); AddVertex(minX, maxY, maxZ, color);
        AddVertex(minX, maxY, maxZ, color); AddVertex(minX, minY, maxZ, color); AddVertex(minX, minY, minZ, color);
        // Ściana Prawa
        AddVertex(maxX, minY, minZ, color); AddVertex(maxX, minY, maxZ, color); AddVertex(maxX, maxY, maxZ, color);
        AddVertex(maxX, maxY, maxZ, color); AddVertex(maxX, maxY, minZ, color); AddVertex(maxX, minY, minZ, color);
    }

    private void AddVertex(float x, float y, float z, RgbaFloat c)
    {
        _batchVertices.Add(x); _batchVertices.Add(y); _batchVertices.Add(z);
        _batchVertices.Add(c.R); _batchVertices.Add(c.G); _batchVertices.Add(c.B); _batchVertices.Add(c.A);
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
            // KLUCZOWE: Czyścimy zarówno tło, jak i bufor głębokości (1f)
            _commandList.ClearColorTarget(0, ClearColor);
            _commandList.ClearDepthStencil(1f);

            // AKTUALIZACJA MACIERZY KAMERY 3D
            Matrix4x4 view = Matrix4x4.CreateLookAt(
                new Vector3(CameraPosition.X, CameraPosition.Y + 22f, CameraPosition.Z + 14f), // Pozycja kamery (zawieszona nad graczem)
                new Vector3(CameraPosition.X, 0f, CameraPosition.Z),                         // Punkt celowania (gracz)
                new Vector3(0, 1, 0)                                                         // Wektor góry (Up)
            );
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4f, Width / Height, 0.1f, 100f
            );
            Matrix4x4 viewProj = view * proj;
            _commandList.UpdateBuffer(_viewProjBuffer, 0, viewProj);

            _batchVertices.Clear();
            foreach (var obj in GameObjects) obj.Render(this);

            if (_batchVertices.Count > 0)
            {
                _commandList.UpdateBuffer(_vertexBuffer, 0, _batchVertices.ToArray());
                _commandList.SetPipeline(_pipeline);
                _commandList.SetGraphicsResourceSet(0, _resourceSet);
                _commandList.SetVertexBuffer(0, _vertexBuffer);
                _commandList.Draw((uint)(_batchVertices.Count / 7)); // 7 floatów na wierzchołek
            }

            _commandList.End();
            _device.SubmitCommands(_commandList);
            _device.SwapBuffers(_device.MainSwapchain);

            frameCount++; fpsTimer += deltaTime;
            if (fpsTimer >= 0.5)
            {
                double fps = frameCount / fpsTimer;
                _window.Title = $"{_baseTitle} [FPS: {fps:F0} (3D BATCHED)] | Renderowane Ściany: {GameObjects.Count - 1}";
                fpsTimer = 0; frameCount = 0;
            }
        }

        _vertexBuffer.Dispose(); _viewProjBuffer.Dispose(); _pipeline.Dispose(); _commandList.Dispose(); _device.Dispose();
        Message.warning("Zasoby 3D zwolnione poprawnie.");
    }
}