using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcessState
{
    Undefined,
    Failed,
    Normal,
    Current
}
