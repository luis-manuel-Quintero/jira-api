using CsvHelper;
using JiraApi.Dto;
using System.Globalization;

namespace JiraApi.Helper
{
    public class CsvExporter
    {
        public void ExportProjectsToCsv(IEnumerable<ProjectDto> projects, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(projects);
        }

        public void ExportIssuesToCsv(IEnumerable<IssueDto> issues, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(issues);
        }
    }
}
