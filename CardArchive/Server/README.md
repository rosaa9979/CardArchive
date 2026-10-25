# Unity Game Server configuration

The Unity game server reads its API credentials from OS environment variables
(`ServerManager.LoadApiCredentials` → `API_USERNAME`, `API_PASSWORD`).
Set these variables before launching the dedicated-server build with
`-batchmode -nographics`. `.env.example` documents the required values;
the server does not load a `.env` file automatically.

Server addresses are kept in `NetworkData.asset` so clients can connect.

The Node.js API server (`Build/Api/TcgEngineAPI` at the repository root) loads its own `.env` through `dotenv`.
