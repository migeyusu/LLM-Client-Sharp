using System.Collections.ObjectModel;
using System.Windows.Data;
using LambdaConverters;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Data;

namespace LLMClient.Endpoints;

/// <summary>
/// used for defining the provider and model information
/// </summary>
public class ProviderDescriptor
{
    public required string ProviderName { get; set; }
    public required string[] ModelNames { get; set; }
}

public class ProviderEntry
{
    public required string ProviderName { get; set; }

    public ThemedIcon? Icon { get; set; }
    public ObservableCollection<ModelEntry> ModelEntries { get; set; } = [];
}

public class ModelEntry
{
    public required string ModelName { get; set; }

    public ThemedIcon? Icon { get; set; }

    /// <summary>
    /// associated models in endpoints.
    /// </summary>
    public ObservableCollection<IEndpointModel> InstanceList { get; set; } = [];
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

            var descriptor = ModelsDefinitions.FirstOrDefault(d => d.ProviderName == value);
            return descriptor?.ModelNames;
        });

    public static SuspendableObservableCollection<ProviderEntry> ProviderEntries { get; } = [];

    private static void DeAssociateModelInstance(string oriProviderName, string oriModelName, IEndpointModel model)
    {
        if (string.IsNullOrEmpty(oriProviderName) || string.IsNullOrEmpty(oriModelName))
        {
            //触发完全遍历
            foreach (var provider in ProviderEntries)
            {
                foreach (var modelEntry in provider.ModelEntries)
                {
                    modelEntry.InstanceList.Remove(model);
                }
            }
        }
        else
        {
            //找到并移除
            var provider = ProviderEntries.FirstOrDefault(p =>
                p.ProviderName.Equals(oriProviderName, StringComparison.OrdinalIgnoreCase));
            var modelEntry = provider?.ModelEntries.FirstOrDefault(me =>
                me.ModelName.Equals(oriModelName, StringComparison.OrdinalIgnoreCase));
            modelEntry?.InstanceList.Remove(model);
        }
    }

    /// <summary>
    /// associate model instance for a provider and model name. This is used to associate the model instance with the provider and model name, so that it can be retrieved later when needed. If the provider or model name does not exist, it will be created. If the model instance already exists for the provider and model name, it will not be added again.
    /// </summary>
    /// <param name="modelInstance"></param>
    private static void AssociateModelInstance(IEndpointModel modelInstance)
    {
        var providerName = modelInstance.Provider;
        var modelName = modelInstance.SeriesName;
        if (string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(modelName))
        {
            return;
        }

        var provider = ProviderEntries.FirstOrDefault(p =>
            p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            provider = new ProviderEntry { ProviderName = providerName, ModelEntries = [] };
            var @default = Enum.GetNames<ModelIconType>()
                .FirstOrDefault(s => s.Equals(providerName, StringComparison.OrdinalIgnoreCase));
            if (@default != null)
            {
                provider.Icon = ((ModelIconType)Enum.Parse(typeof(ModelIconType), @default)).GetIcon();
            }
            else
            {
                provider.Icon = modelInstance.Icon;
            }

            ProviderEntries.Add(provider);
        }

        var modelEntry = provider.ModelEntries.FirstOrDefault(me =>
            me.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase));
        if (modelEntry == null)
        {
            modelEntry = new ModelEntry
            {
                ModelName = modelName, InstanceList = [],
                Icon = modelInstance.Icon
            };
            provider.ModelEntries.Add(modelEntry);
        }

        if (!modelEntry.InstanceList.Contains(modelInstance))
        {
            modelEntry.InstanceList.Add(modelInstance);
        }
    }

    public static async Task Initialize(IEnumerable<IEndpointModel>? endpointModels)
    {
        await LoadModelDefinitionsFromFile(ModelsDefinitions);
        if (endpointModels != null)
            foreach (var model in endpointModels)
            {
                AssociateModelInstance(model);
            }
    }

    public const string ModelsDefinitionFileName = "models_definition.json";

    private static async Task LoadModelDefinitionsFromFile(List<ProviderDescriptor> models)
    {
        var fullPath = Path.GetFullPath(ModelsDefinitionFileName);
        if (File.Exists(fullPath))
        {
            try
            {
                await using var stream = File.OpenRead(fullPath);
                var loadedModels = await JsonSerializer.DeserializeAsync<List<ProviderDescriptor>>(stream,
                    options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

    private static List<ProviderDescriptor>? _modelsDefinitions;

    public static List<ProviderDescriptor> ModelsDefinitions
    {
        get
        {
            if (_modelsDefinitions != null) return _modelsDefinitions;

            _modelsDefinitions =
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
            return _modelsDefinitions;
        }
    }
}