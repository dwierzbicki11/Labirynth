#nullable disable
using System;
using System.IO;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using CyberEngine.Core;
using System.Numerics;

namespace CyberEngine.Graphics;

public class Renderer : IDisposable
{
    public GraphicsDevice Device { get; private set; }
    private CommandList _commandList;
    private Pipeline _pipeline, _postPipeline, _hudPipeline;
    private DeviceBuffer _vertexBuffer, _viewProjBuffer, _lightBuffer, _hudVertexBuffer, _settingsBuffer;
    private ResourceSet _resourceSet, _postResourceSet;
    private ResourceLayout _postResourceLayout;
    private Framebuffer _offscreenFB;
    private Texture _offscreenColor, _offscreenDepth, _wallTexture;
    private TextureView _offscreenColorView, _offscreenDepthView, _wallTextureView;
    private Sampler _sampler;

    private float _currentRenderScale = 1.0f;
    private int _currentResW = 1280;
    private int _currentResH = 720;

    [StructLayout(LayoutKind.Sequential)]
    private struct LightData {
        public Vector4 FlashlightPos; public Vector4 FlashlightDir; public Vector4 Lantern0; public Vector4 Lantern1;
        public Vector4 Lantern2; public Vector4 Lantern3; public Vector4 Lantern4; public Vector4 Lantern5;
        public Vector4 Lantern6; public Vector4 Lantern7; public int LanternCount; public float Time; private float p2; private float p3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsSettingsBlock {
        public float RenderScale; public int Bloom; public int MotionBlur; public float BlurIntensity;
        public int Shadows; public int AntiAliasing; public int AO; public float DrawDistance;
    }

    public void Initialize(Sdl2Window window, int width, int height, GraphicsBackend backend)
    {
        bool isHeadless = Environment.GetEnvironmentVariable("HEADLESS") == "1";
        
        GraphicsDeviceOptions options = new GraphicsDeviceOptions {
            Debug = false, 
            HasMainSwapchain = !isHeadless,
            SyncToVerticalBlank = false, 
            // Zabezpieczenie przed błędem walidacji Veldrid: Format głębi tylko gdy istnieje okno
            SwapchainDepthFormat = isHeadless ? null : PixelFormat.D24_UNorm_S8_UInt
        };

        Device = !isHeadless ? VeldridStartup.CreateGraphicsDevice(window, options, backend) : GraphicsDevice.CreateVulkan(options);
        
        _commandList = Device.ResourceFactory.CreateCommandList();

        // [OFICERSKA OPTYMALIZACJA] W trybie Headless ucinamy wszystkie alokacje VRAM.
        // Silnik działa jak czysty rdzeń obliczeniowy.
        if (isHeadless) return; 

        _currentRenderScale = SystemConfig.RenderScale; 
        _currentResW = SystemConfig.ResolutionWidth; 
        _currentResH = SystemConfig.ResolutionHeight;

        uint size = (uint)Marshal.SizeOf<GraphicsSettingsBlock>();
        _settingsBuffer = Device.ResourceFactory.CreateBuffer(new BufferDescription(size, BufferUsage.UniformBuffer));

        LoadWallTexture();
        PrepareGraphicsPipeline();
        CreateOffscreenFramebuffer();
    }

