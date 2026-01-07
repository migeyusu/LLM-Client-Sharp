using System.Windows.Markup;

namespace LLMClient.Component.Extension;


public class EnumBindingSourceExtension : MarkupExtension
{
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
        var type1 = !(null == this._enumType)
            ? Nullable.GetUnderlyingType(this._enumType)
            : throw new InvalidOperationException("EnumType必须指定的");
        if (type1 == null)
            type1 = this._enumType;
        Type type2 = type1;
        Array values = Enum.GetValues(type2);
        if (type2 == this._enumType)
            return (object)values;
        Array instance = Array.CreateInstance(type2, values.Length + 1);
        values.CopyTo(instance, 1);
        return (object)instance;
    }
}