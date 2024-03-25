namespace LibraryBox.Web;

using Microsoft.AspNetCore.Routing.Template;

public static class RouteMatcher
{
	public static bool TryMatch(string routeTemplate, string requestPath, out RouteValueDictionary values)
	{
		var template = TemplateParser.Parse(routeTemplate);

		var matcher = new TemplateMatcher(template, GetDefaults(template));

		values = [];
		return matcher.TryMatch(requestPath, values);
	}

	// This method extracts the default argument values from the template.
	static RouteValueDictionary GetDefaults(RouteTemplate parsedTemplate)
	{
		var result = new RouteValueDictionary();

		foreach (var parameter in parsedTemplate.Parameters)
		{
			if (parameter.Name != null && parameter.DefaultValue != null)
				result.Add(parameter.Name, parameter.DefaultValue);
		}

		return result;
	}
}
