from pathlib import Path
import shutil

root = Path('src/XSphere')
main = root / 'MainWindow.cs'
db = root / 'Services/ArcadeDatabase.cs'
service = root / 'Services/GameMetadataService.cs'
if not main.exists() or not db.exists():
    raise SystemExit('Run from the XSphere solution directory.')
m = main.read_text()
d = db.read_text()
if 'XSPHERE METADATA V1' in m or service.exists():
    raise SystemExit('Metadata upgrade appears installed already. No changes made.')
checks = [(m,'private Control BuildGameDetailsPage()'),(m,'private Control BuildHomePage()'),(m,'var game = _selectedGame;'),(m,'information.Children.Add(\n            Text("GAME INFORMATION", 13, MutedBrush, true));'),(d,'CREATE TABLE IF NOT EXISTS RomFolders ('),(d,'public List<Game> GetGames()')]
for text, anchor in checks:
    if text.count(anchor) != 1:
        raise SystemExit(f'Unexpected source structure for {anchor!r}; no files changed.')

svc = r'''using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XSphere.Models;

namespace XSphere.Services;

public sealed record GameMetadata(
    string Description, string ReleaseDate, string Developer,
    string Publisher, string Genre, string SourceUrl);

public sealed class GameMetadataService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    static GameMetadataService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "XSphere/0.1.2 (personal game library metadata client)");
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();

    private static string CleanTitle(string title) =>
        Regex.Replace(title, @"\s*\((usa|europe|japan|world|rev[^)]*|en[^)]*)\)\s*", " ",
            RegexOptions.IgnoreCase).Trim();

    private static async Task<JsonDocument> GetJsonAsync(string url)
    {
        using var response = await Http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string? FirstItemId(JsonElement entity, string property)
    {
        if (!entity.TryGetProperty("claims", out var claims) ||
            !claims.TryGetProperty(property, out var statements)) return null;
        foreach (var statement in statements.EnumerateArray())
        {
            if (statement.TryGetProperty("rank", out var rank) &&
                rank.GetString() == "deprecated") continue;
            if (!statement.TryGetProperty("mainsnak", out var snak) ||
                !snak.TryGetProperty("datavalue", out var dv)) continue;
            var value = dv.GetProperty("value");
            if (value.ValueKind == JsonValueKind.Object &&
                value.TryGetProperty("id", out var id)) return id.GetString();
        }
        return null;
    }

    private static string DateValue(JsonElement entity)
    {
        if (!entity.TryGetProperty("claims", out var claims) ||
            !claims.TryGetProperty("P577", out var statements)) return "Not available";
        foreach (var statement in statements.EnumerateArray())
        {
            if (!statement.TryGetProperty("mainsnak", out var snak) ||
                !snak.TryGetProperty("datavalue", out var dv)) continue;
            var value = dv.GetProperty("value");
            if (value.TryGetProperty("time", out var time))
            {
                var raw = time.GetString() ?? "";
                var match = Regex.Match(raw, @"^[+-](\d{4})-(\d{2})-(\d{2})");
                if (match.Success)
                {
                    var precision = value.TryGetProperty("precision", out var p) ? p.GetInt32() : 9;
                    return precision >= 11 ? $"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}" :
                           precision >= 10 ? $"{match.Groups[1].Value}-{match.Groups[2].Value}" :
                           match.Groups[1].Value;
                }
            }
        }
        return "Not available";
    }

    public async Task<GameMetadata?> FetchAsync(Game game)
    {
        var title = CleanTitle(game.Title);
        if (title.Length < 3) return null;
        var search = "https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2" +
            "&generator=search&gsrlimit=8&gsrsearch=" +
            Uri.EscapeDataString(title + " video game") +
            "&prop=extracts%7Cpageprops&exintro=1&explaintext=1&exchars=1100";
        using var doc = await GetJsonAsync(search);
        if (!doc.RootElement.TryGetProperty("query", out var query) ||
            !query.TryGetProperty("pages", out var pages)) return null;

        JsonElement? best = null;
        var bestScore = -1;
        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("title", out var t)) continue;
            var pageTitle = t.GetString() ?? "";
            var normalizedPage = Normalize(Regex.Replace(pageTitle,
                @"\s*\([^)]*(video game|game|\d{4})[^)]*\)", "",
                RegexOptions.IgnoreCase));
            var normalizedGame = Normalize(title);
            var score = normalizedPage == normalizedGame ? 10 :
                normalizedPage.StartsWith(normalizedGame + " ") ? 5 : 0;
            if (score == 0) continue;
            var extract = page.TryGetProperty("extract", out var ex) ? ex.GetString() ?? "" : "";
            // Avoid articles about unrelated media or similarly named subjects.
            if (!Regex.IsMatch(extract, @"\b(video game|videogame|computer game)\b",
                RegexOptions.IgnoreCase)) continue;
            if (score > bestScore) { best = page; bestScore = score; }
        }
        if (best is null) return null;
        var selected = best.Value;
        var wikiTitle = selected.GetProperty("title").GetString() ?? title;
        var description = selected.TryGetProperty("extract", out var extractElement)
            ? extractElement.GetString() ?? "" : "";
        description = Regex.Replace(description, @"\s+", " ").Trim();
        if (description.Length > 750) description = description[..750].TrimEnd() + "…";
        if (description.Length == 0) description = "Not available";
        var date = "Not available";
        var developer = "Not available";
        var publisher = "Not available";
        var genre = "Not available";
        if (selected.TryGetProperty("pageprops", out var props) &&
            props.TryGetProperty("wikibase_item", out var item))
        {
            try
            {
                var id = item.GetString();
                using var wd = await GetJsonAsync(
                    "https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&ids=" +
                    Uri.EscapeDataString(id ?? "") + "&props=claims");
                var entity = wd.RootElement.GetProperty("entities").GetProperty(id!);
                date = DateValue(entity);
                var ids = new[] { FirstItemId(entity, "P178"), FirstItemId(entity, "P123"), FirstItemId(entity, "P136") };
                var nonempty = ids.Where(x => x is not null).Distinct().ToArray();
                if (nonempty.Length > 0)
                {
                    using var labelsDoc = await GetJsonAsync(
                        "https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=labels&languages=en&ids=" +
                        Uri.EscapeDataString(string.Join("|", nonempty)));
                    var entities = labelsDoc.RootElement.GetProperty("entities");
                    string Label(string? entityId) => entityId is not null &&
                        entities.TryGetProperty(entityId, out var obj) &&
                        obj.TryGetProperty("labels", out var labels) &&
                        labels.TryGetProperty("en", out var en)
                        ? en.GetProperty("value").GetString() ?? "Not available" : "Not available";
                    developer = Label(ids[0]);
                    publisher = Label(ids[1]);
                    genre = Label(ids[2]);
                }
            }
            catch (HttpRequestException) { /* Description is still useful. */ }
            catch (JsonException) { /* Incomplete Wikidata record. */ }
            catch (KeyNotFoundException) { /* Incomplete Wikidata record. */ }
        }
        return new GameMetadata(description, date, developer, publisher, genre,
            "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(wikiTitle.Replace(' ', '_')));
    }
}
'''

