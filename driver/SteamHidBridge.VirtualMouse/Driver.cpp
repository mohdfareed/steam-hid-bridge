#include <ntddk.h>

#pragma warning(push)
#pragma warning(disable: 4324)
#include <wdf.h>
#pragma warning(pop)

#include <vhf.h>

#include "Public.h"
#include "HidReports.h"

typedef struct _DEVICE_CONTEXT
{
    VHFHANDLE VhfHandle;
} DEVICE_CONTEXT, *PDEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, DeviceGetContext)

extern "C" DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD SteamHidBridgeEvtDeviceAdd;
EVT_WDF_OBJECT_CONTEXT_CLEANUP SteamHidBridgeEvtDeviceCleanup;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL SteamHidBridgeEvtIoDeviceControl;

static NTSTATUS SubmitMouseReport(_In_ PDEVICE_CONTEXT DeviceContext, _In_ PSTEAM_HID_BRIDGE_MOUSE_REPORT Report)
{
    HID_XFER_PACKET packet = {};
    packet.reportBuffer = reinterpret_cast<PUCHAR>(Report);
    packet.reportBufferLen = sizeof(*Report);
    packet.reportId = Report->ReportId;

    return VhfReadReportSubmit(DeviceContext->VhfHandle, &packet);
}

extern "C"
NTSTATUS DriverEntry(_In_ PDRIVER_OBJECT DriverObject, _In_ PUNICODE_STRING RegistryPath)
{
    WDF_DRIVER_CONFIG config;
    WDF_DRIVER_CONFIG_INIT(&config, SteamHidBridgeEvtDeviceAdd);

    return WdfDriverCreate(DriverObject, RegistryPath, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
}

NTSTATUS SteamHidBridgeEvtDeviceAdd(_In_ WDFDRIVER Driver, _Inout_ PWDFDEVICE_INIT DeviceInit)
{
    UNREFERENCED_PARAMETER(Driver);
    PAGED_CODE();

    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, DEVICE_CONTEXT);
    attributes.EvtCleanupCallback = SteamHidBridgeEvtDeviceCleanup;

    WDFDEVICE device;
    NTSTATUS status = WdfDeviceCreate(&DeviceInit, &attributes, &device);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    status = WdfDeviceCreateDeviceInterface(device, &GUID_DEVINTERFACE_STEAM_HID_BRIDGE_MOUSE, nullptr);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    WDF_IO_QUEUE_CONFIG queueConfig;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchSequential);
    queueConfig.EvtIoDeviceControl = SteamHidBridgeEvtIoDeviceControl;

    status = WdfIoQueueCreate(device, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, WDF_NO_HANDLE);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    PDEVICE_CONTEXT context = DeviceGetContext(device);

    VHF_CONFIG vhfConfig;
    VHF_CONFIG_INIT(
        &vhfConfig,
        WdfDeviceWdmGetDeviceObject(device),
        sizeof(SteamHidBridgeMouseReportDescriptor),
        const_cast<PUCHAR>(SteamHidBridgeMouseReportDescriptor));

    status = VhfCreate(&vhfConfig, &context->VhfHandle);
    if (!NT_SUCCESS(status))
    {
        context->VhfHandle = nullptr;
        return status;
    }

    status = VhfStart(context->VhfHandle);
    if (!NT_SUCCESS(status))
    {
        VhfDelete(context->VhfHandle, TRUE);
        context->VhfHandle = nullptr;
        return status;
    }

    return STATUS_SUCCESS;
}

void SteamHidBridgeEvtDeviceCleanup(_In_ WDFOBJECT DeviceObject)
{
    PDEVICE_CONTEXT context = DeviceGetContext(DeviceObject);
    if (context->VhfHandle != nullptr)
    {
        VhfDelete(context->VhfHandle, TRUE);
        context->VhfHandle = nullptr;
    }
}

void SteamHidBridgeEvtIoDeviceControl(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t OutputBufferLength,
    _In_ size_t InputBufferLength,
    _In_ ULONG IoControlCode)
{
    UNREFERENCED_PARAMETER(OutputBufferLength);
    UNREFERENCED_PARAMETER(InputBufferLength);

    NTSTATUS status = STATUS_SUCCESS;
    size_t length = 0;

    if (IoControlCode != IOCTL_STEAM_HID_BRIDGE_SUBMIT_MOUSE_REPORT)
    {
        WdfRequestComplete(Request, STATUS_INVALID_DEVICE_REQUEST);
        return;
    }

    PSTEAM_HID_BRIDGE_MOUSE_REPORT report = nullptr;
    status = WdfRequestRetrieveInputBuffer(
        Request,
        sizeof(STEAM_HID_BRIDGE_MOUSE_REPORT),
        reinterpret_cast<PVOID*>(&report),
        &length);

    if (NT_SUCCESS(status))
    {
        if (length != sizeof(STEAM_HID_BRIDGE_MOUSE_REPORT) ||
            report->ReportId != STEAM_HID_BRIDGE_MOUSE_REPORT_ID)
        {
            status = STATUS_INVALID_PARAMETER;
        }
    }

    if (NT_SUCCESS(status))
    {
        PDEVICE_CONTEXT context = DeviceGetContext(WdfIoQueueGetDevice(Queue));
        status = SubmitMouseReport(context, report);
    }

    WdfRequestComplete(Request, status);
}
