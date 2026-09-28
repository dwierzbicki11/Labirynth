using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using CyberEngine.Core;

namespace CyberEngine.Graphics;

/// <summary>
/// Native Silk.NET Vulkan renderer.
/// Uses the engine's existing SPIR-V shaders and RenderData path directly.
/// The renderer owns all Vulkan resources and keeps gameplay API-independent.
/// </summary>
public sealed unsafe class SilkNetVulkanRendererBackend : IRendererBackend
{
    private Vk? _vk;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _graphicsQueue;
    private uint _graphicsQueueFamily;
    private Sdl? _sdl;
    private KhrSurface? _surfaceApi;
    private KhrSwapchain? _swapchainApi;
    private SurfaceKHR _surface;
    private SwapchainKHR _swapchain;

    private Image[] _swapchainImages = Array.Empty<Image>();
    private ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    private Framebuffer[] _swapchainFramebuffers = Array.Empty<Framebuffer>();
    private CommandBuffer[] _commandBuffers = Array.Empty<CommandBuffer>();
    private Extent2D _swapchainExtent;
    private Format _swapchainFormat;

    private RenderPass _sceneRenderPass;
    private RenderPass _postRenderPass;

    private Image _sceneColor;
    private DeviceMemory _sceneColorMemory;
    private ImageView _sceneColorView;
    private Image _sceneDepth;
    private DeviceMemory _sceneDepthMemory;
    private ImageView _sceneDepthView;
    private Framebuffer _sceneFramebuffer;
    private uint _sceneWidth;
    private uint _sceneHeight;

    private Silk.NET.Vulkan.Buffer _worldVertexBuffer;
    private DeviceMemory _worldVertexMemory;
    private ulong _worldVertexCapacity;
    private Silk.NET.Vulkan.Buffer _hudVertexBuffer;
    private DeviceMemory _hudVertexMemory;
    private ulong _hudVertexCapacity;

    private Silk.NET.Vulkan.Buffer _viewProjBuffer;
    private DeviceMemory _viewProjMemory;
    private Silk.NET.Vulkan.Buffer _lightBuffer;
    private DeviceMemory _lightMemory;

    private Image _wallTexture;
    private DeviceMemory _wallTextureMemory;
    private ImageView _wallTextureView;
    private Sampler _wallSampler;

    private Sampler _postSampler;
    private DescriptorPool _descriptorPool;
    private DescriptorSetLayout[] _worldSetLayouts = Array.Empty<DescriptorSetLayout>();
    private DescriptorSet[] _worldDescriptorSets = Array.Empty<DescriptorSet>();
    private DescriptorSetLayout[] _postSetLayouts = Array.Empty<DescriptorSetLayout>();
    private DescriptorSet[] _postDescriptorSets = Array.Empty<DescriptorSet>();

    private PipelineLayout _worldPipelineLayout;
    private Pipeline _worldPipeline;
    private PipelineLayout _postPipelineLayout;
    private Pipeline _postPipeline;
    private PipelineLayout _hudPipelineLayout;
    private Pipeline _hudPipeline;

    private CommandPool _commandPool;
    private Silk.NET.Vulkan.Semaphore _imageAvailable;
    private Silk.NET.Vulkan.Semaphore _renderFinished;
    private Fence _inFlightFence;

    private nint _nativeWindow;
    private bool _headless;
    private bool _initialized;
    private string _deviceName = "Silk.NET Vulkan device";

    [StructLayout(LayoutKind.Sequential)]
    private struct LightData
    {
        public Vector4 FlashlightPos;
        public Vector4 FlashlightDir;
        public Vector4 Lantern0;
        public Vector4 Lantern1;
        public Vector4 Lantern2;
        public Vector4 Lantern3;
        public Vector4 Lantern4;
        public Vector4 Lantern5;
        public Vector4 Lantern6;
        public Vector4 Lantern7;
        public int LanternCount;
        public float Time;
        private float _pad0;
        private float _pad1;
    }

    private enum BufferKind
    {
        Vertex,
        Uniform
    }

    public string BackendName => "Silk.NET Vulkan";
    public string DeviceName => _initialized ? _deviceName : "Silk.NET Vulkan (not initialized)";
    public bool IsInitialized => _initialized;

    public void Initialize(int width, int height, bool headless, nint nativeWindowHandle)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        _headless = headless;
        _nativeWindow = nativeWindowHandle;

        if (_initialized)
            return;

        if (_headless)
        {
            _initialized = true;
            return;
        }

        if (_nativeWindow == 0)
            throw new InvalidOperationException("Vulkan backend requires an SDL window handle.");

        _vk = Vk.GetApi();
        _sdl = Sdl.GetApi();

