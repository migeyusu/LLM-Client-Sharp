using System.Windows;
using System.Windows.Controls.Primitives;

namespace LLMClient.Endpoints;

public class ParamView : RangeBase
{
    static ParamView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ParamView), new FrameworkPropertyMetadata(typeof(ParamView)));
    }

    public static readonly DependencyProperty TickFrequencyProperty = DependencyProperty.Register(
        nameof(TickFrequency), typeof(double), typeof(ParamView), new PropertyMetadata(1d));

    public double TickFrequency
    {
        get { return (double)GetValue(TickFrequencyProperty); }
        set { SetValue(TickFrequencyProperty, value); }
    }

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(ParamView), new PropertyMetadata(default(string)));

    public string Unit
    {
        get { return (string)GetValue(UnitProperty); }
        set { SetValue(UnitProperty, value); }
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

public class ParamConfigView : ParamView
{
    static ParamConfigView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ParamConfigView),
            new FrameworkPropertyMetadata(typeof(ParamConfigView)));
    }

    public static readonly DependencyProperty AvailableProperty = DependencyProperty.Register(
        nameof(Available), typeof(bool), typeof(ParamConfigView), new PropertyMetadata(default(bool)));

    public bool Available
    {
        get { return (bool)GetValue(AvailableProperty); }
        set { SetValue(AvailableProperty, value); }
    }

    public static readonly DependencyProperty MaximumEditableProperty = DependencyProperty.Register(
        nameof(MaximumEditable), typeof(bool), typeof(ParamConfigView), new PropertyMetadata(default(bool)));

    public bool MaximumEditable
    {
        get { return (bool)GetValue(MaximumEditableProperty); }
        set { SetValue(MaximumEditableProperty, value); }
    }
}