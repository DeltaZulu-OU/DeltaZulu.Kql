using DeltaZulu.Kql.Compilation;

namespace DeltaZulu.Kql.Tests.Compilation;

/// <summary>
/// A fixed two-table schema catalog mirroring two of Platform's golden canonical
/// views (ProcessEvent, NetworkSession), used by the migrated translator test
/// corpus. Column names and types are copied verbatim from
/// DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Golden.GoldenEventContracts
/// so translation and binding behavior matches what the original Platform tests
/// exercised.
/// </summary>
internal static class CanonicalTestCatalog
{
    public static KqlSchemaCatalog Instance { get; } = new(
    [
        new KqlTableSchema("ProcessEvent",
        [
            new KqlColumnSchema("Timestamp", "datetime"),
            new KqlColumnSchema("DeviceId", "string"),
            new KqlColumnSchema("DeviceName", "string"),
            new KqlColumnSchema("ActionType", "string"),
            new KqlColumnSchema("FileName", "string"),
            new KqlColumnSchema("FolderPath", "string"),
            new KqlColumnSchema("SHA256", "string"),
            new KqlColumnSchema("ProcessId", "long"),
            new KqlColumnSchema("ProcessCommandLine", "string"),
            new KqlColumnSchema("AccountName", "string"),
            new KqlColumnSchema("InitiatingProcessFileName", "string"),
            new KqlColumnSchema("InitiatingProcessCommandLine", "string"),
            new KqlColumnSchema("ReportId", "string"),
            new KqlColumnSchema("AdditionalFields", "dynamic"),
        ]),
        new KqlTableSchema("NetworkSession",
        [
            new KqlColumnSchema("Timestamp", "datetime"),
            new KqlColumnSchema("DeviceName", "string"),
            new KqlColumnSchema("ActionType", "string"),
            new KqlColumnSchema("LocalIP", "string"),
            new KqlColumnSchema("LocalPort", "int"),
            new KqlColumnSchema("RemoteIP", "string"),
            new KqlColumnSchema("RemotePort", "int"),
            new KqlColumnSchema("Protocol", "string"),
            new KqlColumnSchema("RemoteUrl", "string"),
            new KqlColumnSchema("LocalIPType", "string"),
            new KqlColumnSchema("RemoteIPType", "string"),
            new KqlColumnSchema("InitiatingProcessFileName", "string"),
            new KqlColumnSchema("InitiatingProcessFolderPath", "string"),
            new KqlColumnSchema("InitiatingProcessId", "long"),
            new KqlColumnSchema("InitiatingProcessCommandLine", "string"),
            new KqlColumnSchema("InitiatingProcessAccountName", "string"),
            new KqlColumnSchema("InitiatingProcessSHA256", "string"),
            new KqlColumnSchema("ReportId", "string"),
        ]),
    ]);
}
