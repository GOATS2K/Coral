namespace Coral.Essentia;

public abstract class Configurable
{
    private readonly Dictionary<string, object?> _parameters = new();
    private readonly Dictionary<string, object?> _defaultParameters = new();

    public string Name { get; protected set; }

    protected Configurable()
    {
        Name = GetType().Name;
        DeclareParameters();
    }

    public abstract void DeclareParameters();
        
    public virtual void Configure() { }

    public void Configure(Dictionary<string, object> parameters)
    {
        SetParameters(parameters);
        Configure();
    }

    protected void DeclareParameter<T>(string name, T? defaultValue, string description = "")
    {
        _defaultParameters[name] = defaultValue;
        _parameters[name] = defaultValue;
    }

    public void SetParameters(Dictionary<string, object> parameters)
    {
        foreach (var param in parameters)
        {
            if (!_parameters.ContainsKey(param.Key))
            {
                throw new EssentiaException($"Parameter '{param.Key}' is not declared for configurable '{Name}'.");
            }
            _parameters[param.Key] = param.Value;
        }
    }

    protected T GetParameter<T>(string name)
    {
        if (_parameters.TryGetValue(name, out var value))
        {
            // Handle potential null value for non-nullable types
            if (value is null && default(T) is not null)
            {
                throw new EssentiaException($"Parameter '{name}' is null but was requested as a non-nullable type.");
            }
            return (T)Convert.ChangeType(value, typeof(T))!;
        }
        throw new EssentiaException($"Parameter '{name}' not found for configurable '{Name}'.");
    }
}