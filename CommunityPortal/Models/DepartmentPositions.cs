using System.Collections.Generic;

namespace CommunityPortal.Models
{
    public static class DepartmentPositions
    {
        public static readonly Dictionary<string, List<string>> Mappings = new()
        {
            {
                "Maintenance Department", new List<string>
                {
                    "Maintenance Supervisor",
                    "General Maintenance Worker",
                    "Plumber",
                    "Electrician",
                    "HVAC Technician",
                    "Carpenter"
                }
            },
            {
                "Security Department", new List<string>
                {
                    "Security Supervisor",
                    "Security Guard",
                    "CCTV Operator",
                    "Access Control Officer"
                }
            },
            {
                "Housekeeping & Sanitation Department", new List<string>
                {
                    "Housekeeping Supervisor",
                    "Janitor/Cleaner",
                    "Waste Management Staff"
                }
            },
            {
                "Landscaping & Gardening Department", new List<string>
                {
                    "Landscape Supervisor",
                    "Gardener"
                }
            },
            {
                "Engineering & Construction Department", new List<string>
                {
                    "Civil Engineer",
                    "Project Manager",
                    "Mason/Painter"
                }
            }
        };

        public static List<string> GetAllDepartments()
        {
            return new List<string>(Mappings.Keys);
        }

        public static List<string> GetPositionsForDepartment(string department)
        {
            return Mappings.TryGetValue(department, out var positions) ? positions : new List<string>();
        }
    }
} 