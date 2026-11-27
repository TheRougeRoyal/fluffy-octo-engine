# Running Fluffy Octo Engine on Windows

Yes. The recommended Windows setup uses Docker Desktop with Linux containers. This runs PostgreSQL, the .NET backend, the OCaml pricing engine, and the React frontend together, without requiring an OCaml toolchain installed on Windows.

## Requirements

- Windows 10/11 with Docker Desktop installed and running in Linux-container mode.
- Git, to clone the repository.
- A Firebase project with Email/Password sign-in and Cloud Firestore enabled.

## Configure Firebase

From the repository root, copy the frontend environment template to the root `.env` file that Docker Compose reads:

```powershell
Copy-Item frontend/.env.example .env
notepad .env
```

Replace the placeholder `VITE_FIREBASE_*` values with the Firebase web app configuration. Keep `VITE_WS_URL=ws://localhost:5000/ws` for local use.

The backend also requires a Firebase Admin service-account JSON to accept authenticated WebSocket connections. In Firebase Console, create/download a service-account JSON key. Keep it outside source control; do not commit it. In the same PowerShell window you will use to start Docker Compose, set:

```powershell
$env:TradingServer__FirebaseServiceAccountJson = Get-Content "C:\path\to\firebase-service-account.json" -Raw
$env:TradingServer__FirebaseProjectId = "your-firebase-project-id"
```

The compose file configures a local PostgreSQL database automatically. The application stores Firebase activity in Firestore; Firestore rules are in `firestore.rules`.

## Start the application

From the repository root:

```powershell
.\start-all.ps1
```

The first run builds the images and may take a few minutes. When it finishes, open:

- Frontend: <http://localhost:3000>
- Backend health: <http://localhost:5000/health>

Create an account or sign in with Firebase in the frontend, then connect to the gateway and submit orders. The backend WebSocket endpoint is `ws://localhost:5000/ws`.

Useful Docker Compose commands:

```powershell
docker compose ps
docker compose logs -f backend
docker compose down
```

`docker compose down` stops the services but preserves the PostgreSQL data volume.

## Native Windows backend (advanced)

The repository also has a `run-server.ps1` script, but it only builds/runs the .NET project. It does not build the OCaml solver. For a native backend run, install the .NET 8 SDK, PostgreSQL, and a Windows OCaml/Dune toolchain with the `yojson` dependency; build the solver and put the resulting executable at `QuantCore\bin\pricing_api.exe` (the backend adds `.exe` automatically on Windows). Set `DATABASE_URL` and `DATABASE_SSL_MODE` before running `.\run-server.ps1`.

Firebase Admin service-account JSON and project ID must also be configured for WebSocket authentication. Without the Admin credentials, the backend rejects all WebSocket connections. For a complete local stack, Docker Desktop is the simpler option.
