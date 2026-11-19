// ---------------------------------------------------------------------------------------
// Solution: SealedEnv
// Project: SampleConsoleApp
// Filename: Program.cs
// 
// Last modified: 2026-11-19 14:25
// Created:       2026-11-19 12:11
// 
// Copyright: 2026 Algorithman
// ---------------------------------------------------------------------------------------

using SealedEnv;

// Phase 2 exit criteria: the sample app loads a real record from real environment
// variables (SealedEnv-PLAN.md). Run with, e.g.:
//   SAMPLE_DBCONNECTION="Host=db" dotnet run --project samples/SampleConsoleApp
try
{
    var config = SampleConfig.Load();
    Console.WriteLine(
        $"Loaded {nameof(SampleConfig)}: Port={config.Port}, Db=\"{config.DbConnection}\", " +
        $"Shutdown={config.ShutdownTimeout}, Hosts=[{string.Join(", ", config.Hosts)}]");
    return 0;
}
catch (SealedEnvException exception)
{
    // The aggregated startup report: every bad variable at once.
    Console.Error.WriteLine(exception.Message);
    return 1;
}

[SealedEnv(Prefix = "SAMPLE_")]
internal sealed partial record SampleConfig(
    [EnvKey("PORT", Default = "8080")] int Port,
    [EnvKey(Required = true)] string DbConnection,
    [EnvKey(Default = "00:00:30")] TimeSpan ShutdownTimeout,
    [EnvKey] string[] Hosts);