    public unsafe void DrawFrame(RenderData data, Sdl2Window window, float resW, float resH, RgbaFloat clearColor, bool isHeadless)
    {
        if (isHeadless) return;

        CheckAndRebuildOffscreen(window);
        _commandList.Begin();

        GraphicsSettingsBlock settings = new GraphicsSettingsBlock {
            RenderScale = SystemConfig.RenderScale, Bloom = SystemConfig.BloomEnabled ? 1 : 0, MotionBlur = SystemConfig.MotionBlurIntensity > 0 ? 1 : 0,
            BlurIntensity = SystemConfig.MotionBlurIntensity, Shadows = SystemConfig.ShadowsEnabled ? 1 : 0, AntiAliasing = SystemConfig.AntiAliasingMode,
            AO = SystemConfig.AmbientOcclusionEnabled ? 1 : 0, DrawDistance = SystemConfig.DrawDistance
        };
        _commandList.UpdateBuffer(_settingsBuffer, 0, settings);

        _commandList.SetFramebuffer(_offscreenFB);
        _commandList.SetViewport(0, new Viewport(0, 0, _offscreenFB.Width, _offscreenFB.Height, 0, 1));
        _commandList.ClearColorTarget(0, clearColor); _commandList.ClearDepthStencil(1f);

        Vector3 eyePos = new Vector3(data.Camera.Position.X, 0.8f, data.Camera.Position.Z);
        Matrix4x4 view = Matrix4x4.CreateLookAt(eyePos, eyePos + data.Camera.Forward, new Vector3(0, 1, 0));
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView((SystemConfig.Fov * MathF.PI) / 180f, resW / resH, 0.05f, 100f);
        
        // OFICERSKA NAPRAWA VULKANA 3D
        if (Device.IsClipSpaceYInverted) proj.M22 *= -1;

        _commandList.UpdateBuffer(_viewProjBuffer, 0, view * proj);

        LightData lightData = new LightData { FlashlightPos = new Vector4(eyePos, 0.92f), FlashlightDir = new Vector4(data.Camera.Forward, data.TriggerMuzzleFlash ? 12.0f : 3.0f), Time = (float)data.CurrentTime };
        data.Lanterns.Sort((a, b) => Vector3.DistanceSquared(eyePos, a).CompareTo(Vector3.DistanceSquared(eyePos, b)));
        lightData.LanternCount = Math.Min(data.Lanterns.Count, SystemConfig.GraphicsQuality switch { 0 => 2, 1 => 4, 2 => 8, _ => 4 });
        
        if (lightData.LanternCount > 0) lightData.Lantern0 = new Vector4(data.Lanterns[0], 1.0f);
        if (lightData.LanternCount > 1) lightData.Lantern1 = new Vector4(data.Lanterns[1], 1.0f);
        if (lightData.LanternCount > 2) lightData.Lantern2 = new Vector4(data.Lanterns[2], 1.0f);
        if (lightData.LanternCount > 3) lightData.Lantern3 = new Vector4(data.Lanterns[3], 1.0f);
        _commandList.UpdateBuffer(_lightBuffer, 0, lightData);

        if (data.WorldVertexCount > 0) {
            fixed (float* ptr = data.WorldVertices) { _commandList.UpdateBuffer(_vertexBuffer, 0, (IntPtr)ptr, (uint)(data.WorldVertexCount * sizeof(float))); }
            _commandList.SetPipeline(_pipeline); _commandList.SetGraphicsResourceSet(0, _resourceSet); _commandList.SetVertexBuffer(0, _vertexBuffer);
            _commandList.Draw((uint)(data.WorldVertexCount / 9)); data.GpuDrawCalls++;
        }

        _commandList.SetFramebuffer(Device.MainSwapchain.Framebuffer);
        _commandList.SetViewport(0, new Viewport(0, 0, window == null ? resW : window.Width, window == null ? resH : window.Height, 0, 1));
        _commandList.ClearColorTarget(0, RgbaFloat.Black);

        _commandList.SetPipeline(_postPipeline); _commandList.SetGraphicsResourceSet(0, _postResourceSet); _commandList.Draw(3);

        if (data.HudVertexCount > 0) {
            // OFICERSKA NAPRAWA VULKANA 2D
            if (Device.IsClipSpaceYInverted)
            {
                for (int i = 0; i < data.HudVertexCount; i++)
                {
                    data.HudVertices[i * 6 + 1] = -data.HudVertices[i * 6 + 1]; 
                }
            }

            fixed (float* ptr = data.HudVertices) { _commandList.UpdateBuffer(_hudVertexBuffer, 0, (IntPtr)ptr, (uint)(data.HudVertexCount * sizeof(float))); }
            _commandList.SetPipeline(_hudPipeline); _commandList.SetVertexBuffer(0, _hudVertexBuffer); _commandList.Draw((uint)(data.HudVertexCount / 6)); data.GpuDrawCalls++;
        }

        _commandList.End(); Device.SubmitCommands(_commandList);
        Device.SwapBuffers(Device.MainSwapchain);
    }

