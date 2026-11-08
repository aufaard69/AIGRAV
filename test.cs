using System;
using System.Reflection;
using Autodesk.Revit.DB.Structure;
namespace Test {
    class Program {
        static void Main() {
            var t = typeof(StructuralConnectionHandler);
            foreach(var m in t.GetMethods()) {
                Console.WriteLine(m.Name);
            }
        }
    }
}
