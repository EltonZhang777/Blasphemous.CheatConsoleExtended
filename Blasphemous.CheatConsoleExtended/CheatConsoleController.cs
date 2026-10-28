using Blasphemous.ModdingAPI;
using Gameplay.UI.Console;
using Gameplay.UI.Widgets;
using System;

namespace Blasphemous.CheatConsoleExtended;

/// <summary>Provides programmatic access to the game's cheat console.</summary>
public static class CheatConsoleController
{
    private static bool _isUserEnabled = true;
    private static bool _isApiEnabled = true;

    [ThreadStatic]
    private static int _playerInputDepth;

    [ThreadStatic]
    private static int _playerSubmissionDepth;

    [ThreadStatic]
    private static int _playerCommandDepth;

    [ThreadStatic]
    private static int _apiAccessDepth;

    [ThreadStatic]
    private static ConsoleWidget _playerOpenRequest;

    [ThreadStatic]
    private static bool _playerOpenRequestConsumed;

    [ThreadStatic]
    private static bool _playerOpenRequestCaptured;

    /// <summary>Gets whether player-entered commands are allowed.</summary>
    public static bool IsUserEnabled => _isUserEnabled;

    /// <summary>Gets whether other mods can access the console programmatically.</summary>
    public static bool IsApiEnabled => _isApiEnabled;

    /// <summary>Enables or disables player-entered command execution.</summary>
    public static void SetUserEnabled(bool enabled) => _isUserEnabled = enabled;

    /// <summary>Enables or disables programmatic console access from mods.</summary>
    public static void SetApiAccessEnabled(bool enabled) => _isApiEnabled = enabled;

    /// <summary>Gets whether the active console widget is open.</summary>
    public static bool IsConsoleWidgetOpen
    {
        get
        {
            ConsoleWidget console = ConsoleWidget.Instance;
            return console != null && console.IsEnabled();
        }
    }

    /// <summary>Opens or closes the active console widget.</summary>
    /// <exception cref="InvalidOperationException">The console widget has not been initialized.</exception>
    public static void SetConsoleOpen(bool open)
    {
        ConsoleWidget console = ConsoleWidget.Instance ?? throw new InvalidOperationException("The console widget is not available yet.");
        int previousApiDepth = EnterApiAccess();
        try
        {
            console.SetEnabled(open);
        }
        finally
        {
            _apiAccessDepth = previousApiDepth;
        }
    }

    /// <summary>Submits a command through the game's existing parser, whether or not its UI is open.</summary>
    /// <returns>True when a console widget accepted the command; otherwise, false.</returns>
    public static bool ExecuteCommand(string command)
    {
        ConsoleWidget console = ConsoleWidget.Instance;
        if (console == null || !_isApiEnabled)
            return false;

        int previousApiDepth = EnterApiAccess();
        try
        {
            console.ProcessCommand(command);
        }
        finally
        {
            _apiAccessDepth = previousApiDepth;
        }

        return true;
    }

    /// <summary>Determines whether a request to change the console state is allowed.</summary>
    /// <param name="console">The console widget receiving the request.</param>
    /// <param name="open">Whether the request opens or closes the console.</param>
    /// <returns>True when the request is allowed; otherwise, false.</returns>
    internal static bool CanSetConsoleOpen(ConsoleWidget console, bool open)
    {
        bool isPlayerOpenRequest = open
            && ReferenceEquals(_playerOpenRequest, console)
            && !_playerOpenRequestConsumed
            && _apiAccessDepth == 0;

        if (isPlayerOpenRequest)
            _playerOpenRequestConsumed = true;

        return !open || isPlayerOpenRequest || _isApiEnabled;
    }

