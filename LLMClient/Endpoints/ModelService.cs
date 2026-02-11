using System.Windows.Data;
using LambdaConverters;
using System.Diagnostics;
using System.Text.Json;

namespace LLMClient.Endpoints;

public class ModelDescriptor
{
    public required string ProviderName { get; set; }
    public required string[] ModelNames { get; set; }
}

public static class ModelRegister
{
    public static readonly IValueConverter ProviderToModelNamesConverter =
        ValueConverter.Create<string?, ICollection<string>?>(args =>
        {
            var value = args.Value;
            if (value == null || string.IsNullOrEmpty(value))
            {
                return null;
            }

            var descriptor = OfficialModels.FirstOrDefault(d => d.ProviderName == value);
            return descriptor?.ModelNames;
        });

    private static List<ModelDescriptor>? _officialModels;

    public const string ModelsDefinitionFileName = "models_definition.json";

    private static void LoadModelsFromFile(List<ModelDescriptor> models)
    {
        var fullPath = Path.GetFullPath(ModelsDefinitionFileName);
        if (File.Exists(fullPath))
        {
            try
            {
                using var stream = File.OpenRead(fullPath);
                var loadedModels = JsonSerializer.Deserialize<List<ModelDescriptor>>(stream, options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loadedModels != null)
                {
                    foreach (var loadedModel in loadedModels)
                    {
                        var existing = models.FirstOrDefault(m =>
                            m.ProviderName.Equals(loadedModel.ProviderName, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            var newModels = loadedModel.ModelNames
                                .Except(existing.ModelNames, StringComparer.OrdinalIgnoreCase).ToArray();
                            if (newModels.Length > 0)
                            {
                                existing.ModelNames = existing.ModelNames.Concat(newModels).ToArray();
                            }
                        }
                        else
                        {
                            models.Add(loadedModel);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError($"Failed to load models from {ModelsDefinitionFileName}: {e.Message}");
            }
        }
    }

    public static void RegisterModel(string provider, string modelName)
    {
        var modelDescriptors = OfficialModels;
        var providerDescriptor = modelDescriptors.FirstOrDefault(p => p.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));
        if (providerDescriptor == null)
        {
            providerDescriptor = new ModelDescriptor { ProviderName = provider, ModelNames = [] };
            modelDescriptors.Add(providerDescriptor);
        }

        if (!providerDescriptor.ModelNames.Contains(modelName))
        {
            var newList = providerDescriptor.ModelNames.ToList();
            newList.Add(modelName);
            providerDescriptor.ModelNames = newList.ToArray();
        }
    }

    public static List<ModelDescriptor> OfficialModels
    {
        get
        {
            if (_officialModels != null) return _officialModels;
            
            _officialModels =
            [
                new()
                {
                    ProviderName = "OpenAI",
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
                        "GPT-5-nano",
                        "GPT-5.1",
                        "GPT-5.1 Codex Max",
                        "GPT-5.2",
                        "GPT-5.2-Chat",
                        "GPT-5.2 Pro",
                        "GPT-5.3-Codex"
                    ]
                },

                new()
                {
                    ProviderName = "Google",
                    ModelNames =
                    [
                        "Gemini 1.5",
                        "Gemini 2",
                        "Gemini 2.5 Pro",
                        "Gemini 2.5 Flash",
                        "Gemini 2.5 Flash Lite",
                        "Gemini 3 Pro",
                        "Gemini 3 Flash",
                    ]
                },
                new()
                {
                    ProviderName = "StepFun",
                    ModelNames =
                    [
                        "Step 3.5",
                    ]
                },
                new()
                {
                    ProviderName = "xAI",
                    ModelNames =
                    [
                        "Grok 3",
                        "Grok 4",
                        "Grok 4.1",
                        "Grok 4.1 Fast",
                        "Grok Code Fast 1"
                    ]
                },


                new()
                {
                    ProviderName = "Anthropic",
                    ModelNames =
                    [
                        "Claude 2",
                        "Claude 3 Opus",
                        "Claude 3 Sonnet",
                        "Claude 3 Haiku",
                        "Claude 3.5 Sonnet",
                        "Claude 3.7 Sonnet",
                        "Claude Opus 4",
                        "Claude Sonnet 4",
                        "Claude Opus 4.1",
                        "Claude Opus 4.5",
                        "Claude Sonnet 4.5",
                        "Claude Opus 4.6",
                    ]
                },

                new()
                {
                    ProviderName = "Alibaba",
                    ModelNames =
                    [
                        "Qwen2.5-Coder-32B-Instruct",
                        "Qwen3-30B-A3B",
                        "Qwen3-235B-A22B",
                        "Qwen3-Next-80B-A3B",
                        "Qwen3-Coder-480B-A35B",
                        "Qwen3-Coder-Plus",
                        "Qwen3-Coder-Next",
                        "Qwen3-Max",
                    ]
                },

                new()
                {
                    ProviderName = "Deepseek",
                    ModelNames =
                    [
                        "Deepseek-V3",
                        "Deepseek-R1",
                        "Deepseek-V3.1",
                        "Deepseek-V3.2",
                    ]
                },

                new()
                {
                    ProviderName = "Zhipu",
                    ModelNames =
                    [
                        "GLM-4.5",
                        "GLM-4.5 Air",
                        "GLM-4.6",
                        "GLM-4.7",
                        "GLM 4.7 Flash"
                    ]
                },

                new()
                {
                    ProviderName = "Moonshot",
                    ModelNames =
                    [
                        "Kimi K1",
                        "Kimi K2",
                        "Kimi K2.5"
                    ]
                },

                new()
                {
                    ProviderName = "MiniMax",
                    ModelNames =
                    [
                        "MiniMax M1",
                        "MiniMax M2",
                        "MiniMax M2.1"
                    ]
                },
                new()
                {
                    ProviderName = "NVIDIA",
                    ModelNames =
                    [
                        "Nemotron 3 Nano 30B A3B"
                    ]
                }
            ];
            
            LoadModelsFromFile(_officialModels);
            
            return _officialModels;
        }
    }
}