        IntPtr appName = Marshal.StringToHGlobalAnsi("CyberEngine");
        IntPtr engineName = Marshal.StringToHGlobalAnsi("CyberEngine");
        try
        {
            ApplicationInfo appInfo = new()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)appName,
                ApplicationVersion = 1,
                PEngineName = (byte*)engineName,
                EngineVersion = 1,
                ApiVersion = Vk.Version12
            };

            Window* sdlWindow = (Window*)_nativeWindow;
            uint extensionCount = 0;
            if (_sdl.VulkanGetInstanceExtensions(sdlWindow, &extensionCount, null) != SdlBool.True || extensionCount == 0)
                throw new PlatformNotSupportedException("SDL could not provide Vulkan instance extensions.");

            byte** extensionNames = stackalloc byte*[(int)extensionCount];
            if (_sdl.VulkanGetInstanceExtensions(sdlWindow, &extensionCount, extensionNames) != SdlBool.True)
                throw new InvalidOperationException("SDL Vulkan instance extension query failed.");

            InstanceCreateInfo instanceInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = extensionCount,
                PpEnabledExtensionNames = extensionNames
            };
            Check(_vk.CreateInstance(in instanceInfo, null, out _instance), "vkCreateInstance");

            if (!_vk.TryGetInstanceExtension(_instance, out _surfaceApi))
                throw new PlatformNotSupportedException("VK_KHR_surface is unavailable.");

            VkHandle instanceHandle = _instance.ToHandle();
            VkNonDispatchableHandle surfaceHandle;
            if (_sdl.VulkanCreateSurface(sdlWindow, instanceHandle, &surfaceHandle) == SdlBool.False)
                throw new InvalidOperationException("SDL could not create Vulkan surface.");
            _surface = surfaceHandle.ToSurface();

            SelectPhysicalDevice();
            CreateDevice();
            CreateSwapchain(width, height);
            CreateRenderPasses();
            CreateCommandResources();
            CreateSceneTargets();
            CreateFramebuffers();
            CreateUniformBuffers();
            CreateStaticResources();
            CreateDescriptorResources();
            CreatePipelines();

            _initialized = true;
        }
        catch
        {
            DisposeVulkanObjects();
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(appName);
            Marshal.FreeHGlobal(engineName);
        }
    }

    private void SelectPhysicalDevice()
    {
        uint deviceCount = 0;
        Check(_vk!.EnumeratePhysicalDevices(_instance, &deviceCount, null), "vkEnumeratePhysicalDevices(count)");
        if (deviceCount == 0)
            throw new PlatformNotSupportedException("No Vulkan physical device was found.");

        PhysicalDevice[] devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* pDevices = devices)
            Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, pDevices), "vkEnumeratePhysicalDevices");

        foreach (PhysicalDevice device in devices)
        {
            uint queueCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueCount, null);
            if (queueCount == 0)
                continue;

            QueueFamilyProperties[] queues = new QueueFamilyProperties[queueCount];
            fixed (QueueFamilyProperties* pQueues = queues)
                _vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueCount, pQueues);

            for (uint q = 0; q < queueCount; q++)
            {
                if ((queues[q].QueueFlags & QueueFlags.GraphicsBit) == 0)
                    continue;

                uint supported = 0;
                Check(_surfaceApi!.GetPhysicalDeviceSurfaceSupport(device, q, _surface, &supported), "vkGetPhysicalDeviceSurfaceSupportKHR");
                if (supported != 0)
                {
                    _physicalDevice = device;
                    _graphicsQueueFamily = q;

                    PhysicalDeviceProperties props = new();
                    _vk.GetPhysicalDeviceProperties(device, &props);
                    fixed (byte* name = props.DeviceName)
                    {
                        int length = 0;
                        while (length < 256 && name[length] != 0) length++;
                        if (length > 0)
                            _deviceName = System.Text.Encoding.UTF8.GetString(name, length);
                    }
                    return;
                }
            }
        }

        throw new PlatformNotSupportedException("No Vulkan graphics queue family with present support was found.");
    }

    private void CreateDevice()
    {
        float priority = 1f;
        DeviceQueueCreateInfo queueInfo = new()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            QueueCount = 1,
            PQueuePriorities = &priority
        };

        IntPtr swapchainExtension = Marshal.StringToHGlobalAnsi("VK_KHR_swapchain");
        try
        {
            byte* extensionName = (byte*)swapchainExtension;
            DeviceCreateInfo deviceInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueInfo,
                EnabledExtensionCount = 1,
                PpEnabledExtensionNames = &extensionName
            };

            Check(_vk!.CreateDevice(_physicalDevice, in deviceInfo, null, out _device), "vkCreateDevice");
        }
        finally
        {
            Marshal.FreeHGlobal(swapchainExtension);
        }

        _vk.GetDeviceQueue(_device, _graphicsQueueFamily, 0, out _graphicsQueue);
        if (!_vk.TryGetDeviceExtension(_instance, _device, out _swapchainApi))
            throw new PlatformNotSupportedException("VK_KHR_swapchain is unavailable.");
    }

    private void CreateSwapchain(int width, int height)
    {
        SurfaceCapabilitiesKHR capabilities;
        Check(_surfaceApi!.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, &capabilities), "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");

        uint formatCount = 0;
        Check(_surfaceApi.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null), "vkGetPhysicalDeviceSurfaceFormatsKHR(count)");
        if (formatCount == 0)
            throw new InvalidOperationException("Vulkan surface has no supported formats.");

        SurfaceFormatKHR[] formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* pFormats = formats)
            Check(_surfaceApi.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, pFormats), "vkGetPhysicalDeviceSurfaceFormatsKHR");

        SurfaceFormatKHR chosenFormat = formats[0];
        foreach (SurfaceFormatKHR format in formats)
        {
            if (format.Format == Format.B8G8R8A8Srgb && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                chosenFormat = format;
                break;
            }
        }

        uint modeCount = 0;
        Check(_surfaceApi.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &modeCount, null), "vkGetPhysicalDeviceSurfacePresentModesKHR(count)");
        PresentModeKHR[] modes = new PresentModeKHR[modeCount];
        if (modeCount > 0)
        {
            fixed (PresentModeKHR* pModes = modes)
                Check(_surfaceApi.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &modeCount, pModes), "vkGetPhysicalDeviceSurfacePresentModesKHR");
        }

        PresentModeKHR presentMode = PresentModeKHR.FifoKhr;
        foreach (PresentModeKHR mode in modes)
        {
            if (mode == PresentModeKHR.MailboxKhr)
            {
                presentMode = mode;
                break;
            }
        }

        Extent2D extent = capabilities.CurrentExtent;
        if (extent.Width == uint.MaxValue)
        {
            extent.Width = Math.Clamp((uint)Math.Max(width, 1), capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
            extent.Height = Math.Clamp((uint)Math.Max(height, 1), capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);
        }

        uint imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            imageCount = capabilities.MaxImageCount;

        SwapchainCreateInfoKHR info = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = chosenFormat.Format,
            ImageColorSpace = chosenFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true
        };

        Check(_swapchainApi!.CreateSwapchain(_device, in info, null, out _swapchain), "vkCreateSwapchainKHR");

        uint actualCount = 0;
        Check(_swapchainApi.GetSwapchainImages(_device, _swapchain, &actualCount, null), "vkGetSwapchainImagesKHR(count)");
        _swapchainImages = new Image[actualCount];
        fixed (Image* pImages = _swapchainImages)
            Check(_swapchainApi.GetSwapchainImages(_device, _swapchain, &actualCount, pImages), "vkGetSwapchainImagesKHR");

        _swapchainFormat = chosenFormat.Format;
        _swapchainExtent = extent;
        _lastSwapchainRequestWidth = width;
        _lastSwapchainRequestHeight = height;
    }

    private void CreateRenderPasses()
    {
        AttachmentDescription color = new()
        {
            Format = Format.R8G8B8A8Unorm,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ShaderReadOnlyOptimal
        };
        AttachmentDescription depth = new()
        {
            Format = Format.D32Sfloat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        AttachmentReference colorRef = new(0, ImageLayout.ColorAttachmentOptimal);
        AttachmentReference depthRef = new(1, ImageLayout.DepthStencilAttachmentOptimal);

        SubpassDescription sceneSubpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1
        };
        AttachmentReference* sceneColor = stackalloc AttachmentReference[1];
        sceneColor[0] = colorRef;
        sceneSubpass.PColorAttachments = sceneColor;
        sceneSubpass.PDepthStencilAttachment = &depthRef;

        AttachmentDescription* sceneAttachments = stackalloc AttachmentDescription[2];
        sceneAttachments[0] = color;
        sceneAttachments[1] = depth;

        SubpassDescription* sceneSubpasses = stackalloc SubpassDescription[1];
        sceneSubpasses[0] = sceneSubpass;
        SubpassDependency sceneDependency = new()
        {
            SrcSubpass = uint.MaxValue,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.FragmentShaderBit,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.ShaderReadBit
        };

        RenderPassCreateInfo sceneInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = sceneAttachments,
            SubpassCount = 1,
            PSubpasses = sceneSubpasses,
            DependencyCount = 1,
            PDependencies = &sceneDependency
        };

        // The external dependency makes the final color transition visible to the post pass.
        sceneDependency.SrcSubpass = 0;
        sceneDependency.DstSubpass = uint.MaxValue;
        sceneDependency.SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit;
        sceneDependency.DstStageMask = PipelineStageFlags.FragmentShaderBit;
        sceneDependency.SrcAccessMask = AccessFlags.ColorAttachmentWriteBit;
        sceneDependency.DstAccessMask = AccessFlags.ShaderReadBit;

        Check(_vk!.CreateRenderPass(_device, in sceneInfo, null, out _sceneRenderPass), "vkCreateRenderPass(scene)");

        AttachmentDescription swapColor = new()
        {
            Format = _swapchainFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.PresentSrcKhr,
            FinalLayout = ImageLayout.PresentSrcKhr
        };
        AttachmentReference swapRef = new(0, ImageLayout.ColorAttachmentOptimal);
        SubpassDescription postSubpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1
        };
        AttachmentReference* postColor = stackalloc AttachmentReference[1];
        postColor[0] = swapRef;
        postSubpass.PColorAttachments = postColor;

        RenderPassCreateInfo postInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            SubpassCount = 1
        };
        AttachmentDescription* postAttachment = stackalloc AttachmentDescription[1];
        postAttachment[0] = swapColor;
        postInfo.PAttachments = postAttachment;
        SubpassDescription* postSubpasses = stackalloc SubpassDescription[1];
        postSubpasses[0] = postSubpass;
        postInfo.PSubpasses = postSubpasses;

        Check(_vk.CreateRenderPass(_device, in postInfo, null, out _postRenderPass), "vkCreateRenderPass(post)");
    }

    private void CreateSceneTargets()
    {
        _sceneWidth = Math.Max(1u, (uint)(SystemConfig.ResolutionWidth * SystemConfig.RenderScale));
        _sceneHeight = Math.Max(1u, (uint)(SystemConfig.ResolutionHeight * SystemConfig.RenderScale));

        CreateImage(_sceneWidth, _sceneHeight, Format.R8G8B8A8Unorm, ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, out _sceneColor, out _sceneColorMemory);
        _sceneColorView = CreateImageView(_sceneColor, Format.R8G8B8A8Unorm, ImageAspectFlags.ColorBit);

        CreateImage(_sceneWidth, _sceneHeight, Format.D32Sfloat, ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, out _sceneDepth, out _sceneDepthMemory);
        _sceneDepthView = CreateImageView(_sceneDepth, Format.D32Sfloat, ImageAspectFlags.DepthBit);

        FramebufferCreateInfo info = new()
        {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = _sceneRenderPass,
            AttachmentCount = 2,
            Width = _sceneWidth,
            Height = _sceneHeight,
            Layers = 1
        };
        ImageView* attachments = stackalloc ImageView[2];
        attachments[0] = _sceneColorView;
        attachments[1] = _sceneDepthView;
        info.PAttachments = attachments;
        Check(_vk!.CreateFramebuffer(_device, in info, null, out _sceneFramebuffer), "vkCreateFramebuffer(scene)");
    }

    private void CreateFramebuffers()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];
        _swapchainFramebuffers = new Framebuffer[_swapchainImages.Length];

        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            ImageViewCreateInfo viewInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainFormat,
                Components = new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity),
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };
            Check(_vk!.CreateImageView(_device, in viewInfo, null, out _swapchainImageViews[i]), "vkCreateImageView");

            ImageView attachment = _swapchainImageViews[i];
            FramebufferCreateInfo fb = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _postRenderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1
            };
            Check(_vk.CreateFramebuffer(_device, in fb, null, out _swapchainFramebuffers[i]), "vkCreateFramebuffer(swapchain)");
        }
    }

    private void CreateCommandResources()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _graphicsQueueFamily
        };
        Check(_vk!.CreateCommandPool(_device, in poolInfo, null, out _commandPool), "vkCreateCommandPool");

        _commandBuffers = new CommandBuffer[_swapchainImages.Length];
        fixed (CommandBuffer* p = _commandBuffers)
        {
            CommandBufferAllocateInfo info = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)_commandBuffers.Length
            };
            Check(_vk.AllocateCommandBuffers(_device, in info, p), "vkAllocateCommandBuffers");
        }

        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        Check(_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _imageAvailable), "vkCreateSemaphore");
        Check(_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _renderFinished), "vkCreateSemaphore");

        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };
        Check(_vk.CreateFence(_device, in fenceInfo, null, out _inFlightFence), "vkCreateFence");
    }

    private void CreateUniformBuffers()
    {
        CreateHostBuffer(64, BufferUsageFlags.UniformBufferBit, out _viewProjBuffer, out _viewProjMemory);
        CreateHostBuffer((ulong)Marshal.SizeOf<LightData>(), BufferUsageFlags.UniformBufferBit, out _lightBuffer, out _lightMemory);
    }

    private void CreateStaticResources()
    {
        _worldVertexCapacity = 16ul * 1024ul * 1024ul;
        _hudVertexCapacity = 2ul * 1024ul * 1024ul;
        CreateHostBuffer(_worldVertexCapacity, BufferUsageFlags.VertexBufferBit, out _worldVertexBuffer, out _worldVertexMemory);
        CreateHostBuffer(_hudVertexCapacity, BufferUsageFlags.VertexBufferBit, out _hudVertexBuffer, out _hudVertexMemory);

        _postSampler = CreateSampler();
        CreateWallTexture();
    }

    private void CreateDescriptorResources()
    {
        DescriptorPoolSize* poolSizes = stackalloc DescriptorPoolSize[3];
        poolSizes[0] = new DescriptorPoolSize(DescriptorType.UniformBuffer, 2);
        poolSizes[1] = new DescriptorPoolSize(DescriptorType.SampledImage, 2);
        poolSizes[2] = new DescriptorPoolSize(DescriptorType.Sampler, 2);

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = 6,
            PoolSizeCount = 3,
            PPoolSizes = poolSizes
        };
        Check(_vk!.CreateDescriptorPool(_device, in poolInfo, null, out _descriptorPool), "vkCreateDescriptorPool");

        _worldSetLayouts = new DescriptorSetLayout[4];
        _worldDescriptorSets = new DescriptorSet[4];

        CreateDescriptorSetLayout(DescriptorType.UniformBuffer, ShaderStageFlags.VertexBit, out _worldSetLayouts[0]);
        CreateDescriptorSetLayout(DescriptorType.UniformBuffer, ShaderStageFlags.FragmentBit, out _worldSetLayouts[1]);
        CreateDescriptorSetLayout(DescriptorType.SampledImage, ShaderStageFlags.FragmentBit, out _worldSetLayouts[2]);
        CreateDescriptorSetLayout(DescriptorType.Sampler, ShaderStageFlags.FragmentBit, out _worldSetLayouts[3]);

        AllocateDescriptorSets(_worldSetLayouts, _worldDescriptorSets);
        WriteBufferDescriptor(_worldDescriptorSets[0], DescriptorType.UniformBuffer, _viewProjBuffer, 64);
        WriteBufferDescriptor(_worldDescriptorSets[1], DescriptorType.UniformBuffer, _lightBuffer, (ulong)Marshal.SizeOf<LightData>());
        WriteImageDescriptor(_worldDescriptorSets[2], DescriptorType.SampledImage, _wallTextureView);
        WriteSamplerDescriptor(_worldDescriptorSets[3], _wallSampler);

        _postSetLayouts = new DescriptorSetLayout[2];
        _postDescriptorSets = new DescriptorSet[2];
        CreateDescriptorSetLayout(DescriptorType.SampledImage, ShaderStageFlags.FragmentBit, out _postSetLayouts[0]);
        CreateDescriptorSetLayout(DescriptorType.Sampler, ShaderStageFlags.FragmentBit, out _postSetLayouts[1]);
        AllocateDescriptorSets(_postSetLayouts, _postDescriptorSets);
        WriteImageDescriptor(_postDescriptorSets[0], DescriptorType.SampledImage, _sceneColorView);
        WriteSamplerDescriptor(_postDescriptorSets[1], _postSampler);
    }

    private void CreateDescriptorSetLayout(DescriptorType type, ShaderStageFlags stage, out DescriptorSetLayout layout)
    {
        DescriptorSetLayoutBinding binding = new(0, type, 1, stage);
        DescriptorSetLayoutCreateInfo info = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &binding
        };
        Check(_vk!.CreateDescriptorSetLayout(_device, in info, null, out layout), "vkCreateDescriptorSetLayout");
    }

    private void AllocateDescriptorSets(DescriptorSetLayout[] layouts, DescriptorSet[] sets)
    {
        fixed (DescriptorSetLayout* pLayouts = layouts)
        fixed (DescriptorSet* pSets = sets)
        {
            DescriptorSetAllocateInfo info = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = (uint)layouts.Length,
                PSetLayouts = pLayouts
            };
            Check(_vk!.AllocateDescriptorSets(_device, in info, pSets), "vkAllocateDescriptorSets");
        }
    }

    private void WriteBufferDescriptor(DescriptorSet set, DescriptorType type, Silk.NET.Vulkan.Buffer buffer, ulong range)
    {
        DescriptorBufferInfo bufferInfo = new(buffer, 0, range);
        WriteDescriptorSet write = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = set,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = type,
            PBufferInfo = &bufferInfo
        };
        _vk!.UpdateDescriptorSets(_device, 1, in write, 0, null);
    }

    private void WriteImageDescriptor(DescriptorSet set, DescriptorType type, ImageView view)
    {
        DescriptorImageInfo imageInfo = new()
        {
            ImageView = view,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };
        WriteDescriptorSet write = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = set,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = type,
            PImageInfo = &imageInfo
        };
        _vk!.UpdateDescriptorSets(_device, 1, in write, 0, null);
    }

    private void WriteSamplerDescriptor(DescriptorSet set, Sampler sampler)
    {
        DescriptorImageInfo imageInfo = new() { Sampler = sampler };
        WriteDescriptorSet write = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = set,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.Sampler,
            PImageInfo = &imageInfo
        };
        _vk!.UpdateDescriptorSets(_device, 1, in write, 0, null);
    }

    private void CreatePipelines()
    {
        byte[] worldVert = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "vertex.spv"));
        byte[] worldFrag = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "fragment.spv"));
        byte[] postVert = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "post_vertex.spv"));
        byte[] postFrag = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "post_fragment.spv"));
        byte[] hudVert = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "hud_vertex.spv"));
        byte[] hudFrag = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "hud_fragment.spv"));

        CreateWorldPipeline(worldVert, worldFrag);
        CreatePostPipeline(postVert, postFrag);
        CreateHudPipeline(hudVert, hudFrag);
    }

    private void CreateWorldPipeline(byte[] vertexCode, byte[] fragmentCode)
    {
        ShaderModule vert = CreateShaderModule(vertexCode);
        ShaderModule frag = CreateShaderModule(fragmentCode);
        try
        {
            PipelineShaderStageCreateInfo* stages = stackalloc PipelineShaderStageCreateInfo[2];
            IntPtr main0 = Marshal.StringToHGlobalAnsi("main");
            IntPtr main1 = Marshal.StringToHGlobalAnsi("main");
            try
            {
                stages[0] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vert, PName = (byte*)main0 };
                stages[1] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = frag, PName = (byte*)main1 };

                fixed (DescriptorSetLayout* layouts = _worldSetLayouts)
                {
                    PipelineLayoutCreateInfo layoutInfo = new()
                    {
                        SType = StructureType.PipelineLayoutCreateInfo,
                        SetLayoutCount = (uint)_worldSetLayouts.Length,
                        PSetLayouts = layouts
                    };
                    Check(_vk!.CreatePipelineLayout(_device, in layoutInfo, null, out _worldPipelineLayout), "vkCreatePipelineLayout(world)");
                }

                VertexInputBindingDescription binding = new(0, 36, VertexInputRate.Vertex);
                VertexInputAttributeDescription* attributes = stackalloc VertexInputAttributeDescription[4];
                attributes[0] = new VertexInputAttributeDescription(0, 0, Format.R32G32B32Sfloat, 0);
                attributes[1] = new VertexInputAttributeDescription(1, 0, Format.R32G32B32Sfloat, 12);
                attributes[2] = new VertexInputAttributeDescription(2, 0, Format.R32G32Sfloat, 24);
                attributes[3] = new VertexInputAttributeDescription(3, 0, Format.R32Sfloat, 32);

                PipelineVertexInputStateCreateInfo vertexInput = new()
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = 1,
                    PVertexBindingDescriptions = &binding,
                    VertexAttributeDescriptionCount = 4,
                    PVertexAttributeDescriptions = attributes
                };
                PipelineInputAssemblyStateCreateInfo inputAssembly = new()
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList
                };
                PipelineViewportStateCreateInfo viewportState = new()
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    ScissorCount = 1
                };
                PipelineRasterizationStateCreateInfo raster = new()
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    PolygonMode = PolygonMode.Fill,
                    CullMode = CullModeFlags.None,
                    FrontFace = FrontFace.CounterClockwise,
                    LineWidth = 1
                };
                PipelineMultisampleStateCreateInfo multisample = new()
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    RasterizationSamples = SampleCountFlags.Count1Bit
                };
                PipelineDepthStencilStateCreateInfo depth = new()
                {
                    SType = StructureType.PipelineDepthStencilStateCreateInfo,
                    DepthTestEnable = true,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.LessOrEqual
                };
                PipelineColorBlendAttachmentState colorBlendAttachment = new()
                {
                    BlendEnable = false,
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
                };
                PipelineColorBlendStateCreateInfo colorBlend = new()
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    AttachmentCount = 1,
                    PAttachments = &colorBlendAttachment
                };
                DynamicState* dynamicStates = stackalloc DynamicState[2];
                dynamicStates[0] = DynamicState.Viewport;
                dynamicStates[1] = DynamicState.Scissor;
                PipelineDynamicStateCreateInfo dynamic = new()
                {
                    SType = StructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = 2,
                    PDynamicStates = dynamicStates
                };

                GraphicsPipelineCreateInfo pipelineInfo = new()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = stages,
                    PVertexInputState = &vertexInput,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &raster,
                    PMultisampleState = &multisample,
                    PDepthStencilState = &depth,
                    PColorBlendState = &colorBlend,
                    PDynamicState = &dynamic,
                    Layout = _worldPipelineLayout,
                    RenderPass = _sceneRenderPass,
                    Subpass = 0
                };
                Check(_vk.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out _worldPipeline), "vkCreateGraphicsPipelines(world)");
            }
            finally
            {
                Marshal.FreeHGlobal(main0);
                Marshal.FreeHGlobal(main1);
            }
        }
        finally
        {
            _vk!.DestroyShaderModule(_device, vert, null);
            _vk.DestroyShaderModule(_device, frag, null);
        }
    }

    private void CreatePostPipeline(byte[] vertexCode, byte[] fragmentCode)
    {
        ShaderModule vert = CreateShaderModule(vertexCode);
        ShaderModule frag = CreateShaderModule(fragmentCode);
        try
        {
            IntPtr main0 = Marshal.StringToHGlobalAnsi("main");
            IntPtr main1 = Marshal.StringToHGlobalAnsi("main");
            try
            {
                PipelineShaderStageCreateInfo* stages = stackalloc PipelineShaderStageCreateInfo[2];
                stages[0] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vert, PName = (byte*)main0 };
                stages[1] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = frag, PName = (byte*)main1 };

                fixed (DescriptorSetLayout* layouts = _postSetLayouts)
                {
                    PipelineLayoutCreateInfo layoutInfo = new()
                    {
                        SType = StructureType.PipelineLayoutCreateInfo,
                        SetLayoutCount = (uint)_postSetLayouts.Length,
                        PSetLayouts = layouts
                    };
                    Check(_vk!.CreatePipelineLayout(_device, in layoutInfo, null, out _postPipelineLayout), "vkCreatePipelineLayout(post)");
                }

                PipelineVertexInputStateCreateInfo vertexInput = new() { SType = StructureType.PipelineVertexInputStateCreateInfo };
                PipelineInputAssemblyStateCreateInfo inputAssembly = new()
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList
                };
                PipelineViewportStateCreateInfo viewportState = new()
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    ScissorCount = 1
                };
                PipelineRasterizationStateCreateInfo raster = new()
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    PolygonMode = PolygonMode.Fill,
                    CullMode = CullModeFlags.None,
                    FrontFace = FrontFace.CounterClockwise,
                    LineWidth = 1
                };
                PipelineMultisampleStateCreateInfo multisample = new()
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    RasterizationSamples = SampleCountFlags.Count1Bit
                };
                PipelineColorBlendAttachmentState blendAttachment = new()
                {
                    BlendEnable = false,
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
                };
                PipelineColorBlendStateCreateInfo blend = new()
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    AttachmentCount = 1,
                    PAttachments = &blendAttachment
                };
                DynamicState* dynamicStates = stackalloc DynamicState[2];
                dynamicStates[0] = DynamicState.Viewport;
                dynamicStates[1] = DynamicState.Scissor;
                PipelineDynamicStateCreateInfo dynamic = new()
                {
                    SType = StructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = 2,
                    PDynamicStates = dynamicStates
                };

                GraphicsPipelineCreateInfo pipelineInfo = new()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = stages,
                    PVertexInputState = &vertexInput,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &raster,
                    PMultisampleState = &multisample,
                    PColorBlendState = &blend,
                    PDynamicState = &dynamic,
                    Layout = _postPipelineLayout,
                    RenderPass = _postRenderPass,
                    Subpass = 0
                };
                Check(_vk.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out _postPipeline), "vkCreateGraphicsPipelines(post)");
            }
            finally
            {
                Marshal.FreeHGlobal(main0);
                Marshal.FreeHGlobal(main1);
            }
        }
        finally
        {
            _vk!.DestroyShaderModule(_device, vert, null);
            _vk.DestroyShaderModule(_device, frag, null);
        }
    }

    private void CreateHudPipeline(byte[] vertexCode, byte[] fragmentCode)
    {
        ShaderModule vert = CreateShaderModule(vertexCode);
        ShaderModule frag = CreateShaderModule(fragmentCode);
        try
        {
            IntPtr main0 = Marshal.StringToHGlobalAnsi("main");
            IntPtr main1 = Marshal.StringToHGlobalAnsi("main");
            try
            {
                PipelineShaderStageCreateInfo* stages = stackalloc PipelineShaderStageCreateInfo[2];
                stages[0] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vert, PName = (byte*)main0 };
                stages[1] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = frag, PName = (byte*)main1 };

                PipelineVertexInputStateCreateInfo vertexInput = new()
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo
                };
                VertexInputBindingDescription binding = new(0, 24, VertexInputRate.Vertex);
                VertexInputAttributeDescription* attributes = stackalloc VertexInputAttributeDescription[2];
                attributes[0] = new VertexInputAttributeDescription(0, 0, Format.R32G32Sfloat, 0);
                attributes[1] = new VertexInputAttributeDescription(1, 0, Format.R32G32B32A32Sfloat, 8);
                vertexInput.VertexBindingDescriptionCount = 1;
                vertexInput.PVertexBindingDescriptions = &binding;
                vertexInput.VertexAttributeDescriptionCount = 2;
                vertexInput.PVertexAttributeDescriptions = attributes;

                PipelineInputAssemblyStateCreateInfo inputAssembly = new()
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList
                };
                PipelineViewportStateCreateInfo viewportState = new()
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    ScissorCount = 1
                };
                PipelineRasterizationStateCreateInfo raster = new()
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    PolygonMode = PolygonMode.Fill,
                    CullMode = CullModeFlags.None,
                    FrontFace = FrontFace.CounterClockwise,
                    LineWidth = 1
                };
                PipelineMultisampleStateCreateInfo multisample = new()
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    RasterizationSamples = SampleCountFlags.Count1Bit
                };
                PipelineColorBlendAttachmentState blendAttachment = new()
                {
                    BlendEnable = true,
                    SrcColorBlendFactor = BlendFactor.SrcAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    AlphaBlendOp = BlendOp.Add,
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
                };
                PipelineColorBlendStateCreateInfo blend = new()
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    AttachmentCount = 1,
                    PAttachments = &blendAttachment
                };
                DynamicState* dynamicStates = stackalloc DynamicState[2];
                dynamicStates[0] = DynamicState.Viewport;
                dynamicStates[1] = DynamicState.Scissor;
                PipelineDynamicStateCreateInfo dynamic = new()
                {
                    SType = StructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = 2,
                    PDynamicStates = dynamicStates
                };

                GraphicsPipelineCreateInfo layoutPipelineInfo = new()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo
                };

                PipelineLayoutCreateInfo layoutInfo = new() { SType = StructureType.PipelineLayoutCreateInfo };
                Check(_vk!.CreatePipelineLayout(_device, in layoutInfo, null, out _hudPipelineLayout), "vkCreatePipelineLayout(hud)");

                GraphicsPipelineCreateInfo pipelineInfo = new()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = stages,
                    PVertexInputState = &vertexInput,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &raster,
                    PMultisampleState = &multisample,
                    PColorBlendState = &blend,
                    PDynamicState = &dynamic,
                    Layout = _hudPipelineLayout,
                    RenderPass = _postRenderPass,
                    Subpass = 0
                };
                Check(_vk.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out _hudPipeline), "vkCreateGraphicsPipelines(hud)");
            }
            finally
            {
                Marshal.FreeHGlobal(main0);
                Marshal.FreeHGlobal(main1);
            }
        }
        finally
        {
            _vk!.DestroyShaderModule(_device, vert, null);
            _vk.DestroyShaderModule(_device, frag, null);
        }
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        ShaderModuleCreateInfo info = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length
        };
        fixed (byte* pCode = code)
        {
            info.PCode = (uint*)pCode;
            Check(_vk!.CreateShaderModule(_device, in info, null, out ShaderModule module), "vkCreateShaderModule");
            return module;
        }
    }

    private Sampler CreateSampler()
    {
        SamplerCreateInfo info = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Nearest,
            MinFilter = Filter.Nearest,
            MipmapMode = SamplerMipmapMode.Nearest,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MipLodBias = 0,
            MaxAnisotropy = 1,
            MinLod = 0,
            MaxLod = 0
        };
        Check(_vk!.CreateSampler(_device, in info, null, out Sampler sampler), "vkCreateSampler");
        return sampler;
    }

    private void CreateWallTexture()
    {
        const uint width = 256;
        const uint height = 256;
        byte[] pixels = new byte[width * height * 4];
        Random random = new(1337);

        for (uint y = 0; y < height; y++)
        {
            for (uint x = 0; x < width; x++)
            {
                int i = (int)((y * width + x) * 4);
                bool isTop = y < 128;
                bool isLeft = x < 128;

                if (isTop && isLeft)
                {
                    uint cX = x % 16;
                    if (cX > 2 && cX < 13 && random.Next(100) < 65)
                    {
                        byte g = (byte)random.Next(140, 255);
                        pixels[i] = (byte)(g / 6);
                        pixels[i + 1] = g;
                        pixels[i + 2] = (byte)(g / 2);
                        pixels[i + 3] = 255;
                    }
                    else
                    {
                        pixels[i + 1] = 12;
                        pixels[i + 3] = 255;
                    }
                }
                else if (isTop)
                {
                    uint cX = x % 16;
                    if (cX > 2 && cX < 13 && random.Next(100) < 65)
                    {
                        byte r = (byte)random.Next(140, 255);
                        pixels[i] = r;
                        pixels[i + 1] = (byte)(r / 6);
                        pixels[i + 2] = (byte)(r / 6);
                        pixels[i + 3] = 255;
                    }
                    else
                    {
                        pixels[i] = 12;
                        pixels[i + 3] = 255;
                    }
                }
                else if (isLeft)
                {
                    byte val = (byte)random.Next(70, 100);
                    pixels[i] = val;
                    pixels[i + 1] = val;
                    pixels[i + 2] = val;
                    pixels[i + 3] = 255;
                }
                else
                {
                    uint cX = x % 16;
                    if (cX > 2 && cX < 13 && random.Next(100) < 65)
                    {
                        byte b = (byte)random.Next(140, 255);
                        pixels[i] = (byte)(b / 6);
                        pixels[i + 1] = (byte)(b / 2);
                        pixels[i + 2] = b;
                        pixels[i + 3] = 255;
                    }
                    else
                    {
                        pixels[i + 2] = 12;
                        pixels[i + 3] = 255;
                    }
                }
            }
        }

        CreateImage(width, height, Format.R8G8B8A8Unorm, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, out _wallTexture, out _wallTextureMemory);

        ulong size = (ulong)pixels.Length;
        CreateHostBuffer(size, BufferUsageFlags.TransferSrcBit, out Silk.NET.Vulkan.Buffer stagingBuffer, out DeviceMemory stagingMemory);
        try
        {
            UploadMapped(stagingMemory, pixels);
            CommandBuffer cmd = BeginImmediateCommands();
            TransitionImageLayout(cmd, _wallTexture, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, ImageAspectFlags.ColorBit);
            BufferImageCopy region = new()
            {
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageExtent = new Extent3D(width, height, 1)
            };
            _vk!.CmdCopyBufferToImage(cmd, stagingBuffer, _wallTexture, ImageLayout.TransferDstOptimal, 1, in region);
            TransitionImageLayout(cmd, _wallTexture, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, ImageAspectFlags.ColorBit);
            EndImmediateCommands(cmd);
        }
        finally
        {
            _vk!.DestroyBuffer(_device, stagingBuffer, null);
            _vk.FreeMemory(_device, stagingMemory, null);
        }

        _wallTextureView = CreateImageView(_wallTexture, Format.R8G8B8A8Unorm, ImageAspectFlags.ColorBit);
        _wallSampler = CreateSampler();
    }

    private void CreateHostBuffer(ulong size, BufferUsageFlags usage, out Silk.NET.Vulkan.Buffer buffer, out DeviceMemory memory)
    {
        BufferCreateInfo info = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };
        Check(_vk!.CreateBuffer(_device, in info, null, out buffer), "vkCreateBuffer");
        MemoryRequirements requirements = new();
        _vk.GetBufferMemoryRequirements(_device, buffer, out requirements);
        uint memoryType = FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        MemoryAllocateInfo allocation = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = memoryType
        };
        Check(_vk.AllocateMemory(_device, in allocation, null, out memory), "vkAllocateMemory(buffer)");
        Check(_vk.BindBufferMemory(_device, buffer, memory, 0), "vkBindBufferMemory");
    }

    private void CreateImage(uint width, uint height, Format format, ImageUsageFlags usage, MemoryPropertyFlags properties, out Image image, out DeviceMemory memory)
    {
        ImageCreateInfo info = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };
        Check(_vk!.CreateImage(_device, in info, null, out image), "vkCreateImage");
        MemoryRequirements requirements = new();
        _vk.GetImageMemoryRequirements(_device, image, out requirements);
        uint memoryType = FindMemoryType(requirements.MemoryTypeBits, properties);
        MemoryAllocateInfo allocation = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = memoryType
        };
        Check(_vk.AllocateMemory(_device, in allocation, null, out memory), "vkAllocateMemory(image)");
        Check(_vk.BindImageMemory(_device, image, memory, 0), "vkBindImageMemory");
    }

    private ImageView CreateImageView(Image image, Format format, ImageAspectFlags aspect)
    {
        ImageViewCreateInfo info = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            Components = new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity),
            SubresourceRange = new ImageSubresourceRange(aspect, 0, 1, 0, 1)
        };
        Check(_vk!.CreateImageView(_device, in info, null, out ImageView view), "vkCreateImageView(texture)");
        return view;
    }

    private uint FindMemoryType(uint typeBits, MemoryPropertyFlags required)
    {
        PhysicalDeviceMemoryProperties properties = new();
        _vk!.GetPhysicalDeviceMemoryProperties(_physicalDevice, &properties);

        for (uint i = 0; i < properties.MemoryTypeCount; i++)
        {
            if ((typeBits & (1u << (int)i)) != 0 &&
                (properties.MemoryTypes[(int)i].PropertyFlags & required) == required)
                return i;
        }

        throw new InvalidOperationException("No compatible Vulkan memory type was found.");
    }

    private CommandBuffer BeginImmediateCommands()
    {
        CommandBufferAllocateInfo info = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        Check(_vk!.AllocateCommandBuffers(_device, in info, out CommandBuffer commandBuffer), "vkAllocateCommandBuffers(immediate)");

        CommandBufferBeginInfo begin = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        Check(_vk.BeginCommandBuffer(commandBuffer, in begin), "vkBeginCommandBuffer(immediate)");
        return commandBuffer;
    }

    private void EndImmediateCommands(CommandBuffer commandBuffer)
    {
        Check(_vk!.EndCommandBuffer(commandBuffer), "vkEndCommandBuffer(immediate)");
        SubmitInfo submit = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };
        Check(_vk.QueueSubmit(_graphicsQueue, 1, in submit, default), "vkQueueSubmit(immediate)");
        Check(_vk.QueueWaitIdle(_graphicsQueue), "vkQueueWaitIdle(immediate)");
        _vk.FreeCommandBuffers(_device, _commandPool, 1, in commandBuffer);
    }

    private void TransitionImageLayout(CommandBuffer cmd, Image image, ImageLayout oldLayout, ImageLayout newLayout, ImageAspectFlags aspect)
    {
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = uint.MaxValue,
            DstQueueFamilyIndex = uint.MaxValue,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(aspect, 0, 1, 0, 1)
        };

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;
        }
        else if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.ColorAttachmentWriteBit;
        }
        else if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.DepthStencilAttachmentOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.DepthStencilAttachmentWriteBit;
        }
        else
        {
            barrier.SrcAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit;
            barrier.DstAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit;
        }

        _vk!.CmdPipelineBarrier(
            cmd,
            PipelineStageFlags.AllCommandsBit,
            PipelineStageFlags.AllCommandsBit,
            DependencyFlags.None,
            0, null,
            0, null,
            1, in barrier);
    }

    private void UploadMapped(DeviceMemory memory, byte[] data)
    {
        void* destination;
        Check(_vk!.MapMemory(_device, memory, 0, (nuint)data.Length, 0, &destination), "vkMapMemory");
        fixed (byte* source = data)
            System.Buffer.MemoryCopy(source, destination, data.Length, data.Length);
        _vk.UnmapMemory(_device, memory);
    }

    private void UploadRenderData(RenderData data)
    {
        ulong worldBytes = (ulong)(data.WorldVertexCount * sizeof(float));
        EnsureWorldVertexCapacity(worldBytes);
        if (worldBytes > 0)
        {
            void* destination;
            Check(_vk!.MapMemory(_device, _worldVertexMemory, 0, worldBytes, 0, &destination), "vkMapMemory(world)");
            fixed (float* source = data.WorldVertices)
                System.Buffer.MemoryCopy(source, destination, (long)worldBytes, (long)worldBytes);
            _vk.UnmapMemory(_device, _worldVertexMemory);
        }

        ulong hudBytes = (ulong)(data.HudVertexCount * sizeof(float));
        EnsureHudVertexCapacity(hudBytes);
        if (hudBytes > 0)
        {
            void* destination;
            Check(_vk.MapMemory(_device, _hudVertexMemory, 0, hudBytes, 0, &destination), "vkMapMemory(hud)");
            fixed (float* source = data.HudVertices)
                System.Buffer.MemoryCopy(source, destination, (long)hudBytes, (long)hudBytes);
            _vk.UnmapMemory(_device, _hudVertexMemory);
        }
    }

    private void EnsureWorldVertexCapacity(ulong required)
    {
        if (required <= _worldVertexCapacity)
            return;

        _vk!.DeviceWaitIdle(_device);
        _vk.DestroyBuffer(_device, _worldVertexBuffer, null);
        _vk.FreeMemory(_device, _worldVertexMemory, null);

        ulong capacity = Math.Max(1024ul, _worldVertexCapacity);
        while (capacity < required)
            capacity = checked(capacity * 2);

        _worldVertexCapacity = capacity;
        CreateHostBuffer(capacity, BufferUsageFlags.VertexBufferBit, out _worldVertexBuffer, out _worldVertexMemory);
    }

    private void EnsureHudVertexCapacity(ulong required)
    {
        if (required <= _hudVertexCapacity)
            return;

        _vk!.DeviceWaitIdle(_device);
        _vk.DestroyBuffer(_device, _hudVertexBuffer, null);
        _vk.FreeMemory(_device, _hudVertexMemory, null);

        ulong capacity = Math.Max(1024ul, _hudVertexCapacity);
        while (capacity < required)
            capacity = checked(capacity * 2);

        _hudVertexCapacity = capacity;
        CreateHostBuffer(capacity, BufferUsageFlags.VertexBufferBit, out _hudVertexBuffer, out _hudVertexMemory);
    }

    private void UpdateUniforms(RenderData data, float width, float height)
    {
        Vector3 eyePos = new(data.Camera.Position.X, 0.8f, data.Camera.Position.Z);
        Matrix4x4 view = Matrix4x4.CreateLookAt(eyePos, eyePos + data.Camera.Forward, Vector3.UnitY);
        float aspect = height > 0.001f ? width / height : 1f;
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(SystemConfig.Fov * MathF.PI / 180f, aspect, 0.05f, 100f);
        proj.M22 *= -1f;

        unsafe
        {
            void* destination;
            Check(_vk!.MapMemory(_device, _viewProjMemory, 0, 64, 0, &destination), "vkMapMemory(viewProj)");
            *(Matrix4x4*)destination = view * proj;
            _vk.UnmapMemory(_device, _viewProjMemory);
        }

        data.Lanterns.Sort((a, b) => Vector3.DistanceSquared(eyePos, a).CompareTo(Vector3.DistanceSquared(eyePos, b)));
        int maxLanterns = SystemConfig.GraphicsQuality switch { 0 => 2, 1 => 4, 2 => 8, _ => 4 };
        LightData lights = new()
        {
            FlashlightPos = new Vector4(eyePos, 0.92f),
            FlashlightDir = new Vector4(data.Camera.Forward, data.TriggerMuzzleFlash ? 12f : 3f),
            LanternCount = Math.Min(data.Lanterns.Count, maxLanterns),
            Time = (float)data.CurrentTime
        };
        if (lights.LanternCount > 0) lights.Lantern0 = new Vector4(data.Lanterns[0], 1f);
        if (lights.LanternCount > 1) lights.Lantern1 = new Vector4(data.Lanterns[1], 1f);
        if (lights.LanternCount > 2) lights.Lantern2 = new Vector4(data.Lanterns[2], 1f);
        if (lights.LanternCount > 3) lights.Lantern3 = new Vector4(data.Lanterns[3], 1f);
        if (lights.LanternCount > 4) lights.Lantern4 = new Vector4(data.Lanterns[4], 1f);
        if (lights.LanternCount > 5) lights.Lantern5 = new Vector4(data.Lanterns[5], 1f);
        if (lights.LanternCount > 6) lights.Lantern6 = new Vector4(data.Lanterns[6], 1f);
        if (lights.LanternCount > 7) lights.Lantern7 = new Vector4(data.Lanterns[7], 1f);

        void* dst;
        Check(_vk.MapMemory(_device, _lightMemory, 0, (nuint)Marshal.SizeOf<LightData>(), 0, &dst), "vkMapMemory(light)");
        *(LightData*)dst = lights;
        _vk.UnmapMemory(_device, _lightMemory);
    }

    private void RecordFrame(uint imageIndex, RenderData data, Vector4 clearColor)
    {
        CommandBuffer cmd = _commandBuffers[imageIndex];
        Check(_vk!.ResetCommandBuffer(cmd, 0), "vkResetCommandBuffer");

        CommandBufferBeginInfo begin = new() { SType = StructureType.CommandBufferBeginInfo };
        Check(_vk.BeginCommandBuffer(cmd, in begin), "vkBeginCommandBuffer");

        Viewport sceneViewport = new(0, 0, _sceneWidth, _sceneHeight, 0, 1);
        Rect2D sceneScissor = new(new Offset2D(0, 0), new Extent2D(_sceneWidth, _sceneHeight));
        _vk.CmdSetViewport(cmd, 0, 1, in sceneViewport);
        _vk.CmdSetScissor(cmd, 0, 1, in sceneScissor);

        ClearValue* sceneClears = stackalloc ClearValue[2];
        sceneClears[0].Color = new ClearColorValue(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
        sceneClears[1].DepthStencil = new ClearDepthStencilValue(1f, 0);

        RenderPassBeginInfo scenePass = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _sceneRenderPass,
            Framebuffer = _sceneFramebuffer,
            RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D(_sceneWidth, _sceneHeight)),
            ClearValueCount = 2,
            PClearValues = sceneClears
        };
        _vk.CmdBeginRenderPass(cmd, in scenePass, SubpassContents.Inline);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _worldPipeline);

        for (uint setIndex = 0; setIndex < _worldDescriptorSets.Length; setIndex++)
        {
            DescriptorSet set = _worldDescriptorSets[setIndex];
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _worldPipelineLayout, setIndex, 1, in set, 0, null);
        }

        ulong vertexOffset = 0;
        _vk.CmdBindVertexBuffers(cmd, 0, 1, in _worldVertexBuffer, in vertexOffset);
        if (data.WorldVertexCount > 0)
            uint worldVertexCount = (uint)(data.WorldVertexCount / 9);
        if (worldVertexCount > 0)
        {
            _vk.CmdDraw(cmd, worldVertexCount, 1, 0, 0);
            data.GpuDrawCalls++;
            data.GpuVertices += (int)worldVertexCount;
        }
        _vk.CmdEndRenderPass(cmd);

        Viewport postViewport = new(0, 0, _swapchainExtent.Width, _swapchainExtent.Height, 0, 1);
        Rect2D postScissor = new(new Offset2D(0, 0), _swapchainExtent);
        _vk.CmdSetViewport(cmd, 0, 1, in postViewport);
        _vk.CmdSetScissor(cmd, 0, 1, in postScissor);

        ClearValue postClear = new();
        postClear.Color = new ClearColorValue(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
        RenderPassBeginInfo postPass = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _postRenderPass,
            Framebuffer = _swapchainFramebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent),
            ClearValueCount = 1,
            PClearValues = &postClear
        };
        _vk.CmdBeginRenderPass(cmd, in postPass, SubpassContents.Inline);

        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _postPipeline);
        for (uint setIndex = 0; setIndex < _postDescriptorSets.Length; setIndex++)
        {
            DescriptorSet set = _postDescriptorSets[setIndex];
            _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _postPipelineLayout, setIndex, 1, in set, 0, null);
        }
        _vk.CmdDraw(cmd, 3, 1, 0, 0);

        if (data.HudVertexCount > 0)
        {
            _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _hudPipeline);
            _vk.CmdBindVertexBuffers(cmd, 0, 1, in _hudVertexBuffer, in vertexOffset);
            uint hudVertexCount = (uint)(data.HudVertexCount / 6);
            _vk.CmdDraw(cmd, hudVertexCount, 1, 0, 0);
            data.GpuDrawCalls++;
            data.GpuVertices += (int)hudVertexCount;
        }

        _vk.CmdEndRenderPass(cmd);
        Check(_vk.EndCommandBuffer(cmd), "vkEndCommandBuffer");
    }

    public void Render(RenderData data, float width, float height, Vector4 clearColor)
    {
        if (!_initialized)
            throw new InvalidOperationException("Silk.NET Vulkan backend has not been initialized.");
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (_headless)
            return;

        uint requestedSceneWidth = Math.Max(1u, (uint)(SystemConfig.ResolutionWidth * SystemConfig.RenderScale));
        uint requestedSceneHeight = Math.Max(1u, (uint)(SystemConfig.ResolutionHeight * SystemConfig.RenderScale));
        if (requestedSceneWidth != _sceneWidth || requestedSceneHeight != _sceneHeight ||
            (width > 1 && (int)width != _lastSwapchainRequestWidth) ||
            (height > 1 && (int)height != _lastSwapchainRequestHeight))
        {
            RecreateSwapchain((int)Math.Max(1, width), (int)Math.Max(1, height));
        }

        Check(_vk!.WaitForFences(_device, 1, in _inFlightFence, true, ulong.MaxValue), "vkWaitForFences");

        uint imageIndex = 0;
        Result acquire = _swapchainApi!.AcquireNextImage(_device, _swapchain, ulong.MaxValue, _imageAvailable, default, &imageIndex);
        if (acquire == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain((int)Math.Max(1, width), (int)Math.Max(1, height));
            return;
        }
        if (acquire != Result.Success && acquire != Result.SuboptimalKhr)
            Check(acquire, "vkAcquireNextImageKHR");

        UploadRenderData(data);
        UpdateUniforms(data, width, height);

        Check(_vk.ResetFences(_device, 1, in _inFlightFence), "vkResetFences");
        RecordFrame(imageIndex, data, clearColor);

        PipelineStageFlags waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        SubmitInfo submit = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &_imageAvailable,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &_commandBuffers[imageIndex],
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &_renderFinished
        };
        Check(_vk.QueueSubmit(_graphicsQueue, 1, in submit, _inFlightFence), "vkQueueSubmit");

        PresentInfoKHR present = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &_renderFinished,
            SwapchainCount = 1,
            PSwapchains = &_swapchain,
            PImageIndices = &imageIndex
        };
        Result presentResult = _swapchainApi.QueuePresent(_graphicsQueue, in present);
        if (presentResult == Result.ErrorOutOfDateKhr || presentResult == Result.SuboptimalKhr)
        {
            RecreateSwapchain((int)Math.Max(1, width), (int)Math.Max(1, height));
        }
        else
        {
            Check(presentResult, "vkQueuePresentKHR");
        }
    }

    private void RecreateSwapchain(int width, int height)
    {
        if (_headless)
            return;

        _vk!.DeviceWaitIdle(_device);

        // Pipelines and framebuffers are tied to render-pass compatibility, so tear them
        // down before replacing the swapchain and render passes.
        DestroyPipelines();
        DestroySceneTargets();

        if (_sceneRenderPass.Handle != 0)
            _vk.DestroyRenderPass(_device, _sceneRenderPass, null);
        if (_postRenderPass.Handle != 0)
            _vk.DestroyRenderPass(_device, _postRenderPass, null);

        foreach (Framebuffer framebuffer in _swapchainFramebuffers)
            if (framebuffer.Handle != 0) _vk.DestroyFramebuffer(_device, framebuffer, null);
        foreach (ImageView view in _swapchainImageViews)
            if (view.Handle != 0) _vk.DestroyImageView(_device, view, null);
        if (_swapchain.Handle != 0)
            _swapchainApi!.DestroySwapchain(_device, _swapchain, null);

        _swapchainFramebuffers = Array.Empty<Framebuffer>();
        _swapchainImageViews = Array.Empty<ImageView>();
        _swapchainImages = Array.Empty<Image>();

        CreateSwapchain(width, height);
        CreateRenderPasses();
        CreateSceneTargets();
        CreateFramebuffers();
        CreatePipelines();

        WriteImageDescriptor(_postDescriptorSets[0], DescriptorType.SampledImage, _sceneColorView);
    }

    private void DestroySceneTargets()
    {
        if (_sceneFramebuffer.Handle != 0) _vk!.DestroyFramebuffer(_device, _sceneFramebuffer, null);
        if (_sceneColorView.Handle != 0) _vk!.DestroyImageView(_device, _sceneColorView, null);
        if (_sceneDepthView.Handle != 0) _vk!.DestroyImageView(_device, _sceneDepthView, null);
        if (_sceneColor.Handle != 0) _vk!.DestroyImage(_device, _sceneColor, null);
        if (_sceneDepth.Handle != 0) _vk!.DestroyImage(_device, _sceneDepth, null);
        if (_sceneColorMemory.Handle != 0) _vk.FreeMemory(_device, _sceneColorMemory, null);
        if (_sceneDepthMemory.Handle != 0) _vk.FreeMemory(_device, _sceneDepthMemory, null);

        _sceneFramebuffer = default;
        _sceneColorView = default;
        _sceneDepthView = default;
        _sceneColor = default;
        _sceneDepth = default;
        _sceneColorMemory = default;
        _sceneDepthMemory = default;
    }

    private void DestroyPipelines()
    {
        if (_worldPipeline.Handle != 0) _vk!.DestroyPipeline(_device, _worldPipeline, null);
        if (_worldPipelineLayout.Handle != 0) _vk.DestroyPipelineLayout(_device, _worldPipelineLayout, null);
        if (_postPipeline.Handle != 0) _vk.DestroyPipeline(_device, _postPipeline, null);
        if (_postPipelineLayout.Handle != 0) _vk.DestroyPipelineLayout(_device, _postPipelineLayout, null);
        if (_hudPipeline.Handle != 0) _vk.DestroyPipeline(_device, _hudPipeline, null);
        if (_hudPipelineLayout.Handle != 0) _vk.DestroyPipelineLayout(_device, _hudPipelineLayout, null);
        _worldPipeline = default;
        _worldPipelineLayout = default;
        _postPipeline = default;
        _postPipelineLayout = default;
        _hudPipeline = default;
        _hudPipelineLayout = default;
    }

    private void DisposeVulkanObjects()
    {
        if (_vk is null)
            return;

        if (_device.Handle != 0)
        {
            _vk.DeviceWaitIdle(_device);

            DestroyPipelines();
            if (_descriptorPool.Handle != 0) _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
            foreach (DescriptorSetLayout layout in _worldSetLayouts) if (layout.Handle != 0) _vk.DestroyDescriptorSetLayout(_device, layout, null);
            foreach (DescriptorSetLayout layout in _postSetLayouts) if (layout.Handle != 0) _vk.DestroyDescriptorSetLayout(_device, layout, null);

            if (_wallSampler.Handle != 0) _vk.DestroySampler(_device, _wallSampler, null);
            if (_postSampler.Handle != 0) _vk.DestroySampler(_device, _postSampler, null);
            if (_wallTextureView.Handle != 0) _vk.DestroyImageView(_device, _wallTextureView, null);
            if (_wallTexture.Handle != 0) _vk.DestroyImage(_device, _wallTexture, null);
            if (_wallTextureMemory.Handle != 0) _vk.FreeMemory(_device, _wallTextureMemory, null);

            DestroySceneTargets();

            if (_sceneRenderPass.Handle != 0) _vk.DestroyRenderPass(_device, _sceneRenderPass, null);
            if (_postRenderPass.Handle != 0) _vk.DestroyRenderPass(_device, _postRenderPass, null);

            foreach (Framebuffer framebuffer in _swapchainFramebuffers) if (framebuffer.Handle != 0) _vk.DestroyFramebuffer(_device, framebuffer, null);
            foreach (ImageView view in _swapchainImageViews) if (view.Handle != 0) _vk.DestroyImageView(_device, view, null);
            if (_swapchain.Handle != 0 && _swapchainApi is not null) _swapchainApi.DestroySwapchain(_device, _swapchain, null);

            if (_worldVertexBuffer.Handle != 0) _vk.DestroyBuffer(_device, _worldVertexBuffer, null);
            if (_worldVertexMemory.Handle != 0) _vk.FreeMemory(_device, _worldVertexMemory, null);
            if (_hudVertexBuffer.Handle != 0) _vk.DestroyBuffer(_device, _hudVertexBuffer, null);
            if (_hudVertexMemory.Handle != 0) _vk.FreeMemory(_device, _hudVertexMemory, null);
            if (_viewProjBuffer.Handle != 0) _vk.DestroyBuffer(_device, _viewProjBuffer, null);
            if (_viewProjMemory.Handle != 0) _vk.FreeMemory(_device, _viewProjMemory, null);
            if (_lightBuffer.Handle != 0) _vk.DestroyBuffer(_device, _lightBuffer, null);
            if (_lightMemory.Handle != 0) _vk.FreeMemory(_device, _lightMemory, null);

            if (_inFlightFence.Handle != 0) _vk.DestroyFence(_device, _inFlightFence, null);
            if (_renderFinished.Handle != 0) _vk.DestroySemaphore(_device, _renderFinished, null);
            if (_imageAvailable.Handle != 0) _vk.DestroySemaphore(_device, _imageAvailable, null);
            if (_commandPool.Handle != 0) _vk.DestroyCommandPool(_device, _commandPool, null);

            _vk.DestroyDevice(_device, null);
            _device = default;
        }

        if (_surface.Handle != 0 && _surfaceApi is not null)
        {
            _surfaceApi.DestroySurface(_instance, _surface, null);
            _surface = default;
        }

        if (_instance.Handle != 0)
        {
            _vk.DestroyInstance(_instance, null);
            _instance = default;
        }
    }

    public void Dispose()
    {
        DisposeVulkanObjects();

        _vk = null;
        _sdl = null;
        _surfaceApi = null;
        _swapchainApi = null;
        _physicalDevice = default;
        _graphicsQueue = default;
        _graphicsQueueFamily = 0;
        _swapchain = default;
        _swapchainImages = Array.Empty<Image>();
        _swapchainImageViews = Array.Empty<ImageView>();
        _swapchainFramebuffers = Array.Empty<Framebuffer>();
        _commandBuffers = Array.Empty<CommandBuffer>();
        _sceneRenderPass = default;
        _postRenderPass = default;
        _sceneFramebuffer = default;
        _sceneColor = default;
        _sceneDepth = default;
        _sceneColorView = default;
        _sceneDepthView = default;
        _worldVertexBuffer = default;
        _hudVertexBuffer = default;
        _viewProjBuffer = default;
        _lightBuffer = default;
        _wallTexture = default;
        _wallTextureView = default;
        _worldSetLayouts = Array.Empty<DescriptorSetLayout>();
        _worldDescriptorSets = Array.Empty<DescriptorSet>();
        _postSetLayouts = Array.Empty<DescriptorSetLayout>();
        _postDescriptorSets = Array.Empty<DescriptorSet>();
        _descriptorPool = default;
        _commandPool = default;
        _inFlightFence = default;
        _imageAvailable = default;
        _renderFinished = default;
        _initialized = false;
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success)
            throw new InvalidOperationException($"Vulkan {operation} failed: {result}.");
    }
}
