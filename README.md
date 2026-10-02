# CutList

CutList optimizes one-dimensional material cutting to reduce waste when turning stock lengths into required parts. It includes a Blazor web application, a Windows desktop application, a shared packing library, and an MCP server for AI-assisted workflows.

## Features

- Plan cutting jobs with named parts, quantities, and material specifications.
- Optimize cuts across multiple stock lengths, accounting for cutting-tool kerf, stock limits, and stock priority.
- Manage a material catalog covering bar, tube, pipe, angle, channel, and I-beam shapes.
- Enter lengths in feet, inches, and fractions, such as `12'`, `6"`, or `12 1/2"`.
- Review stock utilization, waste, unplaced parts, and required material; print cut-list reports.
- Save optimization results, duplicate jobs, and lock completed plans against accidental edits.
- Integrate through a REST API or stdio MCP tools.

Stock catalog entries describe available cutting lengths, not counted on-hand inventory. Each job must explicitly specify the stock it may use. CutList is a **1D cutting optimizer**, not a sheet-metal or other 2D nesting tool.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source.
- SQL Server for the web application, with a dedicated database and credentials allowed to apply its schema migrations.
- Entity Framework Core CLI tools, version 10.x, for database setup.
- Windows to run the Windows Forms desktop application. The web application, core library, and MCP server can run on other .NET-supported platforms.
- Docker only if you choose the container option.

## Run the web application

The following examples use Bash. Use your shell's equivalent syntax when setting environment variables on Windows.

### 1. Get the source

```bash
git clone https://github.com/ajisaacs/CutList.git
cd CutList
dotnet build CutList.Web/CutList.Web.csproj
```

### 2. Configure a development database

Set `ConnectionStrings__DefaultConnection` to your SQL Server connection string. Replace the placeholders with your own server and credentials, and use a server certificate trusted by your machine:

```bash
export ConnectionStrings__DefaultConnection='Server=<sql-server>;Database=CutList;User Id=<sql-user>;Password=<sql-password>;Encrypt=True;TrustServerCertificate=False;'
```

Do not commit real connection strings or credentials. Use a dedicated development database for this quick start, not an existing production database.

If `dotnet ef` is not installed, install the matching major version:

```bash
dotnet tool install --global dotnet-ef --version '10.*'
```

Apply the included migrations:

```bash
dotnet ef database update --project CutList.Web/CutList.Web.csproj
```

This command creates or updates the database schema. The application does **not** automatically apply migrations at startup.

### 3. Start the application

```bash
dotnet run --project CutList.Web/CutList.Web.csproj --launch-profile http -- --urls http://localhost:5270
```

Open [http://localhost:5270](http://localhost:5270). This command explicitly selects port 5270; the checked-in HTTP launch profile otherwise uses port 5009. In Development mode, API documentation is available at [http://localhost:5270/swagger](http://localhost:5270/swagger).

### 4. Create a cut list

1. Add materials under **Materials** and their standard lengths under **Stock**.
2. Select or configure a cutting tool under **Cutting Tools**, including its kerf.
3. Create a job and add the required parts and quantities.
4. On the job's **Stock** tab, choose catalog stock or custom lengths, quantities, and priorities. Lower priority numbers are used first; a quantity of `-1` means unlimited stock.
5. Optimize the job, review the **Results** tab, and print the cut list. Lock the job when you want to preserve the plan; unlock it to make further edits.

## Windows desktop application

The original Windows Forms application uses the shared packing library and saves documents as JSON files. It does not require the web application's SQL Server database.

Run these commands on Windows from the repository root:

```powershell
dotnet build CutList/CutList.csproj
dotnet run --project CutList/CutList.csproj
```

## Docker

Public web application images are available from GitHub Container Registry:

```bash
docker pull ghcr.io/ajisaacs/cutlist:latest
```

After configuring the connection string and applying database migrations as described above, run the container:

```bash
docker run --rm --name cutlist \
  --publish 127.0.0.1:5270:5270 \
  --env ConnectionStrings__DefaultConnection \
  ghcr.io/ajisaacs/cutlist:latest
```

The SQL Server hostname must be reachable from inside the container; `localhost` inside the container is not the host machine. The image runs in Production mode, so Swagger is disabled. For repeatable deployments, use a full commit-SHA image tag instead of `latest`.

## REST API and MCP

The web application exposes REST endpoints for jobs, materials, stock items, cutting tools, packing, and catalog export. Explore the request and response schemas through Swagger in Development mode.

`CutList.Mcp` is a stdio MCP server that calls the web application's API at `http://localhost:5270`. Start the web application at that address first, then register the following command with your MCP-compatible client:

```bash
dotnet run --project /absolute/path/to/CutList/CutList.Mcp/CutList.Mcp.csproj --no-build
```

Build the MCP project before using that command:

```bash
dotnet build CutList.Mcp/CutList.Mcp.csproj
```

The MCP server provides tools for managing jobs, materials, and stock, and for optimizing jobs. It is launched by the MCP client, not accessed as a separate HTTP service.

## Build and test

From the repository root, build the cross-platform applications and run the core test suite:

```bash
dotnet build CutList.Web/CutList.Web.csproj
dotnet build CutList.Mcp/CutList.Mcp.csproj
dotnet test CutList.Core.Tests/CutList.Core.Tests.csproj
```

On Windows, build the full solution with `dotnet build CutList.sln`. Building the individual cross-platform projects avoids the Windows Forms target on Linux and macOS.

## Project layout

| Directory | Purpose |
| --- | --- |
| `CutList/` | Windows Forms desktop application |
| `CutList.Core/` | Shared domain models, unit formatting, and packing algorithms |
| `CutList.Web/` | Blazor UI, REST API, SQL Server data access, and migrations |
| `CutList.Mcp/` | Stdio MCP integration for the web API |
| `CutList.Core.Tests/` | .NET test suite |
| `tests/` | Python regression tests |
| `docs/` | Additional project documentation |

See [repository workflow](docs/repository-workflow.md) for repository and container publishing details.

## License

No license has been specified in this repository.
