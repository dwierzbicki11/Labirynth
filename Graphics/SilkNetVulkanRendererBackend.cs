using System;
using System.Numerics;
using Silk.NET.Vulkan;
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

    public string BackendName => "Silk.NET Vulkan";
    public string DeviceName => IsInitialized ? "Silk.NET Vulkan device" : "Silk.NET Vulkan (not initialized)";
    public bool IsInitialized { get; private set; }

    public void Initialize(int width, int height, bool headless)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (headless)
        {
            // CI/fuzzing stays GPU-independent.
            IsInitialized = true;
            return;
        }

        if (IsInitialized)
            return;

        _vk = Vk.GetApi();

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

            Check(_vk.CreateInstance(in instanceInfo, null, out _instance), "vkCreateInstance");

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
                        _physicalDevice = devices[i];
                        _graphicsQueueFamily = q;
                        goto DeviceSelected;
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

    public void Render(RenderData data, float width, float height, Vector4 clearColor)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Silk.NET Vulkan backend has not been initialized.");

        if (data is null)
            throw new ArgumentNullException(nameof(data));

        throw new PlatformNotSupportedException(
            "Silk.NET Vulkan device is initialized, but swapchain/render commands are not migrated yet. Veldrid remains the active renderer.");
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
            _vk.DestroyDevice(_device, null);
            _device = default;
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
        IsInitialized = false;
    }
}
