# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CutList is a Windows Forms 1D bin packing optimization application that helps users optimize material cutting. It calculates efficient bin packing solutions to minimize waste when cutting stock materials into required parts.

**Framework**: .NET 8.0 (Windows Forms)
**Key Dependencies**: Math-Expression-Evaluator (input parsing), Newtonsoft.Json (serialization)

## Build Commands

```bash
# Build entire solution
dotnet build CutList.sln

# Build specific project
dotnet build CutList/CutList.csproj
dotnet build CutList.Core/CutList.Core.csproj

# Run the application
dotnet run --project CutList/CutList.csproj

# Clean build
dotnet clean CutList.sln
```

## Architecture

### Project Structure

- **CutList/** - Main Windows Forms application (UI layer)
- **CutList.Core/** - Core library with domain models and packing algorithms (shareable, platform-agnostic)

### Key Patterns

**MVP (Model-View-Presenter)**
- `MainForm` implements `IMainView` (pure UI, no business logic)
- `MainFormPresenter` orchestrates all business logic
- `Document` contains application state

**Result Pattern**
- `Result<T>` in Common/Result.cs for standardized error handling
- Use `Result<T>.Success(value)` and `Result<T>.Failure(error)` instead of exceptions for expected errors

**Factory Pattern**
- `IEngineFactory` creates packing engines
- Allows swapping algorithm implementations

### Data Flow

1. User enters parts/bins in MainForm
2. MainFormPresenter calls CutListService.Pack()
3. CutListService converts input models to core models (MultiBin, BinItem)
4. MultiBinEngine coordinates packing across bin types
5. AdvancedFitEngine performs 1D bin packing (first-fit decreasing with optimization)
6. Results displayed in ResultsForm

### Key Services

- **CutListService**: Bridges UI models to core packing algorithms
- **DocumentService**: JSON file persistence

### Domain Models (CutList.Core)

- **BinItem**: Item to be packed (with label, length)
- **MultiBin**: Stock bin type with length, quantity (-1 = unlimited), priority
- **Bin**: Packed bin containing items
- **Tool**: Cutting tool with kerf/blade width

### Unit Handling

- **ArchUnits**: Converts feet/inches/fractions to decimals (accepts "12'", "6\"", "12 1/2\"", etc.)
- **FormatHelper**: Converts decimals to mixed fractions for display
- Internal calculations use inches; format on display

## Important Conventions

- **Nullable reference types enabled** - handle nulls explicitly
- **Collections are encapsulated** - use AsReadOnly(), access via Add* methods
- **Validation in domain models** - constructors and properties validate inputs
- **Parameterless constructors** on Tool/MultiBin are for JSON serialization only
- **Spacing property** on engines handles blade/kerf width
- **Priority system**: Lower priority bins are used first

## Key Files

| File | Purpose |
|------|---------|
| `Presenters/MainFormPresenter.cs` | Main business logic orchestrator |
| `Services/CutListService.cs` | Packing algorithm interface |
| `CutList.Core/Nesting/AdvancedFitEngine.cs` | Core packing algorithm |
| `CutList.Core/Nesting/MultiBinEngine.cs` | Multi-bin orchestration |
| `CutList.Core/ArchUnits.cs` | Unit conversion |
| `CutList.Core/FormatHelper.cs` | Output formatting |
