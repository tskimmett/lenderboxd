using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Lenderboxd;
using Lenderboxd.Web;
using Lenderboxd.Web.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.ResponseCompression;
using Orleans.Clustering.FoundationDb;
using Orleans.Persistence.FoundationDb;
using Orleans.Streaming.FoundationDb;

var builder = WebApplication.CreateBuilder(args);

builder.AddFoundationDb("fdb");

builder.UseOrleans((Action<ISiloBuilder>)(silo =>
{
    TryConfigureSiloEndpoints(silo, builder);

    // todo: create WithClustering()/WithGrainStorage() extensions for fdb+aspire?
    silo.UseFdbClustering();
    silo.AddFdbGrainStorage("Default");
    silo.AddFdbGrainStorage("PubSubStore");
    silo.AddFdbStreams("Default");

	// This configuration is needed when deploying to app service
    static void TryConfigureSiloEndpoints(ISiloBuilder silo, WebApplicationBuilder builder)
    {
        var privateIp = builder.Configuration["WEBSITE_PRIVATE_IP"];
        if (!string.IsNullOrEmpty(privateIp))
        {
            var endpointAddress = IPAddress.Parse(privateIp);
            var strPorts = builder.Configuration["WEBSITE_PRIVATE_PORTS"]!.Split(',');
            if (strPorts.Length < 2)
            {
                throw new Exception($"Insufficient private ports configured: {builder.Configuration["WEBSITE_PRIVATE_PORTS"]}");
            }

            var (siloPort, gatewayPort) = (int.Parse(strPorts[0]), int.Parse(strPorts[1]));
            silo.ConfigureEndpoints(endpointAddress, siloPort, gatewayPort, listenOnAnyHostAddress: true);
        }
    }
}));

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddRazorComponents();
builder.Services.AddAntiforgery();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["text/event-stream"]);
});
builder.Services.AddLogging();
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
    var films = await listGrain.GetFilms();
    var availability = await listGrain.GetFilmAvailability("www.richlandlibrary.com");
    var pendingFilms = availability!.Count(a => a is null);

    // do as little as possible between fetching grain state and subscribing to avoid missing events
    if (pendingFilms == 0)
    {
        await res.StartEventStream(cancel);
        await SendFilmTableFragment();
        return res.CompleteAsync();
    }

    var done = new TaskCompletionSource();
    cancel.Register(() => done.SetCanceled(cancel));
    var observer = new LetterboxdListObserver(async evt =>
    {
        pendingFilms--;
        await res.DataStarSignal(new { pending = pendingFilms }, cancel);

        await SendAvailabilityFragment(evt.Index, evt.Formats);

        await res.Body.FlushAsync(cancel);

        if (pendingFilms == 0)
        {
            await res.DataStarSignal(new { pending = 0 }, cancel);
            done.SetResult();
        }
    });

    var observerRef = grainFactory.CreateObjectReference<ILetterboxdList.IObserver>(observer);

    // prevent GC?
    LetterboxdListObserver.Cache.Add(observer);

    await res.StartEventStream(cancel);
    await listGrain.Subscribe(observerRef);
    await SendFilmTableFragment();

    try
    {
        await done.Task;
    }
    catch (OperationCanceledException) { }
    finally
    {
        LetterboxdListObserver.Cache.Remove(observer);
        app.Logger.LogInformation("Closing event stream for {User}/{List}", user, list);
        await listGrain.Unsubscribe(observerRef);
        await res.Body.FlushAsync(cancel);
    }
    return res.CompleteAsync();

    async Task SendAvailabilityFragment(int idx, MediaFormat[] formats)
    {
        var markup = await renderer.RenderComponent<MediaFormats>(new()
        {
            ["id"] = $"availability-{idx}",
            [nameof(MediaFormats.Formats)] = formats
        });
        await res.DataStarFragment(markup, cancel);
    }

    async Task SendFilmTableFragment()
    {
        // start by sending the entire film table in case the client is missing events due to reconnect
        var markup = await renderer.RenderComponent<FilmTable>(new()
        {
            [nameof(FilmTable.Films)] = films,
            [nameof(FilmTable.Availability)] = availability
        });
        await res.DataStarFragment(HtmlWhitespace().Replace(markup, "").Replace("\n", ""), cancel);
        await res.Body.FlushAsync();
    }
});

app.Run();

class LetterboxdListObserver : ILetterboxdList.IObserver
{
    public static HashSet<LetterboxdListObserver> Cache { get; } = [];

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

partial class Program
{
    [GeneratedRegex(@"(^\s*)|(\s*$)", RegexOptions.Multiline | RegexOptions.RightToLeft)]
    private static partial Regex HtmlWhitespace();
}
