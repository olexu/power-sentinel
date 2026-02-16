#include <FastLED.h>
#include <WiFi.h>
#include <WebServer.h>
#include <HTTPClient.h>
#include <Preferences.h>

// RGB LED configuration
#define LED_PIN     8
#define NUM_LEDS    1
#define LED_TYPE    WS2812
#define COLOR_ORDER RGB

// BOOT button pin (for reset)
#define BOOT_BUTTON 9

CRGB leds[NUM_LEDS];
Preferences preferences;
WebServer server(80);

// Configuration variables
String wifi_ssid = "";
String wifi_password = "";
String heartbeat_url = "";
String heartbeat_key = "";
unsigned long heartbeat_interval = 15000; // 15 seconds (default)

// State variables
bool configMode = false;
bool forceConfigMode = false;
bool lastRequestFailed = false;
const unsigned long RETRY_INTERVAL = 3000; // 3 seconds for failed requests
unsigned long lastRequestTime = millis() - heartbeat_interval;

// WiFi reconnect policy
unsigned int wifiReconnectAttempts = 0;
const unsigned int MAX_WIFI_RECONNECT_ATTEMPTS = 6; // total attempts before reboot
const unsigned long WIFI_RECONNECT_BASE_DELAY_MS = 2000; // base delay in ms (multiplied by attempt)

// LED status indicators
void setLED(CRGB color) {
  leds[0] = color;
  FastLED.show();
}

void loadConfig() {
  preferences.begin("config", true); // read-only
  wifi_ssid = preferences.getString("ssid", "");
  wifi_password = preferences.getString("password", "");
  heartbeat_url = preferences.getString("url", "");
  heartbeat_key = preferences.getString("key", "");
  heartbeat_interval = preferences.getULong("interval", 15000);
  preferences.end();
  
  Serial.println("\n=== Loaded Configuration ===");
  Serial.println(String("SSID: ") + (wifi_ssid.length() > 0 ? wifi_ssid : "(empty)"));
  Serial.println(String("Password: ") + (wifi_password.length() > 0 ? "(set)" : "(empty)"));
  Serial.println(String("URL: ") + (heartbeat_url.length() > 0 ? heartbeat_url : "(empty)"));
  Serial.println(String("Header: ") + (heartbeat_key.length() > 0 ? "(set)" : "(empty)"));
  Serial.println(String("Interval: ") + String(heartbeat_interval) + "ms");
  Serial.println("============================\n");
}
void saveConfig() {
  preferences.begin("config", false); // read-write
  preferences.putString("ssid", wifi_ssid);
  preferences.putString("password", wifi_password);
  preferences.putString("url", heartbeat_url);
  preferences.putString("key", heartbeat_key);
  preferences.putULong("interval", heartbeat_interval);
  preferences.end();
  Serial.println("Configuration saved!");
}

// Check if configuration is complete
bool isConfigComplete() {
  return (wifi_ssid.length() > 0 && 
          wifi_password.length() > 0 && 
          heartbeat_url.length() > 0 && 
          heartbeat_key.length() > 0);
}

