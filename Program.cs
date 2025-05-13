using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {
        // Command-line parsing
        int days = 7;
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "-d" || args[i] == "-days")
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var d))
            {
                days = d;
                i++;
            }
        }

        // Load configuration: JQL from JSON, other settings from User Secrets
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<Program>()
            .Build();

        var baseUrl = config["Jira:BaseUrl"]
                     ?? throw new InvalidOperationException("Missing Jira:BaseUrl in User Secrets");
        var email = config["Jira:Email"]
                     ?? throw new InvalidOperationException("Missing Jira:Email in User Secrets");
        var token = config["Jira:ApiToken"]
                     ?? throw new InvalidOperationException("Missing Jira:ApiToken in User Secrets");
        var jqlQuery = config["Jira:Jql"]
                      ?? throw new InvalidOperationException("Missing Jira:Jql in appsettings.json");

        // Prepare HTTP client
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{token}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        // Pagination: fetch all issues + changelog
        const int pageSize = 1000;
        var allIssues = new List<Issue>();
        int startAt = 0, total = int.MaxValue;
        while (startAt < total)
        {
            var url = $"/rest/api/3/search?jql={UrlEncoder.Default.Encode(jqlQuery)}&expand=changelog&startAt={startAt}&maxResults={pageSize}";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var pageResult = await response.Content.ReadFromJsonAsync<JiraSearchResult>()
                             ?? throw new Exception("Error parsing Jira response.");
            total = pageResult.Total;
            allIssues.AddRange(pageResult.Issues);
            startAt += pageResult.Issues.Count;
        }

        // Determine cutoff in local timezone
        var cutoff = DateTime.Now.AddDays(-days);

        // Build HTML report
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><style>");
        sb.AppendLine("ins {background-color: #dfd;} del {background-color: #fdd;}");
        sb.AppendLine("body {font-family: Arial, sans-serif;} h2 {margin-top: 2em;} ul {margin-left:1em;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>Jira Änderungen (letzte {days} Tage) — {DateTime.Now:yyyy-MM-dd}</h1>");

        var diffBuilder = new InlineDiffBuilder(new Differ());

        // Summary/description changes (one diff per field)
        foreach (var issue in allIssues)
        {
            var rawChanges = issue.Changelog.Histories
                .Where(h => DateTime.Parse(h.Created).ToLocalTime() >= cutoff)
                .SelectMany(h => h.Items
                    .Where(i => i.Field is "summary" or "description")
                    .Select(i => new
                    {
                        When = DateTime.Parse(h.Created).ToLocalTime(),
                        Field = i.Field,
                        From = i.FromString ?? string.Empty,
                        To = i.ToValue ?? string.Empty
                    }))
                .ToList();

            if (!rawChanges.Any())
                continue;

            // Group by field, pick earliest From and latest To
            var grouped = rawChanges
                .GroupBy(c => c.Field)
                .Select(g =>
                {
                    var ordered = g.OrderBy(c => c.When).ToList();
                    return new
                    {
                        Field = g.Key,
                        From = ordered.First().From,
                        To = ordered.Last().To,
                        When = ordered.Last().When
                    };
                })
                .ToList();

            sb.AppendLine($"<h2><a href=\"{baseUrl}/browse/{issue.Key}\">{issue.Key}</a>: {issue.Fields.Summary}</h2>");
            foreach (var c in grouped)
            {
                sb.AppendLine($"<h3>{c.Field} geändert am {c.When:yyyy-MM-dd HH:mm}</h3>");
                var diffModel = diffBuilder.BuildDiffModel(c.From, c.To);
                sb.AppendLine(RenderDiffHtml(diffModel));
            }
        }

        // Newly created issues
        var newIssues = allIssues
            .Where(i => DateTime.Parse(i.Fields.Created).ToLocalTime() >= cutoff)
            .ToList();
        if (newIssues.Any())
        {
            sb.AppendLine("<h2>Neu erstellte Issues</h2><ul>");
            newIssues.ForEach(n => sb.AppendLine(
                $"<li><a href=\"{baseUrl}/browse/{n.Key}\">{n.Key}</a>: {n.Fields.Summary}</li>"));
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</body></html>");

        // Save HTML
        const string htmlPath = "JiraAenderungen.html";
        await File.WriteAllTextAsync(htmlPath, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"✅ HTML-Bericht gespeichert: {htmlPath}");
    }

    static string RenderDiffHtml(DiffPaneModel model)
    {
        var html = new StringBuilder("<div>");
        foreach (var line in model.Lines)
        {
            var tag = line.Type switch
            {
                ChangeType.Inserted => "ins",
                ChangeType.Deleted => "del",
                _ => "span"
            };
            html.Append($"<{tag}>{System.Net.WebUtility.HtmlEncode(line.Text)}</{tag}><br/>");
        }
        html.Append("</div>");
        return html.ToString();
    }

    // DTOs
    public class JiraSearchResult
    {
        [JsonPropertyName("issues")] public List<Issue> Issues { get; set; } = new();
        [JsonPropertyName("total")] public int Total { get; set; }
    }
    public class Issue
    {
        [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
        [JsonPropertyName("fields")] public Fields Fields { get; set; } = new();
        [JsonPropertyName("changelog")] public Changelog Changelog { get; set; } = new();
    }
    public class Fields
    {
        [JsonPropertyName("summary")] public string Summary { get; set; } = string.Empty;
        [JsonPropertyName("created")] public string Created { get; set; } = string.Empty;
    }
    public class Changelog
    {
        [JsonPropertyName("histories")] public List<History> Histories { get; set; } = new();
    }
    public class History
    {
        [JsonPropertyName("created")] public string Created { get; set; } = string.Empty;
        [JsonPropertyName("items")] public List<Item> Items { get; set; } = new();
    }
    public class Item
    {
        [JsonPropertyName("field")] public string Field { get; set; } = string.Empty;
        [JsonPropertyName("fromString")] public string? FromString { get; set; }
        [JsonPropertyName("toString")] public string? ToValue { get; set; }
    }
}