    /// <summary>Captures an unconsumed player request to open the specified console.</summary>
    /// <param name="console">The console widget receiving the request.</param>
    /// <returns>True when the request was captured; otherwise, false.</returns>
    internal static bool TryCapturePlayerOpenRequest(ConsoleWidget console)
    {
        if (console == null
            || !ReferenceEquals(_playerOpenRequest, console)
            || _apiAccessDepth > 0
            || _playerOpenRequestCaptured)
            return false;

        _playerOpenRequestCaptured = true;
        return true;
    }

    /// <summary>Begins tracking a player submission and determines whether it may proceed.</summary>
    /// <param name="console">The console receiving the submission.</param>
    /// <param name="previousSubmissionDepth">Receives the submission depth to restore when processing ends.</param>
    /// <returns>True when the submission may proceed; otherwise, false.</returns>
    internal static bool BeginPlayerSubmission(ConsoleWidget console, out int previousSubmissionDepth)
    {
        previousSubmissionDepth = _playerSubmissionDepth;
        _playerSubmissionDepth = 0;

        string input = console.input.text;
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(input.Trim()))
            return true;

        if (!ConsumePlayerInput())
            return _isApiEnabled;

        if (_isUserEnabled)
        {
            _playerSubmissionDepth = 1;
            return true;
        }