# Database migration is additive, no changes to existing tables.
d = d.replace('CREATE TABLE IF NOT EXISTS RomFolders (', '''CREATE TABLE IF NOT EXISTS GameMetadata (
                GameId TEXT PRIMARY KEY,
                Description TEXT NOT NULL,
                ReleaseDate TEXT NOT NULL,
                Developer TEXT NOT NULL,
                Publisher TEXT NOT NULL,
                Genre TEXT NOT NULL,
                SourceUrl TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS RomFolders (''', 1)
anchor = '    public List<Game> GetGames()'
d = d.replace(anchor, '''    public GameMetadata? GetGameMetadata(string gameId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Description, ReleaseDate, Developer, Publisher, Genre, SourceUrl FROM GameMetadata WHERE GameId=$id";
        command.Parameters.AddWithValue("$id", gameId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new GameMetadata(
            reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5)) : null;
    }

    public void SaveGameMetadata(string gameId, GameMetadata metadata)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GameMetadata (GameId, Description, ReleaseDate, Developer, Publisher, Genre, SourceUrl)
            VALUES ($id,$desc,$date,$dev,$pub,$genre,$url)
            ON CONFLICT(GameId) DO UPDATE SET
                Description=excluded.Description, ReleaseDate=excluded.ReleaseDate,
                Developer=excluded.Developer, Publisher=excluded.Publisher,
                Genre=excluded.Genre, SourceUrl=excluded.SourceUrl;
            """;
        command.Parameters.AddWithValue("$id", gameId);
        command.Parameters.AddWithValue("$desc", metadata.Description);
        command.Parameters.AddWithValue("$date", metadata.ReleaseDate);
        command.Parameters.AddWithValue("$dev", metadata.Developer);
        command.Parameters.AddWithValue("$pub", metadata.Publisher);
        command.Parameters.AddWithValue("$genre", metadata.Genre);
        command.Parameters.AddWithValue("$url", metadata.SourceUrl);
        command.ExecuteNonQuery();
    }

''' + anchor, 1)

