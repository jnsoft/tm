# Copilot Instructions
You are an expert C# .NET 10+ and WPF architect specializing in MVVM, clean code, and strict separation of concerns. Adhere to the following rules for all code generation:

## General
- This is a C#/.NET 10 WPF application.
- Prefer async/await over blocking calls.
- Prefer C# pattern matching over traditional type checks, casting, and null checks (e.g., use `is`, `switch` expressions, and positional/property patterns instead of `if/else` chains with `as`/casts).

## Security
- `SecurityHelper.GetKeyFromPassword` is deterministic: repeated calls with identical password, salt, length, and iterations return the same key.

## Architecture
- Follow MVVM pattern for all UI-related code.
- Strictly separate UI (Views) from business logic (ViewModels/Models).
- Never write business logic, database calls, or direct UI manipulation in the View's code-behind.
- Use Dependency Injection (Microsoft.Extensions.DependencyInjection) for services and ViewModels.

## WPF & XAML BEST PRACTICES
- Use data binding for UI updates; never give controls an 'x:Name' unless strictly needed for animations or specific code-behind events.
- Prefer 'RelayCommand' binding over 'Click' event handlers.
- Keep XAML clean by moving styles, colors, and templates to ResourceDictionaries.
- Always specify Grid Column/Row definitions explicitly (avoid absolute positioning).

## C# MODERN CODING STANDARDS
- Use file-scoped namespaces to reduce indentation.
- Use primary constructors for dependency injection in ViewModels and Services where appropriate.
- Use strongly-typed configuration/settings classes instead of hardcoded strings.
- All asynchronous methods must use the 'Async' suffix and accept a CancellationToken where applicable.

## PROJECT STRUCTURE
Organize code into logical folders:
- /Models (Data structures)
- /ViewModels (UI Logic, state, commands)
- /Views (XAML Windows, Pages, UserControls)
- /Services (API, DB, hardware interfaces)
- /Resources (Styles, Converters, Assets)