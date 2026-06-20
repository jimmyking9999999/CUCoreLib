namespace CUCoreLib.Data
{
    public sealed class StatusMoodleDefinition
    {
        public int Intensity { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Critical { get; set; }
        public bool ChippedOnly { get; set; }
        public bool Important { get; set; } = true;
    }
}
