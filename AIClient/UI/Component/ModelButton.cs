using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Abstraction;

namespace LLMClient.UI.Component;

public class ModelButton : Button
{
    static ModelButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ModelButton),
            new FrameworkPropertyMetadata(typeof(ModelButton)));
    }

    public static readonly DependencyProperty ChangeModelEnableProperty = DependencyProperty.Register(
        nameof(ChangeModelEnable), typeof(bool), typeof(ModelButton), new PropertyMetadata(true));

    public bool ChangeModelEnable
    {
        get { return (bool)GetValue(ChangeModelEnableProperty); }
        set { SetValue(ChangeModelEnableProperty, value); }
    }

    public static readonly DependencyProperty ChangeModelCommandProperty = DependencyProperty.Register(
        nameof(ChangeModelCommand), typeof(ICommand), typeof(ModelButton), new PropertyMetadata(default(ICommand)));

    public ICommand ChangeModelCommand
    {
        get { return (ICommand)GetValue(ChangeModelCommandProperty); }
        set { SetValue(ChangeModelCommandProperty, value); }
    }

    public static readonly DependencyProperty ModelDetailCommandProperty = DependencyProperty.Register(
        nameof(ModelDetailCommand), typeof(ICommand), typeof(ModelButton), new PropertyMetadata(default(ICommand)));

    public ICommand ModelDetailCommand
    {
        get { return (ICommand)GetValue(ModelDetailCommandProperty); }
        set { SetValue(ModelDetailCommandProperty, value); }
    }
    
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(ILLMModel), typeof(ModelButton), new PropertyMetadata(default(ILLMModel)));

    public ILLMModel Model
    {
        get { return (ILLMModel)GetValue(ModelProperty); }
        set { SetValue(ModelProperty, value); }
    }
}