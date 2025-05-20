using System.Diagnostics;

var builder = WebApplication.CreateSlimBuilder(args);

var app = builder.Build();

var apiGroup = app.MapGroup("/api/xelatex");

apiGroup.MapPost("/", async (HttpContext ctx, CancellationToken cancellationToken) =>
{
    if (ctx.Request.Form.Files is not { Count: 0 }) return Results.BadRequest("Required only one file uploaded.");
    var upload = ctx.Request.Form.Files[0];

    try
    {
        await using var stream = upload.OpenReadStream();
        var pdfFileName = $"{Path.GetFileNameWithoutExtension(upload.FileName)}.pdf";
        return Results.File(await Compile(stream, cancellationToken), "application/pdf", pdfFileName);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"An error occured while processing your request.\n{ex.Message}");
    }
});

app.Run();

return;
async ValueTask<byte[]> Compile(Stream tex, CancellationToken cancellationToken)
{
    var cwd = Directory.CreateTempSubdirectory("tex").FullName;
    var jobName = Guid.NewGuid().ToString();
    var texFileName = $"{jobName}.tex";
    var texFilePath = Path.Combine(cwd, texFileName);

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
        Arguments = $"-interaction=nonstopmode -halt-on-error ${fileName}",
        RedirectStandardInput = true,
        CreateNoWindow = true,
        WorkingDirectory = cwd,
    };

    using var process = Process.Start(psi);
    if (process is null) throw new InvalidOperationException("Create XeLaTex process failed.");
    
    await process.WaitForExitAsync(cancellationToken);
    
    if (process.ExitCode > 0) throw new InvalidOperationException("XeLaTex exited with code " + process.ExitCode + ".");
}