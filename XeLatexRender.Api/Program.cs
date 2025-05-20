using System.Diagnostics;

var builder = WebApplication.CreateSlimBuilder(args);


var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.MapPost("/api/xelatex", async (IFormFile tex, CancellationToken cancellationToken) =>
{
    try
    {
        await using var stream = tex.OpenReadStream();
        var pdfFileName = $"{Path.GetFileNameWithoutExtension(tex.FileName)}.pdf";
        return Results.File(await Compile(stream, cancellationToken), "application/pdf", pdfFileName);
    }
    catch (Exception ex)
    {
        return Results.Text($"An error occured while processing your request.\n{ex.Message}", 
            contentType: "text/plain", 
            statusCode: 400);
    }
}).DisableAntiforgery();

app.Run();

return;
async ValueTask<byte[]> Compile(Stream tex, CancellationToken cancellationToken)
{
    var cwd = Directory.CreateTempSubdirectory("tex").FullName;
    var jobName = Guid.NewGuid().ToString();
    var texFileName = $"{jobName}.tex";
    var texFilePath = Path.Combine(cwd, texFileName);
    
    logger.LogInformation("Starting compiling tex {}", texFilePath);
    
    await using (var texFileWriteStream = File.OpenWrite(texFilePath))
    {
        await tex.CopyToAsync(texFileWriteStream, cancellationToken);
    }

    try
    {
        await XeLaTex(cwd, texFileName, cancellationToken);
        await XeLaTex(cwd, texFileName, cancellationToken);
        
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
        Directory.Delete(cwd, recursive: true);
    }
}

async Task XeLaTex(string cwd, string fileName, CancellationToken cancellationToken)
{
    var psi = new ProcessStartInfo()
    {
        FileName = "xelatex",
        Arguments = $" -interaction=nonstopmode -halt-on-error {fileName} ",
        RedirectStandardInput = true,
        CreateNoWindow = true,
        WorkingDirectory = cwd,
    };

    using var process = Process.Start(psi);
    if (process is null) throw new InvalidOperationException("Create XeLaTex process failed.");
    
    await process.WaitForExitAsync(cancellationToken);
    
    if (process.ExitCode > 0) throw new InvalidOperationException("XeLaTex exited with code " + process.ExitCode + ".");
}