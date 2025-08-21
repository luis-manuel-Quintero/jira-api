using CsvHelper;
using CsvHelper.Configuration;
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

        public void ExportIssuesToCsv(
            List<IssueDto> issues,
            string filePath,
            string[] customFields
        )
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                ShouldQuote = args => true
            };

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, config);

            // Write headers: standard + customs
            var headers = new[]
            {
                    "Key",
                    "Summary",
                    "Created",
                    "Status",
                    "IssueType",
                    "AttachmentUrls"
                }
            .Concat(customFields)
            .Concat(new[] { "Comments", "Description" });
            foreach (var h in headers) csv.WriteField(h);
            csv.NextRecord();

            foreach (var issue in issues)
            {
                // standard fields
                csv.WriteField(issue.Key);
                csv.WriteField(issue.IssueType);
                csv.WriteField(issue.Summary);
                csv.WriteField(issue.Created);
                csv.WriteField(issue.Status);

                // attachments as before
                var urls = issue.Attachments.Select(a => a.ContentUrl);
                csv.WriteField(string.Join(";", urls));

                // custom fields
                foreach (var cf in customFields)
                {
                    issue.CustomFields.TryGetValue(cf, out var val);
                    csv.WriteField(val);
                }
                // Comments: join each as "Author (Date): Body"
                var comments = issue.Comments
                    .Select(c => $"{c.Author} ({c.Created}): {c.Body.Replace("\r\n", " ").Replace("\n", " ")}");
                csv.WriteField(string.Join(" | ", comments));

                csv.WriteField(issue.Description);

                csv.NextRecord();
            }
        }
    }
}
