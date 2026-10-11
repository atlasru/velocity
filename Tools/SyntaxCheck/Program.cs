using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class Program
{
    private static int Main()
    {
        int errors=0,count=0;
        foreach(string file in Directory.GetFiles("Assets","*.cs",SearchOption.AllDirectories))
        {
            count++;var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.CSharp9),file);
            foreach(Diagnostic d in tree.GetDiagnostics())if(d.Severity==DiagnosticSeverity.Error){Console.WriteLine(d);errors++;}
        }
        Console.WriteLine($"C# syntax: {count} files, {errors} errors. Unity API compilation still requires Editor.");return errors==0?0:1;
    }
}
