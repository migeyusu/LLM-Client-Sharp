using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;

namespace LLMClient.UI.Component;

public class TypeValidateRule : ValidationRule
{
    public Type? TargetType { get; set; }

    public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
    {
        //将value转为特定类型
        if (TargetType == null)
        {
            return new ValidationResult(false, "未指定目标类型");
        }

        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.ValidResult;
        }

        var converter = TypeDescriptor.GetConverter(TargetType);
        if (converter.CanConvertFrom(value.GetType()))
        {
            try
            {
                converter.ConvertFrom(value);
            }
            catch (Exception e)
            {
                return new ValidationResult(false, $"无法将 '{value}' 转换为 {TargetType.Name} 类型: {e.Message}");
            }

            return ValidationResult.ValidResult;
        }

        return new ValidationResult(false, $"无法将 '{value}' 转换为 {TargetType.Name} 类型");
    }
}