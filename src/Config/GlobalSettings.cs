namespace LLamaWorker.Config
{
    /// <summary>
    /// ȫ������
    /// </summary>
    public static class GlobalSettings
    {
        /// <summary>
        ///  ��ʼ���ص�ģ������
        /// </summary>
        public static int CurrentModelIndex { get; set; } = 0;
        /// <summary>
        ///  ģ���Ƿ�����˼���
        /// </summary>
        public static bool IsModelLoaded { get; set; } = false;

        /// <summary>
        /// ģ���Զ��ͷ�ʱ��
        /// </summary>
        public static int AutoReleaseTime { get; set; } = 0;

        /// <summary>
        /// ��ʼ��ȫ������
        /// </summary>
        /// <param name="configuration">������Ϣ</param>
        public static void InitializeGlobalSettings(IConfiguration configuration)
        {
            var applicationSettings = configuration.GetSection("GlobalSettings");
            CurrentModelIndex = applicationSettings.GetValue<int>("CurrentModelIndex");
            AutoReleaseTime = applicationSettings.GetValue<int>("AutoReleaseTime");
        }
    }
}
