# YURI 2.0

The .ahk hub, rebuilt on .NET 8 + Avalonia so one codebase runs on Windows
and macOS. This is a port in progress; PORT_PLAN.md is the manifest of what
is done, what is next, and what cannot exist on macOS.

Data lives beside the program in `YURI/` exactly as before (`YURI/config/zeal.ini`
and the rest); an existing folder from the .ahk is picked up as is.

    dotnet build src/YURI/YURI.csproj -c Release
    dotnet run   --project src/YURI

Headless screenshots of any surface (no display needed), for checking a port
against the .ahk: `dotnet run --project tools/Snap -- loading|hub:N [light|mod:N]`
writes PNGs to `~/snaps`. `dotnet run --project tools/Snap -- test` runs the
interaction tests; it must print ALL PASSED before a build ships.
