# LLamaWorker

LLamaWorker ��һ������ LLamaSharp ��Ŀ������ HTTP API �����������ṩ�� OpenAI ���ݵ� API��ʹ�ÿ����߿������ɵؽ���������ģ�ͣ�LLM�����ɵ��Լ���Ӧ�ó����С�

[English](README.md)] | ����

## ����

- **���� OpenAI API**: �ṩ�� OpenAI ���Ƶ� API������Ǩ�ƺͼ��ɡ�
- **��ģ��֧��**: ֧�����ú��л���ͬ��ģ�ͣ����㲻ͬ����������
- **��ʽ��Ӧ**: ֧����ʽ��Ӧ����ߴ�����Ӧ�Ĵ���Ч�ʡ�
- **Ƕ��֧��**: �ṩ�ı�Ƕ�빦�ܣ�֧�ֶ���Ƕ��ģ�͡�
- **�Ի�ģ��**: �ṩ��һЩ�����ĶԻ�ģ�档

## ���ٿ�ʼ

1. ��¡�ֿ⵽����
   ```bash
   git clone https://github.com/sangyuxiaowu/LLamaWorker.git
   ```
2. ������ĿĿ¼
   ```bash
   cd LLamaWorker/src
   ```
3. ������������ѡ����Ŀ�ļ�����Ŀ�ṩ�������汾����Ŀ�ļ���
   - `LLamaWorker.csproj`�������� CPU ������
   - `LLamaWorker_Cuad11.csproj`�������ڴ��� CUDA 11 �� GPU ������
   - `LLamaWorker_Cuad12.csproj`�������ڴ��� CUDA 12 �� GPU ������
   
   ѡ���ʺ�����������Ŀ�ļ�������һ����
   
4. ��װ������
   ```bash
   dotnet restore LLamaWorker.csproj
   ```
   �����ʹ�õ��� CUDA �汾�����滻��Ŀ�ļ�����
   
5. �޸������ļ� `appsettings.json`��Ĭ�������Ѱ���һЩ�����Ŀ�Դģ�����ã���ֻ�谴���޸�ģ���ļ�·����`ModelPath`�����ɡ�
   
6. ����������
   ```bash
   dotnet run --project LLamaWorker.csproj
   ```
   �����ʹ�õ��� CUDA �汾�����滻��Ŀ�ļ�����


## API �ο�

LLamaWorker �ṩ���� API �˵㣺

- `/v1/chat/completions`: �Ի��������
- `/v1/completions`: ��ʾ�������
- `/v1/embeddings`: ����Ƕ��
- `/models/info`: ����ģ�͵Ļ�����Ϣ
- `/models/config`: ���������õ�ģ����Ϣ
- `/models/{modelId}/switch`: �л���ָ��ģ��