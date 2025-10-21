// PdfExporter.cs
using JiraApi.Dto;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO.Compression;

namespace JiraApi.Helper
{
    public class PdfExporter
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public PdfExporter(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task ExportIssuesWithAttachmentsAsync(List<IssueDto> issues, string outputBaseFolder, HttpClient client)
        {
            Directory.CreateDirectory(outputBaseFolder);

            foreach (var issue in issues)
            {
                var createdDate = DateTime.Parse(issue.Created);
                var yearFolder = Path.Combine(outputBaseFolder, createdDate.Year.ToString());
                Directory.CreateDirectory(yearFolder);

                var ticketTempFolder = Path.Combine(yearFolder, issue.Key);
                Directory.CreateDirectory(ticketTempFolder);

                var pdfPath = Path.Combine(ticketTempFolder, issue.Key + ".pdf");
                GenerateIssuePdf(issue, pdfPath);

                // Download attachments to the same folder
                foreach (var attachment in issue.Attachments)
                {
                    var response = await client.GetAsync(attachment.ContentUrl);
                    response.EnsureSuccessStatusCode();
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var attachmentPath = Path.Combine(ticketTempFolder, attachment.FileName);
                    await File.WriteAllBytesAsync(attachmentPath, bytes);
                }

                // Limpiar caracteres inválidos del nombre del archivo
                string safeSummary = string.Concat(issue.Summary
                    .Where(c => !Path.GetInvalidFileNameChars().Contains(c)))
                    .Trim();

                // (Opcional) Limitar la longitud del summary para evitar nombres muy largos
                if (safeSummary.Length > 50)
                    safeSummary = safeSummary.Substring(0, 50);

                // Formar el nombre del zip
                var zipFileName = $"{issue.Key}-({safeSummary}).zip";
                var zipPath = Path.Combine(yearFolder, zipFileName);
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(ticketTempFolder, zipPath);

                try
                {
                    // Asegúrate de liberar cualquier bloqueo de archivo antes de eliminar
                    await WaitForDirectoryReleaseAsync(ticketTempFolder);

                    Directory.Delete(ticketTempFolder, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error procesando el ticket {issue.Key}: {ex.Message}");

                    // Opcional: escribir en un archivo de errores
                    var errorLogPath = Path.Combine(outputBaseFolder, "errores.txt");
                    await File.AppendAllTextAsync(errorLogPath, $"{DateTime.Now}: {issue.Key} - {ex.Message}{Environment.NewLine}");
                }
            }
        }

        private void GenerateIssuePdf(IssueDto issue, string pdfPath)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);

                    page.Content().Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // Label
                                columns.RelativeColumn(3); // Value
                            });

                            void AddRow(string label, string value)
                            {
                                table.Cell().Element(CellStyle).Text(label).Bold();
                                table.Cell().Element(CellStyle).Text(RemoveUnsupportedGlyphs(value ?? ""));
                            }

                            AddRow("Ticket", issue.Key);
                            AddRow("Created", issue.Created);
                            AddRow("Summary", issue.Summary);
                            AddRow("Status", issue.Status);
                            AddRow("Type", issue.IssueType);
                            AddRow("Description", issue.Description);
                        });

                        if (issue.CustomFields.Any())
                        {
                            col.Item().PaddingTop(15).Text("Custom Fields:").Bold().FontSize(14);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(3);
                                });

                                var fieldsWithValue = issue.CustomFields
                                    .Where(cf => !string.IsNullOrWhiteSpace(cf.Value) && cf.Value.Trim() != "<br/>")
                                    .OrderBy(cf => cf.Key)
                                    .ToList();

                                var fieldsEmpty = issue.CustomFields
                                    .Where(cf => string.IsNullOrWhiteSpace(cf.Value) || cf.Value.Trim() == "<br/>")
                                    .OrderBy(cf => cf.Key)
                                    .ToList();

                                foreach (var cf in fieldsWithValue)
                                {
                                    table.Cell().Element(CellStyle).Text(cf.Key + ":").Bold();
                                    table.Cell().Element(CellStyle).Text(RemoveUnsupportedGlyphs(cf.Value));
                                }

                                foreach (var cf in fieldsEmpty)
                                {
                                    table.Cell().Element(CellStyle).Text(cf.Key + ":").FontColor(Colors.Grey.Lighten1);
                                    table.Cell().Element(CellStyle).Text("-").FontColor(Colors.Grey.Lighten1);
                                }
                            });
                        }
                    });
                });
            });

            doc.GeneratePdf(pdfPath);
        }


        static IContainer CellStyle(IContainer container)
        {
            return container
                .PaddingVertical(3)
                .PaddingHorizontal(5)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2);
        }

        private async Task WaitForDirectoryReleaseAsync(string folderPath, int maxRetries = 20, int delayMs = 20000)
        {
            var files = Directory.GetFiles(folderPath);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                bool allFilesAvailable = true;

                foreach (var file in files)
                {
                    try
                    {
                        using (FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                    }
                    catch (IOException)
                    {
                        allFilesAvailable = false;
                        break;
                    }
                }

                if (allFilesAvailable)
                    return;

                await Task.Delay(delayMs);
            }

            throw new IOException($"Timeout waiting for all files in '{folderPath}' to be released.");
        }
        private string RemoveUnsupportedGlyphs(string input)
        {
            // QuestPDF soporta solo caracteres estándar. Filtramos íconos (como FontAwesome) que están en el rango Unicode privado.
            return new string(input
                .Where(c => !char.IsSurrogate(c) && (c < 0xF000 || c > 0xF8FF)) // evita caracteres de uso privado como U+F028
                .ToArray());
        }
    }
}
