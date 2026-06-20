using System;

namespace CUCoreLib.Data
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class StatusOptionsAttribute : Attribute
    {
        public string Key { get; set; }
        public bool SaveEnabled { get; set; } = true;
    }
}
