using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace LLMClient.Component.Extension;

public class RichTextBoxTrackingBehavior : Behavior<RichTextBox>
{
    // 暴露的 IsTextChanged 依赖属性
    public static readonly DependencyProperty IsTextChangedProperty =
        DependencyProperty.Register(
            nameof(IsTextChanged),
            typeof(bool),
            typeof(RichTextBoxTrackingBehavior),
            new PropertyMetadata(false));

    // 是否有文本修改
    public bool IsTextChanged
    {
        get => (bool)GetValue(IsTextChangedProperty);
        set => SetValue(IsTextChangedProperty, value);
    }

    // 在行为附加到控件时初始化
    protected override void OnAttached()
    {
        base.OnAttached();

        // 确保行为附加到 RichTextBox 类型
        if (AssociatedObject is { } richTextBox)
        {
            // 初始化文本撤销堆栈状态
            richTextBox.UndoLimit = 0; // 清空堆栈 (干净状态)
            richTextBox.UndoLimit = -1; // 恢复无限撤销能力

            // 监听 TextChanged 事件
            richTextBox.TextChanged += RichTextBox_TextChanged;
        }
    }

    // 当行为分离时清理事件监听
    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (AssociatedObject is { } richTextBox)
        {
            richTextBox.TextChanged -= RichTextBox_TextChanged;
        }
    }

    // 文本修改事件处理逻辑
    private void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is RichTextBox richTextBox)
        {
            // 如果 RichTextBox 的 Undo 堆栈可以回退，说明文档已被修改
            IsTextChanged = richTextBox.CanUndo;
        }
    }
}