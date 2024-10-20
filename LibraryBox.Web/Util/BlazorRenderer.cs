namespace LibraryBox.Web;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;

class BlazorRenderer
{
	readonly HtmlRenderer _htmlRenderer;
	public BlazorRenderer(HtmlRenderer htmlRenderer)
	{
		_htmlRenderer = htmlRenderer;
	}

	/// <summary>
	/// Renders a component T which doesn't require any parameters
	/// </summary>
	public Task<string> RenderComponent<T>() where T : IComponent
		 => RenderComponent<T>(ParameterView.Empty);

	/// <summary>
	/// Renders a component T using the provided dictionary of parameters
	/// </summary>
	public Task<string> RenderComponent<T>(Dictionary<string, object?> dictionary) where T : IComponent
		 => RenderComponent<T>(ParameterView.FromDictionary(dictionary));

	Task<string> RenderComponent<T>(ParameterView parameters) where T : IComponent
	{
		// Use the default dispatcher to invoke actions in the context of the 
		// static HTML renderer and return as a string
		return _htmlRenderer.Dispatcher.InvokeAsync(async () =>
		{
			HtmlRootComponent output = await _htmlRenderer.RenderComponentAsync<T>(parameters);
			return output.ToHtmlString();
		});
	}
}
