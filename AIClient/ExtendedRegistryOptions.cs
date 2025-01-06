using System.IO;
using System.Text.Json;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace LLMClient
{
    public class ExtendedRegistryOptions : IRegistryOptions
    {
        private readonly ThemeName _defaultTheme;
        private readonly RegistryOptions _registryOptions;

        private static readonly GrammarSerializationContext jsonContext = new GrammarSerializationContext(
            new JsonSerializerOptions()
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

        private readonly Dictionary<string, GrammarDefinition> _availableGrammars =
            new Dictionary<string, GrammarDefinition>();

        public ExtendedRegistryOptions(ThemeName defaultTheme)
        {
            _registryOptions = new RegistryOptions(defaultTheme);
            _defaultTheme = defaultTheme;
            InitializeAvailableGrammars();
        }

        public ICollection<string> GetInjections(string scopeName)
        {
            return null;
        }

        public IRawTheme GetTheme(string scopeName)
        {
            IRawTheme? theme = null;
            if ((theme = _registryOptions.GetTheme(scopeName)) != null)
            {
                return theme;
            }

            return null;
            /*Stream themeStream = ResourceLoader.TryOpenThemeStream(scopeName.Replace("./", string.Empty));

            if (themeStream == null)
                return null;

            using (themeStream)
            using (StreamReader reader = new StreamReader(themeStream))
            {
                return ThemeReader.ReadThemeSync(reader);
            }*/
        }

        public IRawGrammar GetGrammar(string scopeName)
        {
            IRawGrammar? grammar = null;
            if ((grammar = _registryOptions.GetGrammar(scopeName)) != null)
            {
                return grammar;
            }

            var grammarFilePath = GetGrammarFile(scopeName);
            if (grammarFilePath == null)
            {
                return grammar;
            }

            var path = Path.GetFullPath(Path.Combine("Grammars", grammarFilePath));
            if (!File.Exists(path))
            {
                return grammar;
            }

            using (var fileStream = File.OpenRead(path))
            {
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    return GrammarReader.ReadGrammarSync(reader);
                }
            }
        }


        public IRawTheme GetDefaultTheme()
        {
            return _registryOptions.GetDefaultTheme();
        }

        public string GetScopeByExtension(string extension)
        {
            var scopeByExtension = _registryOptions.GetScopeByExtension(extension);
            if (scopeByExtension != null)
            {
                return scopeByExtension;
            }

            foreach (GrammarDefinition definition in _availableGrammars.Values)
            {
                foreach (var language in definition.Contributes.Languages)
                {
                    if (language.Extensions == null)
                        continue;

                    foreach (var languageExtension in language.Extensions)
                    {
                        if (extension.Equals(languageExtension,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var grammar in definition.Contributes.Grammars)
                            {
                                return grammar.ScopeName;
                            }
                        }
                    }
                }
            }

            return null;
        }

        void InitializeAvailableGrammars()
        {
            var directories = new DirectoryInfo("Grammars").GetDirectories();
            foreach (var directory in directories)
            {
                var grammar = directory.Name.ToUpper();
                var baseDir = directory.FullName;
                var packageFileInfo = directory.GetFiles("package.json").FirstOrDefault();
                if (packageFileInfo == null)
                    continue;

                using (Stream stream = packageFileInfo.OpenRead())
                {
                    GrammarDefinition? definition = JsonSerializer.Deserialize(
                        stream, GrammarSerializationContext.Default.GrammarDefinition);
                    if (definition == null)
                    {
                        continue;
                    }

                    foreach (var language in definition.Contributes.Languages)
                    {
                        if (language.ConfigurationFile == null)
                        {
                            language.Configuration = null;
                            continue;
                        }

                        var path = Path.GetFullPath(language.ConfigurationFile, baseDir);
                        using (var fileStream = File.OpenRead(path))
                        {
                            language.Configuration =
                                JsonSerializer.Deserialize(fileStream, jsonContext.LanguageConfiguration);
                        }
                    }

                    if (definition.Contributes?.Snippets != null)
                    {
                        definition.LanguageSnippets = new LanguageSnippets();
                        foreach (var snippet in definition.Contributes.Snippets)
                        {
                            var path = Path.GetFullPath(snippet.Path, baseDir);
                            using (var fileStream = File.OpenRead(path))
                            {
                                definition.LanguageSnippets =
                                    JsonSerializer.Deserialize(fileStream, jsonContext.LanguageSnippets);
                                break;
                            }
                        }
                    }

                    _availableGrammars.Add(grammar, definition);
                }
            }
        }

        string? GetGrammarFile(string scopeName)
        {
            foreach (string grammarName in _availableGrammars.Keys)
            {
                GrammarDefinition definition = _availableGrammars[grammarName];

                foreach (Grammar grammar in definition.Contributes.Grammars)
                {
                    if (scopeName.Equals(grammar.ScopeName))
                    {
                        string grammarPath = grammar.Path;

                        /*if (grammarPath.StartsWith("./"))
                            grammarPath = grammarPath.Substring(2);*/
                        return grammarName.ToLower() + "/" + grammarPath;
                    }
                }
            }

            return null;
        }
    }
}