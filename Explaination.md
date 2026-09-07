# Adhoc System C# — Technical Architecture & ERP Integration Guide

## 1. Executive Summary & Core Usecase

The **Adhoc System C#** is an enterprise-grade, full-stack **Natural Language to SQL (NL2SQL) and Semantic Retrieval Engine** built on **.NET 8.0** and **React 19**. 

### Primary Business Usecase
In large ERP, CRM, and enterprise database systems, business users (sales managers, financial analysts, operations teams) frequently need ad-hoc insights (e.g., *"What were the top 10 products by revenue in 2013?"*, *"Drill down on inventory for product 782"*, or *"Show me long-distance touring equipment"*). Rather than requiring manual SQL query authoring or submitting BI ticket requests, this system enables:
1. **Natural Language / Voice Input**: Querying business data via typed English or real-time voice speech.
2. **Dynamic Schema Reflection**: Self-discovering tables, relationships, foreign keys, and views across enterprise database schemas without hardcoded SQL templates.
3. **Multi-Provider LLM Resilience**: Dynamic circuit breaker routing prompts across Groq, OpenRouter, NVIDIA NIM, and Google Gemini with sub-second failover.
4. **Compiler-Level AST Security**: Validating generated SQL through Microsoft's Transact-SQL ScriptDom compiler parser to strictly prevent mutations (`DROP`, `DELETE`, `UPDATE`, `INSERT`, `EXEC`, `SELECT INTO`).
5. **Multi-Turn Entity Chaining**: Contextual drill-downs across iterative conversation turns with entity row selection.
6. **High-Performance Data Studio**: Sorting, filtering, searching, and exporting hundreds of thousands of records in a responsive dark-themed grid.

---

## 2. Directory & File-by-File Purpose Guide

```text
d:\Workspace\Adhoc_System_C#\
├── AdhocSystem.sln
├── README.md
├── Explaination.md
├── run.ps1
├── run.bat
├── frontend\
├── src\
│   └── AdhocSystem.Api\
└── tests\
    └── AdhocSystem.Tests\
```

---

### 2.1. Root Orchestration Files

