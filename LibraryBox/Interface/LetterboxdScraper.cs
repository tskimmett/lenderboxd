namespace LibraryBox;

using System.Diagnostics;
using AngleSharp;

public class LetterboxdScraper
{
	public static async Task<LetterboxdListData> FetchFilms(string user, string list)
	{
		user = user.ToLower();
		list = list.ToLower();

		if (string.IsNullOrWhiteSpace(user))
			throw new ArgumentNullException(nameof(user));
		if (string.IsNullOrWhiteSpace(list))
			throw new ArgumentNullException(nameof(list));

		string? title = null;
		int numPages = 1;
		List<Film> films = [];
		var ctx = BrowsingContext.New(Configuration.Default.WithDefaultLoader());

		films.AddRange(await ProcessPage(1));
		if (numPages > 1)
		{
			foreach (var page in await Task.WhenAll(Enumerable.Range(2, numPages - 1).Select(ProcessPage)))
				films.AddRange(page);
		}

		return new(title, films.ToArray());

		async Task<IEnumerable<Film>> ProcessPage(int page)
		{
			var doc = await ctx.OpenAsync($"https://letterboxd.com/{user}/list/{list}/detail/page/{page}");

			if (page == 1)
			{
				title = doc.QuerySelector("h1.title-1")?.TextContent.Trim();
				var lastPage = doc.QuerySelectorAll(".paginate-page").LastOrDefault();
				if (lastPage is not null)
					numPages = int.Parse(lastPage.TextContent.Trim());
			}

			return doc.QuerySelectorAll("li.film-detail")
				.Select(detail =>
				{
					var poster = detail.QuerySelector(".poster[data-film-id][data-film-slug]");
					if (poster is null || detail is null)
						return null;

					var title = detail.QuerySelector("h2 > a")!.TextContent.Trim();
					var hasYear = uint.TryParse(detail.QuerySelector("h2 > small")?.TextContent.Trim(), out var releaseYear);
					return new Film(
						Id: poster.GetAttribute("data-film-id")!,
						Slug: poster.GetAttribute("data-film-slug")!,
						Title: title,
						ReleaseYear: hasYear ? releaseYear : null
					);
				})
				.Where(film => film is not null)
				.Select(film => film!);
		}
	}
}

[GenerateSerializer]
public record LetterboxdListData(string? Title, Film[] Films)
{
}