namespace HeatingDaemon;

/// <summary>
/// Configuration for the long-term passive telegram statistics.
/// The statistics are independent of the <c>msg_scan</c> parameter and are
/// written to a plain text file.
/// </summary>
public class PassiveTelegramLogConfig
{
    /// <summary>
    /// Enables or disables the passive telegram statistics.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Path of the plain text statistics file.
    /// </summary>
    public string OutputFile { get; set; } = "passive-telegrams.txt";

    /// <summary>
    /// Interval for writing the statistics file in ISO 8601 format (e.g. P30D, P1D, PT6H).
    /// </summary>
    public string FlushInterval { get; set; } = "P30D";

    /// <summary>
    /// If true, telegrams with an unknown Elster index are collected as well.
    /// </summary>
    public bool IncludeUnknownIndices { get; set; } = false;

    /// <summary>
    /// If true, a SIGUSR1 signal triggers an immediate write of the statistics file.
    /// </summary>
    public bool DumpOnSigusr1 { get; set; } = true;
}
