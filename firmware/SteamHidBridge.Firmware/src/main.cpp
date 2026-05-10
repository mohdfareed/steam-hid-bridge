#include <Arduino.h>

namespace
{
    constexpr uint8_t Magic0 = 'S';
    constexpr uint8_t Magic1 = 'H';
    constexpr uint8_t Magic2 = 'B';
    constexpr uint8_t ProtocolVersion = 1;
    constexpr uint8_t HidInputCommand = 0x10;
    constexpr uint8_t HeaderSize = 7;
    constexpr uint8_t ChecksumSize = 2;
    constexpr uint8_t PayloadSize = 9;
    constexpr uint8_t FrameSize = HeaderSize + PayloadSize + ChecksumSize;

    constexpr uint16_t MouseLeft = 1u << 0;
    constexpr uint16_t MouseRight = 1u << 1;
    constexpr uint16_t MouseMiddle = 1u << 2;
    constexpr uint16_t MouseBack = 1u << 3;
    constexpr uint16_t MouseForward = 1u << 4;

    uint8_t frame[FrameSize];
    uint8_t frameOffset = 0;
    uint16_t lastButtons = 0;
    uint32_t acceptedFrames = 0;
    uint32_t rejectedFrames = 0;
    uint32_t lastDiagnosticAt = 0;

    uint16_t checksum16(const uint8_t *data, uint8_t length)
    {
        uint16_t sum = 0;
        for (uint8_t i = 0; i < length; i++)
        {
            sum = static_cast<uint16_t>(sum + data[i]);
        }

        return sum;
    }

    int16_t readInt16LittleEndian(const uint8_t *data)
    {
        return static_cast<int16_t>(static_cast<uint16_t>(data[0]) | (static_cast<uint16_t>(data[1]) << 8));
    }

    uint16_t readUInt16LittleEndian(const uint8_t *data)
    {
        return static_cast<uint16_t>(data[0]) | (static_cast<uint16_t>(data[1]) << 8);
    }

    int8_t clampMouseDelta(int16_t value)
    {
        if (value > 127)
        {
            return 127;
        }

        if (value < -127)
        {
            return -127;
        }

        return static_cast<int8_t>(value);
    }

    void emitButtonState(uint16_t buttons)
    {
        if ((buttons & MouseLeft) != (lastButtons & MouseLeft))
        {
            (buttons & MouseLeft) ? Mouse.press(MOUSE_LEFT) : Mouse.release(MOUSE_LEFT);
        }

        if ((buttons & MouseRight) != (lastButtons & MouseRight))
        {
            (buttons & MouseRight) ? Mouse.press(MOUSE_RIGHT) : Mouse.release(MOUSE_RIGHT);
        }

        if ((buttons & MouseMiddle) != (lastButtons & MouseMiddle))
        {
            (buttons & MouseMiddle) ? Mouse.press(MOUSE_MIDDLE) : Mouse.release(MOUSE_MIDDLE);
        }

#if defined(MOUSE_BACK) && defined(MOUSE_FORWARD)
        if ((buttons & MouseBack) != (lastButtons & MouseBack))
        {
            (buttons & MouseBack) ? Mouse.press(MOUSE_BACK) : Mouse.release(MOUSE_BACK);
        }

        if ((buttons & MouseForward) != (lastButtons & MouseForward))
        {
            (buttons & MouseForward) ? Mouse.press(MOUSE_FORWARD) : Mouse.release(MOUSE_FORWARD);
        }
#endif

        lastButtons = buttons;
    }

    void emitMove(int16_t deltaX, int16_t deltaY, int8_t wheel)
    {
        while (deltaX != 0 || deltaY != 0 || wheel != 0)
        {
            const int8_t stepX = clampMouseDelta(deltaX);
            const int8_t stepY = clampMouseDelta(deltaY);
            const int8_t stepWheel = clampMouseDelta(wheel);
            Mouse.move(stepX, stepY, stepWheel);
            deltaX = static_cast<int16_t>(deltaX - stepX);
            deltaY = static_cast<int16_t>(deltaY - stepY);
            wheel = static_cast<int8_t>(wheel - stepWheel);
        }
    }

    bool validateFrame()
    {
        if (frame[0] != Magic0 || frame[1] != Magic1 || frame[2] != Magic2)
        {
            return false;
        }

        if (frame[3] != ProtocolVersion || frame[4] != HidInputCommand || frame[6] != PayloadSize)
        {
            return false;
        }

        const uint16_t expected = checksum16(frame, HeaderSize + PayloadSize);
        const uint16_t actual = readUInt16LittleEndian(frame + HeaderSize + PayloadSize);
        return expected == actual;
    }

    void handleFrame()
    {
        if (!validateFrame())
        {
            rejectedFrames++;
            return;
        }

        const uint8_t *payload = frame + HeaderSize;
        const int16_t deltaX = readInt16LittleEndian(payload);
        const int16_t deltaY = readInt16LittleEndian(payload + 2);
        const int8_t wheel = static_cast<int8_t>(payload[4]);
        const uint16_t buttons = readUInt16LittleEndian(payload + 5);

        emitButtonState(buttons);
        emitMove(deltaX, deltaY, wheel);
        acceptedFrames++;
    }

    void resetFrame(uint8_t firstByte)
    {
        frameOffset = 0;
        if (firstByte == Magic0)
        {
            frame[frameOffset++] = firstByte;
        }
    }

    void readSerialFrames()
    {
        while (Serial.available() > 0)
        {
            const uint8_t value = static_cast<uint8_t>(Serial.read());

            if (frameOffset == 0 && value != Magic0)
            {
                continue;
            }

            frame[frameOffset++] = value;

            if (frameOffset == 2 && frame[1] != Magic1)
            {
                resetFrame(value);
            }
            else if (frameOffset == 3 && frame[2] != Magic2)
            {
                resetFrame(value);
            }
            else if (frameOffset == FrameSize)
            {
                handleFrame();
                frameOffset = 0;
            }
        }
    }

    void writeDiagnostics()
    {
        const uint32_t now = millis();
        if (now - lastDiagnosticAt < 1000)
        {
            return;
        }

        lastDiagnosticAt = now;
        Serial.print("ok frames=");
        Serial.print(acceptedFrames);
        Serial.print(" rejected=");
        Serial.println(rejectedFrames);
    }
}

void setup()
{
    Serial.begin(115200);
}

void loop()
{
    readSerialFrames();
    writeDiagnostics();
}
