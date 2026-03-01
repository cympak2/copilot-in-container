# HTTP API Reference

`cic http-server start` launches an embedded HTTP API server that exposes server management and AI prompt execution over plain HTTP/SSE — no CLI invocation required.

## Starting the server

```bash
# Plain — listens on http://0.0.0.0:5000
cic http-server start

# Custom port
cic http-server start --port 8080

# With optional API key auth
cic http-server start --port 8080 --api-key mysecret
```

When `--api-key` is set every request **except** `GET /health` must include:
```
Authorization: Bearer mysecret
```

---

## Endpoints

### `GET /health`
Liveness check. Always returns `200` regardless of API key settings.

```json
{ "status": "ok", "version": "1.0.0" }
```

---

### `GET /api/servers`
List all server instances whose state files exist under `~/.copilot-in-container/servers/`.
Includes live running/stopped status for each instance.

**Response** `200`
```json
{
  "instances": [
    {
      "instanceName": "myproject",
      "containerId": "abc123...",
      "containerName": "copilot-server-myproject",
      "port": 3000,
      "model": "gpt-4o",          // null if not set
      "logLevel": "info",
      "startedAt": "2026-03-01T10:00:00Z",
      "workspaceFolder": "/Users/me/projects/myapp",
      "status": "running",         // "running" | "stopped"
      "uptime": "2h 15m"           // null when stopped
    }
  ]
}
```

---

### `GET /api/servers/{name}/status`
Detailed status for a single named server instance.

**Path param** — `name`: instance name (e.g. `myproject`)

**Response** `200`
```json
{
  "instanceName": "myproject",
  "status": "running",
  "containerId": "abc123...",
  "containerName": "copilot-server-myproject",
  "port": 3000,
  "model": null,
  "logLevel": "info",
  "startedAt": "2026-03-01T10:00:00Z",
  "workspaceFolder": "/Users/me/projects/myapp",
  "uptime": "2h 15m"
}
```

**Response** `404` — instance state file not found.

---

### `GET /api/servers/{name}/logs?tail=N`
Fetch container logs for the named server.

**Query params**
| Param | Required | Description |
|---|---|---|
| `tail` | No | Return only the last N lines |

**Response** `200`
```json
{ "logs": "... raw container log output ..." }
```

**Response** `404` — instance not found.

---

### `POST /api/servers/{name}/start`
Starts the container for an existing server instance.
The state file must already exist (created locally via `cic server start`).
This will **not** create a new container — it only re-starts a stopped one.

**Response** `200`
```json
{ "message": "Server 'myproject' started", "containerId": "abc123..." }
```

**Response** `404` — no state file for this instance name.  
**Response** `409` — container is already running.

---

### `POST /api/servers/{name}/stop`
Stops the running container and removes its state file.

**Response** `200`
```json
{ "message": "Server 'myproject' stopped" }
```

**Response** `404` — instance not found.

---

### `POST /api/servers/{name}/restart`
Stops (if running) then starts the container again.

**Response** `200`
```json
{ "message": "Server 'myproject' restarted" }
```

**Response** `404` — instance not found.

---

### `POST /api/servers/{name}/execute` ← SSE streaming
Send a prompt to a running Copilot server instance and stream the response back.

**Request body**
```json
{ "prompt": "Explain this function" }
```

**Response** — `text/event-stream` (Server-Sent Events)

Each `data:` frame is a JSON object in one of two shapes:

```
# Output line (stdout or stderr)
data: {"line":"Thinking...","type":"stdout"}

# Command finished
data: {"eventType":"done","exitCode":0}
```

**Response** `404` — instance not found.  
**Response** `409` — instance is not running.

#### Browser / fetch example
```javascript
const res = await fetch('/api/servers/myproject/execute', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ prompt: 'Refactor this file to use async/await' }),
});

const reader = res.body.getReader();
const decoder = new TextDecoder();

while (true) {
  const { done, value } = await reader.read();
  if (done) break;

  const chunk = decoder.decode(value);
  for (const line of chunk.split('\n')) {
    if (!line.startsWith('data: ')) continue;
    const event = JSON.parse(line.slice(6));

    if (event.eventType === 'done') {
      console.log('Exit code:', event.exitCode);
      break;
    }
    process.stdout.write(event.line + '\n');
  }
}
```

---

## Web frontend integration

Set these environment variables before starting the Next.js dev server:

| Variable | Default | Description |
|---|---|---|
| `CIC_HTTP_URL` | `http://localhost:5000` | Base URL of the running `cic http-server` |
| `CIC_API_KEY` | _(none)_ | Bearer token — must match `--api-key` passed at startup |

```bash
CIC_HTTP_URL=http://localhost:8080 CIC_API_KEY=mysecret npm run dev
```

---

## Error responses

All error responses share the same shape:
```json
{ "error": "Human-readable description of what went wrong" }
```

| HTTP status | Meaning |
|---|---|
| `400` | Bad request — missing or invalid fields |
| `401` | Unauthorized — API key required but missing/wrong |
| `404` | Instance state file not found |
| `409` | Conflict — e.g. container already running |
| `500` | Internal error — check cic server logs |
| `503` | Cannot reach cic HTTP server (web proxy only) |
