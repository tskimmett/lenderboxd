namespace LibraryBox.Web;

public static class ServerSentEvent
{
	public const string Close = "close-stream";
	/// <summary>
	/// Ensures the provided string is an HTMX-compatible SSE name.
	/// </summary>
	/// <param name="str"></param>
	/// <returns></returns>
	public static string ToSSE(this string str) => str.Replace(",", "_");
}
