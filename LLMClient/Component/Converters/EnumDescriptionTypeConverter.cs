using System.ComponentModel;
using System.Globalization;

namespace LLMClient.Component.Converters;

public class EnumDescriptionTypeConverter : EnumConverter
{
    public EnumDescriptionTypeConverter(Type type) : base(type)
    {
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        // 只有当目标类型是 string 时，我们才介入处理
        if (destinationType == typeof(string) && value != null)
        {
            var fi = EnumType.GetField(value.ToString()!);
            if (fi != null)
            {
                var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                // 如果有 Description，返回描述；否则回退到基类行为（返回枚举名）
                return attributes.Length > 0 && !string.IsNullOrEmpty(attributes[0].Description)
                    ? attributes[0].Description
                    : base.ConvertTo(context, culture, value, destinationType);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}