using Microsoft.AspNetCore.Mvc;
using JiraApi.Services;
using JiraApi.Model;
using JiraApi.Helper;
using System.Net.Http;
using System.IO.Compression;
using JiraApi.Dto;

namespace JiraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JiraController : ControllerBase
    {
        private readonly JiraService _jiraService;
        private readonly CsvExporter _csvExporter;
        private readonly ExcelExporter _excelExporter;
        private readonly PdfExporter _pdfExporter;

        public JiraController(JiraService jiraService, CsvExporter csvExporter, ExcelExporter excelExporter, PdfExporter pdfExporter)
        {
            _jiraService = jiraService;
            _csvExporter = csvExporter;
            _excelExporter = excelExporter;
            _pdfExporter = pdfExporter;
        }

        //[HttpGet("issues-proyecto/{projectKey}")]
        //public async Task<IActionResult> ExportIssues(
        //    string projectKey,
        //    [FromQuery(Name = "customFields")] string[] customFields   // e.g. ?customFields=dirreccion&customFields=responsable
        //)
        //{
        //    // Pass the list into your service:
        //    var issues = await _jiraService.GetIssuesByProjectAsync(projectKey, customFields);

        //    var fileName = $"{projectKey}_issues.xlsx";

        //    _excelExporter.ExportIssuesToExcel(issues, fileName, customFields);

        //    var fileBytes = await System.IO.File.ReadAllBytesAsync(fileName);

        //    // Export to CSV, including custom fields:
        //    //var csvFile = $"{projectKey}_issues.csv";
        //    //_csvExporter.ExportIssuesToCsv(issues, csvFile, customFields);
        //    var totalAttachments = issues.SelectMany(i => i.Attachments).Count();
        //    // Log or print the total (you can log or return in headers)
        //    Console.WriteLine($"Exported {issues.Count} issues with {totalAttachments} total attachments.");

        //    // Optional: return metadata in headers
        //    Response.Headers.Add("X-Issue-Count", issues.Count.ToString());
        //    Response.Headers.Add("X-Attachment-Count", totalAttachments.ToString());

        //    //var allAttachments = issues.SelectMany(i => i.Attachments).ToList();
        //    //var folder = $"attachments_{projectKey}";
        //    //await _jiraService.DownloadAttachmentsAsync(issues, folder);

        //    return File(
        //                fileBytes,
        //                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        //                fileName
        //            );
        //}


        // ✅ Modified controller to pass HttpClient properly from JiraService
        [HttpGet("issue-unico-pdf/{projectKey}/{ticketKey}")]
        public async Task<IActionResult> ExportSingleIssueToPdf(
    string projectKey,
    string ticketKey
)
        {
            // 🔹 Obtener todos los issues del proyecto
            var issues = await _jiraService.GetIssuesByProjectWithAllCustomFieldsAsync(projectKey, includeComments: true);
            var totalcount = issues.Count;

            // 🔹 Filtrar solo el ticket solicitado
            var issue = issues.FirstOrDefault(i => i.Key == ticketKey);
            if (issue == null)
            {
                return BadRequest(new
                {
                    Error = $"El ticket '{ticketKey}' no fue encontrado en el proyecto '{projectKey}'."
                });
            }

            // 🔹 Crear lista con solo ese ticket
            var filteredList = new List<IssueDto> { issue };

            var outputFolder = $"export_{ticketKey}";
            var httpClient = _jiraService.GetHttpClient();
            await _pdfExporter.ExportIssuesWithAttachmentsAsync(filteredList, outputFolder, httpClient);

            // ✅ Crear archivo summary.txt
            var summaryPath = Path.Combine(outputFolder, $"Ticket exportado {ticketKey}.txt");
            var summaryContent = $"Se exportó el ticket: {ticketKey} de un total de {totalcount} tickets del proyecto.";
            await System.IO.File.WriteAllTextAsync(summaryPath, summaryContent);

            // ✅ Devolver mensaje de éxito
            return Ok(new
            {
                Message = $"Ticket {ticketKey} exportado exitosamente a la carpeta {outputFolder}",
                OutputDirectory = Path.GetFullPath(outputFolder),
                SummaryFile = summaryPath
            });
        }

        [HttpGet("issues-proyecto-pdf/{projectKey}")]
        public async Task<IActionResult> ExportIssuesToPdf(
            string projectKey,
            [FromQuery] string? startFromKey = null // <-- nuevo parámetro opcional
        )
        {
            // Obtener todos los issues
            var issues = await _jiraService.GetIssuesByProjectWithAllCustomFieldsAsync(projectKey, includeComments: true); ;
            var totalcount = issues.Count;
            Console.WriteLine("the key is " + startFromKey);
            // Si se especificó una clave de inicio, filtrar desde ahí
            if (!string.IsNullOrEmpty(startFromKey))
            {
                var index = issues.FindIndex(i => i.Key == startFromKey);
                if (index >= 0)
                    issues = issues.Skip(index).ToList();
                else
                {
                    return BadRequest(new
                    {
                        Error = $"The issue key '{startFromKey}' was not found in project '{projectKey}'."
                    });
                }
            }

            var outputFolder = $"export_{projectKey}";
            var httpClient = _jiraService.GetHttpClient();
            await _pdfExporter.ExportIssuesWithAttachmentsAsync(issues, outputFolder, httpClient);

            // ✅ Crear archivo summary.txt con total de tickets exportados
            var summaryPath = Path.Combine(outputFolder, $"Total tickets exported {totalcount}.txt");
            var summaryContent = $"Total tickets exported: {totalcount}";
            await System.IO.File.WriteAllTextAsync(summaryPath, summaryContent);

            // ✅ Devolver mensaje de éxito (sin comprimir la carpeta final)
            return Ok(new
            {
                Message = $"Exported {issues.Count} tickets to folder {outputFolder}",
                OutputDirectory = Path.GetFullPath(outputFolder),
                SummaryFile = summaryPath
            });


        }
    }
}