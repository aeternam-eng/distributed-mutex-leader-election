using System.Text.Json;

public class Process 
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal BatteryPercentage { get; set; } = 0;
    public bool IsFailed { get; set; } = false;
    public bool IsLeader { get; set; } = false;
    public bool HasParent => _currentParent != -1;

    private int _currentParent = -1;
    private IEnumerable<int> _neighbors;
    private Dictionary<int, ProcessState> _processStates;

    private readonly ProcessManager _processManager;

    public Process(int processId, IEnumerable<int> neighboringProcesses, ProcessManager processManager)
    {
        _processManager = processManager;
        _processStates = Enumerable
            .Range(0, _processManager.NumberOfProcesses)
            .ToDictionary(
                p => p,
                p => p == processId
                    ? ProcessState.Current
                    : ProcessState.Undefined);
        _neighbors = neighboringProcesses;

        StartTestingRoutine();
    }

    private async Task<Dictionary<int, decimal>> SendElectionMessages()
    {
        var electionTasks = _neighbors
                .Where(n => _processStates.Any(ps => ps.Key == n && ps.Value == ProcessState.Normal) && n != _currentParent)
                .ToList()
                .Select(async neighbor => await _processManager.SendMessage<Dictionary<int, decimal>>(
                    neighbor,
                    new ElectionProcessMessage() { Data = Id })
                );
        
        var results = await Task.WhenAll(electionTasks);

        return results
            .SelectMany(r => r.Data!)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public async Task<InterProcessMessageResponse<TResponse>> ReceiveMessage<TResponse>(InterProcessMessage message) where TResponse : class
    {
        if (message.Type is InterProcessMessageType.ElectionComplete)
        {
            var completedElectionMessage = message as CompletedElectionProcessMessage;

            Console.WriteLine($"{Label} recebeu uma mensagem de ELEICAO COMPLETA com novo líder P{completedElectionMessage!.Data + 1}");

            if (completedElectionMessage.Data == Id)
            {
                IsLeader = true;
            }
        }

        if (message.Type is InterProcessMessageType.Election)
        {
            var electionMessage = message as ElectionProcessMessage;

            Console.WriteLine($"{Label} recebeu uma mensagem de ELEICAO de P{electionMessage!.Data + 1}");

            if (_currentParent == -1)
            {
                _currentParent = electionMessage!.Data;

                Console.WriteLine($"Setando o pai de {Label} para P{_currentParent + 1}");
            }

            var isLeaf = _processManager.IsLeafNode(Id);

            Console.WriteLine($"{Label} - {(isLeaf ? "é" : "não é")} uma folha");

            if (!isLeaf)
            {
                var results = await SendElectionMessages();

                Console.WriteLine($"{Label} - Dados dos filhos: {JsonSerializer.Serialize(results)}");

                return new InterProcessMessageResponse<TResponse>()
                {
                    Data = results as TResponse
                };
            }
            else
            {
                var result = new Dictionary<int, decimal>()
                {
                    { Id, BatteryPercentage }
                };

                Console.WriteLine($"{Label} - Dado próprio: {JsonSerializer.Serialize(result)}");

                return new InterProcessMessageResponse<TResponse>()
                {
                    Data = result as TResponse
                };
            }
        }

        if (message.Type is InterProcessMessageType.GetCurrentState)
        {
            var states = new InterProcessMessageResponse<TResponse>()
            {
                Data = _processStates as TResponse,
            };
            
            return states;
        }

        return new InterProcessMessageResponse<TResponse>();
    }

    public async Task PerformTest()
    {
        var testedStates = await _processManager.TestByProcessId(Id);
        _processStates = _processStates
            .ToDictionary(
                p => p.Key,
                p => testedStates.Any(ts => ts.Key == p.Key)
                    ? testedStates.First(ts => ts.Key == p.Key).Value
                    : p.Value);
        
        var hasWorkingLeader = _processStates.Any(ps => ps.Key == _processManager.GetLeaderId() && (ps.Value == ProcessState.Normal || ps.Value == ProcessState.Current));
        var isLeaderFailed = _processStates.Any(ps => ps.Key == _processManager.GetLeaderId() && ps.Value == ProcessState.Failed);

        if (!hasWorkingLeader && isLeaderFailed)
        {
            var nodeBatteriesById = await SendElectionMessages();

            var bestOptionFromResults = nodeBatteriesById.MaxBy(item => item.Value);
            var newLeader = bestOptionFromResults.Key;

            if (newLeader == Id)
            {
                IsLeader = true;
            }

            await _processManager.BroadcastMessage(Id, new CompletedElectionProcessMessage()
            {
                Data = newLeader
            });
        }
    }

    public void StartTestingRoutine()
    {
        Task.Factory.StartNew(async () => {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while(true)
            {
                await timer.WaitForNextTickAsync();

                Console.WriteLine($"Iniciando teste de {Label}{(IsLeader ? "(L)" : "")}");

                if (IsFailed)
                    Console.WriteLine($"Processo {Label}: FALHO");
                else 
                {
                    await PerformTest();
                    Console.WriteLine($"Processo {Label}: {JsonSerializer.Serialize(_processStates.Select(ps => ps.Value))}");
                }
            }
        }, TaskCreationOptions.LongRunning);
    }
}
