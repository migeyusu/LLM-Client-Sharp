// #define TESTMODE

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class DialogViewModel : BaseViewModel
{
    public DateTime EditTime
    {
        get => _editTime;
        set
        {
            if (value.Equals(_editTime)) return;
            _editTime = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 用于跟踪对话对象
    /// </summary>
    public string FileName { get; set; } = String.Empty;

    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = false;

    public string Topic
    {
        get => _topic;
        set
        {
            if (value == _topic) return;
            _topic = value;
            OnPropertyChanged();
        }
    }

    public string? PromptString
    {
        get => _promptString;
        set
        {
            if (value == _promptString) return;
            _promptString = value;
            OnPropertyChanged();
            // PromptTokensCount = value?.Length ?? 0;
        }
    }

    public IDialogViewItem? ScrollViewItem
    {
        get => _scrollViewItem;
        set
        {
            if (Equals(value, _scrollViewItem)) return;
            _scrollViewItem = value;
            OnPropertyChanged();
        }
    }

    public long TokensConsumption
    {
        get => _tokensConsumption;
        set
        {
            if (value == _tokensConsumption) return;
            _tokensConsumption = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<IDialogViewItem> DialogItems { get; }

    private ILLMModelClient? _model;

    public ILLMModelClient? Model
    {
        get => _model;
        set
        {
            if (Equals(value, _model)) return;
            if (_model is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= NotifyPropertyChangedOnPropertyChanged;
            }

            if (_model?.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged += NotifyPropertyChangedOnPropertyChanged;
            }

            _model = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SendRequestCommand));
            if (value is INotifyPropertyChanged newValue)
            {
                newValue.PropertyChanged += NotifyPropertyChangedOnPropertyChanged;
            }

            if (_model?.Parameters is INotifyPropertyChanged newParameters)
            {
                newParameters.PropertyChanged += NotifyPropertyChangedOnPropertyChanged;
            }
        }
    }

    public string? Shortcut
    {
        get { return DialogItems.FirstOrDefault(item => { return item is ResponseViewItem; })?.Message?.Text; }
    }

    public ICommand ClearContextCommand => new ActionCommand((o =>
    {
        var item = new EraseViewItem();
        DialogItems.Add(item);
        ScrollViewItem = item;
    }));

    public ICommand ClearDialogCommand => new ActionCommand(async o =>
    {
        if ((await DialogHost.Show(new ConfirmView() { Header = "清空会话？" })) is true)
        {
            DialogItems.Clear();
        }
    });

    public ICommand SendRequestCommand => new RelayCommand((() =>
    {
        if (string.IsNullOrEmpty(PromptString?.Trim()))
        {
            return;
        }

        SendRequest(PromptString);
    }), () => { return Model != null; });

    public ICommand CancelCommand => new ActionCommand((o => { _requestTokenSource?.Cancel(); }));

    public ICommand DeleteCommand => new ActionCommand((o => { this.DeleteItem((o as ResponseViewItem)!); }));

    public ICommand ScrollToNextCommand => new ActionCommand(o =>
    {
        if (DialogItems.Count == 0)
        {
            return;
        }

        var scrollViewItem = this.ScrollViewItem;
        if (scrollViewItem != null)
        {
            var indexOf = this.DialogItems.IndexOf(scrollViewItem);
            if (indexOf != -1 && indexOf < this.DialogItems.Count - 1)
            {
                this.ScrollViewItem = this.DialogItems[indexOf + 1];
            }
        }
        else
        {
            this.ScrollViewItem = this.DialogItems.FirstOrDefault();
        }
    });

    public ICommand ScrollToEndCommand => new ActionCommand((o =>
    {
        if (!DialogItems.Any())
        {
            return;
        }

        this.ScrollViewItem = DialogItems.Last();
    }));

    private string? _promptString;
    private string _topic;
    private IDialogViewItem? _scrollViewItem;
    private CancellationTokenSource? _requestTokenSource;
    private DateTime _editTime = DateTime.Now;
    private long _tokensConsumption;

    private const string Testmd =
        "在 Qt 应用中，如果你希望 C++ 的更改能够在 QML 上实时响应，可以通过以下方式实现：\n\n---\n\n### 1. 使用 **`Q_PROPERTY`** 和信号 (`signal`) 机制\n\n在 Qt 中，`Q_PROPERTY` 提供了一种基于属性的绑定机制，可以让 QML 监听 C++ 后端的属性变化。当属性发生更改时，C++ 触发一个通知信号，QML 就能实时响应。\n\n#### 示例代码：\n\n##### **MyObject.h**\n```cpp\n#include <QObject>\n\nclass MyObject : public QObject {\n    Q_OBJECT\n    Q_PROPERTY(int counter READ counter WRITE setCounter NOTIFY counterChanged)\n\npublic:\n    explicit MyObject(QObject *parent = nullptr) : QObject(parent), m_counter(0) {}\n\n    // Getter\n    int counter() const {\n        return m_counter;\n    }\n\n    // Setter\n    void setCounter(int counter) {\n        if (m_counter != counter) { // 确保值真正改变\n            m_counter = counter;\n            emit counterChanged(); // 发出信号，通知 QML 属性已更新\n        }\n    }\n\nsignals:\n    void counterChanged(); // 定义信号：属性变化时会触发\n\nprivate:\n    int m_counter; // 私有属性\n};\n```\n\n##### **main.cpp**\n```cpp\n#include <QGuiApplication>\n#include <QQmlApplicationEngine>\n#include <QQmlContext>\n#include \"MyObject.h\"\n\nint main(int argc, char *argv[]) {\n    QGuiApplication app(argc, argv);\n\n    QQmlApplicationEngine engine;\n\n    // 创建一个 MyObject 实例\n    MyObject myObject;\n\n    // 将 `myObject` 暴露到 QML\n    engine.rootContext()->setContextProperty(\"myObject\", &myObject);\n\n    engine.load(QUrl(QStringLiteral(\"qrc:/main.qml\")));\n\n    // 检查 QML 是否成功加载\n    if (engine.rootObjects().isEmpty())\n        return -1;\n\n    // 修改 Counter 值，例如在 C++ 中动态更新值\n    myObject.setCounter(42); // QML 将会实时刷新 Counter 的值\n\n    return app.exec();\n}\n```\n\n##### **main.qml**\n```qml\nimport QtQuick 2.15\nimport QtQuick.Controls 2.15\n\nApplicationWindow {\n    visible: true\n    width: 640\n    height: 480\n    title: qsTr(\"Qt C++ to QML Example\")\n\n    Column {\n        anchors.centerIn: parent\n        spacing: 20\n\n        // 显示从 C++ 传来的 counter 值\n        Text {\n            text: \"Counter Value: \" + myObject.counter\n            font.pixelSize: 24\n        }\n\n        // 按钮：点击时调用 C++ 更新 Counter\n        Button {\n            text: \"Increment Counter\"\n            onClicked: myObject.counter += 1; // 修改 counter 时会触发信号并更新 UI\n        }\n    }\n}\n```\n\n在这个示例中：\n1. `MyObject` 定义了一个 `counter` 属性，它通过 `Q_PROPERTY` 注册。\n2. 当 `setCounter` 在 C++ 中被调用时，如果值发生变化，会发出 `counterChanged` 信号。\n3. QML 中通过绑定 `myObject.counter`，监听了 `counter` 属性的变化，UI 会自动更新。\n\n---\n\n### 2. 使用 **`QQmlPropertyMap`** 实现动态数据绑定 (简化模型)\n\n如果你希望快速实现一些简单属性的动态绑定，可以使用 `QQmlPropertyMap`。\n\n#### 示例代码：\n\n##### **main.cpp**\n```cpp\n#include <QGuiApplication>\n#include <QQmlApplicationEngine>\n#include <QQmlContext>\n#include <QQmlPropertyMap>\n\nint main(int argc, char *argv[]) {\n    QGuiApplication app(argc, argv);\n\n    QQmlApplicationEngine engine;\n\n    // 创建 QQmlPropertyMap\n    QQmlPropertyMap data;\n    data.insert(\"name\", QVariant(\"Hello World\"));\n    data.insert(\"counter\", QVariant(42));\n\n    // 将 QQmlPropertyMap 暴露到 QML\n    engine.rootContext()->setContextProperty(\"dataModel\", &data);\n\n    engine.load(QUrl(QStringLiteral(\"qrc:/main.qml\")));\n\n    // 在 C++ 中更新属性，也会实时更新 QML\n    data[\"counter\"] = 100;\n\n    if (engine.rootObjects().isEmpty())\n        return -1;\n\n    return app.exec();\n}\n```\n\n##### **main.qml**\n```qml\nimport QtQuick 2.15\nimport QtQuick.Controls 2.15\n\nApplicationWindow {\n    visible: true\n    width: 640\n    height: 480\n    title: qsTr(\"QQmlPropertyMap Example\")\n\n    Column {\n        anchors.centerIn: parent\n        spacing: 20\n\n        Text {\n            text: \"Name: \" + dataModel.name\n            font.pixelSize: 24\n        }\n\n        Text {\n            text: \"Counter: \" + dataModel.counter\n            font.pixelSize: 24\n        }\n\n        Button {\n            text: \"Update Counter\"\n            onClicked: dataModel.counter += 1; // 更新 Counter\n        }\n    }\n}\n```\n\n在这个示例中：\n- 使用了 `QQmlPropertyMap`，你可以动态管理键值对。\n- C++ 更新 `data[\"counter\"]` 时，QML 会自动更新 UI。\n\n---\n\n### 3. 使用 **`QAbstractListModel`**（更适合数据集合）\n\n如果你需要处理表格或列表等复杂数据，推荐使用 `QAbstractListModel`，并暴露给 QML 使用。\n\n#### 简单列表模型示例：\n\n##### **MyListModel.h**\n```cpp\n#include <QAbstractListModel>\n#include <QStringList>\n\nclass MyListModel : public QAbstractListModel {\n    Q_OBJECT\npublic:\n    explicit MyListModel(QObject *parent = nullptr) : QAbstractListModel(parent) {\n        m_data = {\"Item 1\", \"Item 2\", \"Item 3\"};\n    }\n\n    // 实现行计数\n    int rowCount(const QModelIndex &parent = QModelIndex()) const override {\n        return m_data.size();\n    }\n\n    // 实现数据访问\n    QVariant data(const QModelIndex &index, int role = Qt::DisplayRole) const override {\n        if (!index.isValid() || index.row() >= m_data.size())\n            return QVariant();\n        if (role == Qt::DisplayRole)\n            return m_data[index.row()];\n        return QVariant();\n    }\n\n    // 添加数据\n    void addItem(const QString &item) {\n        beginInsertRows(QModelIndex(), m_data.size(), m_data.size());\n        m_data.append(item);\n        endInsertRows();\n    }\n\nprivate:\n    QStringList m_data; // 模型数据\n};\n```\n\n##### **main.cpp**\n```cpp\n#include <QGuiApplication>\n#include <QQmlApplicationEngine>\n#include \"MyListModel.h\"\n\nint main(int argc, char *argv[]) {\n    QGuiApplication app(argc, argv);\n\n    QQmlApplicationEngine engine;\n\n    // 创建模型实例\n    MyListModel myModel;\n\n    // 暴露给 QML\n    engine.rootContext()->setContextProperty(\"myModel\", &myModel);\n\n    engine.load(QUrl(QStringLiteral(\"qrc:/main.qml\")));\n\n    // 在 C++ 中动态添加新数据\n    myModel.addItem(\"New Item From C++\");\n\n    if (engine.rootObjects().isEmpty())\n        return -1;\n\n    return app.exec();\n}\n```\n\n##### **main.qml**\n```qml\nimport QtQuick 2.15\nimport QtQuick.Controls 2.15\n\nApplicationWindow {\n    visible: true\n    width: 640\n    height: 480\n    title: qsTr(\"List Model Example\")\n\n    ListView {\n        anchors.fill: parent\n        model: myModel // 使用 C++ 暴露的模型\n\n        delegate: Text {\n            text: modelData // 渲染模型数据\n            font.pixelSize: 20\n        }\n    }\n}\n```\n\n在这个例子中：\n- 使用了 `QAbstractListModel`，QML 会监听模型变更，并在 UI 上动态刷新内容。\n\n---\n\n### 总结\n\n1. **简单属性绑定**：推荐使用 `Q_PROPERTY` 和信号机制。\n   - 优势：简单高效，代码清晰。\n   - 示例：方法 1。\n\n2. **动态多属性绑定**：使用 `QQmlPropertyMap`。\n   - 优势：简化了管理多个属性的代码，适合简单场景。\n   - 示例：方法 2。\n\n3. **复杂数据（列表、表格等）**：使用 `QAbstractListModel`。\n   - 优势：适合动态和复杂的数据集合，官方推荐。\n   - 示例：方法 3。\n\n根据你的实际需求选择合适的方式！";

    public DialogViewModel(string topic, ILLMModelClient? modelClient = null,
        IList<IDialogViewItem>? items = null)
    {
        this._topic = topic;
        this.Model = modelClient;
        this.PropertyChanged += (_, __) => IsDataChanged = true;
        DialogItems = items == null ? [] : new ObservableCollection<IDialogViewItem>(items);
        this.DialogItems.CollectionChanged += DialogOnCollectionChanged;
    }

    private void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsDataChanged = true;
        this.EditTime = DateTime.Now;
        OnPropertyChangedAsync(nameof(Shortcut));
    }

    private void NotifyPropertyChangedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }

    public void ScrollNext(object item)
    {
        if (item is IDialogViewItem viewItem)
        {
            var current = DialogItems.IndexOf(viewItem);
            if (current == DialogItems.Count - 1)
            {
                return;
            }

            this.ScrollViewItem = DialogItems[current + 1];
        }
    }

    public void DeleteItem(IDialogViewItem item)
    {
        this.DialogItems.Remove(item);
        if (ScrollViewItem == item)
        {
            ScrollViewItem = this.DialogItems.LastOrDefault();
        }
    }

    public async void SendRequest(string prompt)
    {
        var requestViewItem = new RequestViewItem() { MessageContent = prompt };
        DialogItems.Add(requestViewItem);
#if TESTMODE
        for (int i = 0; i < 1000; i++)
        {
            Dialog.Add(new ResponseViewItem() { Raw = i.ToString() });
        }

        ScrollViewItem = Dialog.Last();
        return;
#endif

        DialogItems.Add(Model);
        ScrollViewItem = DialogItems.Last();
        try
        {
            var list = GenerateHistory(DialogItems);
            var existTokens = list.Sum(item => item.Tokens);
            _requestTokenSource = new CancellationTokenSource();
            var response = await Model.SendRequest(list, _requestTokenSource.Token);
            //如果响应出现错误，认为请求无效，不计入上下文
            requestViewItem.IsAvailableInContext = !response.IsInterrupt;
            this.TokensConsumption += response.Usage.TotalTokenCount ?? 0;
            DialogItems.Add(new ResponseViewItem()
            {
                Raw = response.Response,
                IsInterrupt = response.IsInterrupt,
                Tokens = response.Usage.OutputTokenCount ?? 0,
                ErrorMessage = response.ErrorMessage
            });
            if (!response.IsInterrupt)
            {
                this.PromptString = string.Empty;
            }

            var inputTokenCount = response.Usage.InputTokenCount;
            if (inputTokenCount != null)
            {
                requestViewItem.Tokens = inputTokenCount.Value - existTokens;
            }
        }
        catch (Exception exception)
        {
            //响应之外的错误，删除请求
            DialogItems.Remove(DialogItems.Last(item => item is RequestViewItem));
            MessageBox.Show(exception.Message, "请求失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DialogItems.Remove(DialogItems.Last(item => item is ILLMModelClient));
            _requestTokenSource?.Dispose();
        }

        ScrollViewItem = DialogItems.LastOrDefault();
    }


    public void ReSend(RequestViewItem redoItem)
    {
        if (Model == null)
        {
            return;
        }

        //删除本请求及之后的所有记录
        var indexOf = DialogItems.IndexOf(redoItem);
        var dialogCount = DialogItems.Count;
        for (int i = 0; i < dialogCount - indexOf; i++)
        {
            DialogItems.RemoveAt(indexOf);
        }

        var message = redoItem.MessageContent;
        PromptString = message;
        SendRequest(message);
    }

    private IList<IDialogViewItem> GenerateHistory(IList<IDialogViewItem> source)
    {
        var dialogViewItems = new Stack<IDialogViewItem>();
        for (int i = source.Count - 1; i >= 0; i--)
        {
            var dialogViewItem = source[i];
            if (dialogViewItem is EraseViewItem)
            {
                break;
            }

            if (dialogViewItem is { IsAvailableInContext: true, Message: not null })
            {
                dialogViewItems.Push(dialogViewItem);
            }
        }

        var list = new List<IDialogViewItem>();
        while (dialogViewItems.TryPop(out var dialogViewItem))
        {
            list.Add(dialogViewItem);
        }

        return list;
    }

    public void InsertClearContextItem(IDialogViewItem item)
    {
        var indexOf = DialogItems.IndexOf(item);
        DialogItems.Insert(indexOf, new EraseViewItem());
    }
}