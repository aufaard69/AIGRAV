using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using AIGRAV.Contracts;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.ConnectionNativeTest
{
    public sealed class ConnectionNativeTestSkill : IAIGRAVSkill
    {
        public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo { Name="Native Test", Version="1.0", Category="Test" };
        public bool CanHandle(string toolName) => toolName == "test_methods";
        public string Execute(string toolName, UIApplication app, string argument = null)
        {
            var methods = typeof(StructuralConnectionSettings).GetMethods().Select(m => m.Name).Distinct();
            return string.Join("\n", methods);
        }
    }
}
