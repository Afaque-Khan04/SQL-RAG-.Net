# Adhoc System C# — Full-Stack Natural Language SQL Platform (.NET 8 + React)

A full-stack, enterprise NL2SQL & Semantic Retrieval platform targeting Microsoft SQL Server 2022 (`AdventureWorks2022`) and ChromaDB vector store.

---

## Project Structure

```text
d:\Workspace\Adhoc_System_C#\
├── AdhocSystem.sln               # .NET 8 Visual Studio Solution
├── run.ps1                       # 1-Click Launch Script (PowerShell)
├── run.bat                       # 1-Click Launch Script (Batch / Double-click)
│
├── frontend\                     # [React 19 + TailwindCSS + AG-Grid Source]
│   ├── package.json
│   ├── vite.config.js            # Automatically builds directly into ../src/AdhocSystem.Api/wwwroot
│   ├── src\
│   │   ├── App.jsx               # Main React Studio Canvas
│   │   ├── components\           # DataGrid, MetricsBar, Navbar, QueryInput, SessionBreadcrumbs, etc.
│   │   └── services\api.js       # API client connecting to C# backend
│   └── public\
│
├── src\
│   └── AdhocSystem.Api\          # [.NET 8 ASP.NET Core Web API Backend]
│       ├── Controllers\          # QueryController, SessionController, HealthController
│       ├── Services\             # Database, ScriptDom AST Security, LLM Circuit Breaker, NL2SQL, Routing
│       ├── appsettings.json      # DB connection strings & LLM provider configurations
│       └── wwwroot\              # Hosted production build of the React Studio frontend
│
└── tests\
    └── AdhocSystem.Tests\        # 24 Unit & Integration Tests (100% passing)
```

---

## How to Run

### Option 1: Run Full-Stack (Backend + Embedded Frontend)
```powershell
cd d:\Workspace\Adhoc_System_C#
.\run.ps1
```
Open **[http://localhost:5000](http://localhost:5000)** in your browser.

### Option 2: Frontend Development with Hot Module Reload (Vite)
If you want to edit React code with instant live reload:
1. Start backend:
   ```powershell
   cd d:\Workspace\Adhoc_System_C#\src\AdhocSystem.Api
   dotnet run --launch-profile http
   ```
2. Start Vite dev server in another terminal:
   ```powershell
   cd d:\Workspace\Adhoc_System_C#\frontend
   npm run dev
   ```
   Open **[http://localhost:5173](http://localhost:5173)**. (Requests to `/query`, `/health`, etc. will be automatically proxied to port 5000).

### Option 3: Rebuild Frontend
When you make changes to `frontend/src`:
```powershell
cd d:\Workspace\Adhoc_System_C#\frontend
npm run build
```
The build artifacts are automatically emitted straight into `src/AdhocSystem.Api/wwwroot`!
