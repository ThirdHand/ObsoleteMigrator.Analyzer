namespace ObsoleteMigrator.Analyzer.Demo;

internal class Program
{
    private static void Main()
    {
        Console.WriteLine("Hello, World!");
    }
}

internal class TestClass
{
    public void TestMethod()
    {
        var oldClass = new OldClass();
        oldClass.OldMethodWithALotOfParams(1, 2, 3, 4);
    }
}