m = m.replace('private readonly GameLauncher _launcher = new();', '''private readonly GameLauncher _launcher = new();
    // XSPHERE METADATA V1
    private readonly GameMetadataService _metadataService = new();
    private readonly HashSet<string> _metadataRequests = new();''', 1)
start = m.index('private Control BuildGameDetailsPage()')
end = m.index('private Control BuildHomePage()', start)
part = m[start:end]
part = part.replace('        var game = _selectedGame;', '''        var game = _selectedGame;
        var metadata = _database.GetGameMetadata(game.Id);''', 1)
needle = '''        information.Children.Add(
            Text("GAME INFORMATION", 13, MutedBrush, true));'''
addition = '''        information.Children.Add(
            Text("ABOUT THIS GAME", 13, MutedBrush, true));
        var metadataStatus = Text(
            metadata?.Description ?? "Looking up game information…",
            14, MainTextBrush, wrapping: TextWrapping.Wrap);
        information.Children.Add(metadataStatus);
        var metadataFields = Text(
            FormatMetadata(metadata), 13, MutedBrush,
            wrapping: TextWrapping.Wrap);
        information.Children.Add(metadataFields);
        if (metadata is not null)
            information.Children.Add(Text("Source: " + metadata.SourceUrl,
                12, AccentBrush, wrapping: TextWrapping.Wrap));
        else
            _ = LoadMetadataAsync(game, metadataStatus, metadataFields);

'''
part = part.replace(needle, addition + needle, 1)
m = m[:start] + part + m[end:]
insert = '''    private static string FormatMetadata(GameMetadata? data) => data is null
        ? "Release: —  |  Developer: —  |  Publisher: —  |  Genre: —"
        : $"Release: {data.ReleaseDate}\\nDeveloper: {data.Developer}\\nPublisher: {data.Publisher}\\nGenre: {data.Genre}";

    private async System.Threading.Tasks.Task LoadMetadataAsync(
        Game game, TextBlock description, TextBlock fields)
    {
        if (!_metadataRequests.Add(game.Id)) return;
        try
        {
            var result = await _metadataService.FetchAsync(game);
            if (result is not null)
            {
                _database.SaveGameMetadata(game.Id, result);
                description.Text = result.Description;
                fields.Text = FormatMetadata(result);
            }
            else
                description.Text = "No reliable metadata match found for this game.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Metadata lookup failed: " + ex.Message);
            description.Text = "Metadata unavailable. Check your internet connection.";
        }
        finally { _metadataRequests.Remove(game.Id); }
    }

'''
m = m.replace('    private Control BuildHomePage()', insert + '    private Control BuildHomePage()', 1)

for p in (main, db):
    shutil.copy2(p, p.with_name(p.name + '.before-metadata-v1'))
service.write_text(svc)
db.write_text(d)
main.write_text(m)
print('Installed metadata service, SQLite table, and Game Details fields.')
print('Backups: MainWindow.cs.before-metadata-v1 and ArcadeDatabase.cs.before-metadata-v1')
