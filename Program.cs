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
        // CLI-Parsing
        int tage = 7;
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "-d" || args[i] == "-days")
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var d))
            {
                tage = d;
                i++;
            }
        }

        // Configuration: JQL from JSON, secrets for credentials
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<Program>()
            .Build();

        var basisUrl = config["Jira:BaseUrl"]
                     ?? throw new InvalidOperationException("Fehlende Jira:BaseUrl in User Secrets");
        var email = config["Jira:Email"]
                     ?? throw new InvalidOperationException("Fehlende Jira:Email in User Secrets");
        var token = config["Jira:ApiToken"]
                     ?? throw new InvalidOperationException("Fehlendes Jira:ApiToken in User Secrets");
        var jqlQuery = config["Jira:Jql"]
                     ?? throw new InvalidOperationException("Fehlendes Jira:Jql in appsettings.json");

        // HTTP-Client vorbereiten
        var client = new HttpClient { BaseAddress = new Uri(basisUrl) };
        var auth = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{email}:{token}"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", auth);

        // Paginierung: Issues + Changelog
        const int seitenGroesse = 1000;
        var alleTickets = new List<Issue>();
        int startAt = 0, total = int.MaxValue;
        while (startAt < total)
        {
            var url = $"/rest/api/3/search?jql={UrlEncoder.Default.Encode(jqlQuery)}&expand=changelog&startAt={startAt}&maxResults={seitenGroesse}";
            var resp = await client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var seite = await resp.Content.ReadFromJsonAsync<JiraSearchResult>()
                       ?? throw new Exception("Fehler beim Parsen der Jira-Antwort.");
            total = seite.Total;
            alleTickets.AddRange(seite.Issues);
            startAt += seite.Issues.Count;
        }

        // Cutoff lokal
        var cutoff = DateTime.Now.AddDays(-tage);

        // HTML-Bericht
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><style>");
        sb.AppendLine("ins {background-color: #dfd;} del {background-color: #fdd;} ");
        sb.AppendLine("body {font-family: Arial, sans-serif;} h2 {margin-top: 2em;} ul {margin-left:1em;} ");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>Jira Änderungen (letzte {tage} Tage) — {DateTime.Now:yyyy-MM-dd}</h1>");

        var diffBuilder = new InlineDiffBuilder(new Differ());

        // Änderungen
        foreach (var ticket in alleTickets)
        {
            var aenderungen = ticket.Changelog.Histories
                .Where(h => DateTime.Parse(h.Created).ToLocalTime() >= cutoff)
                .SelectMany(h => h.Items
                    .Where(i => i.Field is "summary" or "description")
                    .Select(i => new {
                        Wann = DateTime.Parse(h.Created).ToLocalTime(),
                        Feld = i.Field,
                        Von = i.FromString ?? string.Empty,
                        Nach = i.ToValue ?? string.Empty
                    }))
                .ToList();
            if (!aenderungen.Any()) continue;
            sb.AppendLine($"<h2><a href=\"{basisUrl}/browse/{ticket.Key}\">{ticket.Key}</a>: {ticket.Fields.Summary}</h2>");
            foreach (var a in aenderungen)
            {
                sb.AppendLine($"<h3>{a.Feld} geändert am {a.Wann:yyyy-MM-dd HH:mm}</h3>");
                sb.AppendLine(RenderDiffHtml(diffBuilder.BuildDiffModel(a.Von, a.Nach)));
            }
        }

        // Neue Tickets
        var neueTickets = alleTickets
            .Where(t => DateTime.Parse(t.Fields.Created).ToLocalTime() >= cutoff)
            .ToList();
        if (neueTickets.Any())
        {
            sb.AppendLine("<h2>Neu erstellte Issues</h2><ul>");
            neueTickets.ForEach(nt =>
                sb.AppendLine($"<li><a href=\"{basisUrl}/browse/{nt.Key}\">{nt.Key}</a>: {nt.Fields.Summary}</li>"));
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</body></html>");

        // Ausgabe
        const string htmlPfad = "JiraAenderungen.html";
        await File.WriteAllTextAsync(htmlPfad, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"✅ HTML-Bericht gespeichert: {htmlPfad}");
    }

    static string RenderDiffHtml(DiffPaneModel model)
    {
        var html = new StringBuilder("<div>");
        foreach (var zeile in model.Lines)
        {
            var tag = zeile.Type switch
            {
                ChangeType.Inserted => "ins",
                ChangeType.Deleted => "del",
                _ => "span"
            };
            html.Append($"<{tag}>{System.Net.WebUtility.HtmlEncode(zeile.Text)}</{tag}><br/>");
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