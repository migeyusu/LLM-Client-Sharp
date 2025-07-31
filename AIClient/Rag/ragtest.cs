namespace LLMClient.Rag;

    /*public async Task AddFileToContext(FileInfo fileInfo)
    {
        var build = Kernel.CreateBuilder().Build();
        DocumentPlugin documentPlugin = new(new WordDocumentConnector(), new LocalFileSystemConnector());
        var doc = await documentPlugin.ReadTextAsync(fileInfo.FullName);
        // 2. 简单按 500 tokens 左右切块（示例用行数）
        var memory = build.GetRequiredService<ISemanticTextMemory>();
        var chunks = SplitByLength(doc, maxChars: 1500);

        IEnumerable<string> SplitByLength(string text, int maxChars)
        {
            var span = text.AsMemory();
            int offset = 0;
            while (offset < span.Length)
            {
                int length = Math.Min(maxChars, span.Length - offset);
                var chunk = span.Slice(offset, length).ToString();
                offset += length;
                yield return chunk;
            }
        }

        Guid docId = Guid.NewGuid();
        // 3. 存向量
        int i = 0;
        foreach (var chunk in chunks)
        {
            await memory.SaveInformationAsync(
                collection: "docs", // 向量库里的“表”或“namespace”
                text: chunk,
                id: $"{docId}_{i++}", // 唯一键
                description: $"doc:{docId}" // 可做元数据
            );
        }

        // var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        // var userQuestion = await embeddingGenerator.GenerateAsync("DDC的初始化有几个步骤？");
        var searchResults = memory.SearchAsync(
            collection: "docs",
            query: "DDC的初始化有几个步骤？",
            limit: 6, // 取前 6 段
            minRelevanceScore: 0.7 // 可调
        );
        Debugger.Break();
    }*/

    /*public ICommand TestCommand => new ActionCommand((async o =>
    {
        if (this.Client == null)
        {
            return;
        }

        var dialogViewItem = this.DialogItems.Last(item => item is MultiResponseViewItem && item.IsAvailableInContext);
        var multiResponseViewItem = dialogViewItem as MultiResponseViewItem;
        var endpoint = EndpointService.AvailableEndpoints[0];
        var first = endpoint.AvailableModelNames.First();
        var llmModelClient = new ModelSelectionViewModel(this.EndpointService)
        {
            SelectedModelName = first,
            SelectedEndpoint = endpoint
        }.GetClient();
        if (llmModelClient == null)
        {
            return;
        }

        if (multiResponseViewItem != null)
        {
            await AppendResponseOn(multiResponseViewItem, llmModelClient);
        }
    }));*/