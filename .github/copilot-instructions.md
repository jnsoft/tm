# Copilot Instructions

## General
- This is a C#/.NET 10 WPF application.
- Prefer async/await over blocking calls.
- Prefer C# pattern matching over traditional type checks, casting, and null checks (e.g., use `is`, `switch` expressions, and positional/property patterns instead of `if/else` chains with `as`/casts).

## Architecture
- Follow MVVM pattern for all UI-related code.