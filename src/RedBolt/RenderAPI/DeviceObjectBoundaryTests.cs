// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.RenderAPI;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class DeviceObjectBoundaryTests
{
    [Fact]
    public void Headless_device_object_processes_reordered_events_without_registering_a_device()
    {
        using var deviceObject = new TrackingDeviceObject(DeviceObjectFlag.Headless);

        Assert.Null(deviceObject.GetRenderDevice());
        deviceObject.SetDebugName("headless-object");
        deviceObject.ProcessDeviceObjectEvent(DeviceObjectEvent.Restore);
        deviceObject.ProcessDeviceObjectEvent(DeviceObjectEvent.Invalidate);
        deviceObject.ProcessDeviceObjectEvent(DeviceObjectEvent.Invalidate);
        deviceObject.ProcessDeviceObjectEvent(DeviceObjectEvent.Destroy);
        deviceObject.ProcessDeviceObjectEvent(DeviceObjectEvent.Destroy);

        Assert.Equal("headless-object", deviceObject.GetDebugName());
        Assert.True(deviceObject.IsDataLost());
        Assert.Equal(["restore", "invalidate", "invalidate", "destroy", "destroy"], deviceObject.Events);
        Assert.Null(deviceObject.GetRenderDevice());

        deviceObject.ClearDataLost();
        Assert.False(deviceObject.IsDataLost());
    }

    [Fact]
    public void Headless_device_object_dispose_is_safe_after_destroy_and_repeated_dispose()
    {
        var deviceObject = new TrackingDeviceObject(DeviceObjectFlag.Headless);

        deviceObject.ProcessDeviceObjectEvent(DeviceObjectEvent.Destroy);
        deviceObject.Dispose();
        deviceObject.Dispose();

        Assert.Equal(["destroy"], deviceObject.Events);
        Assert.Null(deviceObject.GetRenderDevice());
    }

    private sealed class TrackingDeviceObject(DeviceObjectFlag flags) : DeviceObject(flags)
    {
        public List<string> Events { get; } = [];

        public override void Restore()
        {
            Events.Add("restore");
        }

        public override void Invalidate()
        {
            Events.Add("invalidate");
        }

        public override void Destroy()
        {
            Events.Add("destroy");
        }
    }
}
