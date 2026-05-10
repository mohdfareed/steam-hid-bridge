#pragma once

#include <initguid.h>

#if defined(_KERNEL_MODE) || defined(_NTDDK_)
#include <wdm.h>
#else
#include <winioctl.h>
#endif

// {51B576E7-9E82-4E04-9F16-74FC961EF0A5}
DEFINE_GUID(
    GUID_DEVINTERFACE_STEAM_HID_BRIDGE_MOUSE,
    0x51b576e7, 0x9e82, 0x4e04, 0x9f, 0x16, 0x74, 0xfc, 0x96, 0x1e, 0xf0, 0xa5);

#define STEAM_HID_BRIDGE_MOUSE_REPORT_ID 0x01

#define IOCTL_STEAM_HID_BRIDGE_SUBMIT_MOUSE_REPORT \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_WRITE_DATA)

#pragma pack(push, 1)
typedef struct _STEAM_HID_BRIDGE_MOUSE_REPORT
{
    UCHAR ReportId;
    UCHAR Buttons;
    SHORT X;
    SHORT Y;
    CHAR Wheel;
} STEAM_HID_BRIDGE_MOUSE_REPORT, *PSTEAM_HID_BRIDGE_MOUSE_REPORT;
#pragma pack(pop)
