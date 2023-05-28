public record InterProcessMessageResponse<TData>
{
    public TData? Data { get; init; } = default(TData);
}
