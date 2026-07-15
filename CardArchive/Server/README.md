# Unity Game Server launcher

The Unity game server reads its API credentials from **OS environment variables**
(`ServerManager.LoadApiCredentials` → `API_USERNAME`, `API_PASSWORD`), so they are never
baked into the build/scene. These wrapper scripts load those values from a `.env` file and
launch the server headless.

> Server addresses are **not** secret and are kept in `NetworkData.asset` (committed to git) —
> clients need them baked in to connect anyway. Only the API credentials go in `.env`.

## Setup

1. Copy the template and fill in the real account:
   ```bash
   cp .env.example .env      # PowerShell: Copy-Item .env.example .env
   ```
   ```
   API_USERNAME=...
   API_PASSWORD=...
   ```
   `.env` is git-ignored (root `.gitignore`); only `.env.example` is tracked.

2. Place the Unity dedicated-server build in this folder (or point `SERVER_BIN` at it).

## Run

```bash
# Linux
./run-server.sh

# Windows
.\run-server.ps1
```

Both scripts launch the server with `-batchmode -nographics`. Any extra arguments are passed
through, e.g. `./run-server.sh -logFile server.log`.

### Overrides

| Variable     | Default                         | Purpose                          |
|--------------|---------------------------------|----------------------------------|
| `ENV_FILE`   | `<script dir>/.env`             | Path to the env file to load     |
| `SERVER_BIN` | auto-detected in `<script dir>` | Path to the server executable    |

> Note: this is only for the **Unity game server**. The Node.js API server
> (`Api/TcgEngineAPI`) has its own `.env` loaded automatically by `dotenv` — no wrapper needed.
