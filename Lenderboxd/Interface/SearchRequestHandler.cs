namespace Lenderboxd;

using System.Collections.Generic;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Orleans.Streams;
using Orleans.Streams.Core;

public interface ISearchRequestHandler : IGrainWithStringKey
{ }

[ImplicitStreamSubscription("SearchRequests")]
public class SearchRequestHandler : Grain, ISearchRequestHandler, IStreamSubscriptionObserver, IAsyncBatchObserver<string>
{
	static readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create((string resource) =>
	{
		return RateLimitPartition.GetSlidingWindowLimiter(resource, key => new()
		{
			AutoReplenishment = true,
			PermitLimit = 10,
			Window = TimeSpan.FromSeconds(1),
			SegmentsPerWindow = 2,
			QueueLimit = 10000
		});
	});

	readonly ILogger<SearchRequestHandler> _logger;

	public SearchRequestHandler(ILogger<SearchRequestHandler> logger)
	{
		_logger = logger;
	}

	string Library => this.GetPrimaryKeyString();

	public Task OnSubscribed(IStreamSubscriptionHandleFactory handleFactory)
	{
		var handle = handleFactory.Create<string>();
		return handle.ResumeAsync(this);
	}

	readonly List<string> _processed = [];
	async Task IAsyncBatchObserver<string>.OnNextAsync(IList<SequentialItem<string>> films)
	{
		_logger.LogDebug("Received {FilmCount} requests for {Library}. First token: {Token}", films.Count, Library, films.First().Token);
		var unprocessedItems = films.ExceptBy(_processed, i => i.Item);
		await Task.WhenAll(unprocessedItems.Select(async film =>
		{
			var searchGrain = GrainFactory.GetGrain<ICatalogSearch>(CatalogSearch.GetId(Library, film.Item));
			var cached = await searchGrain.GetResult();
			if (cached is null)
			{
				_logger.LogDebug("No result cached for {Film}, executing request.", film.Item);
				using var lease = await _limiter.AcquireAsync(Library);
				await searchGrain.Execute();
			}
			_processed.Add(film.Item);
		}));
		_processed.Clear();
		_logger.LogDebug("Finished");
	}

	Task IAsyncBatchObserver<string>.OnCompletedAsync()
	{
		_logger.LogDebug("SearchRequestHandler OnComplete");
		return Task.CompletedTask;
	}

	Task IAsyncBatchObserver<string>.OnErrorAsync(Exception ex)
	{
		_logger.LogError("SearchRequestHandler OnError: {Error}", ex);
		return Task.CompletedTask;
	}
}
