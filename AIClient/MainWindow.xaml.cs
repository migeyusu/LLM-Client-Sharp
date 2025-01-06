using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using Azure;
using Azure.AI.Inference;
using Markdig;
using Markdig.Renderers;
using Markdig.Wpf;

namespace LLMClient;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {
        _defaultPipeline.Setup(_renderer);
        _cachedPreResponse.Append("正在生成文档。。。。。");
        this.DataContext = this;
        InitializeComponent();
    }

    public string PromptString
    {
        get => _promptString;
        set
        {
            if (value == _promptString) return;
            _promptString = value;
            OnPropertyChanged();
        }
    }

    public string PreResponse
    {
        get => _preResponse;
        set
        {
            if (value == _preResponse) return;
            _preResponse = value;
            OnPropertyChangedAsync();
        }
    }

    public bool IsResponsing
    {
        get => _isResponsing;
        set
        {
            if (value == _isResponsing) return;
            _isResponsing = value;
            OnPropertyChangedAsync();
        }
    }

    private const string ApiToken = "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";

    private readonly MarkdownPipeline _defaultPipeline =
        new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

    public ObservableCollection<DialogItem> Dialog { get; set; } = new ObservableCollection<DialogItem>();

    private readonly CustomRenderer _renderer = new CustomRenderer();

    private readonly StringBuilder _cachedPreResponse = new StringBuilder();

    private string _promptString;
    private bool _isResponsing = false;
    private string _preResponse;

    private const string testmd =
        "在 Qt 应用中，如果你希望 C++ 的更改能够在 QML 上实时响应，可以通过以下方式实现：\n\n---\n\n### 1. 使用 **`Q_PROPERTY`** 和信号 (`signal`) 机制\n\n在 Qt 中，`Q_PROPERTY` 提供了一种基于属性的绑定机制，可以让 QML 监听 C++ 后端的属性变化。当属性发生更改时，C++ 触发一个通知信号，QML 就能实时响应。\n\n#### 示例代码：\n\n##### **MyObject.h**\n```cpp\n#include <QObject>\n\nclass MyObject : public QObject {\n    Q_OBJECT\n    Q_PROPERTY(int counter READ counter WRITE setCounter NOTIFY counterChanged)\n\npublic:\n    explicit MyObject(QObject *parent = nullptr) : QObject(parent), m_counter(0) {}\n\n    // Getter\n    int counter() const {\n        return m_counter;\n    }\n\n    // Setter\n    void setCounter(int counter) {\n        if (m_counter != counter) { // 确保值真正改变\n            m_counter = counter;\n            emit counterChanged(); // 发出信号，通知 QML 属性已更新\n        }\n    }\n\nsignals:\n    void counterChanged(); // 定义信号：属性变化时会触发\n\nprivate:\n    int m_counter; // 私有属性\n};\n```\n\n##### **main.cpp**\n```cpp\n#include <QGuiApplication>\n#include <QQmlApplicationEngine>\n#include <QQmlContext>\n#include \"MyObject.h\"\n\nint main(int argc, char *argv[]) {\n    QGuiApplication app(argc, argv);\n\n    QQmlApplicationEngine engine;\n\n    // 创建一个 MyObject 实例\n    MyObject myObject;\n\n    // 将 `myObject` 暴露到 QML\n    engine.rootContext()->setContextProperty(\"myObject\", &myObject);\n\n    engine.load(QUrl(QStringLiteral(\"qrc:/main.qml\")));\n\n    // 检查 QML 是否成功加载\n    if (engine.rootObjects().isEmpty())\n        return -1;\n\n    // 修改 Counter 值，例如在 C++ 中动态更新值\n    myObject.setCounter(42); // QML 将会实时刷新 Counter 的值\n\n    return app.exec();\n}\n```\n\n##### **main.qml**\n```qml\nimport QtQuick 2.15\nimport QtQuick.Controls 2.15\n\nApplicationWindow {\n    visible: true\n    width: 640\n    height: 480\n    title: qsTr(\"Qt C++ to QML Example\")\n\n    Column {\n        anchors.centerIn: parent\n        spacing: 20\n\n        // 显示从 C++ 传来的 counter 值\n        Text {\n            text: \"Counter Value: \" + myObject.counter\n            font.pixelSize: 24\n        }\n\n        // 按钮：点击时调用 C++ 更新 Counter\n        Button {\n            text: \"Increment Counter\"\n            onClicked: myObject.counter += 1; // 修改 counter 时会触发信号并更新 UI\n        }\n    }\n}\n```\n\n在这个示例中：\n1. `MyObject` 定义了一个 `counter` 属性，它通过 `Q_PROPERTY` 注册。\n2. 当 `setCounter` 在 C++ 中被调用时，如果值发生变化，会发出 `counterChanged` 信号。\n3. QML 中通过绑定 `myObject.counter`，监听了 `counter` 属性的变化，UI 会自动更新。\n\n---\n\n### 2. 使用 **`QQmlPropertyMap`** 实现动态数据绑定 (简化模型)\n\n如果你希望快速实现一些简单属性的动态绑定，可以使用 `QQmlPropertyMap`。\n\n#### 示例代码：\n\n##### **main.cpp**\n```cpp\n#include <QGuiApplication>\n#include <QQmlApplicationEngine>\n#include <QQmlContext>\n#include <QQmlPropertyMap>\n\nint main(int argc, char *argv[]) {\n    QGuiApplication app(argc, argv);\n\n    QQmlApplicationEngine engine;\n\n    // 创建 QQmlPropertyMap\n    QQmlPropertyMap data;\n    data.insert(\"name\", QVariant(\"Hello World\"));\n    data.insert(\"counter\", QVariant(42));\n\n    // 将 QQmlPropertyMap 暴露到 QML\n    engine.rootContext()->setContextProperty(\"dataModel\", &data);\n\n    engine.load(QUrl(QStringLiteral(\"qrc:/main.qml\")));\n\n    // 在 C++ 中更新属性，也会实时更新 QML\n    data[\"counter\"] = 100;\n\n    if (engine.rootObjects().isEmpty())\n        return -1;\n\n    return app.exec();\n}\n```\n\n##### **main.qml**\n```qml\nimport QtQuick 2.15\nimport QtQuick.Controls 2.15\n\nApplicationWindow {\n    visible: true\n    width: 640\n    height: 480\n    title: qsTr(\"QQmlPropertyMap Example\")\n\n    Column {\n        anchors.centerIn: parent\n        spacing: 20\n\n        Text {\n            text: \"Name: \" + dataModel.name\n            font.pixelSize: 24\n        }\n\n        Text {\n            text: \"Counter: \" + dataModel.counter\n            font.pixelSize: 24\n        }\n\n        Button {\n            text: \"Update Counter\"\n            onClicked: dataModel.counter += 1; // 更新 Counter\n        }\n    }\n}\n```\n\n在这个示例中：\n- 使用了 `QQmlPropertyMap`，你可以动态管理键值对。\n- C++ 更新 `data[\"counter\"]` 时，QML 会自动更新 UI。\n\n---\n\n### 3. 使用 **`QAbstractListModel`**（更适合数据集合）\n\n如果你需要处理表格或列表等复杂数据，推荐使用 `QAbstractListModel`，并暴露给 QML 使用。\n\n#### 简单列表模型示例：\n\n##### **MyListModel.h**\n```cpp\n#include <QAbstractListModel>\n#include <QStringList>\n\nclass MyListModel : public QAbstractListModel {\n    Q_OBJECT\npublic:\n    explicit MyListModel(QObject *parent = nullptr) : QAbstractListModel(parent) {\n        m_data = {\"Item 1\", \"Item 2\", \"Item 3\"};\n    }\n\n    // 实现行计数\n    int rowCount(const QModelIndex &parent = QModelIndex()) const override {\n        return m_data.size();\n    }\n\n    // 实现数据访问\n    QVariant data(const QModelIndex &index, int role = Qt::DisplayRole) const override {\n        if (!index.isValid() || index.row() >= m_data.size())\n            return QVariant();\n        if (role == Qt::DisplayRole)\n            return m_data[index.row()];\n        return QVariant();\n    }\n\n    // 添加数据\n    void addItem(const QString &item) {\n        beginInsertRows(QModelIndex(), m_data.size(), m_data.size());\n        m_data.append(item);\n        endInsertRows();\n    }\n\nprivate:\n    QStringList m_data; // 模型数据\n};\n```\n\n##### **main.cpp**\n```cpp\n#include <QGuiApplication>\n#include <QQmlApplicationEngine>\n#include \"MyListModel.h\"\n\nint main(int argc, char *argv[]) {\n    QGuiApplication app(argc, argv);\n\n    QQmlApplicationEngine engine;\n\n    // 创建模型实例\n    MyListModel myModel;\n\n    // 暴露给 QML\n    engine.rootContext()->setContextProperty(\"myModel\", &myModel);\n\n    engine.load(QUrl(QStringLiteral(\"qrc:/main.qml\")));\n\n    // 在 C++ 中动态添加新数据\n    myModel.addItem(\"New Item From C++\");\n\n    if (engine.rootObjects().isEmpty())\n        return -1;\n\n    return app.exec();\n}\n```\n\n##### **main.qml**\n```qml\nimport QtQuick 2.15\nimport QtQuick.Controls 2.15\n\nApplicationWindow {\n    visible: true\n    width: 640\n    height: 480\n    title: qsTr(\"List Model Example\")\n\n    ListView {\n        anchors.fill: parent\n        model: myModel // 使用 C++ 暴露的模型\n\n        delegate: Text {\n            text: modelData // 渲染模型数据\n            font.pixelSize: 20\n        }\n    }\n}\n```\n\n在这个例子中：\n- 使用了 `QAbstractListModel`，QML 会监听模型变更，并在 UI 上动态刷新内容。\n\n---\n\n### 总结\n\n1. **简单属性绑定**：推荐使用 `Q_PROPERTY` 和信号机制。\n   - 优势：简单高效，代码清晰。\n   - 示例：方法 1。\n\n2. **动态多属性绑定**：使用 `QQmlPropertyMap`。\n   - 优势：简化了管理多个属性的代码，适合简单场景。\n   - 示例：方法 2。\n\n3. **复杂数据（列表、表格等）**：使用 `QAbstractListModel`。\n   - 优势：适合动态和复杂的数据集合，官方推荐。\n   - 示例：方法 3。\n\n根据你的实际需求选择合适的方式！";

    private async void SendPrompt_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PromptString))
        {
            return;
        }

        Dialog.Add(new RequestItem(PromptString));
        var document = Markdig.Wpf.Markdown.ToFlowDocument(testmd, _defaultPipeline, _renderer);
        document.IsEnabled = true;
        Dialog.Add(new ResponseItem(testmd, document));
        return;
        _cachedPreResponse.Clear();
        OnPropertyChangedAsync(PreResponse);
        IsResponsing = true;
        try
        {
            var credential = new AzureKeyCredential(ApiToken);
            var client = new ChatCompletionsClient(
                new Uri("https://models.inference.ai.azure.com"),
                credential, new AzureAIInferenceClientOptions());

            var requestOptions = new ChatCompletionsOptions()
            {
                Messages =
                {
                    new ChatRequestSystemMessage(""),
                    new ChatRequestUserMessage(PromptString),
                },
                Model = "gpt-4o",
                Temperature = 1,
                MaxTokens = 4096,
            };

            using (var streamingResponse = await client.CompleteStreamingAsync(requestOptions))
            {
                await foreach (var update in streamingResponse.EnumerateValues())
                {
                    _cachedPreResponse.Append(update.ContentUpdate);
                    PreResponse = _cachedPreResponse.ToString();
                }
            }

            var response = _cachedPreResponse.ToString();
            var flowDocument = Markdig.Wpf.Markdown.ToFlowDocument(response, _defaultPipeline, _renderer);
            ;
            Dialog.Add(new ResponseItem(response, flowDocument));
        }
        catch (Exception exception)
        {
            var message = exception.Message;
            if (_cachedPreResponse.Length > 0)
            {
                var response = _cachedPreResponse.ToString();
                var flowDocument = Markdig.Wpf.Markdown.ToFlowDocument(response, _defaultPipeline);
                Dialog.Add(new ResponseItem(response, flowDocument, message) { IsInterrupt = true });
            }
            else
            {
                Dialog.Add(new ResponseItem(_cachedPreResponse.ToString(), null, message) { IsInterrupt = true });
            }
        }
        finally
        {
            IsResponsing = false;
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual async void OnPropertyChangedAsync([CallerMemberName] string? propertyName = null)
    {
        if (Dispatcher.CheckAccess())
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            await Dispatcher.InvokeAsync(
                () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }
}

public abstract class DialogItem
{
    public DialogItem()
    {
    }

    public abstract ChatRequestMessage? Message { get; }
}

public class RequestItem : DialogItem
{
    public RequestItem(string messageContent) : base()
    {
        MessageContent = messageContent;
        UserMessage = new ChatRequestUserMessage(messageContent);
    }

    public ChatRequestUserMessage? UserMessage { get; set; }

    public string MessageContent { get; }
    public override ChatRequestMessage? Message => UserMessage;
}

public class ResponseItem : DialogItem
{
    /// <summary>
    /// 是否中断
    /// </summary>
    public bool IsInterrupt { get; set; } = false;

    public string? ErrorMessage { get; set; }

    public FlowDocument? Document { get; set; }

    public ResponseItem(string message, FlowDocument? document, string? errorMessage = null)
    {
        AssistantMessage = new ChatRequestAssistantMessage(message);
        Document = document;
        ErrorMessage = errorMessage;
    }

    public ChatRequestAssistantMessage? AssistantMessage { get; set; }

    public override ChatRequestMessage? Message => AssistantMessage;
}