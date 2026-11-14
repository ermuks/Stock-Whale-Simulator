using System;
using System.IO;

/// <summary>A failed audit disk must not crash trading. In-memory auditing continues.</summary>
public sealed class SocialLogSink : IDisposable
{
    private TextWriter writer;
    private readonly Action<string> unavailable;
    public bool Available => writer != null;
    public SocialLogSink(TextWriter writer, Action<string> unavailable) { this.writer = writer; this.unavailable = unavailable; }
    public void Write(string value) { Run(output => output.WriteLine(value)); }
    public void Flush() { Run(output => output.Flush()); }
    private void Run(Action<TextWriter> action)
    {
        if (writer == null) { return; }
        try { action(writer); }
        catch (IOException error) { Fail(error); }
        catch (UnauthorizedAccessException error) { Fail(error); }
    }
    private void Fail(Exception error)
    {
        var failed = writer; writer = null;
        try { failed?.Dispose(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        unavailable?.Invoke(error.Message);
    }
    public void Dispose()
    {
        Run(output => output.Dispose()); writer = null;
    }
}
