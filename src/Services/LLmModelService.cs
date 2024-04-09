﻿using LLama;
using LLama.Batched;
using LLama.Common;
using LLamaWorker.Models;
using LLamaWorker.Models.OpenAI;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace LLamaWorker.Services
{
    public class LLmModelService : IDisposable
    {
        private readonly ILogger<LLmModelService> _logger;
        private readonly LLmModelSettings _settings;
        private readonly LLamaWeights _model;
        private readonly LLamaContext _context;

        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _disposedValue = false;

        public LLmModelService(IOptions<LLmModelSettings> options, ILogger<LLmModelService> logger)
        {
            _logger = logger;
            _settings = options.Value;

            if(string.IsNullOrWhiteSpace(_settings.ModelParams.ModelPath) ||
                !File.Exists(_settings.ModelParams.ModelPath))
            {
                _logger.LogError("Model path is error: {path}.", _settings.ModelParams.ModelPath);
                throw new ArgumentException("Model path is error.");
            }

            _model = LLamaWeights.LoadFromFile(_settings.ModelParams);
            _context = new LLamaContext(_model, _settings.ModelParams);
        }

        public async Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request)
        {
            var genParams = GetInferenceParams(request);
            var chatHistory = GetChatHistory(request.messages);
            var lastMessage = chatHistory.Messages.LastOrDefault();

            // 没有消息
            if (lastMessage is null)
            {
                _logger.LogWarning("No message in chat history.");
                return new ChatCompletionResponse();
            }

            // 去除最后一条消息
            chatHistory.Messages.RemoveAt(chatHistory.Messages.Count - 1);

            var executor = new InteractiveExecutor(_context);
            ChatSession session = new(executor, chatHistory);

            // 设置历史转换器和输出转换器
            session.WithHistoryTransform(new ChatMLHistoryTransform())
                .WithOutputTransform(new ChatMLTextStreamTransform());

            var result = "";
            await foreach (var output in session.ChatAsync(lastMessage, genParams))
            {
                _logger.LogDebug("Message: {output}", output);
                result += output;
            }

            var tokenizedInput = _context.Tokenize(lastMessage.Content);
            var tokenizedOutput = _context.Tokenize(result);

            //session.Executor.Context.Dispose();

            return new ChatCompletionResponse { 
                id = $"chatcmpl-{Guid.NewGuid()}",
                model = request.model,
                created = DateTimeOffset.Now.ToUnixTimeSeconds(),
                choices =
                [
                    new ChatCompletionResponseChoice
                    {
                        index = 0,
                        message = new ChatCompletionMessage
                        {
                            role = "assistant",
                            content = result
                        }
                    }
                ],
                usage = new UsageInfo
                {
                    prompt_tokens = tokenizedInput.Length,
                    completion_tokens = tokenizedOutput.Length,
                    total_tokens = tokenizedInput.Length + tokenizedOutput.Length
                }
            };
        }

        /// <summary>
        /// 流式生成聊天完成
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<string> CreateChatCompletionStreamAsync(ChatCompletionRequest request)
        {
            var genParams = GetInferenceParams(request);
            var chatHistory = GetChatHistory(request.messages);
            var lastMessage = chatHistory.Messages.LastOrDefault();

            // 没有消息
            if (lastMessage is null)
            {
                _logger.LogWarning("No message in chat history.");
                yield break;
            }

            // 去除最后一条消息
            chatHistory.Messages.RemoveAt(chatHistory.Messages.Count - 1);

            var executor = new InteractiveExecutor(_context);
            ChatSession session = new(executor, chatHistory);

            // 设置历史转换器和输出转换器
            session.WithHistoryTransform(new ChatMLHistoryTransform())
                .WithOutputTransform(new ChatMLTextStreamTransform());

            var id = $"chatcmpl-{Guid.NewGuid()}";
            var created = DateTimeOffset.Now.ToUnixTimeSeconds();

            int index = 0;

            // 第一个消息，带着角色名称
            var chunk = JsonSerializer.Serialize(new ChatCompletionChunkResponse
            {
                id = id,
                created = created,
                model = request.model,
                choices = [
                    new ChatCompletionChunkResponseChoice
                    {
                        index = index,
                        delta = new ChatCompletionMessage
                        {
                            role = "assistant"
                        },
                        finish_reason = null
                    }
                ]
            }, _jsonSerializerOptions);
            yield return $"data: {chunk}\n\n";

            // 处理模型输出
            await foreach (var output in session.ChatAsync(lastMessage, genParams))
            {
                _logger.LogDebug("Message: {output}", output);
                chunk = JsonSerializer.Serialize(new ChatCompletionChunkResponse
                {
                    id = id,
                    created = created,
                    model = request.model,
                    choices = [
                           new ChatCompletionChunkResponseChoice
                          {
                            index = ++index,
                            delta = new ChatCompletionMessage
                            {
                                 role = null,
                                 content = output
                            },
                            finish_reason= null
                          }
                      ],

                }, _jsonSerializerOptions);
                yield return $"data: {chunk}\n\n";
            }

            //session.Executor.Context.Dispose();

            // 结束
            chunk = JsonSerializer.Serialize(new ChatCompletionChunkResponse
            {
                id = id,
                created = created,
                model = request.model,
                choices = [
                    new ChatCompletionChunkResponseChoice
                    {
                        index = ++index,
                        delta = null,
                        finish_reason = "stop"
                    }
                ]
            }, _jsonSerializerOptions);
            yield return $"data: {chunk}\n\n";
            yield return "data: [DONE]\n\n";
            yield break;
        }


        /// <summary>
        /// 生成对话历史
        /// </summary>
        /// <param name="messages">对话历史</param>
        /// <returns></returns>
        private ChatHistory GetChatHistory(ChatCompletionMessage[] messages)
        {
            bool isSystem = false;
            var history = new ChatHistory();
            foreach (var message in messages)
            {
                var role = message.role;
                if (role == "system")
                {
                    if (isSystem)
                    {
                        _logger.LogWarning("Continuous system messages.");
                        continue;
                    }
                    isSystem = true;
                    history.AddMessage(AuthorRole.System, message.content);
                }
                else if (role == "user")
                {
                    history.AddMessage(AuthorRole.User, message.content);
                }else if (role == "assistant")
                {
                    history.AddMessage(AuthorRole.Assistant, message.content);
                }
                else
                {
                    _logger.LogWarning("Unknown role: {role}.", role);
                    continue;
                }
            }

            // 添加系统提示
            if(!string.IsNullOrWhiteSpace(_settings.SystemPrompt) && !isSystem)
            {
                _logger.LogInformation("Add system prompt.");
                history.Messages.Insert(0, new ChatHistory.Message(AuthorRole.System, _settings.SystemPrompt));
            }

            return history;
        }

        /// <summary>
        /// 生成推理参数
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static InferenceParams GetInferenceParams(ChatCompletionRequest request)
        {
            const string startToken = "<|im_start|>";
            var stop = new List<string>();
            if (request.stop != null)
            {
                foreach (var item in request.stop)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        stop.Add(item.ToString());
                    }
                    if (stop.Count >= 3)
                    {
                        break;
                    }
                }
                // 如果没有加入写开始标记，并且没有超过3个停止标记
                if (!stop.Contains(startToken) && stop.Count<3)
                {
                    stop.Add(startToken);
                }
            }
            else
            {
                stop.Add(startToken);
            }
            InferenceParams inferenceParams = new InferenceParams()
            {
                MaxTokens = request.max_tokens ?? 512,
                AntiPrompts = stop,
                Temperature = request.temperature,
                TopP = request.top_p,
                PresencePenalty = request.presence_penalty,
                FrequencyPenalty = request.frequency_penalty,
            };
            return inferenceParams;
        }


        /// <summary>
        /// 释放非托管资源
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _model.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
