#pragma once

// Mouse report:
// byte 0: report id
// byte 1: five buttons + padding
// byte 2-3: relative X
// byte 4-5: relative Y
// byte 6: relative wheel
static const UCHAR SteamHidBridgeMouseReportDescriptor[] =
{
    0x05, 0x01,                    // Usage Page (Generic Desktop)
    0x09, 0x02,                    // Usage (Mouse)
    0xA1, 0x01,                    // Collection (Application)
    0x85, STEAM_HID_BRIDGE_MOUSE_REPORT_ID,
    0x09, 0x01,                    //   Usage (Pointer)
    0xA1, 0x00,                    //   Collection (Physical)
    0x05, 0x09,                    //     Usage Page (Button)
    0x19, 0x01,                    //     Usage Minimum (Button 1)
    0x29, 0x05,                    //     Usage Maximum (Button 5)
    0x15, 0x00,                    //     Logical Minimum (0)
    0x25, 0x01,                    //     Logical Maximum (1)
    0x95, 0x05,                    //     Report Count (5)
    0x75, 0x01,                    //     Report Size (1)
    0x81, 0x02,                    //     Input (Data, Variable, Absolute)
    0x95, 0x01,                    //     Report Count (1)
    0x75, 0x03,                    //     Report Size (3)
    0x81, 0x03,                    //     Input (Constant, Variable, Absolute)
    0x05, 0x01,                    //     Usage Page (Generic Desktop)
    0x09, 0x30,                    //     Usage (X)
    0x09, 0x31,                    //     Usage (Y)
    0x16, 0x00, 0x80,              //     Logical Minimum (-32768)
    0x26, 0xFF, 0x7F,              //     Logical Maximum (32767)
    0x75, 0x10,                    //     Report Size (16)
    0x95, 0x02,                    //     Report Count (2)
    0x81, 0x06,                    //     Input (Data, Variable, Relative)
    0x09, 0x38,                    //     Usage (Wheel)
    0x15, 0x81,                    //     Logical Minimum (-127)
    0x25, 0x7F,                    //     Logical Maximum (127)
    0x75, 0x08,                    //     Report Size (8)
    0x95, 0x01,                    //     Report Count (1)
    0x81, 0x06,                    //     Input (Data, Variable, Relative)
    0xC0,                          //   End Collection
    0xC0                           // End Collection
};
