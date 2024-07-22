using LLamaWorker.OpenAIModels;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace LLamaWorker.FunctionCall
{
    /// <summary>
    /// ����������ʾ������
    /// </summary>
    public class ToolPromptGenerator
    {
        private readonly List<ToolPromptConfig> _config;


        /// <summary>
        /// ����������ʾ������
        /// </summary>
        /// <param name="config">����������Ϣ</param>
        public ToolPromptGenerator(IOptions<List<ToolPromptConfig>> config)
        {
            _config = config.Value;
        }

        /// <summary>
        /// ��ȡ����ͣ�ô�
        /// </summary>
        /// <param name="tpl">ģ�����</param>
        /// <returns></returns>
        public string[] GetToolStopWords(int tpl = 0)
        {
            return _config[tpl].FN_STOP_WORDS;
        }

        /// <summary>
        /// ���ɹ�����ʾ��
        /// </summary>
        /// <param name="req">ԭʼ�Ի���������</param>
        /// <param name="tpl">ģ�����</param>
        /// <param name="lang">����</param>
        /// <returns></returns>
        public string GenerateToolPrompt(ChatCompletionRequest req, int tpl = 0, string lang = "zh")
        {
            // ���û�й��߻��߹���ѡ��Ϊ none���򷵻ؿ��ַ���
            if (req.tools == null || req.tools.Length == 0 || (req.tool_choice != null && req.tool_choice.ToString() == "none"))
            {
                return string.Empty;
            }

            var config = _config[tpl];

            var toolDescriptions = req.tools.Select(tool => GetFunctionDescription(tool.function, config.ToolDescTemplate[lang])).ToArray();
            var toolNames = string.Join(",", req.tools.Select(tool => tool.function.name));

            var toolDescTemplate = config.FN_CALL_TEMPLATE_INFO[lang];
            var toolDesc = string.Join("\n\n", toolDescriptions);
            var toolSystem = toolDescTemplate.Replace("{tool_descs}", toolDesc);

            var parallelFunctionCalls = req.tool_choice?.ToString() == "parallel";
            var toolTemplate = parallelFunctionCalls ? config.FN_CALL_TEMPLATE_FMT_PARA[lang] : config.FN_CALL_TEMPLATE_FMT[lang];
            var toolPrompt = string.Format(toolTemplate, config.FN_NAME, config.FN_ARGS, config.FN_RESULT, config.FN_EXIT, toolNames);
            return $"\n\n{toolSystem}\n\n{toolPrompt}";
        }

        private string GetFunctionDescription(FunctionInfo function, string toolDescTemplate)
        {
            var nameForHuman = function.name;
            var nameForModel = function.name;
            var descriptionForModel = function.description ?? string.Empty;
            var parameters = JsonSerializer.Serialize(function.parameters, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });

            return string.Format(toolDescTemplate, nameForHuman, nameForModel, descriptionForModel, parameters).Trim();
        }
    }
}