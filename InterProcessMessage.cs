public enum InterProcessMessageType
{
    GetCurrentState,
    Election,
    ElectionPartialResults,
    ElectionComplete,
}

public record InterProcessMessage
{
    public virtual InterProcessMessageType Type { get; init; }
}

public record InterProcessMessage<TData> : InterProcessMessage
{
    public TData? Data { get; init; }
}

public record ElectionProcessMessage : InterProcessMessage<int>
{
    public override InterProcessMessageType Type => InterProcessMessageType.Election;
}

public record CompletedElectionProcessMessage : InterProcessMessage<int>
{
    public override InterProcessMessageType Type => InterProcessMessageType.ElectionComplete;
}
