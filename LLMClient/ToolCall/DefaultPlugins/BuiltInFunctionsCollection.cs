using System.Collections;
using System.Reflection;
using LLMClient.Abstraction;

namespace LLMClient.ToolCall.DefaultPlugins;

public class BuiltInFunctionsCollection : IEnumerable<IBuiltInFunctionGroup>, IFunctionGroupSource
{
    public BuiltInFunctionsCollection()
    {
        //从当前程序集加载所有实现了KernelFunctionGroup的类型
        var kernelFunctionGroupTypes = typeof(IBuiltInFunctionGroup).ImplementsTypes();
        //创建KernelFunctionGroup的实例并添加到集合中
        KernelFunctionGroups =
            kernelFunctionGroupTypes.Select(t => (IBuiltInFunctionGroup)Activator.CreateInstance(t)!).ToArray();
    }

    public IList<IBuiltInFunctionGroup> KernelFunctionGroups { get; }

    public IEnumerator<IBuiltInFunctionGroup> GetEnumerator()
    {
        return KernelFunctionGroups.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerable<IAIFunctionGroup> GetFunctionGroups()
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        return KernelFunctionGroups.OfType<KernelFunctionGroup>();
    }
}