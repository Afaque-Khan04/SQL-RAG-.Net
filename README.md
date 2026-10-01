# Adhoc System C# — Full-Stack Natural Language SQL Platform (.NET 8 + React)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19.0-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![SQL Server 2022](https://img.shields.io/badge/SQL%20Server-2022-CC292B?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server/)
[![Tailwind CSS v4](https://img.shields.io/badge/Tailwind-v4.0-38B2AC?logo=tailwind-css&logoColor=white)](https://tailwindcss.com/)
[![Tests](https://img.shields.io/badge/Tests-30%20Passing-brightgreen)](file:///d:/Workspace/Adhoc_System_C#/tests/AdhocSystem.Tests)

An enterprise-grade, full-stack **Natural Language to SQL (NL2SQL) and Semantic Retrieval Platform** built with **ASP.NET Core 8.0** and **React 19**. Targets Microsoft SQL Server (`AdventureWorks2022`) and local vector embeddings to let business users query complex enterprise relational data using plain English or voice.

---

## Key Features

- **Universal NL2SQL Engine**: Generates robust, performant Transact-SQL across 31+ tables and views (`Sales`, `Purchasing`, `Production`) without hardcoded query templates.
- **Compiler-Level AST Security Guard**: Validates all generated SQL via Microsoft Transact-SQL `ScriptDom` AST parser before execution. Strictly blocks mutations (`DROP`, `DELETE`, `UPDATE`, `INSERT`, `ALTER`, `EXEC`, `SELECT INTO`, system stored procedures).
- **Multi-Provider LLM Circuit Breaker**: Dynamic failover with automatic cooldown management across:
  - **Groq** (`openai/gpt-oss-120b`) &mdash; Primary fast inference tier (< 1.5s)
  - **Google Gemini** (`gemini-3.7-flash`) &mdash; Fast secondary fallback
  - **OpenRouter** (`nvidia/nemotron-3-ultra-550b`) &mdash; Deep reasoning fallback
  - **NVIDIA NIM** (`minimaxai/minimax-m3`) &mdash; High-throughput tier
- **Zero-Allocation DB Streaming**: Direct, low-level `SqlDataReader` streaming capable of fetching and streaming 56,000+ line-item ledgers in under 3.5 seconds with zero memory thrashing.
- **Multi-Turn Context & 1-Click Entity Drill-Down**: Conversational memory allows users to click any table row to drill down into product inventory, customer history, or territory metrics.
- **Voice-to-SQL (Whisper STT)**: Built-in speech-to-text pipeline supporting voice queries via microphone or audio upload.
- **Hybrid Semantic Search**: Combines SQLite local vector store (cosine similarity) with full-text search (FTS5) for catalog discovery.
- **Interactive Dark-Themed Studio**: Modern UI featuring pagination, fuzzy column search, multi-column sorting, and 1-click CSV export.

---

## Project Structure

```text
d:\Workspace\Adhoc_System_C#\
├── AdhocSystem.sln               # .NET 8 Visual Studio Solution
├── run.ps1                       # 1-Click Launch Script (PowerShell)
├── run.bat                       # 1-Click Launch Script (Batch / Double-click)
│
├── frontend\                     # [React 19 + TailwindCSS Source]
│   ├── package.json
│   ├── vite.config.js            # Pre-configured to build directly into API wwwroot
│   ├── src\
│   │   ├── App.jsx               # Main Query Studio Canvas
│   │   ├── components\           # DataGrid, MetricsBar, Navbar, QueryInput, SessionBreadcrumbs
│   │   └── services\api.js       # Client module communicating with .NET 8 backend
│   └── public\
│
├── src\
│   └── AdhocSystem.Api\          # [.NET 8 ASP.NET Core Web API Backend]
│       ├── Controllers\          # QueryController, SessionController, HealthController
│       ├── Services\
│       │   ├── Database\         # DatabaseService (Streaming), SchemaIntrospector
│       │   ├── Security\         # SqlGuardService (ScriptDom AST parser)
│       │   ├── Llm\              # CircuitBreakerLlm, Groq, Gemini, OpenRouter, Nvidia NIM
│       │   ├── NL2SQL\           # Nl2SqlService, Prompts, Few-Shot Examples
│       │   ├── Routing\          # IntentRouter (Structured vs Semantic classification)
│       │   ├── Semantic\         # SqliteSemanticStore, LocalEmbeddingGenerator
│       │   └── Audio\            # SpeechToTextService (Whisper)
│       ├── appsettings.json      # Base configuration with sanitized placeholders
│       ├── appsettings.Development.json # Local configuration (Git-ignored)
│       └── wwwroot\              # Embedded production build of the React frontend
│
└── tests\
    └── AdhocSystem.Tests\        # 30 Unit & Integration Tests (100% passing)
        ├── SqlGuardTests.cs      # ScriptDom AST security validation tests
        ├── EndToEndApiTests.cs   # End-to-end NL2SQL, large ledger (50k+ rows), and health tests
        ├── DatabaseIntegrationTests.cs # Live database connectivity and schema introspection
        ├── IntentRouterTests.cs  # Intent classification tests
        └── SqliteSemanticStoreTests.cs # Vector search and FTS5 tests
```

---

## Prerequisites

Before running the application, ensure you have the following installed:

1. **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** (v8.0.400 or higher)
   ```powershell
   dotnet --version
   ```
2. **[Node.js](https://nodejs.org/)** (v18.0+ or v20.0+) & npm (only required if modifying the frontend)
   ```powershell
   node --version
   ```
3. **Microsoft SQL Server 2022** (or SQL Server Express / LocalDB)
   - Database: **`AdventureWorks2022`** (Download `.bak` from [Microsoft's official repository](https://github.com/Microsoft/sql-server-samples/releases/tag/adventureworks))
   - Ensure the SQL Server service (`MSSQL$SQLEXPRESS` or `MSSQLSERVER`) is running.
4. **LLM API Key** (At least one provider):
   - [Groq API Key](https://console.groq.com/) *(Free tier recommended for sub-1.5s queries)*
   - [Google AI Studio (Gemini)](https://aistudio.google.com/)
   - [OpenRouter API Key](https://openrouter.ai/)

---

## Configuration & Secrets Setup

The repository uses sanitized placeholder values in `appsettings.json`. To configure your local database connection and API keys:

1. Create a file named `src/AdhocSystem.Api/appsettings.Development.json` (this file is git-ignored):
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "AllowedHosts": "*",
     "Database": {
       "ConnectionString": "Server=lpc:.\\SQLEXPRESS;Database=AdventureWorks2022;Trusted_Connection=True;TrustServerCertificate=True;",
       "ReadOnlyConnectionString": "Server=lpc:.\\SQLEXPRESS;Database=AdventureWorks2022;Trusted_Connection=True;TrustServerCertificate=True;",
       "AllowedSchemas": ["Sales", "Purchasing", "Production"],
       "MaxResultLimit": 150000
     },
     "Llm": {
       "Provider": "groq",
       "Model": "openai/gpt-oss-120b",
       "ReasoningEffort": "low",
       "GroqApiKey": "your_groq_api_key_here",
       "GroqModel": "openai/gpt-oss-120b",
       "GoogleApiKey": "your_gemini_api_key_here",
       "GeminiModel": "gemini-3.7-flash",
       "OpenRouterApiKey": "your_openrouter_api_key_here",
       "OpenRouterModel": "nvidia/nemotron-3-ultra-550b-a55b:free"
     }
   }
   ```

> [!TIP]
> Using local shared memory (`Server=lpc:.\SQLEXPRESS`) bypasses local TCP loopback overhead and UDP browser resolution latency, yielding ~250–300ms faster database connection times on Windows.

---

## Quickstart

### 1-Click Launch (Recommended)
Run the automated PowerShell launch script:
```powershell
cd d:\Workspace\Adhoc_System_C#
.\run.ps1
```
Or double-click `run.bat` in Windows Explorer.

The script automatically cleans up stale port 5000 listeners, compiles the .NET 8 backend, and hosts both the API and the embedded React Studio on:
**[http://localhost:5000](http://localhost:5000)**

---

## Frontend Development Workflow

The frontend is built with React 19 and Tailwind CSS v4.

### Option A: Edit & Hot-Reload with Vite
If you are developing React UI components and want instant Hot Module Reloading (HMR):
1. **Start the backend API** in one terminal:
   ```powershell
   cd d:\Workspace\Adhoc_System_C#\src\AdhocSystem.Api
   dotnet run --launch-profile http
   ```
2. **Start the Vite dev server** in a second terminal:
   ```powershell
   cd d:\Workspace\Adhoc_System_C#\frontend
   npm run dev
   ```
3. Open **[http://localhost:5173](http://localhost:5173)**. API requests (`/query`, `/health`, `/session`) are automatically proxied to port 5000.

### Option B: Build Production Frontend into Backend
To compile the frontend directly into the backend's static directory (`src/AdhocSystem.Api/wwwroot`):
```powershell
cd d:\Workspace\Adhoc_System_C#\frontend
npm run build
```
Once built, running the backend alone (`dotnet run` or `.\run.ps1`) serves the complete web application on port 5000 without requiring Node.js.

---

## Running the Automated Test Suite

The solution includes 30 unit, integration, and security validation tests covering AST safety, vector store querying, and end-to-end API execution:

```powershell
dotnet test
```

To run tests with detailed console logs:
```powershell
dotnet test --logger "console;verbosity=normal"
```

All 30 tests run against the live `AdventureWorks2022` database and test for:
- ScriptDom AST rejection of SQL injection, `DROP`, `DELETE`, `UPDATE`, and `EXEC`.
- Fetching large 56k+ line-item sales ledgers (`Detailed sales report for 2013`) without truncation.
- SQLite hybrid vector semantic search.
- Multi-turn conversational session recording and context retention.

---

## API Reference

Interactive Swagger documentation is available at:
**[http://localhost:5000/swagger](http://localhost:5000/swagger)**

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `POST` | `/query` | Processes natural language queries (structured NL2SQL or semantic vector search). Accepts optional `selected_row_context` for multi-turn drill-downs. |
| `POST` | `/query_audio` | Transcribes voice audio and runs the resulting query through the NL2SQL pipeline. |
| `POST` | `/transcribe` | Transcribes speech audio using the STT service without executing SQL. |
| `POST` | `/session/new` | Initializes a fresh multi-turn conversation thread. |
| `GET` | `/health` | Reports SQL Server connectivity and vector store readiness status. |

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
