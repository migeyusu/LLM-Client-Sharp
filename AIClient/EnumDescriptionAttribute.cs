using System.ComponentModel;

namespace LLMClient
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class EnumDescriptionAttribute : DescriptionAttribute
    {
        private readonly Lazy<string> _lazyDescription;

        public EnumDescriptionAttribute(string description, Type enumType) : base(description)
        {
            _lazyDescription = new Lazy<string>(() =>
            {
                var enumDescription = Extension.GenerateEnumDescription(enumType);
                return string.IsNullOrEmpty(description)
                    ? enumDescription
                    : $"{description} {enumDescription}";
            });
        }

        public override string Description => _lazyDescription.Value;
    }
}