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

    public void Render(RenderData data, float width, float height, Vector4 clearColor)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Silk.NET Vulkan backend has not been initialized.");

        if (data is null)
            throw new ArgumentNullException(nameof(data));

        if (_headless) return;
        throw new PlatformNotSupportedException("Silk.NET Vulkan swapchain is ready; command buffers/render pass are the next stage.");
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
        {
            _vk.DeviceWaitIdle(_device);
            _vk.DestroyDevice(_device, null);
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
        _sdl = null;
        _surfaceApi = null;
        _swapchainApi = null;
        IsInitialized = false;
    }
}
