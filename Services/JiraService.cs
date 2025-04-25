using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraApi.Dto;
using JiraApi.Models;
using Newtonsoft.Json.Linq;

namespace JiraApi.Services
{
    public class JiraService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public JiraService(string baseUrl, string email, string apiToken)
        {
            _baseUrl = baseUrl;

            _httpClient = new HttpClient();
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<Dictionary<string, List<string>>> GetUsersByProjectAsync()
        {
            var result = new Dictionary<string, List<string>>();

            var projectsResponse = await _httpClient.GetAsync($"{_baseUrl}/rest/api/3/project");
            projectsResponse.EnsureSuccessStatusCode();

            var projectsJson = await projectsResponse.Content.ReadAsStringAsync();
            var projects = JsonSerializer.Deserialize<List<Project>>(projectsJson);

            foreach (var project in projects)
            {
                var rolesResponse = await _httpClient.GetAsync($"{_baseUrl}/rest/api/3/project/{project.Key}/role");
                rolesResponse.EnsureSuccessStatusCode();

                var rolesJson = await rolesResponse.Content.ReadAsStringAsync();
                var roles = JsonSerializer.Deserialize<Dictionary<string, string>>(rolesJson);

                foreach (var roleUrl in roles.Values)
                {
                    var roleDetailResponse = await _httpClient.GetAsync(roleUrl);
                    roleDetailResponse.EnsureSuccessStatusCode();

                    var roleDetailJson = await roleDetailResponse.Content.ReadAsStringAsync();
                    var role = JsonSerializer.Deserialize<RoleDetail>(roleDetailJson);

                    if (role?.Actors != null)
                    {
                        foreach (var actor in role.Actors)
                        {
                            if (actor.Type == "atlassian-user-role-actor")
                            {
                                var user = actor.DisplayName;
                                if (!result.ContainsKey(user))
                                    result[user] = new List<string>();

                                if (!result[user].Contains(project.Name))
                                    result[user].Add(project.Name);
                            }
                        }
                    }
                }
            }

            return result;
        }

        public async Task<List<IssueDto>> GetIssuesByProjectAsync(string projectKey)
        {
            var issues = new List<IssueDto>();
            int startAt = 0;
            int maxResults = 50;

            while (true)
            {
                var url = $"/rest/api/3/search?jql=project={projectKey}&startAt={startAt}&maxResults={maxResults}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                foreach (var issue in json["issues"]!)
                {
                    issues.Add(new IssueDto
                    {
                        Key = (string)issue["key"],
                        Summary = (string)issue["fields"]?["summary"],
                        Description = (string)issue["fields"]?["description"],
                        Created = (string)issue["fields"]?["created"],
                        Status = (string)issue["fields"]?["status"]?["name"],
                        Attachments = issue["fields"]?["attachment"]?
                            .Select(a => new AttachmentDto
                            {
                                FileName = (string)a["filename"],
                                ContentUrl = (string)a["content"]
                            }).ToList() ?? new List<AttachmentDto>()
                    });
                }

                int total = (int)json["total"];
                startAt += maxResults;

                if (startAt >= total)
                    break;
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