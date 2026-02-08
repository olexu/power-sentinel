-- 001-create-tables.sql
-- Initial schema for SQLite: devices, events, subscribers
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS devices (
  id TEXT PRIMARY KEY NOT NULL,
  description TEXT,
  heartbeat_key TEXT,
  heartbeat_at DATETIME
);

CREATE TABLE IF NOT EXISTS events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  device_id TEXT NOT NULL,
  is_power_on INTEGER NOT NULL DEFAULT 0,
  start_at DATETIME NOT NULL,
  end_at DATETIME,
  FOREIGN KEY(device_id) REFERENCES devices(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS subscribers (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  chat_id INTEGER NOT NULL,
  is_active INTEGER NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  device_id TEXT,
  FOREIGN KEY(device_id) REFERENCES devices(id) ON DELETE SET NULL
);
