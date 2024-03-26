using LibraryBox;
using LibraryBox.Web;
using LibraryBox.Web.Components;
using Microsoft.AspNetCore.Components.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleans(siloBuilder =>
{
});

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddRazorComponents();
builder.Services.AddAntiforgery();
builder.Services.AddHttpLogging(options => { });
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 5001;
});
builder.Services.AddScoped<HtmlRenderer>();
builder.Services.AddScoped<BlazorRenderer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpLogging();
app.UseHttpsRedirection();

// app.UseMiddleware<ExtensionlessJsMiddleware>();
app.UseStaticFiles();

app.UseRouting();
app.UseAntiforgery();

app.MapRazorComponents<App>();
app.MapControllers();

app.MapGet("/film-list/{user}/{list}/events", async (
    string user,
    string list,
    IGrainFactory grainFactory,
    BlazorRenderer renderer,
    HttpResponse res,
    CancellationToken cancel) =>
{
    var id = $"{user}/{list}";
    var listGrain = grainFactory.GetGrain<ILetterboxdList>(id);
    var availability = await listGrain.GetFilmAvailability("www.richlandlibrary.com");
    var pendingFilms = availability
        .Where(a => a.formats is null)
        .Select(a => a.filmTitle)
        .ToHashSet();

    res.Headers.ContentType = "text/event-stream";
    res.StatusCode = 200;

    if (pendingFilms.Count == 0)
    {
        await res.CompleteAsync();
        return;
    }

    await res.StartAsync(cancel);

    var done = new TaskCompletionSource();
    cancel.Register(() => done.SetCanceled(cancel));
    var observer = new LetterboxdListObserver(async evt =>
    {
        pendingFilms.Remove(evt.Title);
        await res.WriteAsync($"event: {evt.Title.ToSSE()}\n", cancel);
        var markup = await renderer.RenderComponent<MediaFormats>(new() { { nameof(MediaFormats.Formats), evt.Formats } });
        await res.WriteAsync($"data: {markup}\n\n", cancel);
        await res.Body.FlushAsync(cancel);
        // if (pendingFilms.Count == 0)
        //     done.SetResult();
    });

    var observerRef = grainFactory.CreateObjectReference<ILetterboxdList.IObserver>(observer);
    await listGrain.Subscribe(observerRef);

    try
    {
        await done.Task;
    }
    catch (OperationCanceledException) { }

    app.Logger.LogInformation("Closing event stream for {User}/{List}", user, list);

    await listGrain.Unsubscribe(observerRef);

    await res.CompleteAsync();
});

app.Run();

class LetterboxdListObserver : ILetterboxdList.IObserver
{
    readonly Func<FilmAvailabilityEvent, Task> _handler;

    public LetterboxdListObserver(Func<FilmAvailabilityEvent, Task> handler)
    {
        _handler = handler;
    }

    public async Task FilmAvailabilityReady(FilmAvailabilityEvent evt)
    {
        await _handler(evt);
    }
}