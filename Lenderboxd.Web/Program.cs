using Azure.Storage.Queues;
using Lenderboxd;
using Lenderboxd.Web;
using Lenderboxd.Web.Components;
using Microsoft.AspNetCore.Components.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddKeyedAzureTableClient("tables");
builder.AddKeyedAzureQueueClient("queues");
builder.UseOrleans(silo =>
{
    silo.AddAzureQueueStreams("Default", (SiloAzureQueueStreamConfigurator configurator) =>
    {
        configurator.ConfigureAzureQueue(options =>
        {
            options.Configure<IServiceProvider>((queueOptions, sp) =>
            {
                queueOptions.QueueServiceClient = sp.GetKeyedService<QueueServiceClient>("queues");
            });
        });
    });
});

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddRazorComponents();
builder.Services.AddAntiforgery();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
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
app.UseResponseCompression();

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
        .Select((_, idx) => idx)
        .ToHashSet();

    if (pendingFilms.Count == 0)
        return res.CompleteAsync();

    await res.StartEventStream(cancel);

    var done = new TaskCompletionSource();
    cancel.Register(() => done.SetCanceled(cancel));
    var observer = new LetterboxdListObserver(async evt =>
    {
        pendingFilms.Remove(evt.Index);
        await res.DataStarSignal(new { pending = pendingFilms.Count }, cancel);

        var selector = $"tbody tr:nth-child({evt.Index + 1}) .loader";
        if (evt.Formats.Length > 0)
        {
            var markup = await renderer.RenderComponent<MediaFormats>(new() { { nameof(MediaFormats.Formats), evt.Formats } });
            await res.DataStarFragment(markup, cancel, selector);
        }
        else
            await res.DataStarDelete(selector, cancel);

        await res.Body.FlushAsync(cancel);

        if (pendingFilms.Count == 0)
        {
            await res.DataStarSignal(new { pending = 0 }, cancel);
            done.SetResult();
        }
    });

    var observerRef = grainFactory.CreateObjectReference<ILetterboxdList.IObserver>(observer);
    await listGrain.Subscribe(observerRef);

    try
    {
        await done.Task;
    }
    catch (OperationCanceledException) { }
    finally
    {
        app.Logger.LogInformation("Closing event stream for {User}/{List}", user, list);
        await listGrain.Unsubscribe(observerRef);
        await res.Body.FlushAsync(cancel);
    }
    return res.CompleteAsync();
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