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
using LLama.Abstractions;

namespace LLamaWorker.Services
{
    /// <summary>
    /// LLM 模型服务
    /// </summary>
    public class LLmModelService : IDisposable
    {
        private readonly ILogger<LLmModelService> _logger;
        private readonly List<LLmModelSettings> _settings;
        private LLmModelSettings _usedset;
        private LLamaWeights _model;
        private LLamaContext _context;
        private int _loadModelIndex = 0;

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


        /// <summary>
        /// 初始化指定模型
        /// </summary>
        /// <param name="loadModelIndex">模型配置索引</param>
        /// <param name="reLoad">是否重新加载，则会先释放资源</param>
        /// <exception cref="ArgumentException"></exception>
        public void InitModelIndex(int loadModelIndex, bool reLoad = false)
        {
            if (_settings.Count == 0)
            {
                _logger.LogError("No model settings.");
                throw new ArgumentException("No model settings.");
            }

            if (loadModelIndex < 0 || loadModelIndex >= _settings.Count)
            {
                _logger.LogError("Invalid model index: {modelIndex}.", loadModelIndex);
                throw new ArgumentException("Invalid model index.");
            }

            var usedset = _settings[loadModelIndex];

            if (string.IsNullOrWhiteSpace(usedset.ModelParams.ModelPath) ||
                               !File.Exists(usedset.ModelParams.ModelPath))
            {
                _logger.LogError("Model path is error: {path}.", usedset.ModelParams.ModelPath);
                throw new ArgumentException("Model path is error.");
            }

            _loadModelIndex = loadModelIndex;
            _usedset = usedset;

            // 重新加载,释放资源
            if (reLoad)
            {
                _context.Dispose();
                _model.Dispose();
            }

            _model = LLamaWeights.LoadFromFile(usedset.ModelParams);
            _context = new LLamaContext(_model, usedset.ModelParams);
        }


        /// <summary>
        /// LLmModelService
        /// </summary>
        /// <param name="options">模型配置列表</param>
        /// <param name="logger">日志</param>
        public LLmModelService(IOptions<List<LLmModelSettings>> options, ILogger<LLmModelService> logger)
        {
            _logger = logger;
            _settings = options.Value;
            InitModelIndex(_loadModelIndex);
        }

        /// <summary>
        /// 获取模型信息
        /// </summary>
        /// <returns></returns>
        public IReadOnlyDictionary<string,string> GetModelInfo()
        {
            return _model.Metadata;
        }

        /// <summary>
        /// 获取模型的配置列表
        /// </summary>
        /// <returns></returns>
        public List<LLmModelSettings> GetModelSettings()
        {
            return _settings;
        }
        
        /// <summary>
        /// 当前模型配置
        /// </summary>
        /// <returns></returns>
        public LLmModelSettings GetCurrentModelSettings()
        {
            return _usedset;
        }



        /// <summary>
        /// 聊天完成
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
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
            // 清除缓存
            session.Executor.Context.NativeHandle.KvCacheClear();

            // 设置历史转换器和输出转换器
            if (_usedset.WithTransform?.HistoryTransform != null)
            {
                var type = Type.GetType(_usedset.WithTransform.HistoryTransform);
                if (type != null)
                {
                    var historyTransform = Activator.CreateInstance(type) as IHistoryTransform;
                    if (historyTransform != null)
                    {
                        session.WithHistoryTransform(historyTransform);
                    }
                }
            }
            if (_usedset.WithTransform?.OutputTransform != null)
            {
                var type = Type.GetType(_usedset.WithTransform.OutputTransform);
                if (type != null)
                {
                    var outputTransform = Activator.CreateInstance(type) as ITextStreamTransform;
                    if (outputTransform != null)
                    {
                        session.WithOutputTransform(outputTransform);
                    }
                }
            }

            var result = "";
            await foreach (var output in session.ChatAsync(lastMessage, genParams))
            {
                _logger.LogDebug("Message: {output}", output);
                result += output;
            }

            var tokenizedInput = _context.Tokenize(lastMessage.Content);
            var tokenizedOutput = _context.Tokenize(result);

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
        /// 流式生成-聊天完成
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
            // 清除缓存
            session.Executor.Context.NativeHandle.KvCacheClear();

            // 设置历史转换器和输出转换器
            if (_usedset.WithTransform?.HistoryTransform != null)
            {
                var type = Type.GetType(_usedset.WithTransform.HistoryTransform);
                if (type != null)
                {
                    var historyTransform = Activator.CreateInstance(type) as IHistoryTransform;
                    if (historyTransform != null)
                    {
                        session.WithHistoryTransform(historyTransform);
                    }
                }
            }
            if (_usedset.WithTransform?.OutputTransform != null)
            {
                var type = Type.GetType(_usedset.WithTransform.OutputTransform);
                if (type != null)
                {
                    var outputTransform = Activator.CreateInstance(type) as ITextStreamTransform;
                    if (outputTransform != null)
                    {
                        session.WithOutputTransform(outputTransform);
                    }
                }
            }

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
            if(!string.IsNullOrWhiteSpace(_usedset.SystemPrompt) && !isSystem)
            {
                _logger.LogInformation("Add system prompt.");
                history.Messages.Insert(0, new ChatHistory.Message(AuthorRole.System, _usedset.SystemPrompt));
            }

            return history;
        }

        /// <summary>
        /// 生成推理参数
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private InferenceParams GetInferenceParams(ChatCompletionRequest request)
        {
            var stop = new List<string>();
            if (request.stop != null)
            {
                stop.AddRange(request.stop);
            }
            if(_usedset.AntiPrompts?.Length>0)
            {
                stop.AddRange(_usedset.AntiPrompts);
            }
            if (stop.Count > 0)
            {
                //去重，去空，并且至多4个
                stop = stop.Distinct().Where(x => !string.IsNullOrWhiteSpace(x)).Take(4).ToList();
            }

            InferenceParams inferenceParams = new InferenceParams()
            {
                MaxTokens = request.max_tokens.HasValue && request.max_tokens.Value > 0 ? request.max_tokens.Value : 512,
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

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        
    }
}
