namespace LLMClient.Endpoints;

public class ModelDescriptor
{
    public required string ProviderName { get; set; }
    public required string SeriesName { get; set; }
    public required string[] ModelNames { get; set; }
}

public static class ModelRegister
{
    private static List<ModelDescriptor>? _officialModels;

    public static List<ModelDescriptor> OfficialModels
    {
        get
        {
            _officialModels ??=
            [
                new()
                {
                    ProviderName = "OpenAI",
                    SeriesName = "OpenAI",
                    ModelNames =
                    [
                        "GPT-3.5-Turbo",
                        "GPT-4",
                        "GPT-4.1",
                        "GPT-4.1-mini",
                        "GPT-4.1-nano",
                        "GPT-4o",
                        "GPT-4o-mini",
                        "o1",
                        "o1-mini",
                        "o3",
                        "o3-mini",
                        "o4-mini",
                        "GPT-5",
                        "GPT-5-Chat",
                        "GPT-5-mini",
                        "GPT-5.1",
                        "GPT-5.2",
                        "GPT-5.2 Pro"
                    ]
                },

                new()
                {
                    ProviderName = "Google",
                    SeriesName = "Gemini",
                    ModelNames =
                    [
                        "1.5",
                        "2",
                        "2.5 Pro",
                        "2.5 Flash",
                        "2.5 Flash Lite",
                        "3 Pro",
                        "3 Flash",
                    ]
                },

                new()
                {
                    ProviderName = "Anthropic",
                    SeriesName = "Claude",
                    ModelNames =
                    [
                        "2",
                        "Opus 3",
                        "Sonnet 3",
                        "Haiku 3",
                        "Sonnet 3.5",
                        "Sonnet 3.7",
                        "Opus 4",
                        "Sonnet 4",
                        "Opus 4.1",
                        "Opus 4.5",
                        "Sonnet 4.5",
                    ]
                },

                new()
                {
                    ProviderName = "Qwen",
                    SeriesName = "Qwen",
                    ModelNames =
                    [
                        "2.5 Coder 32B Instruct",
                        "3 30B A3B",
                        "3 235B A22B",
                        "3 Next 80B A3B",
                        "3 Coder 480B A35B",
                        "3 Coder Plus",
                        "3 Max",
                    ]
                },

                new()
                {
                    ProviderName = "Deepseek",
                    SeriesName = "Deepseek",
                    ModelNames =
                    [
                        "V3",
                        "R1",
                        "V3.1",
                        "V3.2",
                    ]
                },

                new()
                {
                    ProviderName = "Zhipu",
                    SeriesName = "GLM",
                    ModelNames =
                    [
                        "4.5",
                        "4.5 Air",
                        "4.6",
                        "4.7"
                    ]
                },

                new()
                {
                    ProviderName = "Moonshot",
                    SeriesName = "Kimi",
                    ModelNames =
                    [
                        "K1",
                        "K2"
                    ]
                },

                new()
                {
                    ProviderName = "MiniMax",
                    SeriesName = "MiniMax",
                    ModelNames =
                    [
                        "M1",
                        "M2",
                        "M2.1"
                    ]
                }
            ];
            return _officialModels;
        }
    }

    private static string[]? _officialModelNames;

    public static string[] GetOfficialModelNames
    {
        get
        {
            _officialModelNames ??= OfficialModels.SelectMany(descriptor =>
            {
                var descriptorSeriesName = descriptor.SeriesName;
                return descriptor.ModelNames.Select((s => descriptorSeriesName + "-" + s));
            }).ToArray();
            return _officialModelNames;
        }
    }
}