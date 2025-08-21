
namespace JiraApi.Dto
{
	public class IssueDto
	{
		public string Key { get; set; }
        public string IssueType { get; set; } = "";
        public string Summary { get; set; }
		public string Description { get; set; }
		public string Created { get; set; }
		public string Status { get; set; }
		public List<AttachmentDto> Attachments { get; set; }
        public Dictionary<string, string> CustomFields { get; set; } = new();
        public List<CommentDto> Comments { get; set; } = new();
    }
}