namespace Common.Infrastructure;

public static class ReadinessChecks
{
    /// <summary>
    /// The tag of a check /health/ready runs: a dependency the API can't serve without (the database,
    /// say). Register one with <c>services.AddHealthChecks().AddCheck(..., tags: [ReadinessChecks.Tag])</c>.
    /// /health/live runs no checks.
    /// </summary>
    public const string Tag = "ready";
}
