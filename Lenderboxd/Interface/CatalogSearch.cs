namespace Lenderboxd;

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

public interface ICatalogSearch : IGrainWithStringKey
{
	public Task Execute();
	public Task<MediaFormat[]?> GetResult();
}

public class CatalogSearch : Grain, ICatalogSearch
{
	static HttpClient HttpClient { get; } = new HttpClient();
	static CatalogSearch()
	{
		// most of these aren't necessary, but they make us look more like a real client
		HttpClient.DefaultRequestHeaders.Add("accept", "application/json, text/plain, */*");
		HttpClient.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9");
		HttpClient.DefaultRequestHeaders.Add("dnt", "1");
		HttpClient.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Microsoft Edge\";v=\"122\"");
		HttpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
		HttpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"macOS\"");
		HttpClient.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
		HttpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
		HttpClient.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
		HttpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0");
	}

	readonly ILogger<CatalogSearch> _logger;
	readonly IPersistentState<CatalogSearchState> _state;

	string FilmTitle { get; }
	string Library { get; }

	public CatalogSearch([PersistentState("result")] IPersistentState<CatalogSearchState> state, ILogger<CatalogSearch> logger)
	{
		_logger = logger;
		_state = state;

		var key = this.GetPrimaryKeyString().AsSpan();
		Library = key[..key.IndexOf('/')].ToString();
		FilmTitle = key[(key.IndexOf('/') + 1)..].ToString();
	}

	public override string ToString()
	{
		return this.GetPrimaryKeyString();
	}

	public static string GetId(string library, string filmTitle)
	  => $"{library}/{filmTitle}";

	public async Task Execute()
	{
		if (_state.State.LastRefresh.HasValue)
			return;

		_logger.LogDebug("Hitting library API for {FilmTitle}", FilmTitle);

		var request = new HttpRequestMessage(HttpMethod.Post, "https://www.richlandlibrary.com/api/search/catalog")
		{
			Content = new FormUrlEncodedContent(
			[
				new("page", "1"),
				new("advanced[TI]", FilmTitle),
				new("advanced[TOM][0]", "brd"),
				new("advanced[TOM][1]", "bdv"),
				new("advanced[TOM][2]", "dvd")
			])
		};

		HashSet<MediaFormat> availableFormats = [];
		var response = await HttpClient.SendAsync(request);

		using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
		var rows = json.RootElement.GetProperty("rows");
		foreach (var row in rows.EnumerateArray())
		{
			try
			{
				var rowData = row.GetProperty("full");
				if (rowData.ValueKind != JsonValueKind.Object)
					continue;

				var title = rowData.GetProperty("title").GetString()?.TrimEnd('.');
				var subtitle = rowData.GetProperty("subtitle").GetString()?.TrimEnd('.');
				var fullTitle = title + (subtitle is null or "" ? "" : $": {subtitle}");
				if (fullTitle?.Equals(FilmTitle, StringComparison.OrdinalIgnoreCase) == true)
				{
					if (ReadFormat(rowData) is MediaFormat fmt)
						availableFormats.Add(fmt);
				}

				if (row.GetProperty("type").GetString() == "grouping")
				{
					foreach (var child in row.GetProperty("children").EnumerateArray())
					{
						if (ReadFormat(child) is MediaFormat fmt)
							availableFormats.Add(fmt);
					}
				}

				if (availableFormats.Count == Enum.GetValues<MediaFormat>().Length)
					break;
			}
			catch (InvalidOperationException e)
			{
				_logger.LogError("Error processing JSON for {film} ({row}): {error}", FilmTitle, row, e);
			}
		}

		_state.State.LastRefresh = DateTimeOffset.UtcNow;
		_state.State.Formats = availableFormats.ToArray();
		await _state.WriteStateAsync();

		await PublishResult();

		static MediaFormat? ReadFormat(JsonElement rowData)
		{
			var format = rowData.GetProperty("format").GetProperty("className").GetString();
			if (format?.Contains("blu") == true)
				return MediaFormat.Bluray;
			else if (format?.Contains("dvd") == true)
				return MediaFormat.Dvd;
			else
				return null;
		}
	}

	async Task PublishResult()
	{
		_logger.LogDebug("Publishing result to stream {FilmTitle}: {Formats}", FilmTitle, _state.State.Formats);
		var provider = this.GetStreamProvider("Default");
		var stream = provider.GetStream<CatalogSearchResult>("SearchResults", "www.richlandlibrary.com");
		await stream.OnNextAsync(new CatalogSearchResult(Library, FilmTitle, _state.State.Formats!));
	}

	public Task<MediaFormat[]?> GetResult()
	{
		return Task.FromResult(_state.State.Formats);
	}
}

[GenerateSerializer]
public class CatalogSearchState
{
	public MediaFormat[]? Formats { get; set; }
	public DateTimeOffset? LastRefresh { get; set; }
}

[GenerateSerializer]
public record CatalogSearchResult(string library, string FilmTitle, MediaFormat[] Formats);