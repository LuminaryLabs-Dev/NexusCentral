import { mkdirSync } from "node:fs";
import { dirname } from "node:path";
import { DatabaseSync } from "node:sqlite";

export class SqliteStateStore {
  constructor(filePath) {
    mkdirSync(dirname(filePath), { recursive: true });
    this.database = new DatabaseSync(filePath);
    this.database.exec(`
      PRAGMA journal_mode=WAL;
      PRAGMA foreign_keys=ON;
      CREATE TABLE IF NOT EXISTS snapshots (
        domain_path TEXT PRIMARY KEY,
        revision INTEGER NOT NULL,
        body_json TEXT NOT NULL,
        updated_at INTEGER NOT NULL
      );
      CREATE TABLE IF NOT EXISTS events (
        sequence INTEGER PRIMARY KEY AUTOINCREMENT,
        event_id TEXT UNIQUE NOT NULL,
        type TEXT NOT NULL,
        body_json TEXT NOT NULL,
        created_at INTEGER NOT NULL
      );
      CREATE TABLE IF NOT EXISTS host_values (
        key TEXT PRIMARY KEY,
        value_json TEXT NOT NULL
      );
    `);
    this.putSnapshot = this.database.prepare(`
      INSERT INTO snapshots(domain_path, revision, body_json, updated_at)
      VALUES (?, ?, ?, ?)
      ON CONFLICT(domain_path) DO UPDATE SET revision=excluded.revision, body_json=excluded.body_json, updated_at=excluded.updated_at
    `);
    this.getSnapshots = this.database.prepare("SELECT domain_path, body_json FROM snapshots ORDER BY domain_path");
    this.appendEventStatement = this.database.prepare("INSERT OR IGNORE INTO events(event_id, type, body_json, created_at) VALUES (?, ?, ?, ?)");
    this.putHostValue = this.database.prepare("INSERT INTO host_values(key, value_json) VALUES (?, ?) ON CONFLICT(key) DO UPDATE SET value_json=excluded.value_json");
    this.getHostValueStatement = this.database.prepare("SELECT value_json FROM host_values WHERE key = ?");
  }

  save(domainPath, snapshot) {
    this.putSnapshot.run(domainPath, Number(snapshot.revision || 0), JSON.stringify(snapshot), Date.now());
  }

  saveAll(entries) {
    this.database.exec("BEGIN IMMEDIATE");
    try {
      for (const [domainPath, snapshot] of entries) this.save(domainPath, snapshot);
      this.database.exec("COMMIT");
    } catch (error) {
      this.database.exec("ROLLBACK");
      throw error;
    }
  }

  loadAll() {
    return Object.fromEntries(this.getSnapshots.all().map((row) => [row.domain_path, JSON.parse(row.body_json)]));
  }

  appendEvent(event) {
    this.appendEventStatement.run(event.id, event.type, JSON.stringify(event), Number(event.at || Date.now()));
  }

  setHostValue(key, value) { this.putHostValue.run(key, JSON.stringify(value)); }
  getHostValue(key, fallback = null) { const row = this.getHostValueStatement.get(key); return row ? JSON.parse(row.value_json) : fallback; }

  close() { this.database.close(); }
}
