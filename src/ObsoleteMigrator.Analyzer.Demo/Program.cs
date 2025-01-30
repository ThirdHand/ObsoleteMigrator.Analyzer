namespace ObsoleteMigrator.Analyzer.Demo;

internal class Program
{
    private static void Main()
    {
        var oldClass = new OldClass();
        oldClass.OldMethodWithALotOfParams(1, 2, 3, 4);
    }
}