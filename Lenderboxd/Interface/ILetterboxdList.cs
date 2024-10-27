namespace Lenderboxd;

using System.Diagnostics;
using System.Threading;
using DotNext;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Runtime;
using Orleans.Streams;
using Orleans.Utilities;

/// <summary>
/// Id = user_slug/list_slug
/// </summary>
public interface ILetterboxdList : IGrainWithStringKey
{
	/// <summary>
	/// Gets the films contained in this letterboxd list.
	/// </summary>
	/// <param name="refresh">Indicates whether or not to refresh the list data from letterboxd.</param>
	/// <returns></returns>
	public Task<IEnumerable<Film>> LoadFilms(bool refresh);

	public Task<MediaFormat[]?[]> LoadFilmAvailability(string library);

	[ReadOnly]
	[AlwaysInterleave]
	public Task<string?> GetTitle();

	[ReadOnly]
	[AlwaysInterleave]
	public Task<Film[]?> GetFilms();

	[ReadOnly]
	[AlwaysInterleave]
	public Task<MediaFormat[]?[]?> GetFilmAvailability(string library);

	[AlwaysInterleave]
	public Task Subscribe(IObserver observer);

	[AlwaysInterleave]
	public Task Unsubscribe(IObserver observer);

	public interface IObserver : IGrainObserver
	{
		[OneWay]
		public Task FilmAvailabilityReady(FilmAvailabilityEvent evt);
	}
}

public class LetterboxdList : Grain, ILetterboxdList
{
	readonly ObserverManager<ILetterboxdList.IObserver> _subsManager;
	readonly IPersistentState<LetterboxdListState> _state;
	readonly ILogger<LetterboxdList> _logger;

	string UserSlug { get; }
	string ListSlug { get; }

	public LetterboxdList([PersistentState("list")] IPersistentState<LetterboxdListState> state, ILogger<LetterboxdList> logger)
	{
		_state = state;
		_logger = logger;
		var id = this.GetPrimaryKeyString().AsSpan();
		UserSlug = id[..id.IndexOf('/')].ToString();
		ListSlug = id[(id.IndexOf('/') + 1)..].ToString();

		_subsManager = new ObserverManager<ILetterboxdList.IObserver>(TimeSpan.FromMinutes(5), logger);
	}

	public override async Task OnActivateAsync(CancellationToken cancellationToken)
	{
		_state.State.SearchResultSubscriptions = [.. await Task.WhenAll(_state.State.SearchResultSubscriptions.Select(sub =>
		{
			_logger.LogDebug("Stream subscription found {StreamId}", sub.StreamId);
			return sub.ResumeAsync(HandleSearchResult);
		}))];
	}

	public Task<string?> GetTitle() => Task.FromResult(_state.State.Title);
	public Task<Film[]?> GetFilms() => Task.FromResult(_state.State.LastRefresh != null ? _state.State.Films : null);
	public Task<MediaFormat[]?[]?> GetFilmAvailability(string library) => Task.FromResult(_state.State.AvailabilityResults);

	public async Task<IEnumerable<Film>> LoadFilms(bool refresh)
	{
		if (refresh || _state.State.LastRefresh is null)
		{
			var sw = Stopwatch.StartNew();
			var listData = await LetterboxdScraper.FetchFilms(UserSlug, ListSlug, _logger);
			_state.State.Title = listData.Title;
			_state.State.Films = listData.Films;
			_logger.LogInformation("Took {Time} to fetch films from letterboxd", sw.Elapsed);
			_state.State.LastRefresh = DateTimeOffset.UtcNow;
			await _state.WriteStateAsync();
		}
		return _state.State.Films;
	}

