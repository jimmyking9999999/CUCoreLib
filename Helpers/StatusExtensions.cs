using CUCoreLib.Data;
using CUCoreLib.Registries;

namespace CUCoreLib.Helpers;

public static class StatusExtensions
{
    public static TStatus GetStatus<TStatus>(this Body body)
        where TStatus : BodyStatus, new()
    {
        return StatusRegistry.Get(body).Get<TStatus>();
    }

    public static TStatus GetStatus<TStatus>(this Limb limb)
        where TStatus : LimbStatus, new()
    {
        return StatusRegistry.Get(limb).Get<TStatus>();
    }
}