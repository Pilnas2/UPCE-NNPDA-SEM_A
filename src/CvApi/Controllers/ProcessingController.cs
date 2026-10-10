using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CvApi;

[ApiController]
[Authorize]
[Route("api/cv")]
public sealed class ProcessingController : ControllerBase
{
    private readonly DocumentService _documents;
    private readonly ICvAi _ai;

    public ProcessingController(DocumentService documents, ICvAi ai)
    {
        _documents = documents;
        _ai = ai;
    }

    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ExtractResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ExtractResult> Extract([FromForm] UploadRequest upload, CancellationToken cancellationToken)
    {
        return _documents.Read(upload.File, cancellationToken);
    }

    [HttpPost("check")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CheckResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<CheckResult> Check([FromForm] UploadRequest upload, CancellationToken cancellationToken)
    {
        var result = await _documents.Read(upload.File, cancellationToken);

        // return Check property
        return result.Check;
    }

    [HttpPost("parse")]
    [ProducesResponseType<CvData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public Task<CvData> Parse(ParseRequest request, CancellationToken cancellationToken)
    {
        return _ai.Parse(request.Text, cancellationToken);
    }
}
