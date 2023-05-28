var processManager = new ProcessManager(new()
{
    { 0, new[] { 1, 2, 4 } },
    { 1, new[] { 0, 2, 3 } },
    { 2, new[] { 0, 1, 4 } },
    { 3, new[] { 1 } },
    { 4, new[] { 0, 2 } },
});

while (true)
{
    Console.Write("Digite o número do processo que falhou (1 a 5): ");
    var index = int.TryParse(Console.ReadLine(), out var value) ? value : 1;
    Console.WriteLine($"O processo {index} irá falhar");

    try
    {
        processManager.FailProcess(index - 1);
    }
    catch
    {
        Console.WriteLine($"Erro ao tentar falhar o processo {index}");
    }
}
