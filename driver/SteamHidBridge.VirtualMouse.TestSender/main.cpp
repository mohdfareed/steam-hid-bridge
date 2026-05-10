#include <windows.h>
#include <setupapi.h>
#include <iostream>
#include <string>
#include <vector>

#include "Public.h"

static std::wstring FindDevicePath()
{
    HDEVINFO deviceInfo = SetupDiGetClassDevsW(
        &GUID_DEVINTERFACE_STEAM_HID_BRIDGE_MOUSE,
        nullptr,
        nullptr,
        DIGCF_DEVICEINTERFACE | DIGCF_PRESENT);

    if (deviceInfo == INVALID_HANDLE_VALUE)
    {
        return {};
    }

    SP_DEVICE_INTERFACE_DATA interfaceData = {};
    interfaceData.cbSize = sizeof(interfaceData);

    if (!SetupDiEnumDeviceInterfaces(deviceInfo, nullptr, &GUID_DEVINTERFACE_STEAM_HID_BRIDGE_MOUSE, 0, &interfaceData))
    {
        SetupDiDestroyDeviceInfoList(deviceInfo);
        return {};
    }

    DWORD requiredSize = 0;
    SetupDiGetDeviceInterfaceDetailW(deviceInfo, &interfaceData, nullptr, 0, &requiredSize, nullptr);
    if (requiredSize == 0)
    {
        SetupDiDestroyDeviceInfoList(deviceInfo);
        return {};
    }

    std::vector<BYTE> buffer(requiredSize);
    auto* detail = reinterpret_cast<SP_DEVICE_INTERFACE_DETAIL_DATA_W*>(buffer.data());
    detail->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA_W);

    if (!SetupDiGetDeviceInterfaceDetailW(deviceInfo, &interfaceData, detail, requiredSize, nullptr, nullptr))
    {
        SetupDiDestroyDeviceInfoList(deviceInfo);
        return {};
    }

    std::wstring path = detail->DevicePath;
    SetupDiDestroyDeviceInfoList(deviceInfo);
    return path;
}

static bool SendReport(HANDLE device, STEAM_HID_BRIDGE_MOUSE_REPORT report)
{
    DWORD bytesReturned = 0;
    BOOL ok = DeviceIoControl(
        device,
        IOCTL_STEAM_HID_BRIDGE_SUBMIT_MOUSE_REPORT,
        &report,
        sizeof(report),
        nullptr,
        0,
        &bytesReturned,
        nullptr);

    if (!ok)
    {
        std::wcerr << L"DeviceIoControl failed. GetLastError=" << GetLastError() << L"\n";
        return false;
    }

    return true;
}

int wmain()
{
    std::wstring path = FindDevicePath();
    if (path.empty())
    {
        std::wcerr << L"Virtual mouse device interface not found.\n";
        std::wcerr << L"Install/start the Steam HID Bridge Virtual Mouse driver first.\n";
        return 1;
    }

    std::wcout << L"Opening " << path << L"\n";
    HANDLE device = CreateFileW(
        path.c_str(),
        GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);

    if (device == INVALID_HANDLE_VALUE)
    {
        std::wcerr << L"CreateFile failed. GetLastError=" << GetLastError() << L"\n";
        return 1;
    }

    STEAM_HID_BRIDGE_MOUSE_REPORT report = {};
    report.ReportId = STEAM_HID_BRIDGE_MOUSE_REPORT_ID;

    report.X = 80;
    if (!SendReport(device, report))
    {
        CloseHandle(device);
        return 1;
    }

    Sleep(100);
    report.X = 0;
    report.Buttons = 0x01;
    if (!SendReport(device, report))
    {
        CloseHandle(device);
        return 1;
    }

    Sleep(100);
    report.Buttons = 0x00;
    if (!SendReport(device, report))
    {
        CloseHandle(device);
        return 1;
    }

    Sleep(100);
    report.Wheel = 1;
    if (!SendReport(device, report))
    {
        CloseHandle(device);
        return 1;
    }

    CloseHandle(device);
    std::wcout << L"Sent test mouse reports.\n";
    return 0;
}