// HTML for configuration page
String getConfigPage() {
  return R"rawliteral(
<!doctype html>
<html>
<head>
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>PS-Heartbeat</title>
  <style>
    :root{--bg:#f7fafc;--card:#fff;--accent:#2563eb;--muted:#6b7280}
    body{font-family:system-ui,-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;background:var(--bg);margin:0;padding:24px;display:flex;align-items:center;justify-content:center;height:100vh}
    .card{width:100%;max-width:420px;background:var(--card);padding:20px;border-radius:12px;box-shadow:0 6px 18px rgba(15,23,42,0.06)}
    h1{margin:0 0 12px;font-size:20px;color:#0f172a}
    label{font-size:12px;color:var(--muted);display:block;margin-top:12px}
    input,button{width:100%;padding:10px;margin-top:6px;border-radius:8px;border:1px solid #e6e9ef;font-size:14px;box-sizing:border-box}
    button{background:var(--accent);color:#fff;border:0;cursor:pointer;font-weight:600;margin-top:16px}
    .small{font-size:12px;color:var(--muted);margin-top:8px;text-align:center}
  </style>
</head>
<body>
<div class="card">
  <h1>PS-Heartbeat</h1>
  <form action="/save" method="POST">
    <label>Wi-Fi SSID</label>
    <input type="text" name="ssid" value=")rawliteral" + wifi_ssid + R"rawliteral(" autocomplete="off">
    <label>Password</label>
    <input type="password" name="password" value=")rawliteral" + wifi_password + R"rawliteral(" autocomplete="off">
    <label>Heartbeat URL</label>
    <input type="url" name="url" value=")rawliteral" + heartbeat_url + R"rawliteral(" autocomplete="off">
    <label>Heartbeat Key</label>
    <input type="text" name="key" value=")rawliteral" + heartbeat_key + R"rawliteral(" autocomplete="off">
    <label>Request Interval (ms)</label>
    <input type="number" name="interval" value=")rawliteral" + String(heartbeat_interval) + R"rawliteral(" min="1000" autocomplete="off">
    <button type="submit">Save</button>
  </form>
</div>
</body>
</html>
)rawliteral";
}
void handleRoot() {
  server.send(200, "text/html", getConfigPage());
}
void handleSave() {
  if (server.hasArg("ssid")) wifi_ssid = server.arg("ssid");
  if (server.hasArg("password")) wifi_password = server.arg("password");
  if (server.hasArg("url")) heartbeat_url = server.arg("url");
  if (server.hasArg("key")) heartbeat_key = server.arg("key");
  if (server.hasArg("interval")) heartbeat_interval = server.arg("interval").toInt();
  saveConfig();
  
  String response = R"rawliteral(
<!DOCTYPE html>
<html>
<head>
  <title>Saved</title>
  <meta http-equiv="refresh" content="3;url=/" />
  <style>
    body { font-family: Arial; text-align: center; margin-top: 50px; }
    .success { color: #4CAF50; }
  </style>
</head>
<body>
  <h1 class="success">Configuration Saved!</h1>
  <p>Device will restart in 3 seconds...</p>
</body>
</html>
)rawliteral";
  
  server.send(200, "text/html", response);
  delay(3000);
  ESP.restart();
}

// Start Access Point mode
void startConfigMode() {
  configMode = true;
  Serial.println("\n=== Starting Configuration Mode ===");
  
  WiFi.mode(WIFI_AP);
  WiFi.softAP("ps-heartbeat");
  
  IPAddress IP = WiFi.softAPIP();
  Serial.print("AP IP address: ");
  Serial.println(IP);
  Serial.println("Connect to 'ps-heartbeat' and go to http://192.168.4.1");
  
  // Register HTTP routes and start server for configuration
  server.on("/", handleRoot);
  server.on("/save", HTTP_POST, handleSave);
  server.begin();
  Serial.println("Configuration web server started");
  
  setLED(CRGB::Purple);
}

bool waitForConnectionWithBlinkMs(unsigned long timeoutMs) {
  unsigned long start = millis();
  bool ledOn = false;
  unsigned long lastDot = 0;
  while (WiFi.status() != WL_CONNECTED && (millis() - start) < timeoutMs) {
    ledOn = !ledOn;
    setLED(ledOn ? CRGB::Yellow : CRGB::Black);
    if (millis() - lastDot >= 1000) {
      Serial.print('.');
      lastDot = millis();
    }
    unsigned long stepEnd = millis() + 500;
    while (millis() < stepEnd) {
      delay(10);
      if (WiFi.status() == WL_CONNECTED) break;
    }
  }
  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("\nConnected!");
    Serial.print("IP: ");
    Serial.println(WiFi.localIP());
    setLED(CRGB::Green);
    wifiReconnectAttempts = 0;
    return true;
  }
  return false;
}
bool connectWiFi() {
  Serial.println("\n=== Connecting to WiFi ===");
  Serial.print("SSID: ");
  Serial.println(wifi_ssid);

  WiFi.mode(WIFI_STA);
  // Initial connection attempt
  WiFi.begin(wifi_ssid.c_str(), wifi_password.c_str());

  if (waitForConnectionWithBlinkMs(10000)) return true;

  // Initial attempt failed -> try additional reconnect attempts with backoff
  Serial.println("\nInitial connect failed, starting reconnect attempts...");
  for (unsigned int attempt = 1; attempt <= MAX_WIFI_RECONNECT_ATTEMPTS; ++attempt) {
    wifiReconnectAttempts = attempt;
    Serial.printf("Reconnect attempt %u/%u\n", attempt, MAX_WIFI_RECONNECT_ATTEMPTS);

    WiFi.disconnect(true);
    WiFi.begin(wifi_ssid.c_str(), wifi_password.c_str());

    // Wait up to 10s while blinking; if connected, return true
    if (waitForConnectionWithBlinkMs(10000)) return true;

    unsigned long backoff = WIFI_RECONNECT_BASE_DELAY_MS * attempt;
    if (backoff > 30000) backoff = 30000;
    Serial.printf("Attempt %u failed, waiting %lums before next try\n", attempt, backoff);
    delay(backoff);
  }

  Serial.println("WiFi reconnect failed after multiple attempts - rebooting...");
  ESP.restart();
  return false;
}

// Send HTTP request
bool sendHTTPRequest() {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi not connected!");
    setLED(CRGB::Red);
    return false;
  }
  
  HTTPClient http;
  http.begin(heartbeat_url);
  http.addHeader("Heartbeat-Key", heartbeat_key);
  
  // Flash white briefly during request
  setLED(CRGB::White);
  
  int httpCode = http.POST("");
  
  // Concise output: just show the result code
  bool success = false;
  if (httpCode >= 200 && httpCode < 300) {
    Serial.printf("Heartbeat OK [%d]\n", httpCode);
    setLED(CRGB::Green);
    success = true;
  } else if (httpCode > 0) {
    Serial.printf("Heartbeat failed [%d] - will retry in %lums\n", httpCode, RETRY_INTERVAL);
    setLED(CRGB::Red);
  } else {
    Serial.printf("Request error: %s - will retry in %lums\n", http.errorToString(httpCode).c_str(), RETRY_INTERVAL);
    setLED(CRGB::Red);
  }
  
  http.end();
  return success;
}

void setup() {
  Serial.begin(115200);
  
  // Initialize LED
  FastLED.addLeds<LED_TYPE, LED_PIN, COLOR_ORDER>(leds, NUM_LEDS);
  FastLED.setBrightness(30);
  setLED(CRGB::Cyan);
  
  // Configure BOOT button
  pinMode(BOOT_BUTTON, INPUT_PULLUP);
  
  Serial.println("PS-Heartbeat Device Starting...");
  delay(300);
  
  // Check if BOOT button is held (to enter config mode)
  if (digitalRead(BOOT_BUTTON) == LOW) {
    Serial.println("BOOT button detected - hold for 2s to enter config mode...");
    unsigned long start = millis();
    bool ledState = false;
    while (millis() - start < 2000) {
      // Blink purple to show waiting
      setLED(ledState ? CRGB::Purple : CRGB::Black);
      ledState = !ledState;
      delay(200);
    }

    if (digitalRead(BOOT_BUTTON) == LOW) {
      // Success - entering config mode
      forceConfigMode = true;
      setLED(CRGB::Green);
      delay(150);
      setLED(CRGB::Black);
      Serial.println("Config mode enabled");
    } else {
      Serial.println("Button released - normal boot");
    }
  }
  
  loadConfig();
 
  if (forceConfigMode || !isConfigComplete()) {
    startConfigMode();
  } else {
    if (connectWiFi()) {
      Serial.println("Ready to send HTTP requests");
    }
  }
}

void loop() {
  if (configMode) {
    // Handle web server in config mode
    server.handleClient();
  } else {
    // If WiFi lost, try to restore connection (with backoff and reboot if needed)
    if (WiFi.status() != WL_CONNECTED) {
      // Log only when we are starting reconnect attempts to avoid repeating every loop
      if (wifiReconnectAttempts == 0) Serial.println("WiFi lost - attempting to restore connection");
      connectWiFi();
    }
    // Send HTTP request: use retry interval if last request failed, otherwise use normal heartbeat_interval
    unsigned long currentInterval = lastRequestFailed ? RETRY_INTERVAL : heartbeat_interval;
    if (millis() - lastRequestTime >= currentInterval) {
      lastRequestFailed = !sendHTTPRequest();
      lastRequestTime = millis();
    }
  }  
  delay(10);
}
