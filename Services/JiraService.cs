using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using JiraApi.Dto;
using JiraApi.Model;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.IO.Compression;

namespace JiraApi.Services
{
    public class JiraService
    {
        private readonly HttpClient   _httpClient;
        private readonly JiraSettings _settings;

        public JiraService(HttpClient httpClient, IOptions<JiraSettings> options)
        {
            _settings    = options.Value;
            _httpClient  = httpClient;
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);

            var creds = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_settings.Email}:{_settings.ApiToken}")
            );
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", creds);
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Builds a map of all Jira field names → their internal IDs (e.g. "Responsable" → "customfield_10023")
        /// </summary>
        private async Task<Dictionary<string, string>> GetFieldNameIdMapAsync()
        {
            var response = await _httpClient.GetAsync("/rest/api/3/field");
            response.EnsureSuccessStatusCode();

            var allFields = JArray.Parse(await response.Content.ReadAsStringAsync());
            return allFields
                .Where(f => f["name"] != null && f["id"] != null)
                .GroupBy(f => f["name"]!.Value<string>()!)
                .ToDictionary(
                    grp => grp.Key,
                    grp => grp.First()["id"]!.Value<string>()!
                );
        }

        /// <summary>
        /// Fetches all issues in the given project, including attachments and any custom fields by display name.
        /// </summary>
        public async Task<List<IssueDto>> GetIssuesByProjectAsync(
            string projectKey,
            string[] customFieldNames
        )
        {
            // 1️⃣ Map display names → IDs
            var nameIdMap = await GetFieldNameIdMapAsync();
            var customFieldIds = customFieldNames
                .Where(n => nameIdMap.ContainsKey(n))
                .Select(n => nameIdMap[n])
                .Distinct()
                .ToArray();

            // 2️⃣ Build the fields= query: standard + custom IDs
            var defaultFields = new[] { "issuetype", "summary", "description", "created", "status", "attachment"};
            var allFields     = defaultFields
                .Concat(customFieldIds)
                .Distinct();
            var fieldsParam   = string.Join(",", allFields);

            var issues  = new List<IssueDto>();
            int startAt = 0, max = 50;

            while (true)
            {
                var url = $"/rest/api/3/search" +
                          $"?jql=project={projectKey}" +
                          $"&fields={fieldsParam}" +
                          $"&startAt={startAt}&maxResults={max}";

                var resp = await _httpClient.GetAsync(url);
                resp.EnsureSuccessStatusCode();

                var content = await resp.Content.ReadAsStringAsync();
                var json    = JObject.Parse(content);
                var arr     = (JArray)json["issues"]!;

                foreach (JObject issueObj in arr)
                {
                    var fields = issueObj["fields"]!;

                    // --- Attachments parsing ---
                    var attachmentArray = fields["attachment"] as JArray;
                    var attachments = attachmentArray?
                        .Select(a => new AttachmentDto {
                            FileName   = a["filename"]?.Value<string>() ?? "",
                            ContentUrl = a["content"]?.Value<string>()  ?? ""
                        })
                        .ToList()
                      ?? new List<AttachmentDto>();

                    var commentArray = (fields["comment"]?["comments"] as JArray)
                    ?? new JArray();

                    var comments = commentArray
                        .Select(c => new CommentDto
                        {
                            Author = c["author"]?["displayName"]?.Value<string>() ?? "",
                            Body = c["body"]?.ToString() ?? "",
                            Created = c["created"]?.Value<string>() ?? ""
                        })
                        .ToList();

                    // --- Base DTO ---
                    var dto = new IssueDto {
                        Key         = issueObj["key"]?.Value<string>()           ?? "",
                        IssueType   = fields["issuetype"]?["name"]?.Value<string>() ?? "",
                        Summary     = fields["summary"]?.Value<string>()         ?? "",
                        Description = fields["description"]?.ToString()          ?? "",
                        Created     = fields["created"]?.Value<string>()         ?? "",
                        Status      = fields["status"]?["name"]?.Value<string>() ?? "",
                        Attachments = attachments,
                        Comments = comments,
                        CustomFields = new Dictionary<string,string>()
                    };

                    // --- Custom fields extraction by display name ---
                    foreach (var displayName in customFieldNames)
                    {
                        if (nameIdMap.TryGetValue(displayName, out var fieldId))
                        {
                            var token = fields[fieldId];
                            dto.CustomFields[displayName] = token?.ToString() ?? "";
                        }
                        else
                        {
                            dto.CustomFields[displayName] = "";
                        }
                    }

                    issues.Add(dto);
                }

                int total = json["total"]!.Value<int>();
                startAt += max;
                if (startAt >= total) break;
            }

            return issues;
        }

        public async Task DownloadAttachmentsAsync(List<IssueDto> issues, string baseFolderPath)
        {
            Directory.CreateDirectory(baseFolderPath);

            foreach (var issue in issues)
            {
                if (issue.Attachments == null || !issue.Attachments.Any())
                    continue;

                var ticketFolder = Path.Combine(baseFolderPath, issue.Key);
                Directory.CreateDirectory(ticketFolder);

                // Download attachments into ticket-specific folder
                foreach (var attachment in issue.Attachments)
                {
                    var response = await _httpClient.GetAsync(attachment.ContentUrl);
                    response.EnsureSuccessStatusCode();

                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var filePath = Path.Combine(ticketFolder, attachment.FileName);
                    await File.WriteAllBytesAsync(filePath, bytes);
                }

                // Create ZIP from folder
                var zipPath = Path.Combine(baseFolderPath, $"{issue.Key}.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(ticketFolder, zipPath);

                // Cleanup original attachment files
                Directory.Delete(ticketFolder, true);
            }
        }
    }
}
