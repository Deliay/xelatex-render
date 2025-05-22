using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateSlimBuilder(args);

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

const string contentType = "application/pdf";

app.MapPost("/api/xelatex/body", async (Stream body, CancellationToken cancellationToken)
    => await RenderAsync(body, Guid.NewGuid().ToString(), cancellationToken: cancellationToken))
    .Accepts<string>("text/plain")
    .Produces<byte[]>(200, contentType)
    .DisableAntiforgery();

var knownSessions = new HashSet<string>();

app.MapPost("/api/xelatex/session", async () =>
{
    var session = Guid.NewGuid().ToString();
    knownSessions.Add(session);
    return await Task.FromResult(Results.Text(session, contentType: "text/plain"));
}).Accepts<string>("text/plain").Produces<string>(200, "text/plain").DisableAntiforgery();

app.MapPut("/api/xelatex/session/{session}", async (Stream body, string session,
    [FromQuery] string engine = "latexmk", CancellationToken cancellationToken = default) =>
{

    var baseDir = Path.Combine(Path.GetTempPath(), $"tex-{session}");

    if (!knownSessions.Contains(session))
    {
        if (Directory.Exists(baseDir)) knownSessions.Add(session);
        else return Results.NotFound();
    }
    if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
    
    return await RenderAsync(body, 
        name: session,
        keepFile: true,
        dir: baseDir, 
        engine: engine,
        cancellationToken: cancellationToken);
}).Accepts<string>("text/plain").DisableAntiforgery();

app.Run();

return;

async Task<IResult> RenderAsync(Stream tex, string? name = null, bool keepFile = false,
    string? dir = null, string? returnFileName = null,
    string engine = "latexmk",
    CancellationToken cancellationToken = default)
{
    try
    {
        var jobName = name ?? Guid.NewGuid().ToString();
        var pdfFileName = returnFileName ?? $"{jobName}.pdf";
        var data = await Compile(tex, jobName, dir, engine, keepFile, cancellationToken);
        return Results.File(data, contentType, pdfFileName);
    }
    catch (Exception ex)
    {
        return Results.Text($"An error occured while processing your request.\n{ex.Message}", 
            contentType: "text/plain", 
            statusCode: 400);
    }
}

async ValueTask<byte[]> Compile(Stream tex, string? name = null, string? dir = null,
    string engine = "latexmk",
    bool keepFile = false, CancellationToken cancellationToken = default)
{
    var cwd = dir ?? Directory.CreateTempSubdirectory("tex-").FullName;
    var jobName = name ?? Guid.NewGuid().ToString();
    var texFileName = $"{jobName}.tex";
    var texFilePath = Path.Combine(cwd, texFileName);
    
    logger.LogInformation("Starting compiling tex {}", texFilePath);
    
    if (File.Exists(texFilePath)) File.Delete(texFilePath);
    
    await using (var texFileWriteStream = File.OpenWrite(texFilePath))
    {
        await tex.CopyToAsync(texFileWriteStream, cancellationToken);
    }

    try
    {
        if (engine == "xelatex") await XeLaTex(cwd, texFileName, cancellationToken);
        else await LaTexMk(cwd, texFileName, cancellationToken);
        
        return await File.ReadAllBytesAsync(Path.Combine(cwd, $"{jobName}.pdf"), cancellationToken);
    }
    catch (Exception e)
    {
        var texLogFileName = $"{jobName}.log";
        if (File.Exists(Path.Combine(cwd, texFileName)))
        {
            throw new InvalidOperationException(File.ReadAllText(texLogFileName), e);
        }

        throw;
    }
    finally
    {
        if (!keepFile)
        {
            Directory.Delete(cwd, recursive: true);
        }
    }
}

async Task XeLaTex(string cwd, string fileName, CancellationToken cancellation)
{
    await XeLaTexCore(cwd, fileName, cancellation);
    await XeLaTexCore(cwd, fileName, cancellation);
}

async Task XeLaTexCore(string cwd, string fileName, CancellationToken cancellationToken)
{
    var psi = new ProcessStartInfo()
    {
        FileName = "xelatex",
        Arguments = $" -interaction=nonstopmode -halt-on-error {fileName} ",
        WorkingDirectory = cwd,
        RedirectStandardError = false,
        RedirectStandardOutput = false,
        RedirectStandardInput = false,
    };

    using var process = Process.Start(psi);
    if (process is null) throw new InvalidOperationException("Create XeLaTex process failed.");
    
    await process.WaitForExitAsync(cancellationToken);
    
    if (process.ExitCode > 0) throw new InvalidOperationException("XeLaTex exited with code " + process.ExitCode + ".");
}

async Task LaTexMk(string cwd, string fileName, CancellationToken cancellationToken)
{
    var psi = new ProcessStartInfo()
    {
        FileName = "latexmk",
        Arguments = $" -xelatex -silent -g {fileName} ",
        WorkingDirectory = cwd,
        RedirectStandardError = false,
        RedirectStandardOutput = false,
        RedirectStandardInput = false,
    };

    using var process = Process.Start(psi);
    if (process is null) throw new InvalidOperationException("Create XeLaTex process failed.");
    
    await process.WaitForExitAsync(cancellationToken);
    
    if (process.ExitCode > 0) throw new InvalidOperationException("XeLaTex exited with code " + process.ExitCode + ".");
}
