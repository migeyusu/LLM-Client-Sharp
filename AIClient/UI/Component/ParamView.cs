using System.Windows;
using System.Windows.Controls;

namespace LLMClient.UI.Component;

public class ParamView : Control
{
    static ParamView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ParamView), new FrameworkPropertyMetadata(typeof(ParamView)));
    }

    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(double), typeof(ParamView), new PropertyMetadata(default(double)));

    public double Max
    {
        get { return (double)GetValue(MaxProperty); }
        set { SetValue(MaxProperty, value); }
    }

    public static readonly DependencyProperty MinProperty = DependencyProperty.Register(
        nameof(Min), typeof(double), typeof(ParamView), new PropertyMetadata(default(double)));

    public double Min
    {
        get { return (double)GetValue(MinProperty); }
        set { SetValue(MinProperty, value); }
    }

    public static readonly DependencyProperty TickFrequencyProperty = DependencyProperty.Register(
        nameof(TickFrequency), typeof(double), typeof(ParamView), new PropertyMetadata(1d));

    public double TickFrequency
    {
        get { return (double)GetValue(TickFrequencyProperty); }
        set { SetValue(TickFrequencyProperty, value); }
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ParamView), new PropertyMetadata(default(double)));

    public double Value
    {
        get { return (double)GetValue(ValueProperty); }
        set { SetValue(ValueProperty, value); }
    }


    public static readonly DependencyProperty PropertyNameProperty = DependencyProperty.Register(
        nameof(PropertyName), typeof(string), typeof(ParamView), new PropertyMetadata(default(string)));

    public string PropertyName
    {
        get { return (string)GetValue(PropertyNameProperty); }
        set { SetValue(PropertyNameProperty, value); }
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(ParamView), new PropertyMetadata(default(string)));

    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set { SetValue(DescriptionProperty, value); }
    }
}