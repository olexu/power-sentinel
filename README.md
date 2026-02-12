# Power Sentinel
Power Sentinel is a tool designed to monitor heartbeat signals from various devices. It provides real-time monitoring and alerting through a Telegram bot, ensuring that users are promptly notified of any issues with their devices.

## Features
- Real-time monitoring of heartbeat signals from devices.
- Alerting through a Telegram bot for immediate notifications.
- Easy setup and configuration.
- Support for multiple devices and customizable alert thresholds.

## Installation
The easiest way to install Power Sentinel is through docker-compose. Make sure you have Docker and Docker Compose installed on your system, then follow these steps:

1. Copy the `docker-compose.yml` file to your desired location.
2. Open the `docker-compose.yml` file and fill in the required environment variables:
   - `PublicUrl`: The public URL where Power Sentinel will be accessible.
   - `TelegramBotToken`: The token for your Telegram bot, which will be used to send alerts.
   - `MonitorIntervalSeconds`: The interval (in seconds) at which Power Sentinel will check for heartbeat signals.
   - 'Admin__Username': The username of the admin user for the Power Sentinel web interface.
   - 'Admin__Password': The password of the admin user for the Power Sentinel web interface
3. Open a terminal and navigate to the directory containing the `docker-compose.yml` file.
4. Run the following command to start the Power Sentinel service:
```bash
docker-compose up -d
```
5. The service will start in the background. You can check the logs to ensure everything is running correctly:
```bash
docker-compose logs -f
```

## Usage
Once Power Sentinel is up and running, you can access the web interface using the public URL you provided in the environment variables. Log in http://<PublicUrl>/admin with the admin username and password you set up, and you can start adding devices to monitor. The Telegram bot will send alerts based on the heartbeat signals received from the devices, allowing you to stay informed about their status in real-time.
