namespace LLamaWorker
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
    }
}
