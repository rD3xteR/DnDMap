namespace DnDMap.Models
{
    /// <summary>
    /// Item of image in directory
    /// </summary>
    public class ImageItem(string name, string path)
    {
        /// <summary>
        /// Short name
        /// </summary>
        public string Name { get; set; } = name;

        /// <summary>
        /// Image path
        /// </summary>
        public string Path { get; set; } = path;
    }
}
