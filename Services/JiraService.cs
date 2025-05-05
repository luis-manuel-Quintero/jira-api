using System.Net.Http.Headers;
using System.Text;
using JiraApi.Dto;
using JiraApi.Model;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace JiraApi.Services
{
    public class JiraService
    {
        private readonly HttpClient _httpClient;
        private readonly JiraSettings _settings;

        public JiraService(HttpClient httpClient, IOptions<JiraSettings> options)
        {
            _settings = options.Value;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);

            var creds = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_settings.Email}:{_settings.ApiToken}")
            );
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", creds);
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<IssueDto>> GetIssuesByProjectAsync(string projectKey)
        {
            var issues = new List<IssueDto>();
            int startAt = 0;
            const int max = 50;

            while (true)
            {
                var response = await _httpClient.GetAsync(
                    $"/rest/api/3/search?jql=project={projectKey}&startAt={startAt}&maxResults={max}"
                );
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var arr = (JArray)json["issues"]!;

                foreach (JObject issueObj in arr)
                {
                    var fields = issueObj["fields"]!;
                    // pull attachments safely
                    var attachments = fields["attachment"]?
                        .Select(a => new AttachmentDto
                        {
                            FileName = a["filename"]?.Value<string>() ?? "",
                            ContentUrl = a["content"]?.Value<string>() ?? ""
                        })
                        .ToList()
                        ?? new List<AttachmentDto>();

                    issues.Add(new IssueDto
                    {
                        Key = issueObj["key"]?.Value<string>() ?? "",
                        Summary = fields["summary"]?.Value<string>() ?? "",
                        Description = fields["description"]?.ToString() ?? "",
                        Created = fields["created"]?.Value<string>() ?? "",
                        Status = fields["status"]?["name"]?.Value<string>() ?? "",
                        Attachments = attachments
                    });
                }

                int total = json["total"]!.Value<int>();
                startAt += max;
                if (startAt >= total) break;
            }

            return issues;
        }

        public async Task DownloadAttachmentsAsync(IEnumerable<AttachmentDto> attachments, string folderPath)
        {
            Directory.CreateDirectory(folderPath);

            foreach (var attachment in attachments)
            {
                var response = await _httpClient.GetAsync(attachment.ContentUrl);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var filePath = Path.Combine(folderPath, attachment.FileName);

                await File.WriteAllBytesAsync(filePath, bytes);
            }
        }
    }
}