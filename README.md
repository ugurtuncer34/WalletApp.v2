# 🏦 FamilyFinance Backend (Core API)

An enterprise-grade, highly scalable personal and family finance management API built with **.NET 10**. This backend serves as the central brain and secure gateway of the FamilyFinance ecosystem, seamlessly handling complex financial calculations, recurring billing, real-time cryptocurrency tracking, and concurrent transactions with absolute data integrity.

Designed with a cloud-native vision and strict adherence to **SOLID principles**, it offloads heavy I/O operations to an independent Go microservice via lightning-fast **gRPC** communication and securely orchestrates AI-driven document parsing via a dedicated Python NLP microservice.

## 🚀 Key Architectural Features

### 🛡️ Enterprise-Grade Reliability & Concurrency
* **Optimistic Locking (Concurrency Control):** Prevents data corruption when multiple users (or devices) attempt to update the same financial record simultaneously. Implemented at the database level using PostgreSQL's native `xmin` hidden system column mapped as a RowVersion in Entity Framework Core.
* **Idempotency Guarantee:** All critical POST endpoints (like `CreateTransaction`, `QuickAdd` and `BulkImport`) require an `X-Idempotency-Key` header. Backed by `IDistributedCache`, this ensures that network retries or duplicate client requests never result in double-charging or duplicate database entries.

### 🌐 Secure API Gateway (BFF) & AI Orchestration
* **AI-Powered Bulk Statement Processing:** Acts as a Backend-For-Frontend (BFF) proxy for parsing bank statement PDFs. The API intercepts the file, enforces strict validation (max 5MB, strict MIME types), hydrates the payload with cached master data (Categories/Merchants), and securely proxies the request to an isolated **Python FastAPI microservice** powered by **DeepSeek AI**.
* **Zero Client Exposure:** By routing NLP requests through the internal Docker bridge network (`http://python-nlp-container:8000`), the .NET API completely hides the AI microservice, internal networking routes, and API secrets from the public frontend.

### ⚡ High-Performance Integrations & Caching
* **Parallel gRPC Communication:** Communicates with the Exchange Rate Microservice (Go) using Protocol Buffers. The `CryptoService` leverages `Task.WhenAll` to fire parallel gRPC requests for multiple coin assets simultaneously, reducing portfolio valuation fetch times to milliseconds regardless of the asset count.
* **Aggressive Caching Layer:** Master data (Categories, Merchants, Countries) and high-read/low-write endpoints are heavily cached using `IDistributedCache` to minimize database hits and accelerate internal proxy payloads.

### 🔄 Automated Finance Engine (Hangfire)
* **Subscriptions & Installment Tracking:** Integrated **Hangfire** backed by PostgreSQL storage to act as a CRON worker. A dedicated background worker wakes up daily to process due `RecurringTransactions`, automatically calculating paid installments, writing real transactions to the ledger, and advancing execution dates.

### 🔐 Security & Identity Management
* **JWT Authentication with Stateful Blacklisting:** Employs stateless JWTs for performance, but combines it with an `IDistributedCache`-based blacklist mechanism (`JTI` claim check) to instantly revoke compromised tokens or handle manual logouts/password changes securely.
* **Password Hashing:** Uses `BCrypt.Net-Next` for robust cryptographic hashing of user credentials.

### 🧠 Smart Data Processing
* **NLP-Style "Quick Add" Engine:** Features a custom parsing algorithm that takes raw, unstructured text (e.g., "1500 starbucks coffee") and intelligently extracts the amount, matches the merchant via string proximity, infers the category via a dynamic Rule Engine (`CategoryRules`), and logs the transaction.

### 🛠️ Centralized Logging & Error Handling
* **Global Exception Middleware:** A centralized middleware catches all unhandled exceptions, maps them to standard HTTP status codes (e.g., `DbUpdateConcurrencyException` -> 409 Conflict, `HttpRequestException` -> 502 Bad Gateway), and returns a standardized JSON `ErrorResponse` to the frontend without leaking stack traces in production.
* **Structured Logging (Serilog):** Fully integrated with Serilog, writing structured, easily searchable compact JSON logs to the file system, enriched with HTTP context.

## 💻 Tech Stack

* **Framework:** .NET 10.0 (ASP.NET Core Web API)
* **Database:** PostgreSQL (via `Npgsql.EntityFrameworkCore.PostgreSQL`)
* **ORM:** Entity Framework Core 10.0
* **Background Jobs:** Hangfire
* **Microservice Comm:** gRPC (`Grpc.Net.Client`, `Google.Protobuf`) & HTTP Proxy (`IHttpClientFactory`)
* **Logging:** Serilog (`Serilog.Formatting.Compact`)
* **Security:** JWT (`System.IdentityModel.Tokens.Jwt`), BCrypt
* **Deployment:** Docker (Multi-stage build), Docker Compose

## 🏗️ Project Structure

The project strictly follows a clean Separation of Concerns (SoC) and Dependency Injection:

* **`Controllers/`**: Thin HTTP presentation layer (Dumb Controllers).
* **`Services/`**: The core business logic (Crypto, Transactions, Master Data, Dashboard, NLP Proxying).
* **`Entities/` & `Dtos/`**: Clear separation between database domain models and API payloads.
* **`Middleware/` & `Filters/`**: Cross-cutting concerns (Exception Handling, Idempotency).
* **`Protos/`**: Shared `.proto` contracts for gRPC communication.

## 🚀 Getting Started

1. Clone the repository.
2. Provide a PostgreSQL instance (or use the included `compose.yaml`).
3. Set the required Environment Variables in `appsettings.json` or your deployment environment (e.g. Coolify):
   * `ConnectionStrings:DefaultConnection`
   * `JwtSettings:SecretKey`
   * `GoGrpcServiceUrl` (URL of the internal Go Microservice)
   * `NLP_SERVICE_URL` (Internal Docker network URL for the Python DeepSeek service)
   * `NLP_API_SECRET` (Authentication key for the internal Python service)
4. Run EF Core migrations: `dotnet ef database update`
5. Run the application: `dotnet run` (Available at `http://localhost:5139` and `https://localhost:7222`)