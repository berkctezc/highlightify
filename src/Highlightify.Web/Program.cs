using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Highlightify.Web.Contracts;
using Highlightify.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

var spotifySettings = new SpotifySettings(
	builder.Configuration["SPOTIFY_CLIENT_ID"] ?? builder.Configuration["Spotify:ClientId"],
	builder.Configuration["SPOTIFY_REDIRECT_URI"] ?? builder.Configuration["Spotify:RedirectUri"]
		?? "http://127.0.0.1:5086/api/auth/spotify/callback",
	builder.Configuration["HIGHLIGHTIFY_FRONTEND_URL"] ?? builder.Configuration["App:FrontendUrl"]
		?? "http://127.0.0.1:5173");

var appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
var dataProtectionKeysPath = Path.Combine(appDataPath, "keys");
Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = 12_500_000;
	options.ValueLengthLimit = 6_000_000;
});
builder.Services.AddSingleton(spotifySettings);
builder.Services.AddDataProtection()
	.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
	.SetApplicationName("Highlightify.Web");
builder.Services.AddSingleton<SpotifySessionStore>();
builder.Services.AddSingleton<ArtworkSimilarityService>();
builder.Services.AddSingleton<SpotifyWebService>();
builder.Services.AddSingleton<ImportJobService>();
builder.Services.AddHttpClient("spotify", client =>
{
	client.BaseAddress = new Uri("https://api.spotify.com/v1/");
	client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddHttpClient("instagram", client => client.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddHttpClient("artwork", client =>
{
	client.Timeout = TimeSpan.FromSeconds(15);
	client.DefaultRequestHeaders.UserAgent.ParseAdd("Highlightify/1.0 artwork-matcher");
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
	.WithOrigins(spotifySettings.FrontendUrl.TrimEnd('/'))
	.AllowAnyHeader()
	.AllowAnyMethod()
	.AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
	var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
	var (status, message) = exception switch
	{
		UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, exception.Message),
		KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
		ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
		InvalidOperationException => (StatusCodes.Status409Conflict, exception.Message),
		_ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu.")
	};

	context.Response.StatusCode = status;
	context.Response.ContentType = "application/json";
	await context.Response.WriteAsJsonAsync(new ApiErrorResponse(message));
}));

app.Use(async (context, next) =>
{
	context.Response.Headers.XContentTypeOptions = "nosniff";
	context.Response.Headers.XFrameOptions = "DENY";
	context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
	context.Response.Headers.ContentSecurityPolicy =
		"default-src 'self'; img-src 'self' https: data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self' https://accounts.spotify.com";
	context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
	await next();
});

app.UseCors();
app.Use(async (context, next) =>
{
	SessionIdentity.GetOrCreate(context);
	if (context.Request.Path.StartsWithSegments("/api"))
		context.Response.Headers.CacheControl = "no-store";
	await next();
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
	OnPrepareResponse = context =>
	{
		context.Context.Response.Headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets")
			? "public,max-age=31536000,immutable"
			: "no-cache";
	}
});

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new
{
	status = "ok",
	time = DateTimeOffset.UtcNow
}));

