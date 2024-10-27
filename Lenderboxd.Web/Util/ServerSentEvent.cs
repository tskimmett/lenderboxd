namespace Lenderboxd.Web;

using System.Text.Json;

public static class ServerSentEvent
{
	public const string Close = "close-stream";
	/// <summary>
	/// Ensures the provided string is an HTMX-compatible SSE name.
	/// </summary>
	/// <param name="str"></param>
	/// <returns></returns>
	public static string ToSSE(this string str) => str.Replace(",", "_");

	public static Task StartEventStream(this HttpResponse res, CancellationToken cancellationToken)
	{
		res.Headers.ContentType = "text/event-stream";
		res.Headers.Connection = "keep-alive";
		res.StatusCode = 200;
		return res.StartAsync(cancellationToken);
	}

	public enum DatastarMerge
	{
		Morph,
		Append,
		After,
		Before
	}

	public static async Task DataStarFragment(
		this HttpResponse res,
		string fragment,
		CancellationToken cancel,
		string? selector = null,
		DatastarMerge? merge = null)
	{
		await res.WriteAsync($"event: datastar-fragment\n", cancel);
		if (selector is not null)
			await res.WriteAsync($"data: selector {selector}\n", cancel);
		await res.WriteAsync($"data: fragment {fragment}\n", cancel);

		if (merge is DatastarMerge.Append)
			await res.WriteAsync("data: merge append\n");
		else if (merge is DatastarMerge.After)
			await res.WriteAsync("data: merge after\n");
		else if (merge is DatastarMerge.Before)
			await res.WriteAsync("data: merge before\n");

		await res.WriteAsync($"data: vt false\n\n", cancel);
	}

	public static async Task DataStarDelete(
		this HttpResponse res,
		string selector,
		CancellationToken cancel)
	{
		await res.WriteAsync($"event: datastar-delete\n", cancel);
		await res.WriteAsync($"data: selector {selector}\n\n", cancel);
	}

	public static async Task DataStarSignal(
		this HttpResponse res,
		object store,
		CancellationToken cancel)
	{
		await res.WriteAsync($"event: datastar-signal\n", cancel);
		await res.WriteAsync($"data: store {JsonSerializer.Serialize(store)}\n\n", cancel);
	}
}