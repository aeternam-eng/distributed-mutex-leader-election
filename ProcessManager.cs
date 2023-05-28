public class ProcessManager
{
    private List<Process> _processes;
    private Dictionary<int, IEnumerable<int>> _processStructure;

    public int NumberOfProcesses { get; set; }

    public ProcessManager(Dictionary<int, IEnumerable<int>> processStructure)
    { 
        NumberOfProcesses = processStructure.Count;

        _processStructure = processStructure;
        _processes = Enumerable
            .Range(0, NumberOfProcesses)
            .Select(p => new Process(p, processStructure[p], this)
            {
                Id = p,
                Label = $"P{p + 1}",
                IsFailed = false,
                IsLeader = p == 0,
                BatteryPercentage = Random.Shared.NextInt64() % 100,
            })
            .ToList();
    }

    public void FailProcess(int processId)
    {
        var process = _processes.First(p => p.Id == processId);
        process.IsFailed = true;
    }

    public async Task<InterProcessMessageResponse<TResponse>> SendMessage<TResponse>(int processId, InterProcessMessage message) where TResponse : class
    {
        var process = _processes.First(p => p.Id == processId);

        if (process.IsFailed)
            throw new ApplicationException();

        return await process.ReceiveMessage<TResponse>(message);
    }

    public async Task BroadcastMessage(int sourceProcessId, InterProcessMessage message)
    {
        var processes = _processes.Where(p => p.Id != sourceProcessId && !p.IsFailed);

        var tasks = processes.Select(async p => await p.ReceiveMessage<Object>(message));
        
        await Task.WhenAll(tasks);
    }

    public bool IsLeafNode(int sourceProcessId) => 
        _processes
            .Where(p => _processStructure[sourceProcessId].Any(ps => ps == p.Id))
            .All(p => p.HasParent);

    public int GetLeaderId() => _processes.First(p => p.IsLeader).Id;

    public async Task<Dictionary<int, ProcessState>> TestByProcessId(int sourceProcessId)
    {
        var ordered = _processes.OrderBy(p => p.Id);

        var testedProcessId = (sourceProcessId + 1) % NumberOfProcesses;
        var testedProcess = ordered.ElementAt(testedProcessId);

        var processStates = new Dictionary<int, ProcessState>();

        do
        {
            try
            {
                var testedProcessStates = await SendMessage<Dictionary<int, ProcessState>>(testedProcessId, new() { Type = InterProcessMessageType.GetCurrentState });
                var relevantStates = testedProcessStates.Data!
                    .Where(tps => tps.Key != sourceProcessId && tps.Key != testedProcessId && !processStates.Any(ps => ps.Key == tps.Key))
                    .ToDictionary(tps => tps.Key, tps => tps.Value);

                processStates.Add(testedProcessId, ProcessState.Normal);
                relevantStates
                    .ToList()
                    .ForEach(rs => processStates.Add(rs.Key, rs.Value));

                break;
            }
            catch
            {
                processStates.Add(testedProcessId, ProcessState.Failed);
                testedProcessId = (testedProcessId + 1) % NumberOfProcesses;
            }
        } while (testedProcessId != sourceProcessId);

        return await Task.FromResult(processStates);
    }
}
