using JiraApi.Model;
using JiraApi.Services;
using JiraApi.Helper;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ Bind Jira section to JiraSettings
builder.Services.Configure<JiraSettings>(
    builder.Configuration.GetSection("Jira")
);

// 2️⃣ Register your JiraService and CsvExporter
builder.Services.AddHttpClient<JiraService>();
builder.Services.AddSingleton<CsvExporter>();
builder.Services.AddSingleton<ExcelExporter>();
builder.Services.AddSingleton<PdfExporter>(); 
builder.Services.AddHttpClient("Jira");  


// 3️⃣ Add controllers
builder.Services.AddControllers();

// 4️⃣ Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JiraApi",
        Version = "v1",
        Description = "Fetches Jira issues & attachments"
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "JiraApi v1");
        // c.RoutePrefix = ""; // uncomment to serve at root
    });
}

QuestPDF.Settings.License = LicenseType.Community;

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
