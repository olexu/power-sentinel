Heartbeat sender service
========================

Files in this folder:

- `heartbeat.sh` — the script that POSTs a JSON payload every XX seconds.
- `heartbeat.service` — systemd unit file (template) that runs the script at boot.

Quick install on your Raspberry Pi (run as root or prefix with `sudo`):

```bash
# create destination
mkdir -p /opt/heartbeat

# copy files from this repo to the Pi into /opt/heartbeat (example using scp):
scp heartbeat/heartbeat.sh pi@raspberry:/opt/heartbeat/
scp heartbeat/heartbeat.service pi@raspberry:/opt/heartbeat.service

# make script executable
chmod +x /opt/heartbeat/heartbeat.sh

# move unit into place, reload, enable and start
mv /tmp/heartbeat.service /etc/systemd/system/heartbeat.service

systemctl daemon-reload
systemctl enable --now heartbeat.service