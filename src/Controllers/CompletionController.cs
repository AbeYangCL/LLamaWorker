using LLamaWorker.Models.OpenAI;
using LLamaWorker.Services;
using Microsoft.AspNetCore.Mvc;

namespace LLamaWorker.Controllers
{
    /// <summary>
    /// ��ʾ��ɿ�����
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class CompletionController : ControllerBase
    {

        private readonly ILogger<CompletionController> _logger;

        /// <summary>
        /// ��ʾ��ɿ�����
        /// </summary>
        /// <param name="logger">��־</param>
        public CompletionController(ILogger<CompletionController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// ��ʾ�������
        /// </summary>
        /// <param name="request"></param>
        /// <param name="service"></param>
        /// <remarks>
        /// Ĭ�ϲ�������ʽ����Ҫ�������� stream:true
        /// </remarks>
        /// <response code="200">ģ�ͶԻ����</response>
        /// <response code="400">������Ϣ</response>
        [HttpPost("/v1/completions")]
        [HttpPost("/completions")]
        [HttpPost("/openai/deployments/{model}/completions")]
        [Produces("text/event-stream")]
        [ProducesResponseType(StatusCodes.Status200OK,Type = typeof(CompletionResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
        public async Task<IResult> CreateCompletionAsync([FromBody] CompletionRequest request, [FromServices] ILLmModelService service)
        {
            try
            {
                if (request.stream)
                {

                    string first = " ";
                    await foreach (var item in service.CreateCompletionStreamAsync(request))
                    {
                        if(first == " ")
                        {
                            first = item;
                        }
                        else
                        {
                            if (first.Length > 1)
                            {
                                Response.Headers.ContentType = "text/event-stream";
                                Response.Headers.CacheControl = "no-cache";
                                await Response.Body.FlushAsync();
                                await Response.WriteAsync(first);
                                await Response.Body.FlushAsync();
                                first = "";
                            }
                            await Response.WriteAsync(item);
                            await Response.Body.FlushAsync();
                        }
                    }
                    return Results.Empty;
                }
                else
                {
                    return Results.Ok(await service.CreateCompletionAsync(request));
                }
                
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error in CreateCompletionAsync");
                return Results.Problem($"{ex.Message}");
            }
                
        }
    }
}
