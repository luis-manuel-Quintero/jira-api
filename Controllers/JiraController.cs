using Microsoft.AspNetCore.Mvc;
using JiraApi.Services;
using JiraApi.Helper;

namespace JiraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JiraController : ControllerBase
    {
        private readonly CsvExporter _csvExporter;

        public JiraController(CsvExporter csvExporter)
        {
            _csvExporter = csvExporter;
        }

        [HttpGet("usuarios-proyectos")]
        public async Task<IActionResult> GetUsuariosProyectos()
        {
            var baseUrl = "https://encoracsa.atlassian.net/";
            var email = "email";
            var token = "api-key";

            var jiraService = new JiraService(baseUrl, email, token);
            var data = await jiraService.GetUsersByProjectAsync();

            return Ok(data);
        }

        [HttpGet("issues-proyecto/{projectKey}")]
        public async Task<IActionResult> ExportIssues(string projectKey)
        {
            var baseUrl = "https://encoracsa.atlassian.net/";
            var email = "email";
            var token = "api-key";

            var _jiraService = new JiraService(baseUrl, email, token);
            var issues = await _jiraService.GetIssuesByProjectAsync(projectKey);
            _csvExporter.ExportIssuesToCsv(issues, $"{projectKey}_issues.csv");

            var allAttachments = issues.SelectMany(i => i.Attachments).ToList();
            await _jiraService.DownloadAttachmentsAsync(allAttachments, $"attachments_{projectKey}");

            return Ok(new
            {
                message = $"Exportados {issues.Count} issues y {allAttachments.Count} archivos adjuntos",
                issuesCsv = $"{projectKey}_issues.csv",
                attachmentsFolder = $"attachments_{projectKey}/"
            });
        }
    }
}
