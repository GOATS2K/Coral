namespace Coral.PluginBase;

public interface IHostServiceProxy
{
    public TType GetHostService<TType>()
        where TType : class;
}