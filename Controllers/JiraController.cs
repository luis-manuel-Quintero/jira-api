using Microsoft.AspNetCore.Mvc;
using JiraApi.Services;
using JiraApi.Model;
using JiraApi.Helper;

namespace JiraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JiraController : ControllerBase
    {
        private readonly JiraService _jiraService;
        private readonly CsvExporter _csvExporter;
        private readonly ExcelExporter _excelExporter;

        public JiraController(JiraService jiraService, CsvExporter csvExporter, ExcelExporter excelExporter)
        {
            _jiraService = jiraService;
            _csvExporter = csvExporter;
            _excelExporter = excelExporter;
        }

        [HttpGet("issues-proyecto/{projectKey}")]
        public async Task<IActionResult> ExportIssues(
            string projectKey,
            [FromQuery(Name = "customFields")] string[] customFields   // e.g. ?customFields=dirreccion&customFields=responsable
        )
        {
            // Pass the list into your service:
            var issues = await _jiraService.GetIssuesByProjectAsync(projectKey, customFields);

            var fileName = $"{projectKey}_issues.xlsx";

            _excelExporter.ExportIssuesToExcel(issues, fileName, customFields);

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fileName);

            // Export to CSV, including custom fields:
            //var csvFile = $"{projectKey}_issues.csv";
            //_csvExporter.ExportIssuesToCsv(issues, csvFile, customFields);
            var totalAttachments = issues.SelectMany(i => i.Attachments).Count();
            // Log or print the total (you can log or return in headers)
            Console.WriteLine($"Exported {issues.Count} issues with {totalAttachments} total attachments.");

            // Optional: return metadata in headers
            Response.Headers.Add("X-Issue-Count", issues.Count.ToString());
            Response.Headers.Add("X-Attachment-Count", totalAttachments.ToString());

            //var allAttachments = issues.SelectMany(i => i.Attachments).ToList();
            //var folder = $"attachments_{projectKey}";
            //await _jiraService.DownloadAttachmentsAsync(issues, folder);

            return File(
                        fileBytes,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        fileName
                    );
        }
    }
}