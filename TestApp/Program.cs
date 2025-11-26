// See https://aka.ms/new-console-template for more information
using ORM_v1.core;

namespace TestApp;

class Program
{
    static void Main(string[] args)
    {
        var obj = new DbContext();
        obj.TestPrint();
    }
}

