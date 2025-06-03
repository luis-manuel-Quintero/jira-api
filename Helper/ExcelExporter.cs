using ClosedXML.Excel;
using JiraApi.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JiraApi.Helper
{
    public class ExcelExporter
    {
        private const int MaxCellChars = 32767;

        // Splits a long string into substrings of at most MaxCellChars
        private static IEnumerable<string> ChunkText(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            for (int i = 0; i < text.Length; i += MaxCellChars)
                yield return text.Substring(i, Math.Min(MaxCellChars, text.Length - i));
        }

        public void ExportIssuesToExcel(
            List<IssueDto> issues,
            string filePath,
            string[] customFields
        )
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Issues");

            // 1️⃣ Build headers (Description & Comments parts come last)
            var baseHeaders = new[]
            {
                "Key", "Summary", "Created", "Status", "IssueType", "AttachmentUrls"
            };
            var headers = baseHeaders
                .Concat(customFields)
                .Concat(new[] { "Comments Part 1", "Description Part 1" })
                .ToList();

            // Write header row
            for (int c = 0; c < headers.Count; c++)
            {
                worksheet.Cell(1, c + 1).Value = headers[c];
                worksheet.Cell(1, c + 1).Style.Font.Bold = true;
            }

            // 2️⃣ Write each issue row
            for (int r = 0; r < issues.Count; r++)
            {
                var issue = issues[r];
                int col = 1;

                // Standard fields
                worksheet.Cell(r + 2, col++).Value = issue.Key;
                worksheet.Cell(r + 2, col++).Value = issue.Summary;
                worksheet.Cell(r + 2, col++).Value = issue.Created;
                worksheet.Cell(r + 2, col++).Value = issue.Status;
                worksheet.Cell(r + 2, col++).Value = issue.IssueType;
                worksheet.Cell(r + 2, col++).Value =
                    string.Join(";", issue.Attachments.Select(a => a.ContentUrl));

                // Custom fields
                foreach (var cf in customFields)
                {
                    issue.CustomFields.TryGetValue(cf, out var val);
                    worksheet.Cell(r + 2, col++).Value = val;
                }

                // 3️ Chunk & write comments into adjacent cells
                string commentText = string.Empty;

                if (issue.Comments != null && issue.Comments.Any())
                {
                    commentText = string.Join(" | ",
                        issue.Comments.Select(c =>
                            $"{c.Author} ({c.Created}): {c.Body.Replace("\r\n", " ").Replace("\n", " ")}"
                        )
                    );
                }

                // 3.2 Ensure at least one cell is written, even if empty
                var commentChunks = ChunkText(commentText);
                foreach (var chunk in commentChunks.Any() ? commentChunks : new[] { string.Empty })
                {
                    worksheet.Cell(r + 2, col++).Value = chunk;
                }

                // 4️waaw Chunk & write description into subsequent cells
                foreach (var chunk in ChunkText(issue.Description))
                {
                    worksheet.Cell(r + 2, col++).Value = chunk;  // same splitting logic :contentReference[oaicite:2]{index=2}
                }
            }

            // 5️⃣ Autofit and save
            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }
    }
}