api.MapGet("/config", () => Results.Ok(new AppConfigurationResponse(
	spotifySettings.IsConfigured,
	FindExecutable("yt-dlp") is not null,
	OperatingSystem.IsMacOS() ? "safari" : "firefox",
	Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0")));

api.MapGet("/auth/spotify/login", (HttpContext context, SpotifyWebService spotify, string? returnUrl) =>
{
	var sessionId = SessionIdentity.GetOrCreate(context);
	return Results.Redirect(spotify.CreateAuthorizationUrl(sessionId, returnUrl ?? "/"));
});

api.MapGet("/auth/spotify/callback", async (
	HttpContext context,
	SpotifyWebService spotify,
	string? code,
	string? state,
	string? error,
	CancellationToken cancellationToken) =>
{
	var frontendUrl = ResolveFrontendUrl(context, spotifySettings, app.Environment);
	if (!string.IsNullOrWhiteSpace(error))
		return Results.Redirect($"{frontendUrl}/app?spotify=error&reason={Uri.EscapeDataString(error)}");
	if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
		return Results.Redirect($"{frontendUrl}/app?spotify=error&reason=missing_callback");

	try
	{
		var returnPath = await spotify.CompleteAuthorizationAsync(
			SessionIdentity.GetOrCreate(context), code, state, cancellationToken);
		return Results.Redirect($"{frontendUrl}{returnPath}{(returnPath.Contains('?') ? '&' : '?')}spotify=connected");
	}
	catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
	{
		app.Logger.LogWarning(exception, "Spotify callback failed");
		return Results.Redirect($"{frontendUrl}/app?spotify=error&reason=callback_failed");
	}
});

api.MapGet("/auth/spotify/status", async (
	HttpContext context,
	SpotifyWebService spotify,
	CancellationToken cancellationToken) =>
	Results.Ok(await spotify.GetConnectionAsync(SessionIdentity.GetOrCreate(context), cancellationToken)));

api.MapPost("/auth/spotify/disconnect", (HttpContext context, SpotifyWebService spotify) =>
{
	spotify.Disconnect(SessionIdentity.GetOrCreate(context));
	return Results.NoContent();
});

api.MapGet("/spotify/playlists", async (
	HttpContext context,
	SpotifyWebService spotify,
	CancellationToken cancellationToken) =>
	Results.Ok(await spotify.GetPlaylistsAsync(SessionIdentity.GetOrCreate(context), cancellationToken)));

api.MapGet("/imports", (HttpContext context, ImportJobService jobs) =>
	Results.Ok(jobs.GetAll(SessionIdentity.GetOrCreate(context))));

api.MapGet("/imports/{id:guid}", (HttpContext context, Guid id, ImportJobService jobs) =>
	jobs.Get(SessionIdentity.GetOrCreate(context), id) is { } job
		? Results.Ok(job)
		: Results.NotFound(new ApiErrorResponse("Aktarım bulunamadı.")));

api.MapPost("/imports", async (
	HttpContext context,
	ImportJobService jobs,
	CancellationToken cancellationToken) =>
{
	if (!context.Request.HasFormContentType)
		return Results.BadRequest(new ApiErrorResponse("Aktarım multipart/form-data olarak gönderilmeli."));

	var form = await context.Request.ReadFormAsync(cancellationToken);
	var sources = new List<ImportSourceInput>();
	foreach (var value in form["sources"])
	{
		var source = value?.Trim();
		if (!string.IsNullOrWhiteSpace(source))
			sources.Add(new ImportSourceInput(source, source, null));
	}

	long totalFileSize = 0;
	foreach (var file in form.Files)
	{
		totalFileSize += file.Length;
		if (file.Length == 0)
			continue;
		if (file.Length > 6_000_000 || totalFileSize > 12_000_000)
			return Results.BadRequest(new ApiErrorResponse("HTML dosyaları toplam 12 MB, dosya başına 6 MB sınırını aşamaz."));
		if (!Path.GetExtension(file.FileName).Equals(".html", StringComparison.OrdinalIgnoreCase) &&
		    !Path.GetExtension(file.FileName).Equals(".htm", StringComparison.OrdinalIgnoreCase))
			return Results.BadRequest(new ApiErrorResponse("Yalnızca .html veya .htm dosyaları yüklenebilir."));

		using var reader = new StreamReader(file.OpenReadStream());
		var html = await reader.ReadToEndAsync(cancellationToken);
		sources.Add(new ImportSourceInput(Path.GetFileName(file.FileName), null, html));
	}

	var id = jobs.Start(SessionIdentity.GetOrCreate(context), new StartImportInput(
		sources,
		form["browserSource"].FirstOrDefault()));
	return Results.Accepted($"/api/imports/{id}", new ImportCreatedResponse(id));
}).DisableAntiforgery();

api.MapPost("/imports/{id:guid}/match", (HttpContext context, Guid id, ImportJobService jobs) =>
{
	jobs.RetryMatching(SessionIdentity.GetOrCreate(context), id);
	return Results.Accepted($"/api/imports/{id}", new ImportCreatedResponse(id));
});

api.MapPost("/imports/{id:guid}/export", async (
	HttpContext context,
	Guid id,
	ExportPlaylistRequest request,
	ImportJobService jobs,
	CancellationToken cancellationToken) =>
	Results.Ok(await jobs.ExportAsync(
		SessionIdentity.GetOrCreate(context), id, request, cancellationToken)));

api.MapMethods("/{**path}", ["GET", "POST", "PUT", "PATCH", "DELETE"], () =>
	Results.NotFound(new ApiErrorResponse("API adresi bulunamadı.")));

app.MapFallback(async context =>
{
	var indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
	if (File.Exists(indexPath))
	{
		context.Response.ContentType = "text/html; charset=utf-8";
		await context.Response.SendFileAsync(indexPath);
		return;
	}

	context.Response.StatusCode = StatusCodes.Status404NotFound;
	await context.Response.WriteAsync("Highlightify frontend build bulunamadı. web dizininde 'pnpm build' çalıştırın.");
});

app.Run();

static string ResolveFrontendUrl(HttpContext context, SpotifySettings settings, IWebHostEnvironment environment)
{
	if (environment.IsDevelopment())
		return settings.FrontendUrl.TrimEnd('/');
	return $"{context.Request.Scheme}://{context.Request.Host}";
}

static string? FindExecutable(string name)
{
	var path = Environment.GetEnvironmentVariable("PATH");
	if (string.IsNullOrWhiteSpace(path))
		return null;
	return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		.Select(directory => Path.Combine(directory, name))
		.FirstOrDefault(File.Exists);
}

public partial class Program;
