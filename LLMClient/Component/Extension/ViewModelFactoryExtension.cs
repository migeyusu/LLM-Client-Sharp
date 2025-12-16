using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace LLMClient.Component.Extension
{
    /// <summary>
    /// 一个既是MarkupExtension又是IValueConverter的类。
    /// 它可以直接在XAML的Converter属性中使用。
    /// </summary>
    public class ViewModelFactoryExtension : MarkupExtension, IValueConverter
    {
        // 您要在XAML中传入的目标ViewModel类型
        public Type TargetType { get; set; }

        public ViewModelFactoryExtension() { }

        public ViewModelFactoryExtension(Type targetType)
        {
            TargetType = targetType;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // 返回自身作为转换器
            return this;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || TargetType == null) return null;

            try
            {
                // 使用反射查找包含对应参数的构造函数并实例化
                // 假设 ViewModel 的构造函数接受 value 的类型或其接口
                return Activator.CreateInstance(TargetType, value);
            }
            catch (Exception ex)
            {
                // 实际开发中建议记录日志
                System.Diagnostics.Debug.WriteLine($"ViewModelFactory Error: {ex.Message}");
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}