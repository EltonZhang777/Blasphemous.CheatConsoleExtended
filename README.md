# Cheat Console Extended

Cheat Console Extended requires the Cheat Console mod and extends Blasphemous 1's vanilla console with strict command matching, command discovery, configurable appearance, and programmatic access for other mods.

## Getting the code

```sh
git clone https://github.com/EltonZhang777/Blasphemous.CheatConsoleExtended.git
cd Blasphemous.CheatConsoleExtended
```

## Requirements

- Blasphemous 1
- [ModdingAPI 3.0.0 or later](https://github.com/BrandenEK/Blasphemous.ModdingAPI)
- [Cheat Console](https://github.com/BrandenEK/Blasphemous.CheatConsole)

For development, install the [.NET SDK 9.0.200 or later](https://dotnet.microsoft.com/download/dotnet/9.0) and verify it with `dotnet --version`.

## Features

- Top-level command and subcommand names use strict, case-insensitive exact matching. A prefix is not enough. Close-name suggestions help with mistyped names without executing a command.
- `Help` displays a catalog of vanilla, shared, and mod commands.
- `consolefont list` lists installed system font names, `consolefont set <font family name>` selects one, and `consolefont reset` restores the default console font.
- Separate configurable colors identify the top-level command, subcommand, parameter, and output roles.

## Installation

1. Download a release ZIP from [Releases](https://github.com/EltonZhang777/Blasphemous.CheatConsoleExtended/releases).
2. Open the game's `Modding` folder.
3. Extract the ZIP contents directly into that folder, preserving the archive's directory structure.

## Configuration and mod author API

The mod persists configuration for suggestion distance, console font, and the four console color roles. Mod authors can use the public [`CheatConsoleController`](Blasphemous.CheatConsoleExtended/CheatConsoleController.cs) API to inspect console state, open or close the console, execute commands programmatically, and control player or API access.

## Build and test

```sh
dotnet restore
dotnet build --configuration Release
dotnet test Blasphemous.CheatConsoleExtended.Tests/Blasphemous.CheatConsoleExtended.Tests.csproj --configuration Release
```

## Contributing

Report bugs and request features in [GitHub Issues](https://github.com/EltonZhang777/Blasphemous.CheatConsoleExtended/issues). This project is licensed under the [MIT License](LICENSE).
