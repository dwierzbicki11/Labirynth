using System;
using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.SDL;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Runtime.InteropServices;
using CyberEngine.Core;

namespace CyberEngine.Graphics;

/// <summary>
/// Native Silk.NET Vulkan backend.
/// This stage owns Vulkan instance/physical-device/logical-device creation.
/// The existing Veldrid backend remains the active renderer until the
/// swapchain and RenderData pipeline reach feature parity.
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
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;
    private ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    private RenderPass _renderPass;
    private Framebuffer[] _framebuffers = Array.Empty<Framebuffer>();
    private CommandPool _commandPool;
    private CommandBuffer[] _commandBuffers = Array.Empty<CommandBuffer>();
    private Semaphore _imageAvailable;
    private Semaphore _renderFinished;
    private Fence _inFlightFence;
    private nint _nativeWindow;
    private bool _headless;

    public string BackendName => "Silk.NET Vulkan";
    public string DeviceName => IsInitialized ? "Silk.NET Vulkan device" : "Silk.NET Vulkan (not initialized)";
    public bool IsInitialized { get; private set; }

    public void Initialize(int width, int height, bool headless, nint nativeWindowHandle)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        _headless = headless;
        _nativeWindow = nativeWindowHandle;
        if (headless) { IsInitialized = true; return; }
        if (_nativeWindow == 0) throw new InvalidOperationException("Vulkan backend requires an SDL window handle.");

        if (IsInitialized)
            return;

        _vk = Vk.GetApi();
        _sdl = Sdl.GetApi();

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("CyberEngine"),
            ApplicationVersion = 1u,
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("CyberEngine"),
            EngineVersion = 1u,
            ApiVersion = Vk.Version12
        };

        try
        {
            InstanceCreateInfo instanceInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo
            };

            uint extensionCount = 0;
            Window* sdlWindow = (Window*)_nativeWindow;
            if (_sdl!.VulkanGetInstanceExtensions(sdlWindow, &extensionCount, null) != SdlBool.True || extensionCount == 0)
                throw new PlatformNotSupportedException("SDL could not provide Vulkan instance extensions.");
            byte** extensionNames = stackalloc byte*[(int)extensionCount];
            if (_sdl.VulkanGetInstanceExtensions(sdlWindow, &extensionCount, extensionNames) != SdlBool.True)
                throw new InvalidOperationException("SDL Vulkan instance extension query failed.");
            instanceInfo.EnabledExtensionCount = extensionCount;
            instanceInfo.PpEnabledExtensionNames = extensionNames;
            Check(_vk.CreateInstance(in instanceInfo, null, out _instance), "vkCreateInstance");
            if (!_vk.TryGetInstanceExtension(_instance, out _surfaceApi))
                throw new PlatformNotSupportedException("VK_KHR_surface is unavailable.");
            VkHandle instanceHandle = _instance.ToHandle();
            VkNonDispatchableHandle surfaceHandle;
            if (_sdl.VulkanCreateSurface(sdlWindow, instanceHandle, &surfaceHandle) == SdlBool.False)
                throw new InvalidOperationException("SDL could not create Vulkan surface.");
            _surface = surfaceHandle.ToSurface();

            uint deviceCount = 0;
            Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, null), "vkEnumeratePhysicalDevices(count)");
            if (deviceCount == 0)
                throw new PlatformNotSupportedException("No Vulkan physical device was found.");

            PhysicalDevice[] devices = new PhysicalDevice[deviceCount];
            fixed (PhysicalDevice* devicesPtr = devices)
                Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, devicesPtr), "vkEnumeratePhysicalDevices");

            for (int i = 0; i < devices.Length; i++)
            {
                uint queueCount = 0;
                _vk.GetPhysicalDeviceQueueFamilyProperties(devices[i], &queueCount, null);
                if (queueCount == 0)
                    continue;

                QueueFamilyProperties[] queues = new QueueFamilyProperties[queueCount];
                fixed (QueueFamilyProperties* queuesPtr = queues)
                    _vk.GetPhysicalDeviceQueueFamilyProperties(devices[i], &queueCount, queuesPtr);

                for (uint q = 0; q < queueCount; q++)
                {
                    if ((queues[q].QueueFlags & QueueFlags.GraphicsBit) != 0)
                    {
                        uint supported = 0;
                        _surfaceApi!.GetPhysicalDeviceSurfaceSupport(devices[i], q, _surface, &supported);
                        if (supported != 0)
                        {
                            _physicalDevice = devices[i];
                            _graphicsQueueFamily = q;
                            goto DeviceSelected;
                        }
                    }
                }
            }

            throw new PlatformNotSupportedException("No Vulkan graphics queue family was found.");

        DeviceSelected:
            float priority = 1.0f;
            DeviceQueueCreateInfo queueInfo = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = _graphicsQueueFamily,
                QueueCount = 1,
                PQueuePriorities = &priority
            };

            DeviceCreateInfo deviceInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueInfo
            };

            Check(_vk.CreateDevice(_physicalDevice, in deviceInfo, null, out _device), "vkCreateDevice");
            _vk.GetDeviceQueue(_device, _graphicsQueueFamily, 0, out _graphicsQueue);
            if (!_vk.TryGetDeviceExtension(_instance, _device, out _swapchainApi))
                throw new PlatformNotSupportedException("VK_KHR_swapchain is unavailable.");
            CreateSwapchain(width, height);
            CreateRenderTargets();
            CreateCommandResources();
            IsInitialized = true;
        }
        finally
        {
            Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
            Marshal.FreeHGlobal((nint)appInfo.PEngineName);

            if (!IsInitialized)
                DisposeVulkanObjects();
        }
    }

    private void CreateSwapchain(int width, int height)
    {
        SurfaceCapabilitiesKHR capabilities;
        Check(_surfaceApi!.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, &capabilities), "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");
        uint formatCount = 0;
        Check(_surfaceApi.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null), "vkGetPhysicalDeviceSurfaceFormatsKHR(count)");
        var formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* p = formats) Check(_surfaceApi.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, p), "vkGetPhysicalDeviceSurfaceFormatsKHR");
        var chosen = formats[0];
        foreach (var f in formats) if (f.Format == Format.B8G8R8A8Srgb && f.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr) { chosen = f; break; }
        uint modeCount = 0;
        Check(_surfaceApi.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &modeCount, null), "vkGetPhysicalDeviceSurfacePresentModesKHR(count)");
        var modes = new PresentModeKHR[modeCount];
        fixed (PresentModeKHR* p = modes) if (modeCount > 0) Check(_surfaceApi.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &modeCount, p), "vkGetPhysicalDeviceSurfacePresentModesKHR");
        var presentMode = PresentModeKHR.FifoKhr;
        foreach (var m in modes) if (m == PresentModeKHR.MailboxKhr) { presentMode = m; break; }
        Extent2D extent = capabilities.CurrentExtent;
        if (extent.Width == uint.MaxValue) { extent.Width = Math.Clamp((uint)width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width); extent.Height = Math.Clamp((uint)height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height); }
        uint imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount) imageCount = capabilities.MaxImageCount;
        SwapchainCreateInfoKHR info = new() { SType = StructureType.SwapchainCreateInfoKhr, Surface = _surface, MinImageCount = imageCount, ImageFormat = chosen.Format, ImageColorSpace = chosen.ColorSpace, ImageExtent = extent, ImageArrayLayers = 1, ImageUsage = ImageUsageFlags.ColorAttachmentBit, ImageSharingMode = SharingMode.Exclusive, PreTransform = capabilities.CurrentTransform, CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr, PresentMode = presentMode, Clipped = true };
        Check(_swapchainApi!.CreateSwapchain(_device, in info, null, out _swapchain), "vkCreateSwapchainKHR");
        uint actual = 0; Check(_swapchainApi.GetSwapchainImages(_device, _swapchain, &actual, null), "vkGetSwapchainImagesKHR(count)");
        _swapchainImages = new Image[actual]; fixed (Image* p = _swapchainImages) Check(_swapchainApi.GetSwapchainImages(_device, _swapchain, &actual, p), "vkGetSwapchainImagesKHR");
        _swapchainFormat = chosen.Format; _swapchainExtent = extent;
    }

    private void CreateRenderTargets()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];
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
        }

        AttachmentDescription color = new()
        {
            Format = _swapchainFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };
        AttachmentReference colorRef = new(0, ImageLayout.ColorAttachmentOptimal);
        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1
        };
        AttachmentReference* colorPtr = stackalloc AttachmentReference[1];
        colorPtr[0] = colorRef;
        subpass.PColorAttachments = colorPtr;
        RenderPassCreateInfo rpInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1
        };
        AttachmentDescription* attachmentPtr = stackalloc AttachmentDescription[1];
        attachmentPtr[0] = color;
        rpInfo.PAttachments = attachmentPtr;
        SubpassDescription* subpassPtr = stackalloc SubpassDescription[1];
        subpassPtr[0] = subpass;
        rpInfo.PSubpasses = subpassPtr;
        Check(_vk.CreateRenderPass(_device, in rpInfo, null, out _renderPass), "vkCreateRenderPass");

        _framebuffers = new Framebuffer[_swapchainImageViews.Length];
        for (int i = 0; i < _framebuffers.Length; i++)
        {
            ImageView* attachments = stackalloc ImageView[1];
            attachments[0] = _swapchainImageViews[i];
            FramebufferCreateInfo fbInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 1,
                PAttachments = attachments,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1
            };
            Check(_vk.CreateFramebuffer(_device, in fbInfo, null, out _framebuffers[i]), "vkCreateFramebuffer");
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

        _commandBuffers = new CommandBuffer[_framebuffers.Length];
        fixed (CommandBuffer* p = _commandBuffers)
        {
            CommandBufferAllocateInfo alloc = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)_commandBuffers.Length
            };
            Check(_vk.AllocateCommandBuffers(_device, in alloc, p), "vkAllocateCommandBuffers");
        }

        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };
        Check(_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _imageAvailable), "vkCreateSemaphore");
        Check(_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _renderFinished), "vkCreateSemaphore");

        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };
        Check(_vk.CreateFence(_device, in fenceInfo, null, out _inFlightFence), "vkCreateFence");
    }

    private void RecordClearCommand(uint imageIndex, Vector4 clearColor)
    {
        CommandBuffer cmd = _commandBuffers[imageIndex];
        Check(_vk!.ResetCommandBuffer(cmd, 0), "vkResetCommandBuffer");
        CommandBufferBeginInfo begin = new() { SType = StructureType.CommandBufferBeginInfo };
        Check(_vk.BeginCommandBuffer(cmd, in begin), "vkBeginCommandBuffer");

        ClearValue clear = new();
        clear.Color = new ClearColorValue(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
        RenderPassBeginInfo rp = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent),
            ClearValueCount = 1
        };
        ClearValue* clearPtr = stackalloc ClearValue[1];
        clearPtr[0] = clear;
        rp.PClearValues = clearPtr;
        _vk.CmdBeginRenderPass(cmd, in rp, SubpassContents.Inline);
        _vk.CmdEndRenderPass(cmd);
        Check(_vk.EndCommandBuffer(cmd), "vkEndCommandBuffer");
    }

    public void Render(RenderData data, float width, float height, Vector4 clearColor)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Silk.NET Vulkan backend has not been initialized.");

        if (data is null)
            throw new ArgumentNullException(nameof(data));

        if (_headless) return;
        Check(_vk!.WaitForFences(_device, 1, in _inFlightFence, true, ulong.MaxValue), "vkWaitForFences");
        Check(_vk.ResetFences(_device, 1, in _inFlightFence), "vkResetFences");

        uint imageIndex = 0;
        Result acquire = _swapchainApi!.AcquireNextImage(_device, _swapchain, ulong.MaxValue, _imageAvailable, default, &imageIndex);
        if (acquire != Result.Success && acquire != Result.SuboptimalKhr)
            Check(acquire, "vkAcquireNextImageKHR");

        RecordClearCommand(imageIndex, clearColor);

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
        if (presentResult != Result.Success && presentResult != Result.SuboptimalKhr)
            Check(presentResult, "vkQueuePresentKHR");
    }

    private void Check(Result result, string operation)
    {
        if (result != Result.Success)
            throw new InvalidOperationException($"Vulkan {operation} failed: {result}.");
    }

    private void DisposeVulkanObjects()
    {
        if (_vk is null)
            return;

        if (_device.Handle != 0)
        {
            _vk.DeviceWaitIdle(_device);
            if (_inFlightFence.Handle != 0) _vk.DestroyFence(_device, _inFlightFence, null);
            if (_renderFinished.Handle != 0) _vk.DestroySemaphore(_device, _renderFinished, null);
            if (_imageAvailable.Handle != 0) _vk.DestroySemaphore(_device, _imageAvailable, null);
            if (_commandPool.Handle != 0) _vk.DestroyCommandPool(_device, _commandPool, null);
            foreach (var framebuffer in _framebuffers) if (framebuffer.Handle != 0) _vk.DestroyFramebuffer(_device, framebuffer, null);
            if (_renderPass.Handle != 0) _vk.DestroyRenderPass(_device, _renderPass, null);
            foreach (var view in _swapchainImageViews) if (view.Handle != 0) _vk.DestroyImageView(_device, view, null);
            if (_swapchain.Handle != 0 && _swapchainApi is not null) _swapchainApi.DestroySwapchain(_device, _swapchain, null);
            _swapchain = default;
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
        _physicalDevice = default;
        _graphicsQueue = default;
        _graphicsQueueFamily = 0;
        _swapchainImages = Array.Empty<Image>();
        _swapchainImageViews = Array.Empty<ImageView>();
        _framebuffers = Array.Empty<Framebuffer>();
        _commandBuffers = Array.Empty<CommandBuffer>();
        _commandPool = default;
        _renderPass = default;
        _imageAvailable = default;
        _renderFinished = default;
        _inFlightFence = default;
        _sdl = null;
        _surfaceApi = null;
        _swapchainApi = null;
        IsInitialized = false;
    }
}
