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

        public JiraController(JiraService jiraService, CsvExporter csvExporter)
        {
            _jiraService = jiraService;
            _csvExporter = csvExporter;
        }

        [HttpGet("issues-proyecto/{projectKey}")]
        public async Task<IActionResult> ExportIssues(string projectKey)
        {
            var issues = await _jiraService.GetIssuesByProjectAsync(projectKey);
            var csvFile = $"{projectKey}_issues.csv";
            _csvExporter.ExportIssuesToCsv(issues, csvFile);

            var allAttachments = issues.SelectMany(i => i.Attachments).ToList();
            var folder = $"attachments_{projectKey}";
            await _jiraService.DownloadAttachmentsAsync(allAttachments, folder);

            return Ok(new
            {
                Message = $"Exported {issues.Count} issues and {allAttachments.Count} attachments.",
                IssuesCsvFile = csvFile,
                AttachmentsFolder = folder
            });
        }
    }
}