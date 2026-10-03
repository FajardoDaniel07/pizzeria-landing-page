using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Pizzeria.Tests.TestSupport;

/// <summary>
/// Captures everything the app logs (message and exception text) so tests can assert that
/// no personal data reaches the logs.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Text)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, string Text)> Entries => _entries;

    /// <summary>Every captured entry joined in one string.</summary>
    public string AllText => string.Join(Environment.NewLine, _entries.Select(entry => entry.Text));

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string category, ConcurrentQueue<(LogLevel, string)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue((logLevel, $"{category}: {formatter(state, exception)} {exception}"));
        }
    }
}
