# Regression checks

Run from the repository root:

```powershell
dotnet run --project tests/SelectionChecks -- tests/GeneratedCompile/obj/Generated
dotnet run --project tests/GeneratedCompile
```

The first command checks selection behaviour and schema/code generation, then writes generated fixtures under `obj`.
The second compiles both combined and separate-file exports using the real MVVM Toolkit, LiteDB, and BaseLocalCacheModel package, and checks property notifications and serialization.
