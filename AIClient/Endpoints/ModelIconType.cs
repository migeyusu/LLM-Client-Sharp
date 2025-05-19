namespace LLMClient.Endpoints;

public enum ModelIconType : int
{
    None = 0,
    [DarkMode] ChatGpt,
    Gemini,
    Claude,
    Mistral,
    Deepseek,
    [DarkMode] Grok,
    Qwen,
    Microsoft,
    Nvidia,
    Meta,
    CodeGeex,
    Cohere,
    Doubao,
    Hunyuan,
    [DarkMode] Kimi,
    [DarkMode] Moonshot,
    Qingyan,
    Wenxin,
    [DarkMode] Yi,
    Zhipu
}

public class DarkModeAttribute : Attribute
{
}