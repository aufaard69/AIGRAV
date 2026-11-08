using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.RotateColumn
{
    public class RotateColumnSkill : IAIGRAVSkill
    {
        public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
        {
            Name = "Rotate Column Skill",
            Version = "1.0",
            Category = "Modeling",
            Description = "Rotates structural columns by 90 degrees",
            Source = "External",
            Priority = 100
        };

        public bool CanHandle(string toolName) => toolName == "aigrav_rotate_column";

        public string Execute(string toolName, UIApplication app, string? argument = null)
        {
            Document doc = app.ActiveUIDocument.Document;
            var columns = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            if (columns.Count == 0) return "FAIL: No structural columns found.";

            int count = 0;
            using (Transaction tx = new Transaction(doc, "Rotate Columns 90 Degrees"))
            {
                tx.Start();
                foreach (var col in columns)
                {
                    LocationPoint loc = col.Location as LocationPoint;
                    if (loc != null)
                    {
                        // Rotate 90 degrees (PI / 2)
                        Line axis = Line.CreateBound(loc.Point, loc.Point + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, col.Id, axis, Math.PI / 2);
                        count++;
                    }
                }
                tx.Commit();
            }

            return $"SUCCESS: Rotated {count} structural columns by 90 degrees.";
        }
    }
}