    private void CheckAndRebuildOffscreen(Sdl2Window window)
    {
        bool rebuild = false;
        if (Device.SyncToVerticalBlank != SystemConfig.VSync) Device.SyncToVerticalBlank = SystemConfig.VSync;
        if (_currentResW != SystemConfig.ResolutionWidth || _currentResH != SystemConfig.ResolutionHeight) {
            _currentResW = SystemConfig.ResolutionWidth; _currentResH = SystemConfig.ResolutionHeight;
            if (window != null) { window.Width = SystemConfig.ResolutionWidth; window.Height = SystemConfig.ResolutionHeight; } rebuild = true;
        }
        if (Math.Abs(_currentRenderScale - SystemConfig.RenderScale) > 0.01f) { _currentRenderScale = SystemConfig.RenderScale; rebuild = true; }
        if (rebuild) CreateOffscreenFramebuffer();
    }

    private void CreateOffscreenFramebuffer()
    {
        if (_offscreenFB != null) { _offscreenFB.Dispose(); _offscreenColor.Dispose(); _offscreenDepth.Dispose(); _offscreenColorView.Dispose(); _postResourceSet.Dispose(); }
        uint w = (uint)(SystemConfig.ResolutionWidth * _currentRenderScale); uint h = (uint)(SystemConfig.ResolutionHeight * _currentRenderScale);
        if (w < 1) w = 1; if (h < 1) h = 1;
        _offscreenColor = Device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(w, h, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Sampled));
        _offscreenDepth = Device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(w, h, 1, 1, PixelFormat.D24_UNorm_S8_UInt, TextureUsage.DepthStencil | TextureUsage.Sampled));
        _offscreenDepthView = Device.ResourceFactory.CreateTextureView(_offscreenDepth);
        _offscreenFB = Device.ResourceFactory.CreateFramebuffer(new FramebufferDescription(_offscreenDepth, _offscreenColor));
        _offscreenColorView = Device.ResourceFactory.CreateTextureView(_offscreenColor);
        _postResourceSet = Device.ResourceFactory.CreateResourceSet(new ResourceSetDescription(_postResourceLayout, _offscreenColorView, Device.LinearSampler, _settingsBuffer, _offscreenDepthView));
    }

    private void PrepareGraphicsPipeline()
    {
        ResourceFactory factory = Device.ResourceFactory; string baseDir = AppContext.BaseDirectory;
        byte[] vertSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "vertex.spv"));
        byte[] fragSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "fragment.spv"));
        
        Shader[] shaders = new[] { factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, vertSpv, "main")), factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, fragSpv, "main")) };
        _vertexBuffer = factory.CreateBuffer(new BufferDescription(8000000, BufferUsage.VertexBuffer));
        _viewProjBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
        _lightBuffer = factory.CreateBuffer(new BufferDescription(176, BufferUsage.UniformBuffer));
        _hudVertexBuffer = factory.CreateBuffer(new BufferDescription(2000000, BufferUsage.VertexBuffer));

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
            Outputs = new OutputDescription(new OutputAttachmentDescription(PixelFormat.D24_UNorm_S8_UInt), new OutputAttachmentDescription(PixelFormat.R8_G8_B8_A8_UNorm))
        };
        _pipeline = factory.CreateGraphicsPipeline(ref pd);

        byte[] postVertSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "post_vertex.spv"));
        byte[] postFragSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "post_fragment.spv"));
        byte[] hudVertSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "hud_vertex.spv"));
        byte[] hudFragSpv = File.ReadAllBytes(Path.Combine(baseDir, "Shaders", "hud_fragment.spv"));

        Shader[] postShaders = new[] { factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, postVertSpv, "main")), factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, postFragSpv, "main")) };
        Shader[] hudShaders = new[] { factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, hudVertSpv, "main")), factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, hudFragSpv, "main")) };

        _postResourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("u_ScreenTex", ResourceKind.TextureReadOnly, ShaderStages.Fragment), new ResourceLayoutElementDescription("u_Sampler", ResourceKind.Sampler, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("GraphicsSettingsBlock", ResourceKind.UniformBuffer, ShaderStages.Fragment), new ResourceLayoutElementDescription("u_DethText", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
        ));

        GraphicsPipelineDescription postPd = new GraphicsPipelineDescription {
            BlendState = BlendStateDescription.SingleOverrideBlend, DepthStencilState = DepthStencilStateDescription.Disabled, RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = PrimitiveTopology.TriangleList, ResourceLayouts = new[] { _postResourceLayout },
            ShaderSet = new ShaderSetDescription(Array.Empty<VertexLayoutDescription>(), postShaders), Outputs = Device.MainSwapchain.Framebuffer.OutputDescription
        };
        _postPipeline = factory.CreateGraphicsPipeline(ref postPd);

        GraphicsPipelineDescription hpd = new GraphicsPipelineDescription {
            BlendState = BlendStateDescription.SingleAlphaBlend, DepthStencilState = DepthStencilStateDescription.Disabled, RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = PrimitiveTopology.TriangleList, ResourceLayouts = Array.Empty<ResourceLayout>(),
            ShaderSet = new ShaderSetDescription(new[] { new VertexLayoutDescription(new VertexElementDescription("InsidePos", VertexElementSemantic.Position, VertexElementFormat.Float2, 0), new VertexElementDescription("InColor", VertexElementSemantic.Color, VertexElementFormat.Float4, 8)) }, hudShaders),
            Outputs = Device.MainSwapchain.Framebuffer.OutputDescription
        };
        _hudPipeline = factory.CreateGraphicsPipeline(ref hpd);
    }

    private void LoadWallTexture() {
        uint w = 256; uint h = 256; byte[] pd = new byte[w * h * 4]; Random rand = new Random(1337);
        for (uint y = 0; y < h; y++) {
            for (uint x = 0; x < w; x++) {
                uint i = (y * w + x) * 4; uint cX = x % 16;
                if (cX > 2 && cX < 13 && rand.Next(100) < 65) { byte g = (byte)rand.Next(140, 255); pd[i] = (byte)(g / 6); pd[i + 1] = g; pd[i + 2] = (byte)(g / 2); pd[i + 3] = 255; }
                else { pd[i] = 0; pd[i + 1] = 12; pd[i + 2] = 0; pd[i + 3] = 255; }
            }
        }
        _wallTexture = Device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(w, h, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));
        Device.UpdateTexture(_wallTexture, pd, 0, 0, 0, w, h, 1, 0, 0); _wallTextureView = Device.ResourceFactory.CreateTextureView(_wallTexture); _sampler = Device.ResourceFactory.CreateSampler(SamplerDescription.Point);
    }

    public void Dispose() {
        // Zabezpieczenie przed rzucaniem null exception (w Headless te obiekty są puste)
        _pipeline?.Dispose(); _postPipeline?.Dispose(); _hudPipeline?.Dispose();
        _vertexBuffer?.Dispose(); _viewProjBuffer?.Dispose(); _lightBuffer?.Dispose(); _hudVertexBuffer?.Dispose(); _settingsBuffer?.Dispose();
        _offscreenFB?.Dispose(); _offscreenColor?.Dispose(); _offscreenDepth?.Dispose(); _wallTexture?.Dispose();
        _commandList?.Dispose(); Device?.Dispose();
    }
}