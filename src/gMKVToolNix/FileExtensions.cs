using System.IO;

namespace gMKVToolNix
{
    public enum ExistingFileHandling
    {
        Rename,
        Overwrite,
        Skip
    }

    public static class FileExtensions
    {
        public static string GetOutputFilename(this string filename, bool overwriteExisting = false)
        {
            return filename.GetOutputFilename(
                overwriteExisting ? ExistingFileHandling.Overwrite : ExistingFileHandling.Rename);
        }

        public static string GetOutputFilename(this string filename, ExistingFileHandling existingFileHandling)
        {
            // Check if file already exists
            if (existingFileHandling == ExistingFileHandling.Skip && File.Exists(filename))
            {
                return null;
            }

            while (existingFileHandling == ExistingFileHandling.Rename && File.Exists(filename))
            {
                string outputFilenameDirectory = Path.GetDirectoryName(filename);
                string outputFilenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
                string outputFilenameExtension = Path.GetExtension(filename);
                int lastDotIndex = outputFilenameWithoutExtension.LastIndexOf('.');

                int outputFilenameCounter = 0;
                // Check if the filename contains a dot
                if (lastDotIndex > -1)
                {
                    // Get the last part of filename after the last dot
                    string outputFilenameCounterString = outputFilenameWithoutExtension.Substring(lastDotIndex + 1);
                    // Check if it's an integer (counter)
                    if (int.TryParse(outputFilenameCounterString, out outputFilenameCounter))
                    {
                        // Isolate the filename without the counter part
                        outputFilenameWithoutExtension = outputFilenameWithoutExtension.Substring(0, lastDotIndex);
                    }
                }

                filename = Path.Combine(
                    outputFilenameDirectory,
                    string.Format("{0}.{1}{2}", outputFilenameWithoutExtension, (outputFilenameCounter + 1), outputFilenameExtension)
                );
            }

            return filename;
        }
    }
}
