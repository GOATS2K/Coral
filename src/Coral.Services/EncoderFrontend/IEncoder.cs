using System.Diagnostics;
using Coral.Database.Models;

namespace Coral.Services.EncoderFrontend;

public interface IEncoder
{
    bool EnsureEncoderExists();
    IArgumentBuilder Configure();
}