	public async Task<MediaFormat[]?[]> LoadFilmAvailability(string library = "www.richlandlibrary.com")
	{
		if (_state.State.AvailabilityResults is null or [])
		{
			_logger.LogDebug("Pulling availability from catalog search...");
			var timer = Stopwatch.StartNew();
			// initialize availability results to indicate they are pending
			_state.State.AvailabilityResults = await Task.WhenAll(
				_state.State.Films
					.Select(f => GrainFactory.GetGrain<ICatalogSearch>(CatalogSearch.GetId("www.richlandlibrary.com", f.Title)).GetResult())
			);
			_logger.LogDebug("Pulled {Count} results in {Time}", _state.State.AvailabilityResults.Length, timer.Elapsed);

			// only make requests if some results are missing
			if (_state.State.AvailabilityResults.Any(r => r is null))
			{
				_logger.LogDebug("Queuing search requests...");
				timer.Restart();
				var streamProvider = this.GetStreamProvider("Default");

				// queue requests
				var requestStream = streamProvider.GetStream<string>("SearchRequests", library);
				await Task.WhenAll(
					_state.State.Films
						.Select(f => f.Title)
						.BatchIEnumerable(25)
						.Select(batch => requestStream.OnNextBatchAsync(batch))
				);
				_logger.LogDebug("Queued requests in {Time}", timer.Elapsed);

				// listen for results
				var resultStream = streamProvider.GetStream<CatalogSearchResult>("SearchResults", library);
				_state.State.SearchResultSubscriptions.Add(await resultStream.SubscribeAsync(HandleSearchResult));
			}

			await _state.WriteStateAsync();
		}

		return _state.State.AvailabilityResults;
	}

	readonly Dictionary<string, List<int>> _filmIndex = [];
	async Task HandleSearchResult(IList<SequentialItem<CatalogSearchResult>> results)
	{
		List<Task> notifications = [];

		if (_filmIndex.Count == 0)
		{
			for (int idx = 0; idx < _state.State.Films.Length; idx++)
			{
				var film = _state.State.Films[idx];
				if (!_filmIndex.ContainsKey(film.Title))
					_filmIndex[film.Title] = [idx];
				else
					_filmIndex[film.Title].Add(idx);
			}
		}

		foreach (var result in results)
		{
			if (_filmIndex.TryGetValue(result.Item.FilmTitle, out var indexes))
			{
				_logger.LogDebug("{List} handling result for relevant film: {Film}", this, result.Item.FilmTitle);
				foreach (var resultIdx in indexes)
				{
					_state.State.AvailabilityResults![resultIdx] = result.Item.Formats;
					notifications.Add(_subsManager.Notify(observer => observer.FilmAvailabilityReady(new(resultIdx, result.Item.FilmTitle, result.Item.Formats))));
				}
			}
		}

		if (notifications.Count > 0)
		{
			_logger.LogCritical("{List} awaiting observer notification tasks ({Count})", this, notifications.Count);
			await Task.WhenAll([_state.WriteStateAsync(), .. notifications]);
			_logger.LogCritical("{List} completed observer notifications!)", this);
			if (_state.State.AvailabilityResults!.All(r => r is not null) && _state.State.SearchResultSubscriptions.Count > 0)
			{
				await Task.WhenAll(_state.State.SearchResultSubscriptions.Select(sub =>
				{
					_logger.LogDebug("{List} unsubscribing from stream {StreamId}", this, sub.StreamId);
					return sub.UnsubscribeAsync();
				}));
				_state.State.SearchResultSubscriptions.Clear();
				await _state.WriteStateAsync();
			}
		}
		else
		{
			_logger.LogDebug("{List} received no relevant search results", this);
		}
	}

	public Task Subscribe(ILetterboxdList.IObserver observer)
	{
		_logger.LogDebug("{Observer} subscribed for events", observer);
		_subsManager.Subscribe(observer, observer);
		return Task.CompletedTask;
	}

	public Task Unsubscribe(ILetterboxdList.IObserver observer)
	{
		_subsManager.Unsubscribe(observer);
		return Task.CompletedTask;
	}

	public override string ToString()
	{
		return this.GetPrimaryKeyString();
	}
}

[GenerateSerializer]
public class LetterboxdListState
{
	[Id(0)]
	public DateTimeOffset? LastRefresh { get; set; }
	[Id(1)]
	public Film[] Films { get; set; } = [];
	[Id(2)]
	public MediaFormat[]?[]? AvailabilityResults { get; set; }
	[Id(3)]
	public List<StreamSubscriptionHandle<CatalogSearchResult>> SearchResultSubscriptions { get; set; } = [];
	[Id(4)]
	public string? Title { get; set; }
}

[GenerateSerializer]
public record Film(
	string Id,
	string Slug,
	string Title,
	uint? ReleaseYear
);

[GenerateSerializer]
public record FilmAvailabilityEvent(int Index, string Title, MediaFormat[] Formats);

public enum MediaFormat : int
{
	Dvd,
	Bluray
}