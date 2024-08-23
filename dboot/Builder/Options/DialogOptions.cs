namespace dboot.Builder.Options
{
    public class DialogOptions
    {
        public string Title { get; set; }
        public byte[] Icon { get; set; }
        public bool AutoClose { get; set; } = false;
        public bool ShowTimeRemaining { get; set; } = false;
    }
}