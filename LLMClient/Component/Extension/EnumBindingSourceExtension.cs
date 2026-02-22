using System.ComponentModel;
using System.Windows.Markup;

namespace LLMClient.Component.Extension;

public enum EnumSortOrder
{
    Default,
    ByName,
}

public class EnumBindingSourceExtension : MarkupExtension
{
    public EnumSortOrder SortOrder { get; set; } = EnumSortOrder.Default;

    private Type? _enumType;

    public Type? EnumType
    {
        get => this._enumType;
        set
        {
            if (value == this._enumType)
                return;
            if (value != null)
            {
                var type = Nullable.GetUnderlyingType(value);
                if (type == null)
                    type = value;
                if (!type.IsEnum)
                    throw new ArgumentException("类型必须为Enum");
            }

            this._enumType = value;
        }
    }

    public EnumBindingSourceExtension()
    {
    }

    public EnumBindingSourceExtension(Type enumType) => this.EnumType = enumType;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (this._enumType == null)
            throw new InvalidOperationException("EnumType必须指定的");

        var underlyingType = Nullable.GetUnderlyingType(this._enumType) ?? this._enumType;

        var values = Enum.GetValues(underlyingType).Cast<object>();

        values = SortOrder switch
        {
            EnumSortOrder.ByName => values.OrderBy(v => v.ToString()),
            _ => values, // Default：保持枚举定义顺序
        };

        var result = values.ToArray();

        // 处理可空枚举：在头部插入 null 占位
        if (underlyingType != this._enumType)
        {
            var instance = Array.CreateInstance(underlyingType, result.Length + 1);
            result.CopyTo(instance, 1);
            return instance;
        }

        return result;
    }
}