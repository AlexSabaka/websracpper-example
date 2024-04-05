using HtmlAgilityPack;

HttpClient webClient = new HttpClient();

string? OUTPUT_DIR = Environment.GetEnvironmentVariable(nameof(OUTPUT_DIR));
string? PROXY = Environment.GetEnvironmentVariable(nameof(PROXY));

if (OUTPUT_DIR is null || PROXY is null)
{
    throw new ArgumentNullException();
}

Directory.CreateDirectory(OUTPUT_DIR);

var styles = await DownloadStringAsync($"{PROXY}https://metanit.com/style44.css?v=2");

var urls = await ScrapWebsiteUrlsAsync($"{PROXY}https://metanit.com/sharp/", "//div/p/a");
urls = urls.Skip(1).Take(1); // Take only first section. Comment this line to scrap all the sections

var sectionUrls = await Task.WhenAll(urls.Select(u => ScrapWebsiteUrlsAsync(u.url, "//ol/li/p/a")));
var savedFiles = await Task.WhenAll(sectionUrls.Zip(urls.Select(u => u.title)).Select(x => ScrapUrlsToPdfAsync(x.Second, x.First)));

foreach (var file in savedFiles)
{
    Console.WriteLine($"File is saved to: {file}");
}


return;

async Task<string> ScrapUrlsToPdfAsync(string documentTitle, IEnumerable<(string title, string url)> urls)
{
    var document = new HtmlDocument();
    document.LoadHtml("<html><head></head><body></body></html>");
    document.DocumentNode.SelectSingleNode("//head").AppendChild(HtmlNode.CreateNode($"<style>{styles}</style>"));
    var body = document.DocumentNode.SelectSingleNode("//body");

    // TODO: Render Table of Contents

    foreach (var (title, url) in urls)
    {
        var html = await DownloadHtmlPageAsync(url);
        var contentNode = html.DocumentNode.SelectSingleNode("//div[@class='item center menC']");

        // TODO: Download images and replaces their src attribute in the html
        contentNode.RemoveChildren(contentNode.SelectNodes("//div[@class='socBlock']"));
        contentNode.RemoveChildren(contentNode.SelectNodes("//div[@class='nav']"));
        // TODO: Remove ads elements
        // contentNode.RemoveChildren(contentNode.SelectNodes("//div[contains(@id, 'yandex_rtb_R')]"));

        body.AppendChild(HtmlNode.CreateNode("<hr />"));
        body.AppendChild(HtmlNode.CreateNode("<br />"));
        body.AppendChild(HtmlNode.CreateNode($"<h1>{title}</h1>"));
        body.AppendChild(contentNode);
    }

    // TODO: Render HTML document to PDF

    var filename = FormatFileName(documentTitle, ".html");
    document.Save(filename);

    return filename;
}

async Task<IEnumerable<(string title, string url)>> ScrapWebsiteUrlsAsync(string url, string xpathSelector)
{
    var html = await DownloadHtmlPageAsync(url);
    return html.DocumentNode
        .SelectNodes(xpathSelector)
        .Select(node => (title: node.InnerText, url: url + node.Attributes["href"].Value));
}

async Task<HtmlDocument> DownloadHtmlPageAsync(string url)
{
    var content = await DownloadStringAsync(url);
    var html = new HtmlDocument();
    html.LoadHtml(content);

    return html;
}

async Task<string> DownloadStringAsync(string url)
{
    var response = await webClient.GetAsync(url);
    return await response.Content.ReadAsStringAsync();
}

string FormatFileName(string name, string ext)
    => Path.Combine(OUTPUT_DIR, string.Join('_', name.Split(Path.GetInvalidFileNameChars())) + ext);
