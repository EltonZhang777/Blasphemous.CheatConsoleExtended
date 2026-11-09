# Cheat Console Extended

Extends Blasphemous 1's cheat console with configurable appearance, safer command validation, command suggestion, and programmatic access for other mods.

---

## Features

- Customizable console text color
  - Separate configurable colors for top-level command, subcommand, parameter, and output roles.
- Adds custom font support for console text
  - `consolefont list` lists installed system font names, `consolefont set <font_name>` selects one, and `consolefont reset` restores the default console font.

[<img src="docs\readme\custom_font.png"/>]()

- Safer command matching and validation
  - Removes vanilla fuzzy match to prevent mis-triggering high-risk commands like `invtest`. Top-level command and subcommand names use strict, case-insensitive exact matching. A prefix is not enough. 
- Adds close-name suggestions that shows up when mistyping a command.

[<img src="docs\readme\command_color.png"/>]()

- Rewrites `help` command to display a separated and clean catalog of vanilla, shared, and mod commands.

[<img src="docs\readme\help_command_rewrite.png"/>]()

## Configuration and mod author API

The mod persists configuration for suggestion distance, console font, and the four console color roles. Mod authors can use the public [`CheatConsoleController`](Blasphemous.CheatConsoleExtended/CheatConsoleController.cs) API to inspect console state, open or close the console, execute commands programmatically, and control player or API access.

---

## Installation

This mod is available for download through the [Blasphemous Mod Installer](https://github.com/BrandenEK/Blasphemous.Modding.Installer)

Required dependencies (dependencies are auto-downloaded if installed through Mod Installer):
- [Modding API](https://github.com/BrandenEK/Blasphemous.ModdingAPI)
- [Cheat Console](https://github.com/brandenEK/Blasphemous.CheatConsole)

### Manual Installation

1. Download a release ZIP from [Releases](https://github.com/EltonZhang777/Blasphemous.CheatConsoleExtended/releases).
2. Open the game's `Modding` folder.
3. Extract the ZIP contents directly into that folder, preserving the archive's directory structure.

## Requirements

- [ModdingAPI 3.0.0 or later](https://github.com/BrandenEK/Blasphemous.ModdingAPI)
- [Cheat Console](https://github.com/BrandenEK/Blasphemous.CheatConsole)

## Build and test

For development, install the [.NET SDK 9.0.200 or later](https://dotnet.microsoft.com/download/dotnet/9.0) and verify it with `dotnet --version`.

```sh
dotnet restore
dotnet build --configuration Release
dotnet test Blasphemous.CheatConsoleExtended.Tests/Blasphemous.CheatConsoleExtended.Tests.csproj --configuration Release
```

## Contributing

Report bugs and request features in [GitHub Issues](https://github.com/EltonZhang777/Blasphemous.CheatConsoleExtended/issues). 

This project is licensed under the [MIT License](LICENSE).