| File | Purpose |
| :--- | :--- |
| [`AdhocSystem.sln`](file:///d:/Workspace/Adhoc_System_C#/AdhocSystem.sln) | .NET 8 Visual Studio Solution file uniting the API backend and the Test Suite projects. |
| [`run.ps1`](file:///d:/Workspace/Adhoc_System_C#/run.ps1) | 1-Click PowerShell launcher that terminates stale instances on port 5000, activates .NET 8 SDK, and starts the API with launch profile `http`. |
| [`run.bat`](file:///d:/Workspace/Adhoc_System_C#/run.bat) | 1-Click Windows Command Prompt / double-clickable batch launcher for quick deployment. |
| [`README.md`](file:///d:/Workspace/Adhoc_System_C#/README.md) | Developer quickstart guide, architectural layout, and development workflow documentation. |
| [`Explaination.md`](file:///d:/Workspace/Adhoc_System_C#/Explaination.md) | Comprehensive structural and file-by-file technical reference document. |

---

### 2.2. Backend API Project (`src/AdhocSystem.Api`)

```text
src/AdhocSystem.Api/
├── AdhocSystem.Api.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Properties/launchSettings.json
├── Models/
├── Services/
├── Controllers/
└── wwwroot/
```

#### Configuration & Startup
- **[`AdhocSystem.Api.csproj`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/AdhocSystem.Api.csproj)**: Project manifest targeting `net8.0`. Defines enterprise dependencies: `Dapper` (high-speed micro-ORM), `Microsoft.Data.SqlClient` (connection pooling), `Microsoft.SqlServer.TransactSql.ScriptDom` (AST parser), `Polly` (resilience), and `Swashbuckle.AspNetCore` (Swagger documentation).
- **[`Program.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Program.cs)**: Main application composition root. Registers Dependency Injection (DI) services, typed HTTP clients, CORS policies, Swagger generation, static file serving (`wwwroot`), and SPA fallback routing (`app.MapFallbackToFile("index.html")`).
- **[`appsettings.json`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/appsettings.json)**: Strongly-typed application configuration for SQL Server connection strings, allowed database schemas (`Sales`, `Purchasing`, `Production`), LLM provider keys/models (Groq, OpenRouter, NVIDIA NIM, Gemini), and vector store settings.
- **[`Properties/launchSettings.json`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Properties/launchSettings.json)**: Development runtime profiles setting application URL to `http://localhost:5000`.

---

#### Data Models & Contracts (`src/AdhocSystem.Api/Models`)

```text
Models/
├── Common/
│   ├── Enums.cs
│   └── AppSettings.cs
├── Requests/
│   └── QueryRequests.cs
└── Responses/
    └── QueryResponses.cs
```

- **[`Models/Common/Enums.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Models/Common/Enums.cs)**: Defines the `IntentType` enum (`Structured` for SQL relational data, `Semantic` for vector descriptive search).
- **[`Models/Common/AppSettings.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Models/Common/AppSettings.cs)**: Strongly-typed POCO classes mapping `appsettings.json` sections: `DatabaseOptions`, `LlmOptions`, and `VectorStoreOptions`.
- **[`Models/Requests/QueryRequests.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Models/Requests/QueryRequests.cs)**: Input DTOs:
  - `QueryRequest`: Encapsulates `query`, `session_id`, `is_follow_up`, and `selected_row_context`.
  - `AudioQueryRequest`: Form-data contract for direct audio query submission.
- **[`Models/Responses/QueryResponses.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Models/Responses/QueryResponses.cs)**: Output DTOs:
  - `QueryResponse`: Returns `query`, `intent`, `generated_sql`, `results` (dynamic row dictionaries), `answer`, `execution_time_ms`, `session_id`, and `turn_index`.
  - `TranscribeResponse`: Returns `{ text, language, duration }`.
  - `AudioQueryResponse`: Unified audio-to-retrieval output payload.
  - `HealthResponse`: System and database connectivity status report.

---

#### Services Layer (`src/AdhocSystem.Api/Services`)

##### A. Database & Schema Introspection (`Services/Database`)
- **[`IDatabaseService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Database/IDatabaseService.cs) & [`DatabaseService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Database/DatabaseService.cs)**: Read-only database query execution engine using Dapper and `Microsoft.Data.SqlClient`. Enforces connection pooling, cancellation tokens, and database health checks (`CheckConnectionAsync`).
- **[`ISchemaIntrospector.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Database/ISchemaIntrospector.cs) & [`SchemaIntrospector.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Database/SchemaIntrospector.cs)**: Queries SQL Server system metadata (`sys.tables`, `sys.columns`, `sys.foreign_keys`, `sys.views`) across configured schemas. Formats an optimized ~2,150 token schema representation and caches it in memory with thread-safe `SemaphoreSlim` initialization.

##### B. AST Security Guard (`Services/Security`)
- **[`ISqlGuardService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Security/ISqlGuardService.cs) & [`SqlGuardService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Security/SqlGuardService.cs)**: Enterprise security compiler using `Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser`. Parses raw LLM output into an Abstract Syntax Tree (AST), validates single-statement single-batch integrity, enforces strict `SelectStatement` constraints, blocks `SELECT ... INTO`, and traverses the AST with `AstSafetyVisitor` to reject mutations (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `EXEC`).

##### C. Multi-Provider LLM & Circuit Breaker (`Services/Llm`)
- **[`IChatClient.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Llm/IChatClient.cs) & [`ChatMessage.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Llm/ChatMessage.cs)**: Provider-agnostic chat interface and message payload contracts.
- **[`GroqChatClient.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Llm/GroqChatClient.cs)**: Groq OpenAI-compatible client (`openai/gpt-oss-120b`).
- **[`OpenRouterChatClient.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Llm/OpenRouterChatClient.cs)**: OpenRouter fallback client (`nvidia/nemotron-3-ultra-550b-a55b:free`).
- **[`NvidiaNimChatClient.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Llm/NvidiaNimChatClient.cs)**: NVIDIA NIM enterprise inference client (`minimaxai/minimax-m3`).
- **[`GeminiChatClient.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Llm/GeminiChatClient.cs)**: Google Gemini native REST API client (`gemini-3.7-flash`).
- **[`ICircuitBreakerLlm.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Llm/ICircuitBreakerLlm.cs) & [`CircuitBreakerLlm.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Llm/CircuitBreakerLlm.cs)**: Thread-safe multi-tier circuit breaker. Automatically detects rate limits (HTTP 429), gateway timeouts (502/503), or network outages, parses `Retry-After` cooldown headers, and fails over to backup LLMs in < 5ms without dropping requests.

##### D. NL2SQL Generation (`Services/NL2SQL`)
- **[`Prompts.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/NL2SQL/Prompts.cs)**: Stores few-shot domain prompts, SQL Server dialect rules (e.g. `TOP N`, wildcard matching `LIKE '%term%'`, schema qualification `Sales.SalesOrderHeader`), and contextual multi-turn drill-down templates.
- **[`INl2SqlService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/NL2SQL/INl2SqlService.cs) & [`Nl2SqlService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/NL2SQL/Nl2SqlService.cs)**: Injects dynamic database schema, user question, and previous turn state into LLM prompts, passes output to ScriptDom AST guard, and executes the sanitized query against SQL Server.

##### E. Intent Routing & Session Management (`Services/Routing` & `Services/Session`)
- **[`IIntentRouter.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Routing/IIntentRouter.cs) & [`IntentRouter.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Routing/IntentRouter.cs)**: Zero-latency hybrid classifier. Uses fast regex keywords for ~90% of business queries (e.g. *"top"*, *"revenue"*, *"sales"*, *"inventory"*, *"count"*) with low-temperature LLM fallback for ambiguous queries.
- **[`ISessionManager.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Session/ISessionManager.cs), [`SessionManager.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Session/SessionManager.cs), & [`SessionState.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Session/SessionState.cs)**: Thread-safe `ConcurrentDictionary` session cache with sliding TTL (2 hours) tracking conversation turns, previous SQL queries, sample rows, and active drill-down entity contexts.

##### F. Semantic Vector Search & Audio STT (`Services/Semantic` & `Services/Audio`)
- **[`ILocalEmbeddingGenerator.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Semantic/ILocalEmbeddingGenerator.cs) & [`LocalEmbeddingGenerator.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Semantic/LocalEmbeddingGenerator.cs)**: 100% offline, deterministic subword and morphological feature-hashing vectorizer producing 384-dimensional dense L2-normalized embeddings with zero external API dependencies.
- **[`ISqliteSemanticStore.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Semantic/ISqliteSemanticStore.cs) & [`SqliteSemanticStore.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Semantic/SqliteSemanticStore.cs)**: Embedded SQLite database (`data/semantic_store.db`) managing product catalogs, FTS5 virtual tables with BM25 ranking, and SIMD-accelerated (`TensorPrimitives.CosineSimilarity`) vector scoring.
- **[`SemanticSyncBackgroundService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Semantic/SemanticSyncBackgroundService.cs)**: Hosted background service that auto-seeds/syncs 294+ AdventureWorks product descriptions on application startup.
- **[`QueryDecomposer.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Semantic/QueryDecomposer.cs)**: Splits multi-entity queries (e.g., *"best bike and best helmet"*, *"compare mountain bikes vs road bikes"*) while preserving modifiers.
- **[`ISemanticSearchService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Semantic/ISemanticSearchService.cs) & [`SemanticSearchService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Semantic/SemanticSearchService.cs)**: Hybrid search orchestrator combining SQLite FTS5 BM25 matches with dense vector cosine similarity and round-robin multi-category interleaving.
- **[`ISpeechToTextService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Audio/ISpeechToTextService.cs) & [`SpeechToTextService.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Services/Audio/SpeechToTextService.cs)**: Sub-second audio transcription via Groq Whisper API (`whisper-large-v3-turbo`).

---

#### Controllers (`src/AdhocSystem.Api/Controllers`)

- **[`QueryController.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Controllers/QueryController.cs)**: Primary API controller exposing `POST /query`, `POST /transcribe`, and `POST /query_audio`.
- **[`SemanticController.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Controllers/SemanticController.cs)**: Semantic search diagnostics (`GET /api/semantic/status`), manual sync (`POST /api/semantic/sync`), and direct test querying (`POST /api/semantic/search`).
- **[`SessionController.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Controllers/SessionController.cs)**: Session management endpoints: `POST /session/new`, `GET /session/{sessionId}/history`, and `DELETE /session/{sessionId}`.
- **[`HealthController.cs`](file:///d:/Workspace/Adhoc_System_C#/src/AdhocSystem.Api/Controllers/HealthController.cs)**: Live system health check (`GET /health`) pinging database connectivity.

---

### 2.3. Frontend Studio (`frontend/`)

```text
frontend/
├── package.json
├── vite.config.js
├── index.html
├── src/
│   ├── App.jsx
│   ├── main.jsx
│   ├── index.css
│   ├── components/
│   │   ├── DataGrid.jsx
│   │   ├── MetricsBar.jsx
│   │   ├── Navbar.jsx
│   │   ├── QueryInput.jsx
│   │   ├── SessionBreadcrumbs.jsx
│   │   ├── Sidebar.jsx
│   │   ├── SqlInspector.jsx
│   │   ├── ThinkingIndicator.jsx
│   │   └── ErrorBanner.jsx
│   └── services/
│       └── api.js
└── public/
```

- **[`vite.config.js`](file:///d:/Workspace/Adhoc_System_C#/frontend/vite.config.js)**: Configures Vite build output directly into `../src/AdhocSystem.Api/wwwroot` and sets up local dev proxying to `http://localhost:5000`.
- **[`src/App.jsx`](file:///d:/Workspace/Adhoc_System_C#/frontend/src/App.jsx)**: Main React application canvas managing state for active queries, session breadcrumbs, sidebar toggling, and data presentation.
- **[`src/components/DataGrid.jsx`](file:///d:/Workspace/Adhoc_System_C#/frontend/src/components/DataGrid.jsx)**: Enterprise tabular data studio with multi-column sorting, in-table text filtering, row density toggle, sticky headers, pagination, and CSV export.
- **[`src/components/QueryInput.jsx`](file:///d:/Workspace/Adhoc_System_C#/frontend/src/components/QueryInput.jsx)**: Top query bar with voice recording, animated audio waveforms, live speech-to-text preview, and quick preset buttons.
- **[`src/components/SessionBreadcrumbs.jsx`](file:///d:/Workspace/Adhoc_System_C#/frontend/src/components/SessionBreadcrumbs.jsx)**: Interactive visual navigation trail showing multi-turn query history (#1 -> #2 -> #3) and drill-downs.
- **[`src/components/Sidebar.jsx`](file:///d:/Workspace/Adhoc_System_C#/frontend/src/components/Sidebar.jsx)**: Collapsible sidebar with category presets and local query history.
- **[`src/services/api.js`](file:///d:/Workspace/Adhoc_System_C#/frontend/src/services/api.js)**: Centralized HTTP client communicating with backend endpoints.

---

### 2.4. Test Suite (`tests/AdhocSystem.Tests`)

- **[`SqlGuardTests.cs`](file:///d:/Workspace/Adhoc_System_C#/tests/AdhocSystem.Tests/SqlGuardTests.cs)**: Validates ScriptDom AST security blocking (rejects `DROP`, `DELETE`, `UPDATE`, `INSERT`, `EXEC`, `SELECT INTO`, chained `;` queries).
- **[`QueryDecomposerTests.cs`](file:///d:/Workspace/Adhoc_System_C#/tests/AdhocSystem.Tests/QueryDecomposerTests.cs)**: Tests composite natural language query decomposition.
- **[`SessionManagerTests.cs`](file:///d:/Workspace/Adhoc_System_C#/tests/AdhocSystem.Tests/SessionManagerTests.cs)**: Tests multi-turn sliding session expiration and state tracking.
- **[`DatabaseIntegrationTests.cs`](file:///d:/Workspace/Adhoc_System_C#/tests/AdhocSystem.Tests/DatabaseIntegrationTests.cs)**: Live SQL Server connection and dynamic schema introspection tests against `AdventureWorks2022`.
- **[`EndToEndApiTests.cs`](file:///d:/Workspace/Adhoc_System_C#/tests/AdhocSystem.Tests/EndToEndApiTests.cs)**: Full-pipeline integration testing using `WebApplicationFactory<Program>` (`GET /health`, `POST /session/new`, `POST /query`).

---

## 3. How to Refactor & Integrate into an Existing ERP Model

When embedding this capability into an existing ERP solution (e.g., SAP, Microsoft Dynamics 365, NetSuite, or a custom in-house .NET ERP), the standalone application structure can be significantly consolidated and streamlined.

### 3.1. What Can Be Combined / Simplified?

```
┌─────────────────────────────────────────────────────────────┐
│                 CONSOLIDATION BLUEPRINT                     │
├──────────────────────────────┬──────────────────────────────┤
│ Current Standalone Structure │ Consolidated ERP Module      │
├──────────────────────────────┼──────────────────────────────┤
│ 8 LLM Provider Clients       │ 1 Unified Polymorphic Client │
│ 6 Request/Response Files     │ 1 Contracts.cs File          │
│ Standalone DatabaseService   │ ERP's Existing DbContext     │
│ In-Memory Session Manager    │ ERP Distributed Cache (Redis)│
│ Standalone Frontend Project  │ Embedded ERP Widget / Modal  │
└──────────────────────────────┴──────────────────────────────┘
```

#### 1. Consolidate DTOs into a Single File (`Contracts.cs`)
Instead of separate folders for Requests, Responses, and Enums, combine them into one file:
- `QueryRequest`, `QueryResponse`, `IntentType`, `TurnContext`, `HealthResponse`.

#### 2. Consolidate LLM Clients into a Single Factory/Client
Instead of 4 separate chat client files (`GroqChatClient.cs`, `OpenRouterChatClient.cs`, `NvidiaNimChatClient.cs`, `GeminiChatClient.cs`), combine them into a single `UniversalOpenAiChatClient.cs` since Groq, OpenRouter, and NVIDIA NIM all use the standard OpenAI REST specification format.

#### 3. Merge Prompt Templates & NL2SQL Engine
`Prompts.cs` and `Nl2SqlService.cs` can be combined into a single service: `Nl2SqlEngine.cs`.

#### 4. Leverage Existing ERP Infrastructure
- **Database Access**: If the ERP already uses Entity Framework Core or Dapper, you do not need a custom `DatabaseService.cs`. Replace it with the ERP’s existing read-only `IDbConnection` or `DbContext.Database.GetDbConnection()`.
- **Session Management**: Replace the in-memory `ConcurrentDictionary` in `SessionManager.cs` with the ERP’s distributed session cache (e.g., Redis via `IDistributedCache`) to support multi-server ERP clusters.

---

### 3.2. Recommended ERP Architecture Shifting (Modular Package)

To integrate cleanly without polluting the ERP's core domain, package the functionality as an isolated **Class Library / Feature Module** (e.g. `YourCompany.ERP.AiAdhoc`):

```text
YourCompany.ERP/
├── YourCompany.ERP.Web/                   # Main ERP Web Host
│   ├── Controllers/
│   │   └── AdhocQueryController.cs       # (Or MediatR Endpoint)
│   └── ClientApp/                         # ERP React / Angular Frontend
│       └── components/AdhocAssistant/     # Embedded Chat / Table Widget
│
└── YourCompany.ERP.Modules.AdhocRag/      # 📦 Embedded Class Library
    ├── AdhocRagServiceExtensions.cs       # services.AddAdhocRag(...)
    ├── Contracts.cs                       # All Models & DTOs
    ├── Engine/
    │   ├── Nl2SqlEngine.cs                # Prompts + LLM Pipeline
    │   ├── SchemaIntrospector.cs          # Reads ERP Tables & Columns
    │   └── IntentRouter.cs                # Regex + LLM Classifier
    ├── Security/
    │   └── SqlGuard.cs                    # ScriptDom AST Safety Parser
    └── LLM/
        ├── CircuitBreakerLlm.cs           # Multi-Provider Failover
        └── LlmClient.cs                   # HTTP Chat Client
```

---

### 3.3. Step-by-Step ERP Integration Plan

1. **Add Dependencies to ERP**:
   - Add `Microsoft.SqlServer.TransactSql.ScriptDom` (for AST security) and `Polly` (for circuit breaker).
2. **Register Extension Method in ERP `Program.cs`**:
   ```csharp
   // In ERP Program.cs / Startup.cs
   builder.Services.AddAdhocRag(options => {
       options.ConnectionString = builder.Configuration.GetConnectionString("ErpReadOnlyDb");
       options.AllowedSchemas = new[] { "Sales", "Inventory", "Accounting" };
       options.GroqApiKey = builder.Configuration["Ai:GroqKey"];
   });
   ```
3. **Enforce Role-Based Schema Filtering**:
   - Modify `SchemaIntrospector.cs` to filter table introspection based on the logged-in ERP user's permissions (e.g., hiding `Payroll` or `Accounting` tables from non-financial users).
4. **Embed the Frontend Component**:
   - Copy `DataGrid.jsx` and `QueryInput.jsx` into the ERP's frontend (React, Angular, or Blazor) as a floating sidebar assistant or an **"Ad-Hoc AI Query Studio"** tab within the ERP dashboard.
