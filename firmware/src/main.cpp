#include <Arduino.h>

static const uint32_t kStatusIntervalMs = 1000;
static uint32_t lastStatusMs = 0;

void setup()
{
    Serial.begin(115200);
    pinMode(LED_BUILTIN, OUTPUT);
}

void loop()
{
    const uint32_t now = millis();
    if (now - lastStatusMs >= kStatusIntervalMs)
    {
        lastStatusMs = now;
        digitalWrite(LED_BUILTIN, !digitalRead(LED_BUILTIN));

        if (Serial)
        {
            Serial.println("steam-hid-bridge firmware placeholder: waiting for transport implementation");
        }
    }
}
