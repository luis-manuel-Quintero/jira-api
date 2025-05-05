using CsvHelper;
using JiraApi.Dto;
using System.Globalization;
using System.Text;

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

        public void ExportIssuesToCsv(List<IssueDto> issues, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Write headers
            csv.WriteField("Key");
            csv.WriteField("Summary");
            csv.WriteField("Description");
            csv.WriteField("Created");
            csv.WriteField("Status");
            csv.WriteField("AttachmentUrls");
            csv.NextRecord();

            foreach (var issue in issues)
            {
                csv.WriteField(issue.Key);
                csv.WriteField(issue.Summary);
                csv.WriteField(issue.Description);
                csv.WriteField(issue.Created);
                csv.WriteField(issue.Status);

                // Join all attachment URLs into one string
                var urls = issue.Attachments?.Select(a => a.ContentUrl) ?? Enumerable.Empty<string>();
                csv.WriteField(string.Join("; ", urls));

                csv.NextRecord();
            }
        }
    }
}