        ReportUserCommandRejected(console);
        return false;
    }

    /// <summary>Restores the submission depth saved by <see cref="BeginPlayerSubmission"/>.</summary>
    /// <param name="previousSubmissionDepth">The submission depth to restore.</param>
    internal static void EndPlayerSubmission(int previousSubmissionDepth)
    {
        _playerSubmissionDepth = previousSubmissionDepth;
    }

    /// <summary>Determines whether a command may execute and tracks player-originated execution.</summary>
    /// <param name="console">The console processing the command.</param>
    /// <param name="previousCommandDepth">Receives the command depth to restore when execution ends.</param>
    /// <returns>True when command execution is allowed; otherwise, false.</returns>
    internal static bool BeginCommandExecution(ConsoleWidget console, out int previousCommandDepth)
    {
        previousCommandDepth = _playerCommandDepth;
        if (ConsumePlayerInput())
        {
            if (!_isUserEnabled)
            {
                ReportUserCommandRejected(console);
                return false;
            }

            _playerCommandDepth = previousCommandDepth + 1;
            return true;
        }

        if (!_isApiEnabled)
            return false;

        _playerCommandDepth = 0;
        return true;
    }

    /// <summary>Restores the command depth saved by <see cref="BeginCommandExecution"/>.</summary>
    /// <param name="previousCommandDepth">The command depth to restore.</param>
    internal static void EndCommandExecution(int previousCommandDepth)
    {
        _playerCommandDepth = previousCommandDepth;
    }

    /// <summary>Processes a submitted command while preserving its player-input context.</summary>
    /// <param name="console">The console processing the command.</param>
    /// <param name="command">The command text to process.</param>
    internal static void ProcessCommandFromSubmit(ConsoleWidget console, string command)
    {
        int previousPlayerDepth = _playerInputDepth;
        if (_playerSubmissionDepth > 0 && _apiAccessDepth == 0)
            _playerInputDepth++;

        try
        {
            console.ProcessCommand(command);
        }
        finally
        {
            _playerInputDepth = previousPlayerDepth;
        }
    }

    /// <summary>Executes a parsed console command while preserving its player-input context.</summary>
    /// <param name="command">The parsed console command to execute.</param>
    /// <param name="name">The command name.</param>
    /// <param name="parameters">The command parameters.</param>
    internal static void ExecuteConsoleCommandFromParser(ConsoleCommand command, string name, string[] parameters)
    {
        int previousPlayerDepth = _playerInputDepth;
        int previousCommandDepth = _playerCommandDepth;
        bool isPlayerCommand = previousCommandDepth > 0
            && _apiAccessDepth == 0
            && command.GetType().Assembly == typeof(ConsoleCommand).Assembly;

        _playerInputDepth = isPlayerCommand ? previousPlayerDepth + 1 : 0;
        if (!isPlayerCommand)
            _playerCommandDepth = 0;

        try
        {
            command.Execute(name, parameters);
        }
        finally
        {
            _playerInputDepth = previousPlayerDepth;
            _playerCommandDepth = previousCommandDepth;
        }
    }

    /// <summary>Executes a shared command while preserving its player-input context.</summary>
    /// <param name="command">The shared-command handler.</param>
    /// <param name="name">The shared command name.</param>
    /// <returns>True when the shared command handled the name; otherwise, false.</returns>
    internal static bool ExecuteSharedCommandFromParser(SharedCommandsCommand command, string name)
    {
        int previousPlayerDepth = _playerInputDepth;
        int previousCommandDepth = _playerCommandDepth;
        bool isPlayerCommand = previousCommandDepth > 0 && _apiAccessDepth == 0;

        _playerInputDepth = isPlayerCommand ? previousPlayerDepth + 1 : 0;
        if (!isPlayerCommand)
            _playerCommandDepth = 0;

        try
        {
            return command.ExecuteIfIsCommand(name);
        }
        finally
        {
            _playerInputDepth = previousPlayerDepth;
            _playerCommandDepth = previousCommandDepth;
        }
    }

    /// <summary>Changes the console state while marking the request as player input.</summary>
    /// <param name="console">The console widget to change.</param>
    /// <param name="open">Whether to open or close the console.</param>
    internal static void SetEnabledFromPlayerInput(ConsoleWidget console, bool open)
    {
        ConsoleWidget previousPlayerOpenRequest = _playerOpenRequest;
        bool previousPlayerOpenRequestConsumed = _playerOpenRequestConsumed;
        bool previousPlayerOpenRequestCaptured = _playerOpenRequestCaptured;
        _playerOpenRequest = open ? console : null;
        _playerOpenRequestConsumed = false;
        _playerOpenRequestCaptured = false;

        try
        {
            console.SetEnabled(open);
        }
        finally
        {
            _playerOpenRequest = previousPlayerOpenRequest;
            _playerOpenRequestConsumed = previousPlayerOpenRequestConsumed;
            _playerOpenRequestCaptured = previousPlayerOpenRequestCaptured;
        }
    }

    /// <summary>Submits the current console input while marking it as player input.</summary>
    /// <param name="console">The console widget submitting its input.</param>
    internal static void SubmitFromPlayerInput(ConsoleWidget console)
    {
        int previousPlayerDepth = EnterPlayerInput();
        try
        {
            console.Submit();
        }
        finally
        {
            _playerInputDepth = previousPlayerDepth;
        }
    }

    /// <summary>Consumes the pending input marker and reports whether it represents player input.</summary>
    /// <returns>True when the pending input came from the player; otherwise, false.</returns>
    private static bool ConsumePlayerInput()
    {
        bool isPlayerInput = _playerInputDepth > 0 && _apiAccessDepth == 0;
        _playerInputDepth = 0;
        return isPlayerInput;
    }

    /// <summary>Reports that a player command was rejected to the console and mod log.</summary>
    /// <param name="console">The console to receive the rejection message.</param>
    private static void ReportUserCommandRejected(ConsoleWidget console)
    {
        const string message = "Error: Player command rejected because user access is disabled.";
        console.Write(message);
        console.scrollRect?.verticalNormalizedPosition = 0f;
        ModLog.Error("Cheat Console: " + message);
    }

    /// <summary>Marks the current operation as player input.</summary>
    /// <returns>The previous player-input depth.</returns>
    private static int EnterPlayerInput()
    {
        int previousDepth = _playerInputDepth;
        _playerInputDepth++;
        return previousDepth;
    }

    /// <summary>Marks the current operation as programmatic API access.</summary>
    /// <returns>The previous API-access depth.</returns>
    private static int EnterApiAccess()
    {
        int previousDepth = _apiAccessDepth;
        _apiAccessDepth++;
        return previousDepth;
    }
}