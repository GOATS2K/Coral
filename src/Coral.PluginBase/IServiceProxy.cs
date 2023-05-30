namespace Coral.PluginBase;

public interface IServiceProxy
{
    public TType GetService<TType>()
        where TType : class;
}