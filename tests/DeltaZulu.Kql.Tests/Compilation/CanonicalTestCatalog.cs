using DeltaZulu.Kql.Compilation;
using Kusto.Language.Symbols;

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
            new KqlColumnSchema("Timestamp", ScalarTypes.DateTime),
            new KqlColumnSchema("DeviceId", ScalarTypes.String),
            new KqlColumnSchema("DeviceName", ScalarTypes.String),
            new KqlColumnSchema("ActionType", ScalarTypes.String),
            new KqlColumnSchema("FileName", ScalarTypes.String),
            new KqlColumnSchema("FolderPath", ScalarTypes.String),
            new KqlColumnSchema("SHA256", ScalarTypes.String),
            new KqlColumnSchema("ProcessId", ScalarTypes.Long),
            new KqlColumnSchema("ProcessCommandLine", ScalarTypes.String),
            new KqlColumnSchema("AccountName", ScalarTypes.String),
            new KqlColumnSchema("InitiatingProcessFileName", ScalarTypes.String),
            new KqlColumnSchema("InitiatingProcessCommandLine", ScalarTypes.String),
            new KqlColumnSchema("ReportId", ScalarTypes.String),
            new KqlColumnSchema("AdditionalFields", ScalarTypes.Dynamic),
        ]),
        new KqlTableSchema("NetworkSession",
        [
            new KqlColumnSchema("Timestamp", ScalarTypes.DateTime),
            new KqlColumnSchema("DeviceName", ScalarTypes.String),
            new KqlColumnSchema("ActionType", ScalarTypes.String),
            new KqlColumnSchema("LocalIP", ScalarTypes.String),
            new KqlColumnSchema("LocalPort", ScalarTypes.Int),
            new KqlColumnSchema("RemoteIP", ScalarTypes.String),
            new KqlColumnSchema("RemotePort", ScalarTypes.Int),
            new KqlColumnSchema("Protocol", ScalarTypes.String),
            new KqlColumnSchema("RemoteUrl", ScalarTypes.String),
            new KqlColumnSchema("LocalIPType", ScalarTypes.String),
            new KqlColumnSchema("RemoteIPType", ScalarTypes.String),
            new KqlColumnSchema("InitiatingProcessFileName", ScalarTypes.String),
            new KqlColumnSchema("InitiatingProcessFolderPath", ScalarTypes.String),
            new KqlColumnSchema("InitiatingProcessId", ScalarTypes.Long),
            new KqlColumnSchema("InitiatingProcessCommandLine", ScalarTypes.String),
            new KqlColumnSchema("InitiatingProcessAccountName", ScalarTypes.String),
            new KqlColumnSchema("InitiatingProcessSHA256", ScalarTypes.String),
            new KqlColumnSchema("ReportId", ScalarTypes.String),
        ]),
    ]);
}
