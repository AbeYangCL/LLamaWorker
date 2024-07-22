
namespace LLamaWorker.Services
{
    /// <summary>
    /// ��Ϣ��ʷ��¼���
    /// </summary>
    public class ChatHistoryResult
    {
        /// <summary>
        /// ��ʷ��¼
        /// </summary>
        public string ChatHistory { get; set; }

        /// <summary>
        /// �Ƿ������˹�����ʾ
        /// </summary>
        public bool IsToolPromptEnabled { get; set; }

        /// <summary>
        /// ���߽������
        /// </summary>
        public string[]? ToolStopWords { get; set; }

        /// <summary>
        /// ��Ϣ��ʷ��¼���
        /// </summary>
        /// <param name="chatHistory">��ʷ��¼</param>
        /// <param name="isToolPromptEnabled">�Ƿ������˹�����ʾ</param>
        /// <param name="toolStopWords">���߽������</param>
        public ChatHistoryResult(string chatHistory, bool isToolPromptEnabled, string[]? toolStopWords)
        {
            ChatHistory = chatHistory;
            IsToolPromptEnabled = isToolPromptEnabled;
            ToolStopWords = toolStopWords;
        }
    }
}