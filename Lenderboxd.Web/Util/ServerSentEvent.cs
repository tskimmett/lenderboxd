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

	public static async Task DataStarFragment(
		this HttpResponse res,
		string fragment,
		CancellationToken cancel,
		string? selector = null)
	{
		await res.WriteAsync($"event: datastar-fragment\n", cancel);
		if (selector is not null)
			await res.WriteAsync($"data: selector {selector}\n", cancel);
		await res.WriteAsync($"data: fragment {fragment}\n\n", cancel);
		await res.Body.FlushAsync(cancel);
	}

	public static async Task DataStarDelete(
		this HttpResponse res,
		string selector,
		CancellationToken cancel)
	{
		await res.WriteAsync($"event: datastar-delete\n", cancel);
		await res.WriteAsync($"data: selector {selector}\n\n", cancel);
		await res.Body.FlushAsync(cancel);
	}

	public static async Task DataStarSignal(
		this HttpResponse res,
		object store,
		CancellationToken cancel)
	{
		await res.WriteAsync($"event: datastar-signal\n", cancel);
		// todo: json might not work?
		await res.WriteAsync($"data: store {JsonSerializer.Serialize(store)}\n\n", cancel);
		await res.Body.FlushAsync(cancel);
	}
}