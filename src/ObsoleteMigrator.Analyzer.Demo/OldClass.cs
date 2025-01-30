namespace ObsoleteMigrator.Analyzer.Demo;

public class OldClass
{
    public void OldMethod()
    {
        Console.WriteLine("Old Hello, World!");
    }

    public void OldMethodWithALotOfParams(int a, int b, int c, int d)
    {
        Console.WriteLine(a);
        Console.WriteLine(b);
        Console.WriteLine(c);
        Console.WriteLine(d);
    }
}